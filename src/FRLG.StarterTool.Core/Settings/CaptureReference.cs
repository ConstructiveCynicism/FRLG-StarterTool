namespace FRLG.StarterTool.Core.Settings;

public sealed class CaptureReference
{
    public const int MaxFrames = 99;

    public string Image { get; set; } = "";

    public int Frames { get; set; }

    public static string FileName(int frames) =>
        frames.ToString("+0;-0;0", System.Globalization.CultureInfo.InvariantCulture) + ".png";

    public static string Describe(int frames) => frames switch
    {
        0 => "on target",
        1 => "1 frame late",
        -1 => "1 frame early",
        > 0 => frames + " frames late",
        _ => -frames + " frames early",
    };

    public CaptureReference Clone() => new() { Image = Image, Frames = Frames };
}
