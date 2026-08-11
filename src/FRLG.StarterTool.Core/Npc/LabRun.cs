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
    IReadOnlyList<HiddenMoves>? LabHidden = null)
{
    public bool Completes(NpcEvent e) =>
        e.Kind != NpcEventKind.Step || e.Frame + ObjectEventSim.NormalWalkFrames <= ObservableFrames;

    public int GapFrames => LabFrame - Fence.OakFrame;

    public int AdvancesAt(int framesSinceTextClose)
    {
        if (framesSinceTextClose <= 0) return AdvancesAtTextClose;
        if (framesSinceTextClose < AdvancesByFrame.Count) return AdvancesByFrame[framesSinceTextClose];

        return AdvancesByFrame[^1] + (framesSinceTextClose - AdvancesByFrame.Count + 1);
    }

    public int FramesTo(int targetAdvances)
    {
        if (targetAdvances <= AdvancesAtTextClose) return 0;

        for (int frame = 1; frame < AdvancesByFrame.Count; frame++)
        {
            if (AdvancesByFrame[frame] >= targetAdvances) return frame;
        }

        return AdvancesByFrame.Count - 1 + (targetAdvances - AdvancesByFrame[^1]);
    }

    public int CountdownFrame(int targetAdvances, int manualAdvances = 0) =>
        LabPressFrame
        + FramesTo(targetAdvances - manualAdvances - RouteTimeline.BallGenerationAdvances)
        + VariableOffsetCalculator.TidLagFrames;

    public int AdvancesAtCountdownFrame(int countdownFrame, int manualAdvances = 0) =>
        AdvancesAt(countdownFrame - VariableOffsetCalculator.TidLagFrames - LabPressFrame)
        + manualAdvances + RouteTimeline.BallGenerationAdvances;

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

public static class LabRun
{
    public const int HorizonFrames = 1200;

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
        var all = new LabCandidate[fence.Count * perFence];
        Parallel.For(0, all.Length, i =>
        {
            FenceCandidate candidate = fence[i / perFence];
            all[i] = Simulate(seed, candidate, labFrame(candidate, i % perFence), pressFrame,
                observableFrames);
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

    public static LabCandidate Simulate(int seed, FenceCandidate fence, int labFrame,
        int pressFrame = 0, int observableFrames = RouteTimeline.LabObservableFrames)
    {
        int frozenFrames = Math.Max(0,
            labFrame - fence.OakFrame - RouteTimeline.OakTextToLabLoadFrames);

        GameRng rng = GameRng.At(seed, fence.AdvancesBeforeLabLoad);

        OverworldSim lab = RouteTimeline.EnterLab(rng, frozenFrames);
        lab.FreezeAll(false);

        int advancesAtTextClose = rng.Advances;

        var upcoming = new List<NpcEvent>();
        var advancesByFrame = new int[HorizonFrames + 1];
        advancesByFrame[0] = advancesAtTextClose;

        for (int frame = 1; frame <= HorizonFrames; frame++)
        {
            if (frame == observableFrames + 1) lab.FreezeAll(true);

            lab.StepFrame(upcoming);
            advancesByFrame[frame] = rng.Advances;
        }

        List<NpcEvent> Window(NpcId npc) => upcoming
            .Where(e => e.Npc == npc)
            .Select(e => e with { Frame = e.Frame - frozenFrames })
            .Where(e => e.Frame >= 0 && e.Frame <= observableFrames)
            .ToList();

        List<NpcEvent> Restamped(NpcId npc) => Window(npc).Where(e => !e.Silent).ToList();

        List<HiddenMoves> hidden = new[] { NpcId.Aide, NpcId.ScientistLeft, NpcId.ScientistRight }
            .Select(npc => HiddenMoves.Count(npc, Window(npc),
                _ => RouteTimeline.LabObservable.Contains(npc)))
            .ToList();

        return new LabCandidate(fence, labFrame, pressFrame, frozenFrames, observableFrames,
            Restamped(NpcId.Aide), Restamped(RouteTimeline.LabObservableScientist),
            advancesAtTextClose, advancesByFrame, hidden);
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

public sealed class LabTracker
{
    private readonly List<LabOption> _all;
    private readonly List<double> _likelihoods;
    private int? _focus;

    public LabTracker(IReadOnlyList<LabCandidate> candidates,
        double gapMs = 0.0, double fps = 0.0, double contextMs = 0.0,
        IReadOnlyDictionary<(int, int), double>? fenceBelief = null)
    {
        _all = LabRun.Group(candidates).ToList();
        _likelihoods = Rank(_all, gapMs, fps, contextMs, fenceBelief);
    }

    public static LabTracker Build(int seed, IReadOnlyList<FenceCandidate> fence, double oakElapsedMs,
        double labElapsedMs, double fps, double contextMs,
        IReadOnlyList<double>? fenceLikelihoods = null, bool late = false) =>
        new(LabRun.Build(seed, fence, oakElapsedMs, labElapsedMs, fps, contextMs, Window(late)),
            labElapsedMs - oakElapsedMs, fps, contextMs, MapFence(fence, fenceLikelihoods))
        {
            Late = late,
        };

    public static int Window(bool late) =>
        late ? RouteTimeline.LabObservableLateFrames : RouteTimeline.LabObservableFrames;

    public bool Late { get; private init; }

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
        double contextMs, IReadOnlyDictionary<(int, int), double>? fence)
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

                weight += gap * belief;
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
