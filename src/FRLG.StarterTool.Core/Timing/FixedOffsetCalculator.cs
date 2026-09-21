using System.Globalization;

namespace FRLG.StarterTool.Core.Timing;

public enum FixedTargetUnit
{
    Milliseconds,

    Frames
}

public struct FixedTimerInfo
{
    public double[] Targets;

    public string[] Labels;

    public double[] WindowFrames;

    public double[] TargetsMs;

    public FixedTargetUnit Unit;

    public VariableInfo Schedule;
}

public readonly record struct FixedCue(int Target, int Beat, double TimeMs, bool Final);

public static class FixedOffsetCalculator
{
    private const double MaxTargetMs = (uint)ushort.MaxValue << 9;

    public static readonly char[] Separators = { '/', ',' };

    public static TimerError Parse(
        string? offsetsText,
        string? intervalText,
        string? numBeepsText,
        FixedTargetUnit unit,
        string? fpsText,
        string? audioOffsetText,
        string? visualOffsetText,
        string? delayOffsetText,
        out FixedTimerInfo info)
    {
        info = new FixedTimerInfo
        {
            Unit = unit,
            Targets = Array.Empty<double>(),
            TargetsMs = Array.Empty<double>(),
            Labels = Array.Empty<string>(),
            WindowFrames = Array.Empty<double>()
        };

        TimerError error = VariableOffsetCalculator.Parse(
            "0", fpsText, Blank(audioOffsetText), visualOffsetText, delayOffsetText, intervalText, numBeepsText,
            out info.Schedule);
        if (error != TimerError.NoError) return error;

        info.Schedule.NoInputLag = true;
        if (info.Schedule.NumBeeps == 0) return TimerError.InvalidNumBeeps;

        double fps = info.Schedule.Fps;
        double frameMs = 1000.0 / fps;

        string[] parts = (offsetsText ?? "").Split(Separators);
        var parsed = new List<(double Target, string Label, double Window)>(parts.Length);
        foreach (string part in parts)
        {
            string text = part.Trim();
            int dash = text.IndexOf('-');
            if (!Number(dash < 0 ? text : text[..dash], out double first)) return TimerError.InvalidOffset;

            if (dash < 0)
            {
                parsed.Add((first, text, 1.0));
                continue;
            }

            if (!Number(text[(dash + 1)..], out double last) || last <= first) return TimerError.InvalidOffset;

            double window = unit == FixedTargetUnit.Frames ? last - first + 1.0 : (last - first) / frameMs + 1.0;
            parsed.Add(((first + last) / 2.0, text, window));
        }

        parsed.Sort((a, b) => a.Target.CompareTo(b.Target));
        info.Targets = parsed.Select(entry => entry.Target).ToArray();
        info.Labels = parsed.Select(entry => entry.Label).ToArray();
        info.WindowFrames = parsed.Select(entry => entry.Window).ToArray();
        info.TargetsMs = Array.ConvertAll(info.Targets, t => ToMs(t, unit, fps));

        foreach (double ms in info.TargetsMs)
        {
            if (ms >= MaxTargetMs) return TimerError.InvalidOffset;
        }

        return TimerError.NoError;

        static string Blank(string? text) => string.IsNullOrWhiteSpace(text) ? "0" : text;

        static bool Number(string text, out double value) =>
            double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value) && value >= 0.0;
    }

    public static double HitChance(in FixedTimerInfo info, int target, double deltaMs)
    {
        double frameMs = 1000.0 / info.Schedule.Fps;
        double halfSpan = Math.Max(info.WindowFrames[target], 1.0) * frameMs / 2.0;
        return Math.Clamp((halfSpan - Math.Abs(deltaMs)) / frameMs + 0.5, 0.0, 1.0);
    }

    public static double ToMs(double target, FixedTargetUnit unit, double fps) =>
        unit == FixedTargetUnit.Frames ? target / fps * 1000.0 : target;

    public static string ConvertOffsets(string offsetsText, FixedTargetUnit from, FixedTargetUnit to, double fps)
    {
        if (from == to || fps <= 0.0) return offsetsText;

        return System.Text.RegularExpressions.Regex.Replace(offsetsText, @"\d+(\.\d+)?", match =>
        {
            double value = double.Parse(match.Value, CultureInfo.InvariantCulture);
            double converted = to == FixedTargetUnit.Frames ? value / 1000.0 * fps : value / fps * 1000.0;
            return Math.Round(converted, to == FixedTargetUnit.Frames ? 2 : 0)
                .ToString("0.##", CultureInfo.InvariantCulture);
        });
    }

    public static double LandingTargetMs(in FixedTimerInfo info, int target, double shiftMs) =>
        info.TargetsMs[target] + info.Schedule.DelayOffset + shiftMs;

    public static double BeepTargetMs(in FixedTimerInfo info, int target, double shiftMs) =>
        LandingTargetMs(info, target, shiftMs) + info.Schedule.Offset;

    public static double FlashTargetMs(in FixedTimerInfo info, int target, double shiftMs) =>
        LandingTargetMs(info, target, shiftMs) + info.Schedule.VisualOffset;

    public static double EndSeconds(in FixedTimerInfo info, IReadOnlyList<double> shiftsMs)
    {
        double end = 0.0;
        for (int i = 0; i < info.TargetsMs.Length; i++)
        {
            end = Math.Max(end, BeepTargetMs(info, i, shiftsMs[i]));
        }

        return Math.Max(end / 1000.0, 0.001);
    }

    public static List<FixedCue> Cues(in FixedTimerInfo info, IReadOnlyList<double> shiftsMs, bool audio)
    {
        var cues = new List<FixedCue>();
        int beats = (int)info.Schedule.NumBeeps;

        for (int target = 0; target < info.TargetsMs.Length; target++)
        {
            double final = audio
                ? BeepTargetMs(info, target, shiftsMs[target])
                : FlashTargetMs(info, target, shiftsMs[target]);

            for (int beat = 0; beat < beats; beat++)
            {
                double time = final - (beats - 1 - beat) * (double)info.Schedule.Interval;
                if (time >= 0.0) cues.Add(new FixedCue(target, beat, time, beat == beats - 1));
            }
        }

        cues.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        return cues;
    }

    public static int TargetOf(
        in FixedTimerInfo info, IReadOnlyList<double> shiftsMs, IReadOnlyList<bool> scored, double elapsedMs)
    {
        double late = VariableOffsetCalculator.LandingWindowMs(info.Schedule);
        double early = VariableOffsetCalculator.EarlyLandingWindowMs(info.Schedule);

        int best = -1;
        double bestDistance = double.MaxValue;
        for (int i = 0; i < info.TargetsMs.Length; i++)
        {
            if (scored[i]) continue;

            double delta = elapsedMs - LandingTargetMs(info, i, shiftsMs[i]);
            if (delta > late || -delta > early) continue;
            if (Math.Abs(delta) >= bestDistance) continue;

            best = i;
            bestDistance = Math.Abs(delta);
        }

        return best;
    }

    public static int NextTarget(in FixedTimerInfo info, IReadOnlyList<double> shiftsMs, double elapsedMs)
    {
        for (int i = 0; i < info.TargetsMs.Length; i++)
        {
            if (LandingTargetMs(info, i, shiftsMs[i]) > elapsedMs) return i;
        }

        return -1;
    }

    public static int TargetFrame(in FixedTimerInfo info, int target) =>
        (int)Math.Round(info.Unit == FixedTargetUnit.Frames
            ? info.Targets[target]
            : info.TargetsMs[target] / 1000.0 * info.Schedule.Fps);

    public static int LandedFrame(in FixedTimerInfo info, int target, double deltaMs) =>
        Math.Max(0, (int)Math.Floor((info.TargetsMs[target] + deltaMs) / 1000.0 * info.Schedule.Fps + 0.5));
}
