namespace FRLG.StarterTool.Core.Training;

public sealed class TrainingRound
{
    public int Number { get; init; }

    public int TargetFrame { get; init; }

    public int LandedFrame { get; init; }

    public double DeltaMs { get; init; }

    public double ErrorFrames { get; init; }

    public int OffsetUsed { get; init; }

    public double HitChance { get; init; }

    public bool Missed { get; init; }
}
