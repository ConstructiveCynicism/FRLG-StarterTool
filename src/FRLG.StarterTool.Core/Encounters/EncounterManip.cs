namespace FRLG.StarterTool.Core.Encounters;

public readonly record struct ManipPress(string Name, int Frame, int Window)
{
    public int Span => Math.Max(Window, 1);

    public int LastFrame => Frame + Span - 1;

    public string Frames => Span > 1
        ? Frame.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-"
          + LastFrame.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : Frame.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public const int MaxWindow = 60;

    public static ManipPress? Parse(string name, string? text)
    {
        string value = (text ?? "").Trim();
        int dash = value.IndexOf('-');
        if (Number(dash <= 0 ? value : value[..dash]) is not int first || first <= 0) return null;
        if (dash <= 0 || Number(value[(dash + 1)..]) is not int last || last <= first) return new ManipPress(name, first, 1);
        return new ManipPress(name, first, Math.Min(last - first + 1, MaxWindow));
    }

    public static readonly char[] Separators = { ',', '/' };

    public static List<ManipPress> ParseList(string name, string? text, int firstNumber = 2)
    {
        var presses = new List<ManipPress>();
        foreach (string item in (text ?? "").Split(Separators))
        {
            if (Parse($"{name} {firstNumber + presses.Count}", item) is ManipPress press) presses.Add(press);
        }
        return presses;
    }

    public static string FormatList(IEnumerable<ManipPress> presses) => string.Join(",", presses.Select(press => press.Frames));

    private static int? Number(string text) =>
        int.TryParse(text.Trim(), System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out int value) && value >= 0 ? value : null;
}

public static class EncounterManip
{
    public static double FrameMs(double fps) => 1000.0 / fps;

    public static double TargetMs(in ManipPress press, int delayMs, double fps)
        => (press.Frame - 1 + (press.Span - 1) / 2.0) * FrameMs(fps) + delayMs;

    public static double WindowChance(double deltaMs, int window, double fps)
    {
        double frameMs = FrameMs(fps);
        double halfSpan = Math.Max(window, 1) * frameMs / 2.0;
        return Math.Clamp((halfSpan - Math.Abs(deltaMs)) / frameMs + 0.5, 0.0, 1.0);
    }

    public static int FrameAt(double elapsedMs, int delayMs, double fps)
        => (int)Math.Floor((elapsedMs - delayMs) / FrameMs(fps) + 0.5) + 1;
}
