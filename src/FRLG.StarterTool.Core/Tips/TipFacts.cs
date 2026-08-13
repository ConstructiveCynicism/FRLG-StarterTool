namespace FRLG.StarterTool.Core.Tips;

public readonly record struct TipFacts
{
    public bool BallPressUnseen { get; init; }

    public bool TrainerUsed { get; init; }

    public bool OddsCalculated { get; init; }

    public bool FenceStopReported { get; init; }

    public bool CuedLabPress { get; init; }

    public double ContextWindowMs { get; init; }

    public bool DefaultWindowSize { get; init; }

    public bool DefaultStatBoxColors { get; init; }

    public bool OffsetsShared { get; init; }

    public int MissStreak { get; init; }

    public int? SuggestedOffsetMs { get; init; }

    public int RecentLikelyHits { get; init; }

    public int RecentAttempts { get; init; }

    public bool LastLikelyHit { get; init; }

    public long HiddenRolls { get; init; }

    public int Attempts { get; init; }

    public int LikelyHits { get; init; }
}
