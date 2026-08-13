using System.Globalization;

namespace FRLG.StarterTool.Core.Timing;

public static class VariableOffsetCalculator
{
    private const uint MaxIntervalAndBeeps = (uint)ushort.MaxValue << 9;

    private const uint MaxFrame = (uint)ushort.MaxValue << 8;

    public const int TidLagFrames = 3;

    public static TimerError Parse(
        string? frameText,
        string? fpsText,
        string? offsetText,
        string? intervalText,
        string? numBeepsText,
        out VariableInfo info)
        => Parse(frameText, fpsText, offsetText, "0", intervalText, numBeepsText, out info);

    public static TimerError Parse(
        string? frameText,
        string? fpsText,
        string? offsetText,
        string? visualOffsetText,
        string? intervalText,
        string? numBeepsText,
        out VariableInfo info)
    {
        info = new VariableInfo();

        string frame = StripAdjustment(frameText ?? string.Empty);

        if (!uint.TryParse(frame, NumberStyles.Integer, CultureInfo.InvariantCulture, out info.Frame))
        {
            return TimerError.InvalidFrame;
        }

        if (!double.TryParse(fpsText, NumberStyles.Float, CultureInfo.InvariantCulture, out info.Fps) || info.Fps <= 0.0)
        {
            return TimerError.InvalidFps;
        }

        if (!int.TryParse(offsetText, NumberStyles.Integer | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out info.Offset))
        {
            return TimerError.InvalidOffset;
        }

        if (string.IsNullOrWhiteSpace(visualOffsetText))
        {
            info.VisualOffset = 0;
        }
        else if (!int.TryParse(visualOffsetText, NumberStyles.Integer | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out info.VisualOffset))
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

        if (info.Interval >= MaxIntervalAndBeeps) return TimerError.InvalidInterval;
        if (info.NumBeeps >= MaxIntervalAndBeeps) return TimerError.InvalidNumBeeps;
        if (info.Frame >= MaxFrame) return TimerError.InvalidFrame;

        return TimerError.NoError;
    }

    public static string StripAdjustment(string frameText)
    {
        int plus = frameText.IndexOf('+');
        int minus = frameText.IndexOf('-');
        int cut = Math.Max(plus, minus);
        return cut < 0 ? frameText : frameText.Substring(0, cut);
    }

    public static string FormatFrameWithAdjustment(uint frame, double adjustedMs, double fps)
        => FormatFrameWithAdjustment(frame, FramesAdjusted(adjustedMs, fps));

    public static string FormatFrameWithAdjustment(uint frame, int frames)
    {
        string text = frame.ToString(CultureInfo.InvariantCulture);
        if (frames != 0)
        {
            text += frames.ToString("+#;-#", CultureInfo.InvariantCulture);
        }
        return text;
    }

    public static int FramesAdjusted(double adjustedMs, double fps) => (int)Math.Round(adjustedMs / 1000.0 * fps);

    public static int AdjustmentFrames(string frameText)
    {
        int cut = Math.Max(frameText.IndexOf('+'), frameText.IndexOf('-'));
        if (cut < 0) return 0;

        return int.TryParse(
            frameText.AsSpan(cut),
            NumberStyles.Integer | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out int frames)
            ? frames
            : 0;
    }

    public static int EffectiveFrame(in VariableInfo info)
        => Math.Max(0, (int)info.Frame + info.AdvanceCorrection);

    public static double TargetTimeSeconds(in VariableInfo info, double adjustedMs = 0.0)
        => EffectiveFrame(info) / info.Fps + (info.Offset + adjustedMs) / 1000.0;

    public static double LandingTargetMs(in VariableInfo info, double adjustedMs = 0.0)
        => (EffectiveFrame(info) - TidLagFrames) / info.Fps * 1000.0 + adjustedMs;

    public static int FrameAtTime(in VariableInfo info, double elapsedMs)
    {
        double frame = Math.Floor(elapsedMs / 1000.0 * info.Fps + 0.5) + TidLagFrames;
        return frame < 0.0 ? 0 : (int)frame;
    }

    public static double HalfFrameMs(double fps) => 500.0 / fps;

    public static double LandingDeltaMs(double elapsedMs, double targetMs) => elapsedMs - targetMs;

    public static double HitChance(double deltaMs, double fps)
    {
        double frameMs = 1000.0 / fps;
        return Math.Max(0.0, 1.0 - Math.Abs(deltaMs) / frameMs);
    }

    public static int AlternateFrame(int landedFrame, double deltaMs, double fps)
        => Math.Max(0, landedFrame + (Residual(deltaMs, fps) >= 0.0 ? 1 : -1));

    public static double AlternateChance(double deltaMs, double fps) => Math.Abs(Residual(deltaMs, fps));

    private static double Residual(double deltaMs, double fps)
    {
        double d = deltaMs / 1000.0 * fps;
        return d - Math.Floor(d + 0.5);
    }

    public static int TargetFrame(in VariableInfo info) => (int)info.Frame;

    public static int LandedFrame(in VariableInfo info, double elapsedMs, double adjustedMs)
        => Math.Max(0, FrameAtTime(info, elapsedMs)
                       - FramesAdjusted(adjustedMs, info.Fps)
                       - info.AdvanceCorrection);

    public static double BeepOffsetMs(in VariableInfo info, double elapsedMs, double adjustedMs = 0.0)
        => EffectiveFrame(info) / info.Fps * 1000.0 - elapsedMs + info.Offset + adjustedMs;

    public static double[] BeepSchedule(double finalBeepOffsetMs, uint intervalMs, uint numBeeps)
    {
        var beeps = new List<double>((int)numBeeps);
        for (int i = (int)numBeeps - 1; i >= 0; i--)
        {
            double t = finalBeepOffsetMs - i * (double)intervalMs;
            if (t >= 0.0) beeps.Add(t);
        }
        return beeps.ToArray();
    }

    public const double MinCueGapMs = 100.0;

    public static double[] RemainingSchedule(
        double finalOffsetMs, uint intervalMs, int remaining, double floorMs = 0.0)
    {
        if (remaining <= 0 || finalOffsetMs < floorMs) return Array.Empty<double>();

        double room = finalOffsetMs - floorMs;

        int count = Math.Min(remaining, (int)Math.Floor(room / MinCueGapMs) + 1);
        if (count <= 1) return new[] { finalOffsetMs };

        double spacing = Math.Min(intervalMs, room / (count - 1));

        var cues = new double[count];
        for (int i = 0; i < count; i++)
        {
            cues[i] = finalOffsetMs - (count - 1 - i) * spacing;
        }

        cues[^1] = finalOffsetMs;
        return cues;
    }

    public static double FlashTargetMs(in VariableInfo info, double adjustedMs = 0.0)
        => EffectiveFrame(info) / info.Fps * 1000.0 + adjustedMs + info.VisualOffset;

    public static double[] FlashSchedule(double finalFlashMs, uint intervalMs, uint numBeeps)
        => BeepSchedule(finalFlashMs, intervalMs, numBeeps);

    public static double FlashIntensity(double[] schedule, double elapsedMs, double intervalMs)
        => FlashIntensity(schedule, elapsedMs, intervalMs, out _);

    public static double FlashIntensity(double[] schedule, double elapsedMs, double intervalMs, out int index)
    {
        index = -1;
        if (schedule.Length == 0 || intervalMs <= 0.0) return 0.0;

        for (int i = schedule.Length - 1; i >= 0; i--)
        {
            if (elapsedMs < schedule[i]) continue;

            double age = elapsedMs - schedule[i];
            if (age >= intervalMs) return 0.0;

            index = i;
            return 1.0 - age / intervalMs;
        }

        return 0.0;
    }

    public static bool CanSubmit(in VariableInfo info, double currentTimeSeconds)
        => EffectiveFrame(info) / info.Fps + info.Offset / 1000.0
           >= currentTimeSeconds + info.Interval * (info.NumBeeps - 1) / 1000.0;

    public const double CueGuardMs = 50.0;

    public static double LeadInMs(in VariableInfo info) => info.Interval * ((double)info.NumBeeps - 1.0);

    public static double CountdownStartMs(
        in VariableInfo info, double adjustedMs, bool beepsEnabled, bool flashEnabled)
    {
        double lead = LeadInMs(info);
        double beeps = TargetTimeSeconds(info, adjustedMs) * 1000.0 - lead;
        double flash = FlashTargetMs(info, adjustedMs) - lead;

        if (flashEnabled) return beepsEnabled ? Math.Min(beeps, flash) : flash;

        return beeps;
    }

    public static bool CanAdjust(double currentTimeSeconds, double currentOffsetSeconds)
        => currentTimeSeconds < currentOffsetSeconds - CueGuardMs / 1000.0;

    public static double LandingWindowMs(in VariableInfo info) => Math.Max(info.Interval, 250.0);

    public static double EarlyLandingWindowMs(in VariableInfo info)
        => Math.Max(info.Interval * ((double)info.NumBeeps - 1.0), LandingWindowMs(info));

    public static double LandingCloseMs(in VariableInfo info, double adjustedMs = 0.0)
        => LandingTargetMs(info, adjustedMs) + LandingWindowMs(info);

    public static double AdjustmentMs(int numFrames, double fps) => numFrames * 1000.0 / fps;

    public static string ToFormattedString(this double val) => val.ToString("F3", CultureInfo.InvariantCulture);
}
