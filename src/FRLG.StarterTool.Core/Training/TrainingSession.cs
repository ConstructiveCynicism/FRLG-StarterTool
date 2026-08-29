using FRLG.StarterTool.Core.Timing;

namespace FRLG.StarterTool.Core.Training;

public sealed class TrainingSession
{
    public const double MinLeadMs = 1000.0;

    public const double LeadSpreadMs = 3000.0;

    private readonly List<TrainingRound> _rounds = new();

    public TrainingSession(int roundCount, int initialOffsetMs, double fps, bool visual = false)
    {
        RoundCount = Math.Max(1, roundCount);
        InitialOffsetMs = initialOffsetMs;
        Fps = fps > 0.0 ? fps : 60.0;
        Visual = visual;
    }

    public int RoundCount { get; private set; }

    public void SetRoundCount(int roundCount) => RoundCount = Math.Max(1, roundCount);

    public int InitialOffsetMs { get; }

    public bool Visual { get; }

    public double Fps { get; }

    public OffsetTuner Tuner { get; } = new();

    public IReadOnlyList<TrainingRound> Rounds => _rounds;

    public int CompletedRounds => Tuner.Observations;

    public int CurrentRound => Math.Min(CompletedRounds + 1, RoundCount);

    public bool IsComplete => CompletedRounds >= RoundCount;

    public int RecommendedOffsetMs => Tuner.RecommendedOffsetMs(InitialOffsetMs, Fps);

    public double ExpectedHits
    {
        get
        {
            double total = 0.0;
            foreach (TrainingRound round in _rounds)
            {
                if (!round.Missed) total += round.HitChance;
            }
            return total;
        }
    }

    public double AverageHitChance => CompletedRounds == 0 ? 0.0 : ExpectedHits / CompletedRounds;

    public uint NextTargetFrame(in VariableInfo info, Random random)
    {
        double firstBeepMs = MinLeadMs + random.NextDouble() * LeadSpreadMs;
        double finalBeepMs = firstBeepMs + (info.NumBeeps - 1) * (double)info.Interval;

        double frame = Math.Ceiling(
            (finalBeepMs - (Visual ? info.VisualOffset : info.Offset) - info.DelayOffset)
            / 1000.0 * info.Fps);

        return frame < 0.0 ? 0u : (uint)frame;
    }

    public double ErrorFrames(double deltaMs, int offsetUsed) =>
        (deltaMs - (offsetUsed - InitialOffsetMs)) / 1000.0 * Fps;

    public TrainingRound Record(double deltaMs, int offsetUsed, int landedFrame, int targetFrame, double hitChance)
    {
        double error = ErrorFrames(deltaMs, offsetUsed);

        var round = new TrainingRound
        {
            Number = CurrentRound,
            TargetFrame = targetFrame,
            LandedFrame = landedFrame,
            DeltaMs = deltaMs,
            ErrorFrames = error,
            OffsetUsed = offsetUsed,
            HitChance = hitChance
        };

        Tuner.Observe(error);
        _rounds.Add(round);

        return round;
    }

    public TrainingRound MarkMissed(int targetFrame)
    {
        var round = new TrainingRound
        {
            Number = CurrentRound,
            TargetFrame = targetFrame,
            LandedFrame = -1,
            OffsetUsed = InitialOffsetMs,
            Missed = true
        };

        _rounds.Add(round);
        return round;
    }
}
