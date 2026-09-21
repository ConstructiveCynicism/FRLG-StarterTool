using System.Globalization;
using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Timing;

namespace FRLG.StarterTool.App;

public sealed class FixedOffsetTimer : BaseTimer
{
    private sealed class Track
    {
        public required string Name;
        public FixedTimerInfo Info;

        public int[] ExtraFrames = Array.Empty<int>();

        public bool[] Scored = Array.Empty<bool>();

        public int StandingFrames;
    }

    private readonly MainForm _form;

    private List<Track> _tracks = new();

    private bool _valid;

    private int _adjustFrames;

    private bool _hasTargets;

    private double _landingTimerStart;
    private double _landingStartLagMs;

    private readonly HashSet<(int Track, int Target, int Beat)> _beepsDelivered = new();

    private List<(int Track, FixedCue Cue)> _flashPlan = new();

    private double[] _beepTargetsMs = Array.Empty<double>();

    private bool _writingAdjust;

    public FixedOffsetTimer(MainForm form)
    {
        _form = form;
    }

    private FixedOffsetPanel Panel => _form.FixedPanel;

    private bool Active => ReferenceEquals(StarterTool.CurrentTab, this);

    public override void OnInit()
    {
        _adjustFrames = StarterTool.Settings.FixedAdjustFrames;

        Panel.SelectionChanged += (_, _) =>
        {
            if (Panel.AdjustAll || _adjustFrames == 0) return;

            _adjustFrames = 0;
            if (Active) WriteAdjustBox();
        };
        Panel.Changed += (_, _) => ShowIdle();

        _form.TextBoxFrame.TextChanged += (_, _) =>
        {
            if (!Active || _writingAdjust) return;

            int.TryParse(_form.TextBoxFrame.Text.Trim(), NumberStyles.Integer | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out _adjustFrames);
            if (StarterTool.IsTimerRunning) Requeue();
            else ShowIdle();
        };

        foreach (Control control in new Control[]
                 { _form.TextBoxOffset, _form.TextBoxVisualOffset, _form.TextBoxDelayOffset })
        {
            control.TextChanged += (_, _) => SettingsChanged();
        }

        _form.ComboBoxFps.SelectedIndexChanged += (_, _) => SettingsChanged();
        _form.CheckBoxBeepEnabled.CheckedChanged += (_, _) => SettingsChanged();
        _form.CheckBoxFlashEnabled.CheckedChanged += (_, _) => SettingsChanged();
    }

    public void CaptureSettings(AppSettings settings) => settings.FixedAdjustFrames = _adjustFrames;

    private void SettingsChanged()
    {
        if (!Active) return;

        if (StarterTool.IsTimerRunning) Requeue();
        else ShowIdle();
    }

    public override void OnSelected(bool selected)
    {
        if (!selected) return;

        _form.TextBoxFrame.Enabled = true;
        _form.ButtonPlus.Enabled = true;
        _form.ButtonMinus.Enabled = true;
        WriteAdjustBox();
        ShowIdle();
    }

    private void WriteAdjustBox()
    {
        _writingAdjust = true;
        try
        {
            _form.TextBoxFrame.Text = _adjustFrames.ToString("+#;-#;0", CultureInfo.InvariantCulture);
        }
        finally
        {
            _writingAdjust = false;
        }
    }

    private TimerError ParseChecked(out List<Track> tracks, out string failed)
    {
        tracks = new List<Track>();
        failed = "";

        foreach (FixedTimerEntry entry in Panel.CheckedEntries)
        {
            TimerError error = FixedOffsetCalculator.Parse(
                entry.Offsets, entry.Interval, entry.NumBeeps, Panel.Unit,
                _form.ComboBoxFps.SelectedItem as string,
                _form.TextBoxOffset.Text, _form.TextBoxVisualOffset.Text, _form.TextBoxDelayOffset.Text,
                out FixedTimerInfo info);
            if (error != TimerError.NoError)
            {
                failed = entry.Name;
                tracks.Clear();
                return error;
            }

            tracks.Add(new Track
            {
                Name = entry.Name,
                Info = info,
                StandingFrames = _adjustFrames,
                ExtraFrames = new int[info.TargetsMs.Length],
                Scored = new bool[info.TargetsMs.Length]
            });
        }

        return tracks.Count == 0 ? TimerError.InvalidOffset : TimerError.NoError;
    }

    private void ShowIdle()
    {
        if (!Active || StarterTool.IsTimerRunning) return;

        TimerError error = ParseChecked(out List<Track> tracks, out string failed);
        if (error != TimerError.NoError)
        {
            _form.LabelTimer.Text = TimeText.Format(0.0, StarterTool.TimeFormat);
            Panel.Readout.ForeColor = Theme.LandingMissText;
            Panel.Readout.Text = Describe(error, failed);
            return;
        }

        if (Panel.Readout.ForeColor == Theme.LandingMissText && !_hasTargets) Panel.Readout.Text = "";
        double[] targets = BeepTargets(tracks);
        _form.LabelTimer.Text = TimeText.Format(
            Math.Max(targets.Length > 0 ? targets[0] : 0.0, 0.0) / 1000.0, StarterTool.TimeFormat);
    }

    private static string Describe(TimerError error, string name)
    {
        string timer = name.Length == 0 ? "A ticked timer" : $"\"{name}\"";
        return error switch
        {
            TimerError.InvalidOffset => $"{timer}: offsets are numbers or ranges (300-302), separated by , or /",
            TimerError.InvalidInterval => $"{timer}: the interval is not a number",
            TimerError.InvalidNumBeeps => $"{timer}: the beep count is not a number above zero",
            TimerError.InvalidFps => "The FPS is not a number",
            _ => $"{timer} cannot be run"
        };
    }

    private double[] Shifts(Track track)
    {
        var shifts = new double[track.Info.TargetsMs.Length];
        for (int i = 0; i < shifts.Length; i++)
        {
            shifts[i] = VariableOffsetCalculator.AdjustmentMs(
                track.StandingFrames + track.ExtraFrames[i], track.Info.Schedule.Fps);
        }

        return shifts;
    }

    private double[] BeepTargets(List<Track> tracks)
    {
        var targets = new List<double>();
        foreach (Track track in tracks)
        {
            double[] shifts = Shifts(track);
            for (int i = 0; i < shifts.Length; i++)
            {
                targets.Add(FixedOffsetCalculator.BeepTargetMs(track.Info, i, shifts[i]));
            }
        }

        targets.Sort();
        return targets.ToArray();
    }

    public override void OnTimerStart()
    {
        _beepsDelivered.Clear();
        _flashPlan = new List<(int, FixedCue)>();
        _form.LabelTimer.ClearFlash();

        _valid = ParseChecked(out _tracks, out _) == TimerError.NoError;
        _hasTargets = _valid;
        if (!_valid)
        {
            _beepTargetsMs = Array.Empty<double>();
            return;
        }

        _landingTimerStart = StarterTool.TimerStart;
        _landingStartLagMs = StarterTool.TimerStartLagMs;

        Panel.SetRunning(true);
        Panel.Readout.Text = "";
        Queue();

        foreach (Track track in _tracks)
        {
            ContextSession.Log(string.Format(CultureInfo.InvariantCulture,
                "fixed timer \"{0}\" started: targets {1} {2}, interval {3} ms x{4}, adjust {5:+#;-#;0} frames, "
                + "offset {6} ms, delay {7} ms, fps {8}",
                track.Name,
                string.Join("/", track.Info.Labels),
                track.Info.Unit == FixedTargetUnit.Frames ? "frames" : "ms",
                track.Info.Schedule.Interval, track.Info.Schedule.NumBeeps,
                _adjustFrames, track.Info.Schedule.Offset, track.Info.Schedule.DelayOffset, track.Info.Schedule.Fps));
        }
    }

    private void Requeue()
    {
        if (!Active || !StarterTool.IsTimerRunning || !_valid) return;

        if (ParseChecked(out List<Track> tracks, out _) != TimerError.NoError || tracks.Count != _tracks.Count)
        {
            return;
        }

        for (int i = 0; i < tracks.Count; i++)
        {
            if (tracks[i].Info.TargetsMs.Length != _tracks[i].Info.TargetsMs.Length) return;
        }

        for (int i = 0; i < tracks.Count; i++) _tracks[i].Info = tracks[i].Info;
        Queue();
    }

    private void Queue()
    {
        foreach (Track track in _tracks) track.StandingFrames = _adjustFrames;

        double elapsedMs = Win32.GetTime() - StarterTool.TimerStart;
        bool flash = _form.CheckBoxFlashEnabled.Checked;

        var beeps = new List<double>();
        var plan = new List<(int Track, FixedCue Cue)>();
        var kept = new HashSet<(int, int, int)>();
        if (flash)
        {
            foreach ((int track, FixedCue cue) in _flashPlan)
            {
                if (cue.TimeMs > elapsedMs) continue;
                plan.Add((track, cue));
                kept.Add((track, cue.Target, cue.Beat));
            }
        }

        for (int t = 0; t < _tracks.Count; t++)
        {
            Track track = _tracks[t];
            double[] shifts = Shifts(track);

            foreach (FixedCue cue in FixedOffsetCalculator.Cues(track.Info, shifts, audio: true))
            {
                if (_beepsDelivered.Contains((t, cue.Target, cue.Beat))) continue;

                if (cue.TimeMs <= elapsedMs + VariableOffsetCalculator.CueGuardMs)
                {
                    if (cue.TimeMs <= elapsedMs) _beepsDelivered.Add((t, cue.Target, cue.Beat));
                    continue;
                }

                beeps.Add(cue.TimeMs - elapsedMs);
            }

            if (!flash) continue;
            foreach (FixedCue cue in FixedOffsetCalculator.Cues(track.Info, shifts, audio: false))
            {
                if (!kept.Contains((t, cue.Target, cue.Beat)) && cue.TimeMs > elapsedMs) plan.Add((t, cue));
            }
        }

        beeps.Sort();
        StarterTool.Beeps.QueueBeeps(_form.CheckBoxBeepEnabled.Checked ? beeps : new List<double>());

        plan.Sort((a, b) => a.Cue.TimeMs.CompareTo(b.Cue.TimeMs));
        _flashPlan = plan;

        double fade = _tracks.Min(track => (double)track.Info.Schedule.Interval);
        _form.LabelTimer.SetSchedule(
            plan.Select(entry => entry.Cue.TimeMs).ToArray(),
            fade,
            StarterTool.TimerStart,
            plan.Select(entry => entry.Cue.Final).ToArray());

        _beepTargetsMs = BeepTargets(_tracks);
    }

    public override double TimerCallback(double startTimeMs)
    {
        _form.LabelTimer.Sample();

        double elapsedMs = Win32.GetTime() - startTimeMs;
        foreach (double target in _beepTargetsMs)
        {
            if (target > elapsedMs) return Math.Max((target - elapsedMs) / 1000.0, 0.001);
        }

        return 0.0;
    }

    public override void OnTimerStop()
    {
        Panel.SetRunning(false);

        if (StarterTool.TimerExpired)
        {
            _form.LabelTimer.LetFlashFinish();
        }
        else
        {
            _form.LabelTimer.ClearFlash();
            _hasTargets = false;
        }

        if (!_valid) _hasTargets = false;

        if (StarterTool.TimerExpired && _valid) Panel.AdvanceAfterRun();
        ShowIdle();
    }

    public override void OnKeyEvent(InputPress press)
    {
        AppSettings settings = StarterTool.Settings;

        if (settings.AddFrame.IsPressed(press)) Nudge(1);
        else if (settings.SubFrame.IsPressed(press)) Nudge(-1);
    }

    public override void Nudge(int direction)
    {
        int frames = direction * VariableOffsetTimer.FrameStepMultiplier;
        bool running = StarterTool.IsTimerRunning && _valid;

        if (running && !Panel.AdjustAll && _tracks.Count > 1)
        {
            double elapsedMs = Win32.GetTime() - StarterTool.TimerStart;
            Track? nextTrack = null;
            int nextTarget = -1;
            double nextMs = double.MaxValue;
            foreach (Track track in _tracks)
            {
                double[] shifts = Shifts(track);
                int target = FixedOffsetCalculator.NextTarget(track.Info, shifts, elapsedMs);
                if (target < 0) continue;

                double ms = FixedOffsetCalculator.LandingTargetMs(track.Info, target, shifts[target]);
                if (ms >= nextMs) continue;
                (nextTrack, nextTarget, nextMs) = (track, target, ms);
            }

            if (nextTrack == null) return;

            for (int i = 0; i < nextTrack.ExtraFrames.Length; i++) nextTrack.ExtraFrames[i] += frames;
            Panel.Readout.ForeColor = Theme.DimText;
            Panel.Readout.Text = string.Format(CultureInfo.InvariantCulture,
                "\"{0}\" adjusted {1:+#;-#;0} frames",
                nextTrack.Name, nextTrack.StandingFrames + nextTrack.ExtraFrames[nextTarget]);
        }
        else
        {
            _adjustFrames += frames;
            WriteAdjustBox();
        }

        if (running) Queue();
        else ShowIdle();
    }

    private string TargetText(Track track, int target) =>
        (_tracks.Count > 1 ? track.Name + " " : "")
        + track.Info.Labels[target]
        + (track.Info.Unit == FixedTargetUnit.Frames ? "" : " ms");

    public override bool TryRecordLanding(double pressTimeMs, double pressLagMs = 0.0)
    {
        if (!_hasTargets) return false;

        double elapsedMs = pressTimeMs - _landingTimerStart;

        Track? hit = null;
        int target = -1;
        double deltaMs = double.MaxValue;
        foreach (Track track in _tracks)
        {
            double[] shifts = Shifts(track);
            int candidate = FixedOffsetCalculator.TargetOf(track.Info, shifts, track.Scored, elapsedMs);
            if (candidate < 0) continue;

            double delta = elapsedMs - FixedOffsetCalculator.LandingTargetMs(track.Info, candidate, shifts[candidate]);
            if (Math.Abs(delta) >= Math.Abs(deltaMs)) continue;
            (hit, target, deltaMs) = (track, candidate, delta);
        }

        if (hit == null) return false;

        hit.Scored[target] = true;
        if (!StarterTool.IsTimerRunning && _tracks.All(track => track.Scored.All(scored => scored)))
        {
            _hasTargets = false;
        }

        double chance = FixedOffsetCalculator.HitChance(hit.Info, target, deltaMs);
        int targetFrame = FixedOffsetCalculator.TargetFrame(hit.Info, target);
        int landedFrame = FixedOffsetCalculator.LandedFrame(hit.Info, target, deltaMs);
        int adjusted = hit.StandingFrames + hit.ExtraFrames[target];

        ContextSession.Log(string.Format(CultureInfo.InvariantCulture,
            "fixed landing on \"{0}\" {1}: pressed at {2:F1} ms ({3:+0.0;-0.0;0.0} ms off), likely frame {4} of {5}, "
            + "hit chance {6:P0} - start lag {7:F1} ms, press lag {8:F1} ms",
            hit.Name, TargetText(hit, target), elapsedMs, deltaMs, landedFrame, targetFrame, chance,
            _landingStartLagMs, pressLagMs));

        Label readout = Panel.Readout;
        readout.ForeColor = chance > 0.5 ? Theme.LandingHitText
            : chance > 0.0 ? Theme.LandingMaybeText
            : Theme.LandingMissText;

        string suffix = adjusted == 0 ? "" : adjusted.ToString("+#;-#", CultureInfo.InvariantCulture);
        string aimed = hit.Info.Unit == FixedTargetUnit.Frames
            ? $"Target {TargetText(hit, target)}{suffix}"
            : $"Target {TargetText(hit, target)} - Frame {targetFrame}{suffix}";
        readout.Text =
            $"Likely Frame {landedFrame}, {aimed}"
            + $"  ({deltaMs:+0;-0;0}ms)  Hit Chance {MainForm.FormatChance(chance)}";

        return true;
    }
}
