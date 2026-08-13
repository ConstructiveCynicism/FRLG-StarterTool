using FRLG.StarterTool.Core.Npc;

namespace FRLG.StarterTool.Core.Troubleshoot;

public sealed record FenceRow(
    int ExitFrame,
    int OakFrame,
    int RespawnFrame,
    int VisibleFrame,
    int Advances,
    IReadOnlyList<StripMove> Moves)
{
    public IReadOnlyList<StripToken> Strip => _strip ??= MovementStrip.Layout(Moves);

    private IReadOnlyList<StripToken>? _strip;

    public int OptionalLeadingMoves
    {
        get
        {
            int count = 0;
            while (count < Moves.Count && Moves[count].Frame < FullyVisibleFrame) count++;
            return count;
        }
    }

    private static int FullyVisibleFrame =>
        RouteTimeline.LeadWalkFatManFullyVisibleFrames - RouteTimeline.LeadWalkFatManRespawnFrames;

    public override string ToString() =>
        $"exit {ExitFrame} oak {OakFrame} -> {Advances} [{MovementStrip.Format(Strip)}]";
}

public sealed record LabRow(
    int Members,
    int LabFrame,
    int FrozenFrames,
    int Advances,
    IReadOnlyList<StripMove> Aide,
    IReadOnlyList<StripMove> Scientist,
    bool Focused)
{
    public IReadOnlyList<StripToken> AideStrip => _aide ??= MovementStrip.Layout(Aide);

    public IReadOnlyList<StripToken> ScientistStrip => _scientist ??= MovementStrip.Layout(Scientist);

    private IReadOnlyList<StripToken>? _aide;
    private IReadOnlyList<StripToken>? _scientist;

    public override string ToString() =>
        $"{MovementStrip.Format(AideStrip)} / {MovementStrip.Format(ScientistStrip)} -> {Advances}";
}

public sealed class RunRecord
{
    public string FileName { get; init; } = "";

    public string Path { get; init; } = "";

    public DateTime Started { get; init; }

    public int Seed { get; init; }

    public int ManualAdvances { get; init; }

    public IReadOnlyList<FenceRow> Fence { get; init; } = Array.Empty<FenceRow>();

    public IReadOnlyList<LabRow> Lab { get; init; } = Array.Empty<LabRow>();

    public int LabWindowFrames { get; init; }

    public int? Correction { get; init; }

    public int? EffectiveFrame { get; init; }

    public int? LandedFrame { get; init; }

    public int? TargetFrame { get; init; }

    public string Outcome { get; init; } = "";

    public IReadOnlyList<Direction> Taps { get; init; } = Array.Empty<Direction>();

    public int RefusedTaps { get; init; }

    public int? OakPressFrame { get; init; }

    public int? LabPressFrame { get; init; }

    public double Fps { get; init; } = DefaultFps;

    public const double DefaultFps = 59.7275;

    public double FrameMs => Fps > 0.0 ? 1000.0 / Fps : 1000.0 / DefaultFps;

    public bool HasField => Fence.Count > 0 || Lab.Count > 0;

    public override string ToString() =>
        $"{FileName}: seed {Seed}, {Fence.Count} fence, {Lab.Count} lab";
}
