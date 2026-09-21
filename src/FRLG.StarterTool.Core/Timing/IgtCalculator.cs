using System.Globalization;

namespace FRLG.StarterTool.Core.Timing;

public struct IgtTimerInfo
{
    public uint Frame;

    public int Offset;
    public uint Interval;
    public uint NumBeeps;
    public double Fps;
}

public static class IgtCalculator
{
    public const int Repeats = 10;

    private const uint MaxIntervalAndBeeps = (uint)ushort.MaxValue << 9;

    private const uint MaxFrame = (uint)ushort.MaxValue << 8;

    public static TimerError Parse(
        string? frameText,
        string? offsetText,
        string? intervalText,
        string? numBeepsText,
        string? fpsText,
        out IgtTimerInfo info)
    {
        info = new IgtTimerInfo();

        if (!uint.TryParse(frameText, NumberStyles.Integer, CultureInfo.InvariantCulture, out info.Frame))
        {
            return TimerError.InvalidFrame;
        }

        if (!int.TryParse(offsetText, NumberStyles.Integer | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out info.Offset))
        {
            return TimerError.InvalidOffset;
        }

        if (!uint.TryParse(intervalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out info.Interval))
        {
            return TimerError.InvalidInterval;
        }

        if (!uint.TryParse(numBeepsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out info.NumBeeps))
        {
            return TimerError.InvalidNumBeeps;
        }

        if (!double.TryParse(fpsText, NumberStyles.Float, CultureInfo.InvariantCulture, out info.Fps) || info.Fps <= 0.0)
        {
            return TimerError.InvalidFps;
        }

        if (info.Frame >= MaxFrame) return TimerError.InvalidFrame;
        if (info.Offset >= MaxIntervalAndBeeps) return TimerError.InvalidOffset;
        if (info.Interval >= MaxIntervalAndBeeps) return TimerError.InvalidInterval;
        if (info.NumBeeps == 0 || info.NumBeeps >= MaxIntervalAndBeeps) return TimerError.InvalidNumBeeps;

        return TimerError.NoError;
    }

    public static double SecondMs(double fps) => 60.0 / fps * 1000.0;

    public static double[] PlayOffsets(in IgtTimerInfo info, double elapsedMs, double adjustedMs)
    {
        double second = SecondMs(info.Fps);
        double lead = info.Interval * ((double)info.NumBeeps - 1.0);
        double offset = info.Frame / info.Fps * 1000.0 - elapsedMs + info.Offset + adjustedMs;

        if (offset - lead < 0.0) offset += Math.Ceiling((lead - offset) / second) * second;

        var offsets = new double[Repeats];
        for (int i = 0; i < Repeats; i++) offsets[i] = offset + second * i;
        return offsets;
    }

    public static double[] BeepSchedule(in IgtTimerInfo info, double[] finalOffsetsMs)
    {
        var beeps = new List<double>(finalOffsetsMs.Length * (int)info.NumBeeps);
        foreach (double final in finalOffsetsMs)
        {
            beeps.AddRange(VariableOffsetCalculator.BeepSchedule(final, info.Interval, info.NumBeeps));
        }

        beeps.Sort();
        return beeps.ToArray();
    }
}
