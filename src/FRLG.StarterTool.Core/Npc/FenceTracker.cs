namespace FRLG.StarterTool.Core.Npc;

public readonly record struct FenceInput(Direction Direction, double ElapsedMs);

public sealed class FenceTracker
{
    private readonly List<FenceCandidate> _all;
    private readonly List<FenceCandidate> _alive;
    private readonly List<FenceInput> _inputs = new();

    private (int Exit, int Oak, SpawnRead Spawn, SpawnRead Respawn)? _focus;

    public FenceTracker(IReadOnlyList<FenceCandidate> candidates, double fps, double contextMs)
    {
        _all = candidates.ToList();
        _alive = candidates.ToList();
        Fps = fps;
        ContextMs = Math.Max(0.0, contextMs);
        Prune();
    }

    public static FenceTracker Build(int seed, double exitElapsedMs, double oakElapsedMs,
        double fps, double contextMs, int manualAdvances = 0,
        FenceGuyParity parity = FenceGuyParity.Post)
    {
        return new FenceTracker(
            FenceRun.Build(seed, exitElapsedMs, oakElapsedMs, fps, contextMs, manualAdvances, parity),
            fps, contextMs);
    }

    public double Fps { get; }

    public double ContextMs { get; }

    public IReadOnlyList<FenceCandidate> All => _all;

    public IReadOnlyList<FenceCandidate> Alive => _alive;

    public IReadOnlyList<FenceInput> Inputs => _inputs;

    public bool Complete { get; private set; }

    public IReadOnlyList<int> ExitFrames => _alive.Select(c => c.ExitFrame).Distinct().Order().ToList();

    public IReadOnlyList<int> TotalAdvances =>
        _alive.Select(c => c.TotalAdvances).Distinct().Order().ToList();

    public IReadOnlyList<double> Likelihoods => _likelihoods;

    public int MostLikelyIndex
    {
        get
        {
            if (_alive.Count == 0) return -1;

            int best = 0;
            for (int i = 1; i < _likelihoods.Count; i++)
            {
                if (_likelihoods[i] > _likelihoods[best]) best = i;
            }

            return best;
        }
    }

    public int FocusedIndex
    {
        get
        {
            if (_alive.Count == 0) return -1;
            if (_focus is not { } focus) return MostLikelyIndex;

            int index = _alive.FindIndex(c => c.ExitFrame == focus.Exit && c.OakFrame == focus.Oak
                && c.SpawnReadSide == focus.Spawn && c.RespawnReadSide == focus.Respawn);
            return index < 0 ? 0 : index;
        }
        set
        {
            if (_alive.Count == 0)
            {
                _focus = null;
                return;
            }

            int index = Math.Clamp(value, 0, _alive.Count - 1);
            _focus = (_alive[index].ExitFrame, _alive[index].OakFrame,
                _alive[index].SpawnReadSide, _alive[index].RespawnReadSide);
        }
    }

    public FenceCandidate? Focused => FocusedIndex < 0 ? null : _alive[FocusedIndex];

    public void MoveFocus(int delta) => FocusedIndex = FocusedIndex + delta;

    public bool LastTapRefused { get; private set; }

    public int Tap(Direction direction, double elapsedMs)
    {
        LastTapRefused = false;
        if (direction == Direction.None) return _alive.Count;

        int before = _alive.Count;
        _inputs.Add(new FenceInput(direction, elapsedMs));
        int after = Prune();

        if (after > 0 || before == 0) return after;

        _inputs.RemoveAt(_inputs.Count - 1);
        LastTapRefused = true;
        return Prune();
    }

    public int Undo()
    {
        LastTapRefused = false;
        if (_inputs.Count > 0) _inputs.RemoveAt(_inputs.Count - 1);
        return Prune();
    }

    public int Clear()
    {
        LastTapRefused = false;
        _inputs.Clear();
        Complete = false;
        _focus = null;
        return Prune();
    }

    public int SetComplete(bool complete)
    {
        Complete = complete;
        return Prune();
    }

    public int Observe(IReadOnlyList<FenceInput> inputs, bool complete = false)
    {
        LastTapRefused = false;
        _inputs.Clear();
        _inputs.AddRange(inputs.Where(i => i.Direction != Direction.None));
        Complete = complete;
        return Prune();
    }

    private readonly Dictionary<(int Exit, int Oak), int> _offsets = new();

    private readonly List<double> _likelihoods = new();

    private int Prune()
    {
        _alive.Clear();
        _offsets.Clear();
        _likelihoods.Clear();

        var scores = new List<double>();

        foreach (FenceCandidate candidate in _all)
        {
            int offset = MatchOffset(candidate, out double score);
            if (offset < 0) continue;

            _alive.Add(candidate);
            _offsets[(candidate.ExitFrame, candidate.OakFrame)] = offset;
            scores.Add(score + AnchorPrior(candidate));
        }

        Normalise(scores);
        return _alive.Count;
    }

    private static double AnchorPrior(FenceCandidate candidate) =>
        Math.Log(Math.Max(candidate.AnchorWeight, MinimumAnchorWeight));

    private const double MinimumAnchorWeight = 1e-6;

    private void Normalise(List<double> scores)
    {
        if (scores.Count == 0) return;

        double top = scores.Max();
        double total = 0.0;

        foreach (double score in scores)
        {
            double weight = Math.Exp(score - top);
            _likelihoods.Add(weight);
            total += weight;
        }

        for (int i = 0; i < _likelihoods.Count; i++) _likelihoods[i] /= total;
    }

    private int MatchOffset(FenceCandidate candidate, out double score)
    {
        int last = candidate.FirstRequiredEvent;

        int best = -1;
        double bestScore = double.NegativeInfinity;

        for (int offset = 0; offset <= last; offset++)
        {
            if (!Consistent(candidate, offset)) continue;

            double candidateScore = Score(candidate, offset);
            if (candidateScore <= bestScore) continue;

            best = offset;
            bestScore = candidateScore;
        }

        score = best < 0 ? 0.0 : bestScore;
        return best;
    }

    private double Score(FenceCandidate candidate, int offset)
    {
        if (_inputs.Count == 0) return 0.0;

        double frameMs = 1000.0 / Fps;
        double visibleMs = candidate.LeadWalkVisibleFrame * frameMs;

        double least = double.PositiveInfinity;
        var lags = new double[_inputs.Count];

        for (int i = 0; i < _inputs.Count; i++)
        {
            NpcEvent predicted = candidate.LeadWalk[offset + i];
            double stepMs = (candidate.LeadWalkStartFrame + predicted.Frame) * frameMs;

            lags[i] = _inputs[i].ElapsedMs - Math.Max(stepMs, visibleMs);
            if (lags[i] < least) least = lags[i];
        }

        double score = least >= 0.0 ? -least / LatencyScaleMs : least / EarlyScaleMs;

        foreach (double lag in lags) score -= (lag - least) / ReactionScaleMs;

        return score;
    }

    public const double ReactionScaleMs = 600.0;

    public const double LatencyScaleMs = 2000.0;

    public const double EarlyScaleMs = 50.0;

    private bool Consistent(FenceCandidate candidate, int offset)
    {
        IReadOnlyList<NpcEvent> predicted = candidate.LeadWalk;
        int visible = predicted.Count - offset;

        if (Complete
            ? visible != _inputs.Count
            : visible < _inputs.Count)
        {
            return false;
        }

        for (int i = 0; i < _inputs.Count; i++)
        {
            if (predicted[offset + i].Direction != _inputs[i].Direction) return false;
        }

        return true;
    }

    private int OffsetOf(FenceCandidate candidate) =>
        _offsets.TryGetValue((candidate.ExitFrame, candidate.OakFrame), out int offset) ? offset : 0;

    public IReadOnlyList<(IReadOnlyList<Direction> Next, IReadOnlyList<(int Exit, int Oak)> Pairs)>
        Continuations(int lookahead = 4)
    {
        return _alive
            .GroupBy(c => Directions.Format(
                c.LeadWalk.Skip(OffsetOf(c) + _inputs.Count).Take(lookahead).Select(e => e.Direction)))
            .Select(g => (
                (IReadOnlyList<Direction>)g.First().LeadWalk
                    .Skip(OffsetOf(g.First()) + _inputs.Count).Take(lookahead)
                    .Select(e => e.Direction).ToList(),
                (IReadOnlyList<(int, int)>)g
                    .Select(c => (c.ExitFrame, c.OakFrame)).Order().ToList()))
            .OrderBy(x => x.Item2[0])
            .ToList();
    }

    public (int Min, int Max) EventCount()
    {
        if (_alive.Count == 0) return (0, 0);

        return (_alive.Min(c => c.LeadWalk.Count - OffsetOf(c)),
                _alive.Max(c => c.LeadWalk.Count - OffsetOf(c)));
    }
}
