using System.Globalization;

using FRLG.StarterTool.Core.Npc;

namespace FRLG.StarterTool.Core.Troubleshoot;

public sealed record AnchorFit(string Name, int Frame, int Centre, int Low, int High)
{
    public int OutOfContextFrames =>
        Frame < Low ? Low - Frame : Frame > High ? Frame - High : 0;

    public bool InContext => OutOfContextFrames == 0;

    public int OffsetFrames => Frame - Centre;
}

public sealed record RouteOption(
    int Advances,
    IReadOnlyList<AnchorFit> Anchors,
    IReadOnlyList<StripLine> Lines,
    MatchQuality Quality,
    int Distance,
    int ObservableFrames,
    SpawnRead Spawn,
    SpawnRead Respawn,
    int? Landing,
    IReadOnlyList<string> Corrections,
    int StreamShift = 0)
{
    public bool InContext => Anchors.All(a => a.InContext);

    public int OutOfContextFrames => Anchors.Count == 0 ? 0 : Anchors.Max(a => a.OutOfContextFrames);

    public double OutOfContextMs(double frameMs) => OutOfContextFrames * frameMs;

    public string Parity => (Spawn, Respawn) switch
    {
        (SpawnRead.PostVBlank, SpawnRead.PostVBlank) => "post",
        (SpawnRead.PreVBlank, SpawnRead.PreVBlank) => "pre",
        (SpawnRead.PostVBlank, _) => "post/pre",
        _ => "pre/post",
    };

    public string Offsets => string.Join(" ", Anchors.Select(a =>
        string.Format(CultureInfo.InvariantCulture, "{0}{1:+#;-#;0}", a.Name, a.OffsetFrames)));
}

public sealed record SearchOutcome
{
    public IReadOnlyList<RouteOption> Options { get; init; } = Array.Empty<RouteOption>();

    public int Scanned { get; init; }

    public string Summary { get; init; } = "";

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    public bool Found => Options.Count > 0 && Options[0].Quality != MatchQuality.None;
}

public static class TroubleshootSearch
{
    public const int DefaultRadius = 6;

    public const int MostOptions = 5;

    public const int StreamShiftRadius = 3;

    private static readonly SpawnRead[] Reads = { SpawnRead.PostVBlank, SpawnRead.PreVBlank };

    public static SearchOutcome Run(RunRecord run,
        IReadOnlyList<StripToken>? fenceGuy,
        IReadOnlyList<StripToken>? aide,
        IReadOnlyList<StripToken>? scientist,
        int? frameHit = null,
        double contextMs = 0.0,
        int radius = DefaultRadius,
        int? window = null)
    {
        if (run.Seed <= 0 || run.Fence.Count == 0)
        {
            return new SearchOutcome { Summary = "Run recorded too little to search." };
        }

        if (fenceGuy is { Count: 0 }) fenceGuy = null;
        if (aide is { Count: 0 }) aide = null;
        if (scientist is { Count: 0 }) scientist = null;

        if (fenceGuy == null && aide == null && scientist == null && frameHit == null)
        {
            return new SearchOutcome
            {
                Summary = "Report a movement or a frame, then Search."
            };
        }

        var notes = new List<string>();

        (int[] exits, int exitCentre, int exitLow, int exitHigh) =
            Axis(run.Fence.Select(r => r.ExitFrame), radius);
        (int[] oaks, int oakCentre, int oakLow, int oakHigh) =
            Axis(run.Fence.Select(r => r.OakFrame), radius);

        var keys = new List<(int Exit, int Oak, SpawnRead Spawn, SpawnRead Respawn)>();

        foreach (int exit in exits)
        {
            foreach (int oak in oaks)
            {
                foreach (SpawnRead spawn in Reads)
                {
                    foreach (SpawnRead respawn in Reads)
                    {
                        keys.Add((exit, oak, spawn, respawn));
                    }
                }
            }
        }

        var walks = new Walk[keys.Count];
        Parallel.For(0, keys.Count, i =>
        {
            (int exit, int oak, SpawnRead spawn, SpawnRead respawn) = keys[i];

            FenceCandidate candidate =
                FenceRun.Simulate(run.Seed, exit, oak, run.ManualAdvances, spawn, respawn);

            IReadOnlyList<StripToken> strip = Strip(candidate.LeadWalk);

            (MatchQuality quality, int distance) = fenceGuy == null
                ? (MatchQuality.Exact, 0)
                : Troubleshooter.Compare(strip, fenceGuy, candidate.FirstRequiredEvent);

            walks[i] = new Walk(candidate, strip, quality, distance,
                new AnchorFit("exit", exit, exitCentre, exitLow, exitHigh),
                new AnchorFit("oak", oak, oakCentre, oakLow, oakHigh));
        });

        if (run.Lab.Count == 0)
        {
            notes.Add("No lab boxes: count is at map load, Lady/Scientist ignored.");

            return Rank(run, walks.Select(FromWalk).ToList(), keys.Count, frameHit, contextMs, notes);
        }

        (int[] labs, int labCentre, int labLow, int labHigh) =
            Axis(run.Lab.Select(r => r.LabFrame), radius);

        Walk[] parents = walks
            .GroupBy(w => (w.Candidate.OakFrame, w.Candidate.AdvancesBeforeLabLoad))
            .Select(g => g.OrderByDescending(w => w.Quality)
                .ThenBy(w => w.Distance)
                .ThenBy(w => Math.Max(w.Exit.OutOfContextFrames, w.Oak.OutOfContextFrames))
                .ThenBy(w => Math.Abs(w.Exit.OffsetFrames) + Math.Abs(w.Oak.OffsetFrames))
                .First())
            .ToArray();

        int[] windows = window is { } only ? new[] { only } : WindowChoices(run);

        var pairs = new List<(int Parent, int Lab, int Window, int Shift)>();

        for (int parent = 0; parent < parents.Length; parent++)
        {
            bool shiftable = parents[parent].Exit.InContext && parents[parent].Oak.InContext;

            foreach (int lab in labs)
            {
                foreach (int observable in windows)
                {
                    pairs.Add((parent, lab, observable, 0));

                    if (!shiftable || Math.Abs(lab - labCentre) > StreamShiftRadius) continue;

                    for (int shift = -StreamShiftRadius; shift <= StreamShiftRadius; shift++)
                    {
                        if (shift != 0) pairs.Add((parent, lab, observable, shift));
                    }
                }
            }
        }

        var options = new RouteOption[pairs.Count];
        Parallel.For(0, pairs.Count, i =>
        {
            (int p, int lab, int window, int shift) = pairs[i];
            Walk walk = parents[p];

            LabCandidate box = LabRun.Simulate(run.Seed, walk.Candidate, lab,
                run.LabPressFrame ?? 0, window, shift);

            IReadOnlyList<StripToken> left = Strip(box.Aide, box);
            IReadOnlyList<StripToken> right = Strip(box.Scientist, box);

            (MatchQuality Quality, int Distance)? a =
                aide == null ? null : Troubleshooter.Compare(left, aide, 0);
            (MatchQuality Quality, int Distance)? b =
                scientist == null ? null : Troubleshooter.Compare(right, scientist, 0);

            options[i] = new RouteOption(
                box.AdvancesAtTextClose,
                new[]
                {
                    walk.Exit,
                    walk.Oak,
                    new AnchorFit("lab", lab, labCentre, labLow, labHigh),
                },
                new[]
                {
                    new StripLine("F", walk.Strip),
                    new StripLine("L", left),
                    new StripLine("S", right),
                },
                Weakest(walk.Quality, Weakest(a?.Quality, b?.Quality)),
                walk.Distance + (a?.Distance ?? 0) + (b?.Distance ?? 0),
                window,
                walk.Candidate.SpawnReadSide,
                walk.Candidate.RespawnReadSide,
                null,
                Array.Empty<string>(),
                shift);
        });

        return Rank(run, options.ToList(), keys.Count + pairs.Count, frameHit, contextMs, notes);
    }

    private readonly record struct Walk(
        FenceCandidate Candidate,
        IReadOnlyList<StripToken> Strip,
        MatchQuality Quality,
        int Distance,
        AnchorFit Exit,
        AnchorFit Oak);

    private static RouteOption FromWalk(Walk walk) => new(
        walk.Candidate.TotalAdvances,
        new[] { walk.Exit, walk.Oak },
        new[] { new StripLine("F", walk.Strip) },
        walk.Quality,
        walk.Distance,
        0,
        walk.Candidate.SpawnReadSide,
        walk.Candidate.RespawnReadSide,
        null,
        Array.Empty<string>());

    private static (int[] Frames, int Centre, int Low, int High) Axis(IEnumerable<int> recorded,
        int radius)
    {
        int[] distinct = recorded.Distinct().Order().ToArray();

        int low = distinct[0];
        int high = distinct[^1];

        int centre = (int)Math.Round((low + high) / 2.0, MidpointRounding.AwayFromZero);

        var frames = new List<int>();
        for (int frame = Math.Max(0, low - radius); frame <= high + radius; frame++) frames.Add(frame);

        return (frames.ToArray(), centre, low, high);
    }

    public static int[] WindowChoices(RunRecord run)
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

        return windows.ToArray();
    }

    private static SearchOutcome Rank(RunRecord run, List<RouteOption> options, int scanned,
        int? frameHit, double contextMs, List<string> notes)
    {
        int? used = Used(run);

        if (frameHit is { } hit)
        {
            if (run.LandedFrame is not { } landed || used is not { } against)
            {
                notes.Add("Frame hit ignored: run logged no landing or no chosen box.");
            }
            else
            {
                int reach = Reach(run.FrameMs, contextMs);

                options = options
                    .Select(o => o with { Landing = landed + (o.Advances - against) })
                    .Where(o => Math.Abs(hit - o.Landing!.Value) <= reach)
                    .ToList();

                notes.Add(string.Format(CultureInfo.InvariantCulture,
                    "Only landings within {1}f of {0} shown.", hit, reach));
            }
        }

        List<RouteOption> ranked = options
            .OrderByDescending(o => o.Quality)
            .ThenBy(o => o.Distance)
            .ThenBy(o => o.OutOfContextFrames)
            .ThenBy(o => Math.Abs(o.StreamShift))
            .ThenBy(o => o.Anchors.Sum(a => Math.Abs(a.OffsetFrames)))
            .ThenBy(o => o.ObservableFrames == run.LabWindowFrames ? 0 : 1)
            .ThenBy(o => o.Spawn == SpawnRead.PostVBlank && o.Respawn == SpawnRead.PostVBlank ? 0 : 1)
            .GroupBy(o => (o.Advances, o.ObservableFrames,
                string.Join("|", o.Lines.Select(l => MovementStrip.Format(l.Tokens)))))
            .Select(g => g.First())
            .Take(MostOptions)
            .Select(o => o with { Corrections = Corrections(run, o, used) })
            .ToList();

        return new SearchOutcome
        {
            Options = ranked,
            Scanned = scanned,
            Summary = Describe(ranked, run, used, scanned),
            Notes = notes
        };
    }

    private static int Reach(double frameMs, double contextMs) =>
        (int)Math.Floor((frameMs / 2.0 + Math.Max(0.0, contextMs) + FrameWindow.MinimumContextMs)
            / frameMs);

    private static int? Used(RunRecord run)
    {
        LabRow? focused = run.Lab.FirstOrDefault(r => r.Focused);
        if (focused != null) return focused.Advances;

        return run.Lab.Count == 1 ? run.Lab[0].Advances : null;
    }

    private static IReadOnlyList<string> Corrections(RunRecord run, RouteOption option, int? used)
    {
        if (!option.InContext || run.Lab.Count == 0) return Array.Empty<string>();

        int index = -1;
        for (int i = 0; i < run.Lab.Count; i++)
        {
            if (run.Lab[i].Focused) index = i;
        }

        if (index < 0) return Array.Empty<string>();
        if (used == option.Advances) return Array.Empty<string>();

        LabRow chosen = run.Lab[index];
        var lines = new List<string>
        {
            string.Format(CultureInfo.InvariantCulture,
                "On screen; run picked box {0} ({1}).", index + 1, chosen.Advances),
        };

        foreach ((string npc, IReadOnlyList<StripToken> theirs, string label) in new[]
                 {
                     ("Lady", chosen.AideStrip, "L"),
                     ("Scientist", chosen.ScientistStrip, "S"),
                 })
        {
            StripLine line = option.Lines.FirstOrDefault(l => l.Label == label);
            if (line.Tokens == null) continue;

            if (Difference(npc, theirs, line.Tokens) is { } difference) lines.Add(difference);
        }

        return lines;
    }

    private static string? Difference(string npc, IReadOnlyList<StripToken> chosen,
        IReadOnlyList<StripToken> truth)
    {
        int slots = Math.Max(chosen.Count, truth.Count);

        for (int slot = 0; slot < slots; slot++)
        {
            StripToken mine = slot < chosen.Count ? chosen[slot] : StripToken.Quiet;
            StripToken theirs = slot < truth.Count ? truth[slot] : StripToken.Quiet;

            if (mine.Direction == theirs.Direction) continue;

            return string.Format(CultureInfo.InvariantCulture,
                "{0} slot {1}: {2}, not {3}.", npc, slot + 1, Spell(theirs), Spell(mine));
        }

        return null;
    }

    private static string Spell(StripToken token) =>
        token.IsQuiet ? "–" : Directions.Letter(token.Direction);

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

    private static string Describe(IReadOnlyList<RouteOption> options, RunRecord run, int? used,
        int scanned)
    {
        if (options.Count == 0)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} readings, none fit.", scanned);
        }

        RouteOption best = options[0];

        if (best.Quality == MatchQuality.None)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "No exact fit in {0}.\r\nClosest {1} · {2} out · {3}",
                scanned, best.Advances, best.Distance, Cost(best, run));
        }

        int[] frames = options
            .Where(o => o.Quality != MatchQuality.None)
            .Select(o => o.Advances)
            .Distinct()
            .Order()
            .ToArray();

        string named = frames.Length == 1
            ? string.Format(CultureInfo.InvariantCulture, "Frame {0}", frames[0])
            : string.Format(CultureInfo.InvariantCulture, "Frame {0} ({1} fit)",
                best.Advances, frames.Length);

        if (used is not { } against)
        {
            return named + "\r\nNo box picked · " + Cost(best, run);
        }

        int outBy = best.Advances - against;

        return named + "\r\n" + (outBy == 0
            ? "Run used it · " + Cost(best, run)
            : string.Format(CultureInfo.InvariantCulture, "Run used {0} · {1:+#;-#}f ({2:+#;-#} ms) · {3}",
                against, outBy, Math.Round(outBy * run.FrameMs), Cost(best, run)));
    }

    private static string Cost(RouteOption option, RunRecord run)
    {
        string context = option.InContext
            ? "in context"
            : string.Format(CultureInfo.InvariantCulture, "needs +{0}f context ({1} ms)",
                option.OutOfContextFrames, Math.Round(option.OutOfContextMs(run.FrameMs)));

        return option.StreamShift == 0 ? context : context + string.Format(CultureInfo.InvariantCulture,
            " · stream {0:+#;-#} (model, not you)", option.StreamShift);
    }
}
