using System.Globalization;

using FRLG.StarterTool.Core.Npc;

namespace FRLG.StarterTool.Core.Troubleshoot;

public sealed record SweepHit(
    int Advances,
    int Frame,
    int OffsetFrames,
    MatchQuality Quality,
    int Distance,
    IReadOnlyList<StripLine> Lines,
    int ObservableFrames = 0,
    int StreamShift = 0);

public sealed record SweepResult
{
    public IReadOnlyList<SweepHit> Hits { get; init; } = Array.Empty<SweepHit>();

    public int Scanned { get; init; }

    public int RadiusFrames { get; init; }

    public string Summary { get; init; } = "";

    public string Note { get; init; } = "";

    public bool Found => Hits.Count > 0 && Hits[0].Quality != MatchQuality.None;
}

public static class FrameSweep
{
    public const int DefaultRadius = 120;

    private const int MostHits = 12;

    private const int StreamShiftRadius = 3;

    public static SweepResult Lab(RunRecord run, IReadOnlyList<StripToken>? aide,
        IReadOnlyList<StripToken>? scientist, int radius = DefaultRadius,
        IReadOnlyList<StripToken>? fenceGuy = null)
    {
        if (run.Seed <= 0 || run.Fence.Count == 0 || run.Lab.Count == 0)
        {
            return new SweepResult { Summary = "This run did not record enough to sweep from." };
        }

        if ((aide?.Count ?? 0) + (scientist?.Count ?? 0) == 0)
        {
            return new SweepResult { Summary = "Report what you saw first, then sweep for it." };
        }

        if (fenceGuy is { Count: 0 }) fenceGuy = null;

        IReadOnlyList<FenceCandidate> fence = Fence(run);

        int centre = (int)Math.Round(run.Lab.Average(r => (double)r.LabFrame));

        var pairs = new List<(int Observable, int Shift, int Frame, int Parent)>();

        foreach (int observable in Windows(run))
        {
            for (int shift = -StreamShiftRadius; shift <= StreamShiftRadius; shift++)
            {
                int reach = shift == 0 ? radius : StreamShiftRadius;

                for (int frame = centre - reach; frame <= centre + reach; frame++)
                {
                    for (int parent = 0; parent < fence.Count; parent++)
                    {
                        pairs.Add((observable, shift, frame, parent));
                    }
                }
            }
        }

        var walks = fenceGuy == null ? null : fence
            .Select(parent => Troubleshooter.Compare(
                MovementStrip.Layout(parent.LeadWalk.Select(e => new StripMove(e.Frame, e.Direction))),
                fenceGuy, parent.FirstRequiredEvent))
            .ToArray();

        var hits = new SweepHit[pairs.Count];
        Parallel.For(0, pairs.Count, i =>
        {
            (int observable, int shift, int frame, int p) = pairs[i];

            FenceCandidate parent = fence[p];
            FenceCandidate resumed = shift == 0 ? parent
                : parent with { AdvancesBeforeLabLoad = parent.AdvancesBeforeLabLoad + shift };

            LabCandidate box = LabRun.Simulate(run.Seed, resumed, frame,
                run.LabPressFrame ?? 0, observable);

            IReadOnlyList<StripToken> left = Strip(box.Aide, box);
            IReadOnlyList<StripToken> right = Strip(box.Scientist, box);

            (MatchQuality Quality, int Distance)? a =
                aide == null ? null : Troubleshooter.Compare(left, aide, 0);
            (MatchQuality Quality, int Distance)? b =
                scientist == null ? null : Troubleshooter.Compare(right, scientist, 0);
            (MatchQuality Quality, int Distance)? f = walks?[p];

            hits[i] = new SweepHit(
                box.AdvancesAtTextClose,
                box.LabFrame,
                box.LabFrame - centre,
                Weakest(Weakest(a?.Quality, b?.Quality), f?.Quality),
                (a?.Distance ?? 0) + (b?.Distance ?? 0) + (f?.Distance ?? 0),
                new[] { new StripLine("L", left), new StripLine("S", right) },
                observable,
                shift);
        });

        return Rank(hits.ToList(), hits.Length, radius, "lab press", run.LabWindowFrames);
    }

    public static SweepResult Fence(RunRecord run, IReadOnlyList<StripToken> report,
        int radius = DefaultRadius)
    {
        if (run.Seed <= 0 || run.Fence.Count == 0)
        {
            return new SweepResult { Summary = "This run did not record enough to sweep from." };
        }

        if (report.Count == 0)
        {
            return new SweepResult { Summary = "Report what you saw first, then sweep for it." };
        }

        int[] exits = run.Fence.Select(r => r.ExitFrame).Distinct().Order().ToArray();
        int centre = (int)Math.Round(run.Fence.Average(r => (double)r.OakFrame));

        var hits = new List<SweepHit>();
        int scanned = 0;

        foreach (int exit in exits)
        {
            for (int oak = centre - radius; oak <= centre + radius; oak++)
            {
                FenceCandidate candidate = FenceRun.Simulate(run.Seed, exit, oak, run.ManualAdvances);
                scanned++;

                IReadOnlyList<StripToken> strip = Strip(candidate.LeadWalk);

                (MatchQuality quality, int distance) =
                    Troubleshooter.Compare(strip, report, candidate.FirstRequiredEvent);

                hits.Add(new SweepHit(
                    candidate.TotalAdvances,
                    oak,
                    oak - centre,
                    quality,
                    distance,
                    new[] { new StripLine("", strip) }));
            }
        }

        return Rank(hits, scanned, radius, "Oak press", 0);
    }

    private static IReadOnlyList<int> Windows(RunRecord run)
    {
        var windows = new List<int>();

        if (run.LabWindowFrames > 0) windows.Add(run.LabWindowFrames);

        foreach (int window in new[]
                 {
                     RouteTimeline.LabObservableFrames,
                     RouteTimeline.LabObservableLateFrames,
                     RouteTimeline.LabObservableVeryLateFrames,
                 })
        {
            if (!windows.Contains(window)) windows.Add(window);
        }

        return windows;
    }

    private static IReadOnlyList<FenceCandidate> Fence(RunRecord run) => run.Fence
        .Select(row => FenceRun.Simulate(run.Seed, row.ExitFrame, row.OakFrame, run.ManualAdvances))
        .ToList();

    private static IReadOnlyList<StripToken> Strip(IReadOnlyList<NpcEvent> events) =>
        MovementStrip.Layout(events.Select(e => new StripMove(e.Frame, e.Direction)));

    private static IReadOnlyList<StripToken> Strip(IReadOnlyList<NpcEvent> events, LabCandidate box) =>
        MovementStrip.Layout(events.Select(e => new StripMove(e.Frame, e.Direction, box.Completes(e))));

    private static MatchQuality Weakest(MatchQuality? left, MatchQuality? right) => (left, right) switch
    {
        (null, null) => MatchQuality.Exact,
        (null, { } only) => only,
        ({ } only, null) => only,
        var (a, b) => (MatchQuality)Math.Min((int)a!.Value, (int)b!.Value),
    };

    private static SweepResult Rank(List<SweepHit> hits, int scanned, int radius, string press,
        int reportedWindow)
    {
        List<SweepHit> ranked = hits
            .OrderByDescending(h => h.Quality)
            .ThenBy(h => h.Distance)
            .ThenBy(h => Math.Abs(h.StreamShift))
            .ThenBy(h => Math.Abs(h.OffsetFrames))
            .ThenBy(h => h.ObservableFrames == reportedWindow ? 0 : 1)
            .ThenBy(h => h.OffsetFrames)
            .GroupBy(h => (h.Advances, h.ObservableFrames,
                string.Join("|", h.Lines.Select(l => MovementStrip.Format(l.Tokens)))))
            .Select(g => g.First())
            .Take(MostHits)
            .ToList();

        return new SweepResult
        {
            Hits = ranked,
            Scanned = scanned,
            RadiusFrames = radius,
            Summary = Describe(ranked, scanned, radius, press, reportedWindow),
            Note = ranked.Count == 0 ? "" : Window(ranked[0], reportedWindow)
        };
    }

    private static string Describe(IReadOnlyList<SweepHit> hits, int scanned, int radius, string press,
        int reportedWindow)
    {
        if (hits.Count == 0)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "Swept {0} frames and found nothing at all.", scanned);
        }

        SweepHit best = hits[0];

        if (best.Quality == MatchQuality.None)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "Nothing within {0} of the {1} produces exactly that.\r\nClosest is {2}, {3} "
                + "movement{4} out, at {5}.",
                radius, press, best.Advances, best.Distance, best.Distance == 1 ? "" : "s",
                Where(best));
        }

        int[] frames = hits
            .Where(h => h.Quality != MatchQuality.None)
            .Select(h => h.Advances)
            .Distinct()
            .Order()
            .ToArray();

        string found = frames.Length == 1
            ? string.Format(CultureInfo.InvariantCulture, "Nearest matching frame → {0}", frames[0])
            : string.Format(CultureInfo.InvariantCulture,
                "Nearest matching frame → {0} ({1} frames produce that report; the nearest is shown "
                + "first)", best.Advances, frames.Length);

        return found + "\r\n" + string.Format(CultureInfo.InvariantCulture,
            "It puts the {0} at {1}.", press, Where(best));
    }

    private static string Where(SweepHit hit) => hit.OffsetFrames switch
    {
        0 => "the measured press",
        1 or -1 => hit.OffsetFrames > 0 ? "1 frame later" : "1 frame earlier",
        _ => string.Format(CultureInfo.InvariantCulture, "{0} frames {1}",
            Math.Abs(hit.OffsetFrames), hit.OffsetFrames > 0 ? "later" : "earlier"),
    };

    private static string Window(SweepHit best, int reportedWindow)
    {
        string window = reportedWindow > 0 && best.ObservableFrames != reportedWindow
            ? string.Format(CultureInfo.InvariantCulture,
                "a {0}-frame window, not {1}", best.ObservableFrames, reportedWindow)
            : "";

        string shift = best.StreamShift != 0
            ? string.Format(CultureInfo.InvariantCulture,
                "the stream {0:+#;-#} advance{1} at the lab - the model's count, not your presses",
                best.StreamShift, Math.Abs(best.StreamShift) == 1 ? "" : "s")
            : "";

        return (window, shift) switch
        {
            ("", "") => "",
            ("", _) => "Needs " + shift,
            (_, "") => "Needs " + window,
            _ => "Needs " + shift + ", and " + window,
        };
    }
}
