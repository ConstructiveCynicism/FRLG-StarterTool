using FRLG.StarterTool.Core.Timing;

namespace FRLG.StarterTool.Core.Npc;

public readonly record struct LabCandidate(
    FenceCandidate Fence,
    int LabFrame,
    int LabPressFrame,
    int FrozenFrames,
    int ObservableFrames,
    IReadOnlyList<NpcEvent> Aide,
    IReadOnlyList<NpcEvent> Scientist,
    int AdvancesAtTextClose,
    IReadOnlyList<int> AdvancesByFrame,
    IReadOnlyList<HiddenMoves>? LabHidden = null,
    int StreamShift = 0,
    LabLive? Live = null)
{
    public int Rate => RouteTimeline.AdvancesPerFrame(Fence.Adapter);

    public bool ParityOk(int targetAdvances, int window)
    {
        if (Rate == 1) return true;

        int count = Live is { } live
            ? live.Advances[Math.Clamp(window, 0, live.Advances.Count - 1)]
            : AdvancesAt(window);
        return ((count + RouteTimeline.BallGeneration(Fence.Adapter) + targetAdvances) & 1) == 0;
    }

    public bool ParityOk(int targetAdvances) => ParityOk(targetAdvances, ObservableFrames);
    public bool Completes(NpcEvent e) =>
        e.Kind != NpcEventKind.Step || e.Frame + ObjectEventSim.NormalWalkFrames <= ObservableFrames;

    public int GapFrames => LabFrame - Fence.OakFrame;

    public int AdvancesAt(int framesSinceTextClose)
    {
        if (framesSinceTextClose <= 0) return AdvancesAtTextClose;
        if (framesSinceTextClose < AdvancesByFrame.Count) return AdvancesByFrame[framesSinceTextClose];

        return AdvancesByFrame[^1] + (framesSinceTextClose - AdvancesByFrame.Count + 1) * Rate;
    }

    public int FramesTo(int targetAdvances)
    {
        if (targetAdvances <= AdvancesAtTextClose) return 0;

        for (int frame = 1; frame < AdvancesByFrame.Count; frame++)
        {
            if (AdvancesByFrame[frame] >= targetAdvances) return frame;
        }

        int shortfall = targetAdvances - AdvancesByFrame[^1];
        return AdvancesByFrame.Count - 1 + (shortfall + Rate - 1) / Rate;
    }

    public int CountdownFrame(int targetAdvances, int manualAdvances = 0) =>
        LabPressFrame
        + FramesTo(targetAdvances - manualAdvances - RouteTimeline.BallGeneration(Fence.Adapter))
        + VariableOffsetCalculator.TidLagFrames;

    public int AdvancesAtCountdownFrame(int countdownFrame, int manualAdvances = 0) =>
        AdvancesAt(countdownFrame - VariableOffsetCalculator.TidLagFrames - LabPressFrame)
        + manualAdvances + RouteTimeline.BallGeneration(Fence.Adapter);

    public IReadOnlyList<HiddenMoves> Hidden
    {
        get
        {
            var all = new List<HiddenMoves>(4) { Fence.Hidden };
            if (LabHidden != null) all.AddRange(LabHidden);
            return all;
        }
    }

    public IEnumerable<NpcEvent> Observable =>
        Aide.Concat(Scientist).OrderBy(e => e.Frame).ThenBy(e => e.Slot);

    public override string ToString() =>
        $"lab {LabFrame} frozen {FrozenFrames} -> {AdvancesAtTextClose} "
        + $"({Aide.Count} aide, {Scientist.Count} scientist)";
}

public sealed record LabLive(IReadOnlyList<int> Advances, IReadOnlyList<NpcEvent> Events)
{
    public IReadOnlyList<(int Start, int End)> LadyWalks =>
        Events.Where(e => e.Npc == NpcId.Aide && e.Kind == NpcEventKind.Step)
            .Select(e => (e.Frame, e.Frame + ObjectEventSim.NormalWalkFrames))
            .ToList();

    public bool LadyWalking(int window)
    {
        int frame = window - 1;
        foreach ((int start, int end) in LadyWalks)
        {
            if (frame >= start && frame < end) return true;
        }
        return false;
    }
}

public sealed record MissedLab(LabCandidate Lab, IReadOnlyList<HiddenMoves> Scientists);

public static class LabRun
{
    public const int HorizonFrames = 1200;

    public const int LiveHorizonFrames = 600;

    public const int AdapterMaxWindowFrames = 240;

    public const double WrongParityPrior = 0.15;

    public const double LaterWindowPrior = 0.5;

    public const int StreamShiftRadius = 1;

    public const int StreamShiftNarrowedParents = 1;

    public static IReadOnlyList<LabCandidate> Build(int seed, IReadOnlyList<FenceCandidate> fence,
        double oakElapsedMs, double labElapsedMs, double fps, double contextMs,
        int observableFrames = RouteTimeline.LabObservableFrames) =>
        BuildFromGaps(seed, fence, GapFrames(oakElapsedMs, labElapsedMs, fps, contextMs),
            PressFrame(labElapsedMs, fps), observableFrames);

    public static int PressFrame(double labElapsedMs, double fps) =>
        FrameWindow.LikelyFrame(labElapsedMs, fps);

    public static IReadOnlyList<int> GapFrames(double oakElapsedMs, double labElapsedMs, double fps,
        double contextMs) =>
        FrameWindow.Candidates(labElapsedMs - oakElapsedMs, fps, contextMs);

    public static IReadOnlyList<LabCandidate> BuildFromGaps(int seed,
        IReadOnlyList<FenceCandidate> fence, IEnumerable<int> gapFrames, int pressFrame = 0,
        int observableFrames = RouteTimeline.LabObservableFrames)
    {
        List<int> gaps = gapFrames.ToList();
        return Cross(seed, fence, gaps.Count, (candidate, g) => candidate.OakFrame + gaps[g],
            pressFrame, observableFrames);
    }

    public static IReadOnlyList<LabCandidate> Build(int seed, IReadOnlyList<FenceCandidate> fence,
        IEnumerable<int> labFrames, int pressFrame = 0,
        int observableFrames = RouteTimeline.LabObservableFrames)
    {
        List<int> frames = labFrames.ToList();
        return Cross(seed, fence, frames.Count, (_, i) => frames[i], pressFrame, observableFrames);
    }

    private static IReadOnlyList<LabCandidate> Cross(int seed, IReadOnlyList<FenceCandidate> fence,
        int perFence, Func<FenceCandidate, int, int> labFrame, int pressFrame, int observableFrames)
    {
        var shifts = new List<int> { 0 };
        if (fence.Count <= StreamShiftNarrowedParents)
        {
            for (int s = 1; s <= StreamShiftRadius; s++) shifts.Add(-s);
        }

        int perShift = fence.Count * perFence;
        var all = new LabCandidate[perShift * shifts.Count];
        Parallel.For(0, all.Length, i =>
        {
            FenceCandidate candidate = fence[i % perShift / perFence];
            all[i] = Simulate(seed, candidate, labFrame(candidate, i % perFence), pressFrame,
                observableFrames, shifts[i / perShift]);
        });

        var seen = new HashSet<string>();
        var candidates = new List<LabCandidate>();
        foreach (LabCandidate candidate in all)
        {
            if (seen.Add(Observable(candidate))) candidates.Add(candidate);
        }

        return candidates;
    }

    private static string Observable(LabCandidate candidate) =>
        candidate.AdvancesAtTextClose + "|" + string.Join(",",
            candidate.Observable.Select(e => $"{e.Npc}{e.Kind}{e.Direction}@{e.Frame}"));

    public static IReadOnlyList<LabOption> Group(IEnumerable<LabCandidate> candidates) =>
        candidates
            .GroupBy(Appearance)
            .Select(g => new LabOption(g.First(), g.ToList()))
            .ToList();

    private static string Appearance(LabCandidate candidate) =>
        string.Concat(candidate.Aide.Select(e => Directions.Letter(e.Direction)))
        + "/" + string.Concat(candidate.Scientist.Select(e => Directions.Letter(e.Direction)));

    private static readonly int[] MissedFrozenFrames = [912, 886, 937, 860, 963];

    public static MissedLab Missed(int seed, FenceCandidate fence, int oakPressFrame, int targetAdvances)
    {
        var runs = MissedFrozenFrames
            .Select(frozen => Simulate(seed, fence,
                fence.OakFrame + RouteTimeline.OakTextToLabLoadFrames + frozen,
                oakPressFrame + RouteTimeline.OakTextToLabLoadFrames + frozen))
            .ToList();

        LabCandidate lab = runs
            .GroupBy(run => run.CountdownFrame(targetAdvances))
            .OrderByDescending(g => g.Count())
            .First()
            .First();

        return new MissedLab(lab, new[] { NpcId.ScientistLeft, NpcId.ScientistRight }
            .Select(npc => MissedScientist(npc, runs))
            .ToList());
    }

    private static HiddenMoves MissedScientist(NpcId npc, IReadOnlyList<LabCandidate> runs)
    {
        HiddenMoves Of(LabCandidate run) => run.LabHidden!.First(h => h.Npc == npc);
        int Spins(LabCandidate run, int window) =>
            run.Live!.Events.Count(e => e.Npc == npc && e.Frame < window);

        int guaranteed = runs.Min(run => Spins(run, RouteTimeline.LabObservableFrames));
        bool again = runs.Any(run => Spins(run, RouteTimeline.LabObservableVeryLateFrames) > guaranteed);

        return new HiddenMoves(npc,
            runs.Min(run => Of(run).OffScreen),
            runs.Min(run => Of(run).Bonks),
            runs.Min(run => Of(run).SilentTurns))
        {
            NextSpin = again ? guaranteed + 1 : 0,
        };
    }

    public static LabCandidate Simulate(int seed, FenceCandidate fence, int labFrame,
        int pressFrame = 0, int observableFrames = RouteTimeline.LabObservableFrames,
        int streamShift = 0)
    {
        int frozenFrames = Math.Max(0,
            labFrame - fence.OakFrame - RouteTimeline.OakTextToLabLoadFrames);

        GameRng rng = GameRng.At(seed, fence.AdvancesBeforeLabLoad + streamShift, fence.Adapter);

        OverworldSim lab = RouteTimeline.EnterLab(rng, frozenFrames);
        lab.FreezeAll(false);

        int advancesAtTextClose = rng.Advances;

        var upcoming = new List<NpcEvent>();
        var live = new int[LiveHorizonFrames + 1];
        live[0] = advancesAtTextClose;
        for (int frame = 1; frame <= LiveHorizonFrames; frame++)
        {
            lab.StepFrame(upcoming);
            live[frame] = rng.Advances;
        }

        var events = upcoming
            .Select(e => e with { Frame = e.Frame - frozenFrames })
            .Where(e => e.Frame >= 0)
            .ToList();

        var record = new LabLive(live, events);
        var seedCandidate = new LabCandidate(fence, labFrame, pressFrame, frozenFrames, 0,
            Array.Empty<NpcEvent>(), Array.Empty<NpcEvent>(), advancesAtTextClose,
            Array.Empty<int>(), null, streamShift, record);

        return Rewindow(seedCandidate, observableFrames);
    }

    public static LabCandidate Rewindow(LabCandidate candidate, int observableFrames)
    {
        if (candidate.Live is not { } live)
            throw new InvalidOperationException("A hand-built candidate carries no live window to cut.");

        int window = Math.Clamp(observableFrames, 0, LiveHorizonFrames);
        int rate = candidate.Rate;

        var advancesByFrame = new int[HorizonFrames + 1];
        for (int frame = 0; frame <= HorizonFrames; frame++)
        {
            advancesByFrame[frame] = frame <= window
                ? live.Advances[frame]
                : live.Advances[window] + (frame - window) * rate;
        }

        List<NpcEvent> Window(NpcId npc) => live.Events
            .Where(e => e.Npc == npc && e.Frame < window)
            .ToList();

        List<NpcEvent> Restamped(NpcId npc) => Window(npc).Where(e => !e.Silent).ToList();

        List<HiddenMoves> hidden = new[] { NpcId.Aide, NpcId.ScientistLeft, NpcId.ScientistRight }
            .Select(npc => HiddenMoves.Count(npc, Window(npc),
                _ => RouteTimeline.LabObservable.Contains(npc)))
            .ToList();

        return candidate with
        {
            ObservableFrames = window,
            Aide = Restamped(NpcId.Aide),
            Scientist = Restamped(RouteTimeline.LabObservableScientist),
            AdvancesByFrame = advancesByFrame,
            LabHidden = hidden,
        };
    }

    public static IReadOnlyList<LabCandidate> BuildAdapter(int seed, IReadOnlyList<FenceCandidate> fence,
        double oakElapsedMs, double labElapsedMs, double? ballElapsedMs, double fps, double contextMs,
        double ballWindowMs)
    {
        List<int> gaps = GapFrames(oakElapsedMs, labElapsedMs, fps, contextMs).ToList();
        int pressFrame = PressFrame(labElapsedMs, fps);

        List<int>? measured = ballElapsedMs is { } ball
            ? FrameWindow.Candidates(ball - labElapsedMs, fps, ballWindowMs)
                .Select(w => w + BallPressWindowOffset).ToList()
            : null;

        var shifts = new List<int> { 0 };
        if (fence.Count <= StreamShiftNarrowedParents)
        {
            for (int s = 1; s <= StreamShiftRadius; s++) shifts.Add(-s);
        }

        int perShift = fence.Count * gaps.Count;
        var lives = new LabCandidate[perShift * shifts.Count];
        Parallel.For(0, lives.Length, i =>
        {
            FenceCandidate candidate = fence[i % perShift / gaps.Count];
            lives[i] = Simulate(seed, candidate, candidate.OakFrame + gaps[i % gaps.Count], pressFrame,
                LiveHorizonFrames, shifts[i / perShift]);
        });

        var seen = new HashSet<string>();
        var candidates = new List<LabCandidate>();
        foreach (LabCandidate live in lives)
        {
            IEnumerable<int> windows = measured ?? Windows(live.Live!);
            foreach (int window in windows)
            {
                LabCandidate cut = Rewindow(live, window);
                string key = Observable(cut) + "|" + cut.AdvancesAt(cut.ObservableFrames) + "|" + cut.ObservableFrames;
                if (seen.Add(key)) candidates.Add(cut);
            }
        }

        return candidates;
    }

    public const int BallPressWindowOffset = 0;

    private static IEnumerable<int> Windows(LabLive live)
    {
        var boundaries = new SortedSet<int> { RouteTimeline.LabObservableFrames };
        foreach (NpcEvent e in live.Events)
        {
            int opens = e.Frame + 1;
            if (opens > AdapterMaxWindowFrames) continue;
            if (opens > RouteTimeline.LabObservableFrames) boundaries.Add(opens);
            if (e.Kind == NpcEventKind.Step)
            {
                int end = e.Frame + ObjectEventSim.NormalWalkFrames;
                if (end > RouteTimeline.LabObservableFrames && end <= AdapterMaxWindowFrames)
                    boundaries.Add(end);
            }
        }

        return boundaries;
    }

    public static double WindowPrior(LabCandidate candidate, int targetAdvances)
    {
        if (candidate.Rate == 1 || candidate.Live is not { } live) return 1.0;
        if (!candidate.ParityOk(targetAdvances)) return WrongParityPrior;

        int first = -1;
        for (int w = RouteTimeline.LabObservableFrames; w <= candidate.ObservableFrames; w++)
        {
            bool ok = candidate.ParityOk(targetAdvances, w);
            if (ok && first < 0) first = w;
            if (!ok && first >= 0) return LaterWindowPrior;
        }

        return 1.0;
    }
}

public readonly record struct LabParity(bool NeedWalking, int LadyDelay)
{
    public static LabParity Of(int seed, FenceCandidate fence, int targetAdvances)
    {
        int labFrame = fence.OakFrame + RouteTimeline.OakTextToLabLoadFrames
            + RouteTimeline.LabLoadToReleaseFrames + 8;
        LabCandidate lab = LabRun.Simulate(seed, fence, labFrame, 0, RouteTimeline.LabObservableFrames);

        NpcEvent? first = lab.Live?.Events.FirstOrDefault(e => e.Npc == NpcId.Aide);
        int delay = first is { } e ? e.Frame : 0;

        return new LabParity(!lab.ParityOk(targetAdvances, 0), delay);
    }
}

public sealed record LabOption(LabCandidate Representative, IReadOnlyList<LabCandidate> Members)
{
    public IReadOnlyList<Direction> Aide =>
        Representative.Aide.Select(e => e.Direction).ToList();

    public IReadOnlyList<Direction> Scientist =>
        Representative.Scientist.Select(e => e.Direction).ToList();

    public (int Min, int Max) CorrectionSpan(int targetFrame, int manualAdvances = 0)
    {
        int min = int.MaxValue, max = int.MinValue;
        foreach (LabCandidate member in Members)
        {
            int correction = member.CountdownFrame(targetFrame, manualAdvances) - targetFrame;
            if (correction < min) min = correction;
            if (correction > max) max = correction;
        }

        return (min, max);
    }

    public SpawnReadSides CompatibleReads
    {
        get
        {
            SpawnReadSides sides = SpawnReadSides.None;
            foreach (LabCandidate member in Members) sides |= member.Fence.CompatibleReads;
            return sides;
        }
    }

    public bool IsExact(int targetFrame, int manualAdvances = 0)
    {
        (int min, int max) = CorrectionSpan(targetFrame, manualAdvances);
        return min == max;
    }

    public int Correction(int targetFrame, int manualAdvances = 0) =>
        Representative.CountdownFrame(targetFrame, manualAdvances) - targetFrame;

    public int AdvancesAtCountdownFrame(int countdownFrame, int manualAdvances = 0) =>
        Representative.AdvancesAtCountdownFrame(countdownFrame, manualAdvances);

    public override string ToString() =>
        $"{Directions.Format(Aide)} / {Directions.Format(Scientist)} ({Members.Count})";
}

public enum LabLateness
{
    Fast,

    Late,

    VeryLate,
}

public sealed class LabTracker
{
    public const double StreamShiftPrior = 0.15;

    private readonly IReadOnlyList<LabCandidate> _candidates;
    private readonly List<LabOption> _all;
    private readonly List<double> _likelihoods;
    private int? _focus;

    public LabTracker(IReadOnlyList<LabCandidate> candidates,
        double gapMs = 0.0, double fps = 0.0, double contextMs = 0.0,
        IReadOnlyDictionary<(int, int), double>? fenceBelief = null)
    {
        _candidates = candidates;
        _all = LabRun.Group(candidates).ToList();
        _gapMs = gapMs;
        _fps = fps;
        _contextMs = contextMs;
        _fenceBelief = fenceBelief;
        _likelihoods = Rank(_all, gapMs, fps, contextMs, fenceBelief, null);
    }

    public static LabTracker Build(int seed, IReadOnlyList<FenceCandidate> fence, double oakElapsedMs,
        double labElapsedMs, double fps, double contextMs,
        IReadOnlyList<double>? fenceLikelihoods = null, LabLateness lateness = LabLateness.Fast) =>
        new(LabRun.Build(seed, fence, oakElapsedMs, labElapsedMs, fps, contextMs, Window(lateness)),
            labElapsedMs - oakElapsedMs, fps, contextMs, MapFence(fence, fenceLikelihoods))
        {
            Lateness = lateness,
        };

    public static LabTracker BuildAdapter(int seed, IReadOnlyList<FenceCandidate> fence,
        double oakElapsedMs, double labElapsedMs, double? ballElapsedMs, double fps, double contextMs,
        IReadOnlyList<double>? fenceLikelihoods, int targetAdvances)
    {
        var tracker = new LabTracker(
            LabRun.BuildAdapter(seed, fence, oakElapsedMs, labElapsedMs, ballElapsedMs, fps, contextMs,
                contextMs),
            labElapsedMs - oakElapsedMs, fps, contextMs, MapFence(fence, fenceLikelihoods))
        {
            Adapter = true,
            BallMeasured = ballElapsedMs != null,
        };
        tracker.SetTarget(targetAdvances);
        return tracker;
    }

    public bool Adapter { get; private init; }

    public bool BallMeasured { get; private init; }

    public int Target { get; private set; }

    public bool SetTarget(int targetAdvances)
    {
        if (!Adapter || (Target == targetAdvances && _targetSet)) return false;

        Target = targetAdvances;
        _targetSet = true;
        if (!BallMeasured) KeepReachable(targetAdvances);
        _likelihoods.Clear();
        _likelihoods.AddRange(Rank(_all, _gapMs, _fps, _contextMs, _fenceBelief,
            member => LabRun.WindowPrior(member, targetAdvances)));
        return true;
    }

    private void KeepReachable(int targetAdvances)
    {
        LabCandidate? held = FocusPinned ? Focused?.Representative : null;

        List<LabCandidate> kept = _candidates.Where(c => c.ParityOk(targetAdvances)).ToList();
        _all.Clear();
        _all.AddRange(LabRun.Group(kept.Count > 0 ? kept : _candidates));

        _focus = null;
        if (held is { } candidate)
        {
            int index = IndexOf(candidate);
            if (index >= 0) _focus = index;
        }
    }

    private bool _targetSet;
    private readonly double _gapMs;
    private readonly double _fps;
    private readonly double _contextMs;
    private readonly IReadOnlyDictionary<(int, int), double>? _fenceBelief;

    public static int Window(LabLateness lateness) => lateness switch
    {
        LabLateness.VeryLate => RouteTimeline.LabObservableVeryLateFrames,
        LabLateness.Late => RouteTimeline.LabObservableLateFrames,
        _ => RouteTimeline.LabObservableFrames,
    };

    public LabLateness Lateness { get; private init; }

    public bool Late => Lateness != LabLateness.Fast;

    private static Dictionary<(int, int), double>? MapFence(IReadOnlyList<FenceCandidate> fence,
        IReadOnlyList<double>? likelihoods)
    {
        if (likelihoods == null || likelihoods.Count != fence.Count) return null;

        var map = new Dictionary<(int, int), double>();
        for (int i = 0; i < fence.Count; i++)
        {
            map[(fence[i].ExitFrame, fence[i].OakFrame)] = likelihoods[i];
        }

        return map;
    }

    private static List<double> Rank(List<LabOption> options, double gapMs, double fps,
        double contextMs, IReadOnlyDictionary<(int, int), double>? fence,
        Func<LabCandidate, double>? windowPrior)
    {
        var flat = Enumerable.Repeat(options.Count == 0 ? 0.0 : 1.0 / options.Count, options.Count)
            .ToList();
        if (fps <= 0.0 || options.Count == 0) return flat;

        var weights = new List<double>(options.Count);
        foreach (LabOption option in options)
        {
            double weight = 0.0;
            foreach (LabCandidate member in option.Members)
            {
                double gap = FrameWindow.Weight(gapMs, fps, contextMs, member.GapFrames);
                double belief = fence != null
                    && fence.TryGetValue((member.Fence.ExitFrame, member.Fence.OakFrame), out double f)
                    ? f
                    : 1.0;

                double shift = Math.Pow(StreamShiftPrior, Math.Abs(member.StreamShift));

                double window = windowPrior?.Invoke(member) ?? 1.0;

                weight += gap * belief * shift * window;
            }

            weights.Add(weight);
        }

        double total = weights.Sum();
        return total <= 0.0 ? flat : weights.Select(w => w / total).ToList();
    }

    public IReadOnlyList<LabOption> All => _all;

    public IReadOnlyList<double> Likelihoods => _likelihoods;

    public int MostLikelyIndex
    {
        get
        {
            int best = -1;
            for (int i = 0; i < _likelihoods.Count; i++)
            {
                if (best < 0 || _likelihoods[i] > _likelihoods[best]) best = i;
            }

            return best;
        }
    }

    public int FocusedIndex
    {
        get => _all.Count == 0 ? -1
            : _focus is { } pinned ? Math.Clamp(pinned, 0, _all.Count - 1)
            : MostLikelyIndex;
        set
        {
            if (_all.Count == 0) return;

            _focus = Math.Clamp(value, 0, _all.Count - 1);
        }
    }

    public LabOption? Focused => FocusedIndex < 0 ? null : _all[FocusedIndex];

    public bool FocusPinned => _focus is not null;

    public int IndexOf(LabCandidate candidate) =>
        Find(m => m.Fence.ExitFrame == candidate.Fence.ExitFrame
            && m.Fence.OakFrame == candidate.Fence.OakFrame
            && m.LabFrame == candidate.LabFrame);

    public int IndexOfFence(FenceCandidate fence) =>
        Find(m => m.Fence.ExitFrame == fence.ExitFrame && m.Fence.OakFrame == fence.OakFrame);

    private int Find(Func<LabCandidate, bool> match)
    {
        for (int i = 0; i < _all.Count; i++)
        {
            foreach (LabCandidate member in _all[i].Members)
            {
                if (match(member)) return i;
            }
        }

        return -1;
    }

    public void MoveFocus(int delta) => FocusedIndex = FocusedIndex + delta;

    public int ManualAdvances { get; set; }

    public int? Correction(int targetFrame) =>
        Focused?.Correction(targetFrame, ManualAdvances);

    public int? AdvancesAtCountdownFrame(int countdownFrame) =>
        Focused?.AdvancesAtCountdownFrame(countdownFrame, ManualAdvances);

    public (int Min, int Max)? CorrectionSpan(int targetFrame) =>
        Focused?.CorrectionSpan(targetFrame, ManualAdvances);
}
