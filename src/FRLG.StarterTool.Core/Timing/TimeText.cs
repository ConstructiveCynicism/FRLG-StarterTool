using System.Globalization;

namespace FRLG.StarterTool.Core.Timing;

public enum TimeFormat
{
    Seconds,

    Minutes
}

public static class TimeText
{
    public static string Format(double seconds, TimeFormat format)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return "-";

        long ms = (long)Math.Round(Math.Abs(seconds) * 1000.0, MidpointRounding.AwayFromZero);

        string sign = seconds < 0.0 && ms != 0L ? "-" : "";
        long whole = ms / 1000L;
        long frac = ms % 1000L;

        return format == TimeFormat.Minutes
            ? string.Format(CultureInfo.InvariantCulture, "{0}{1}:{2:00}.{3:000}", sign, whole / 60L, whole % 60L, frac)
            : string.Format(CultureInfo.InvariantCulture, "{0}{1}.{2:000}", sign, whole, frac);
    }

    public static string Widest(TimeFormat format) => format == TimeFormat.Minutes ? "16:39.999" : "999.999";
}
