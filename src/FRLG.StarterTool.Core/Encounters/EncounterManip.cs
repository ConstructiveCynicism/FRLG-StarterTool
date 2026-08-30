namespace FRLG.StarterTool.Core.Encounters;

public readonly record struct ManipPress(string Name, int Frame, int Window)
{
    public int Span => Math.Max(Window, 1);

    public int LastFrame => Frame + Span - 1;

    public string Frames => Span > 1
        ? Frame.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-"
          + LastFrame.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : Frame.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public static class EncounterManip
{
    public static double FrameMs(double fps) => 1000.0 / fps;

    public static double TargetMs(in ManipPress press, int delayMs, double fps)
        => (press.Frame + (press.Span - 1) / 2.0) * FrameMs(fps) + delayMs;

    public static double WindowChance(double deltaMs, int window, double fps)
    {
        double frameMs = FrameMs(fps);
        double halfSpan = Math.Max(window, 1) * frameMs / 2.0;
        return Math.Clamp((halfSpan - Math.Abs(deltaMs)) / frameMs + 0.5, 0.0, 1.0);
    }

    public static int FrameAt(double elapsedMs, int delayMs, double fps)
        => (int)Math.Floor((elapsedMs - delayMs) / FrameMs(fps) + 0.5);
}
