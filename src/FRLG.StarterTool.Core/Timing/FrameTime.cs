namespace FRLG.StarterTool.Core.Timing;

public static class FrameTime
{
    public static string Format(int frame, double fps, TimeFormat format)
    {
        if (fps <= 0.0 || frame < 0) return "-";

        return TimeText.Format(frame / fps, format);
    }
}
