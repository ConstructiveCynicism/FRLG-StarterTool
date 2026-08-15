namespace FRLG.StarterTool.Core.Tips;

public sealed class TipAttempt
{
    public double? DeltaMs { get; set; }

    public int OffsetMs { get; set; }

    public DateTime? ClosedAt { get; set; }

    public double HitChance { get; set; }

    public bool LikelyHit => HitChance >= RunTip.LikelyHitChance;
}
