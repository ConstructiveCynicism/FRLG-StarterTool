using System.Globalization;

namespace FRLG.StarterTool.Core.Timing;

public static class FrameTime
{
    public static string Format(int frame, double fps)
    {
        if (fps <= 0.0 || frame < 0) return "-";

        double totalSeconds = frame / fps;
        int minutes = (int)(totalSeconds / 60.0);
        int seconds = (int)totalSeconds % 60;
        int centis = (int)((totalSeconds - Math.Floor(totalSeconds)) * 100.0);

        return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}.{2:00}", minutes, seconds, centis);
    }
}
