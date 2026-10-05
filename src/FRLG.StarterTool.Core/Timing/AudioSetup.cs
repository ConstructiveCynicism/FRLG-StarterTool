using System.Text.RegularExpressions;

namespace FRLG.StarterTool.Core.Timing;

public static class AudioSetup
{
    private static readonly Regex Device = new(
        "audio: device \"([^\"]*)\"", RegexOptions.CultureInvariant);

    private static readonly Regex Output = new(
        @"audio: output (\S+)", RegexOptions.CultureInvariant);

    public static bool Read(string line, ref string? device, ref string? output)
    {
        if (Device.Match(line) is { Success: true } d)
        {
            device = d.Groups[1].Value;
            output = null;
            return true;
        }
        if (Output.Match(line) is { Success: true } o)
        {
            output = o.Groups[1].Value;
            return true;
        }
        return false;
    }

    public static string? Key(string? device, string? output) =>
        device == null ? null : output == null ? device : device + " | " + output;

    public static string? Parse(IEnumerable<string> lines)
    {
        string? device = null;
        string? output = null;
        foreach (string line in lines) Read(line, ref device, ref output);
        return Key(device, output);
    }

    public static bool Matches(string? pressSetup, string? setup) =>
        string.Equals(pressSetup, setup, StringComparison.Ordinal);
}
