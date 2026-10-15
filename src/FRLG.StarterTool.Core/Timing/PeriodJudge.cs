namespace FRLG.StarterTool.Core.Timing;

public static class PeriodJudge
{
    public static bool OverPulls(double pullFrames, double clockFrames, double allowanceFrames)
        => pullFrames - clockFrames > allowanceFrames;

    public static double? NextPeriodMs(double periodMs, double defaultPeriodMs)
        => periodMs < defaultPeriodMs - 0.01 ? Math.Min(periodMs * 2.0, defaultPeriodMs) : null;
}
