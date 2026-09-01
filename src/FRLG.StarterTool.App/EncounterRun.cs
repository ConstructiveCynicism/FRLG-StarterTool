using System.Globalization;
using FRLG.StarterTool.Core.Encounters;
using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Timing;

namespace FRLG.StarterTool.App;

public readonly record struct EncounterLandingRow(
    string Press, string Target, string Landed, string Off, string Hit, double? Chance);

public readonly record struct EncounterNeighbourRow(
    string Window, string Frames, int Seed, int Encounters, double Rate, string Where, double Chance);

internal sealed class EncounterRun
{
    internal sealed class Target
    {
        internal Target(ManipPress press, double targetMs)
        {
            Press = press;
            TargetMs = targetMs;
        }

        public ManipPress Press { get; }

        public double TargetMs { get; }

        public double? DeltaMs { get; private set; }

        public double? Chance { get; private set; }

        public int? LandedFrame { get; private set; }

        public bool Missed { get; private set; }

        public bool Scored => DeltaMs != null;

        public bool Owed => !Scored && !Missed;

        internal void Score(double deltaMs, double chance, int landedFrame)
        {
            DeltaMs = deltaMs;
            Chance = chance;
            LandedFrame = landedFrame;
        }

        internal void Miss() => Missed = true;
    }

    private readonly VariableInfo _info;

    public EncounterRun(EncounterRoutePreset route, in VariableInfo info)
    {
        Route = route;
        _info = info;

        if (route.OffsetMs is int offsetMs)
        {
            _info.VisualOffset += offsetMs - _info.Offset;
            _info.Offset = offsetMs;
        }
        if (route.DelayMs != 0) _info.DelayOffset = 0;

        var targets = new List<Target>();
        foreach (ManipPress press in route.Presses())
        {
            targets.Add(new Target(press, EncounterManip.TargetMs(press, route.DelayMs, info.Fps)));
        }
        Targets = targets;
    }

    public EncounterRoutePreset Route { get; }

    public IReadOnlyList<Target> Targets { get; }

    public double Fps => _info.Fps;

    public double LastTargetMs => Targets.Count == 0 ? 0.0 : Targets[^1].TargetMs;

    private double CuedPressMs(double targetMs) => targetMs + _info.DelayOffset;

    public double LastPressMs => CuedPressMs(LastTargetMs);

    private double CueMs(double targetMs, double offsetMs)
        => CuedPressMs(targetMs)
           + VariableOffsetCalculator.TidLagFrames / _info.Fps * 1000.0
           + offsetMs;

    public double[] BeepSchedule(double elapsedMs)
    {
        var beeps = new List<double>();
        foreach (Target target in Targets)
        {
            beeps.AddRange(VariableOffsetCalculator.BeepSchedule(
                CueMs(target.TargetMs, _info.Offset) - elapsedMs, _info.Interval, _info.NumBeeps));
        }
        beeps.Sort();
        return beeps.ToArray();
    }

    public double[] FlashSchedule()
    {
        var flashes = new List<double>();
        foreach (Target target in Targets)
        {
            flashes.AddRange(VariableOffsetCalculator.FlashSchedule(
                CueMs(target.TargetMs, _info.VisualOffset), _info.Interval, _info.NumBeeps));
        }
        flashes.Sort();
        return flashes.ToArray();
    }

    public double EndSeconds => CueMs(LastTargetMs, _info.Offset) / 1000.0;

    public double Interval => _info.Interval;

    public double CloseMs => CuedPressMs(LastTargetMs) + VariableOffsetCalculator.LandingWindowMs(_info);

    public bool AllScored => Targets.All(target => target.Scored);

    public bool AnyOwed => Targets.Any(target => target.Owed);

    public Target? Owed(double elapsedMs)
    {
        foreach (Target target in Targets)
        {
            if (!target.Owed) continue;

            double deltaMs = elapsedMs - CuedPressMs(target.TargetMs);
            double window = deltaMs >= 0.0
                ? VariableOffsetCalculator.LandingWindowMs(_info)
                : VariableOffsetCalculator.EarlyLandingWindowMs(_info);
            if (Math.Abs(deltaMs) <= window) return target;
        }
        return null;
    }

    public void Score(Target target, double elapsedMs)
    {
        double deltaMs = elapsedMs - CuedPressMs(target.TargetMs);
        target.Score(
            deltaMs,
            EncounterManip.WindowChance(deltaMs, target.Press.Window, _info.Fps),
            EncounterManip.FrameAt(elapsedMs, Route.DelayMs + _info.DelayOffset, _info.Fps));

        foreach (Target earlier in Targets)
        {
            if (ReferenceEquals(earlier, target)) break;
            if (earlier.Owed) earlier.Miss();
        }
    }

    public void MissRest()
    {
        foreach (Target target in Targets)
        {
            if (target.Owed) target.Miss();
        }
    }

    public List<EncounterLandingRow> Rows()
    {
        var rows = new List<EncounterLandingRow>(Targets.Count);
        foreach (Target target in Targets)
        {
            string landed = target.Scored ? target.LandedFrame!.Value.ToString(CultureInfo.InvariantCulture)
                : target.Missed ? "none"
                : "";
            string off = target.Scored ? $"{target.DeltaMs!.Value:+0;-0;0} ms" : "";
            string hit = target.Scored ? MainForm.FormatChance(target.Chance!.Value)
                : target.Missed ? "missed"
                : "";
            rows.Add(new EncounterLandingRow(
                target.Press.Name, target.Press.Frames, landed, off, hit,
                target.Scored ? target.Chance : target.Missed ? 0.0 : null));
        }
        return rows;
    }

    public string Status()
    {
        if (Targets.Count == 0) return $"Route \"{Route.Name}\" has no presses set.";

        var parts = new List<string> { $"Route = \"{Route.Name}\"" };
        if (Route.DelayMs != 0) parts.Add($"Delay = {Route.DelayMs}ms");
        if (Route.OffsetMs is int offsetMs) parts.Add($"Offset = {offsetMs}ms");
        foreach (Target target in Targets)
        {
            if (target.Scored)
            {
                parts.Add($"{target.Press.Name} = {MainForm.FormatChance(target.Chance!.Value)} ({target.DeltaMs!.Value:+0;-0;0} ms)");
            }
            else if (target.Missed)
            {
                parts.Add($"{target.Press.Name} = missed");
            }
            else
            {
                parts.Add($"{target.Press.Name} = [{target.Press.Frames}]");
            }
        }

        return string.Join(", ", parts);
    }

    public double WorstChance
    {
        get
        {
            double worst = 1.0;
            foreach (Target target in Targets)
            {
                if (target.Scored) worst = Math.Min(worst, target.Chance!.Value);
                else if (target.Missed) worst = 0.0;
            }
            return worst;
        }
    }

    public string ArmLog()
    {
        var parts = new List<string>();
        foreach (Target target in Targets)
        {
            parts.Add(string.Format(CultureInfo.InvariantCulture,
                "{0} frame {1} due {2:F1} ms (final beep {3:F1} ms)",
                target.Press.Name, target.Press.Frames, CuedPressMs(target.TargetMs),
                CueMs(target.TargetMs, _info.Offset)));
        }
        return string.Format(CultureInfo.InvariantCulture,
            "encounter manip armed: route \"{0}\", reset delay {1:+#;-#;+0} ms, {2}, offset {3} ms, "
            + "countdown delay {4} ms, fps {5}",
            Route.Name, Route.DelayMs, string.Join("; ", parts), _info.Offset, _info.DelayOffset,
            _info.Fps);
    }
}
