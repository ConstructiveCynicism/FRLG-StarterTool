using System.Globalization;
using FRLG.StarterTool.Core.Npc;
using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Timing;

namespace FRLG.StarterTool.App;

public sealed class VariableOffsetTimer : BaseTimer
{
    private const int ArmDelayMs = 120;

    private static readonly string[] FpsPresets =
    {
        "59.7275",
        "59.8261",
        "60",
        "59.94",
        "50"
    };

    private readonly MainForm _form;

    public VariableInfo Info;

    public bool Submitted;

    public double CurrentOffset = double.MaxValue;

    public double Adjusted;

    public double CurrentTime;

    private bool _hasLandingTarget;
    private double _landingTimerStart;
    private double _landingStartLagMs;
    private double _landingTargetMs;
    private int _landingTargetFrame;
    private double _landingAdjustedMs;
    private VariableInfo _landingInfo;

    private System.Windows.Forms.Timer? _armDebounce;

    private bool _writingFrameBox;

    private bool _started;

    private string _lastArmLog = "";

    public VariableOffsetTimer(MainForm form)
    {
        _form = form;
    }

    public override void OnInit()
    {
        AppSettings settings = StarterTool.Settings;

        _form.ComboBoxFps.Items.AddRange(FpsPresets);
        int fpsIndex = Array.IndexOf(FpsPresets, settings.Fps);
        _form.ComboBoxFps.SelectedIndex = fpsIndex >= 0 ? fpsIndex : 0;
        _form.TextBoxOffset.Text = settings.Offset;
        _form.TextBoxVisualOffset.Text = settings.VisualOffset;
        _form.TextBoxInterval.Text = settings.Interval;
        _form.TextBoxBeeps.Text = settings.NumBeeps;
        _form.CheckBoxBeepEnabled.Checked = settings.BeepEnabled;
        _form.CheckBoxFlashEnabled.Checked = settings.FlashEnabled;

        _form.ButtonStart.Click += (_, _) => StarterTool.StartTimer();
        _form.ButtonStop.Click += (_, _) => StarterTool.StopTimer(false);
        _form.ButtonPlus.Click += (_, _) => Nudge(1);
        _form.ButtonMinus.Click += (_, _) => Nudge(-1);

        _form.TextBoxFrame.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;

            Arm();
            e.SuppressKeyPress = true;
        };

        _armDebounce = new System.Windows.Forms.Timer { Interval = ArmDelayMs };
        _armDebounce.Tick += (_, _) =>
        {
            _armDebounce!.Stop();
            Arm();
        };

        _form.TextBoxFrame.TextChanged += (_, _) =>
        {
            if (!_writingFrameBox) Adjusted = ReadFrameBoxAdjustment();

            OnDataChange();
            RequestArm();
        };

        foreach (Control control in new Control[]
                 { _form.TextBoxOffset, _form.TextBoxVisualOffset, _form.TextBoxInterval, _form.TextBoxBeeps })
        {
            control.TextChanged += (_, _) =>
            {
                OnDataChange();
                RequestArm();
            };
        }

        _form.CheckBoxBeepEnabled.CheckedChanged += (_, _) => Arm();
        _form.CheckBoxFlashEnabled.CheckedChanged += (_, _) => Arm();
        _form.ComboBoxFps.SelectedIndexChanged += (_, _) =>
        {
            OnDataChange();
            RequestArm();
        };

        StarterTool.Beeps.Sound = settings.BeepSound;
        StarterTool.Beeps.Volume = settings.Volume;

        OnTimerStop();
    }

    public void ChangeBeepSound(string name)
    {
        StarterTool.Beeps.Sound = name;
        PreviewOrRearm();
    }

    public void ChangeVolume(int volume)
    {
        StarterTool.Beeps.Volume = volume;
        if (Submitted) Arm();
    }

    private void PreviewOrRearm()
    {
        if (Submitted)
        {
            Arm();
        }
        else
        {
            StarterTool.Beeps.Preview();
        }
    }

    public int OffsetMs =>
        int.TryParse(_form.TextBoxOffset.Text, NumberStyles.Integer | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out int offset)
            ? offset
            : 0;

    public int VisualOffsetMs =>
        int.TryParse(_form.TextBoxVisualOffset.Text, NumberStyles.Integer | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out int offset)
            ? offset
            : 0;

    public bool TrainingUsesVisualOffset =>
        _form.CheckBoxFlashEnabled.Checked && !_form.CheckBoxBeepEnabled.Checked;

    public void ApplyOffset(int offsetMs)
    {
        _form.TextBoxOffset.Text = offsetMs.ToString(CultureInfo.InvariantCulture);
    }

    public void ApplyVisualOffset(int offsetMs)
    {
        _form.TextBoxVisualOffset.Text = offsetMs.ToString(CultureInfo.InvariantCulture);
    }

    public double SelectedFps =>
        double.TryParse(_form.ComboBoxFps.SelectedItem as string, NumberStyles.Float, CultureInfo.InvariantCulture, out double fps)
        && fps > 0.0
            ? fps
            : 60.0;

    public void SetFrame(int frame)
    {
        WriteFrameBox((uint)Math.Max(frame, 0));
    }

    public int PressShiftFrames =>
        VariableOffsetCalculator.FramesAdjusted(Adjusted, SelectedFps)
        - VariableOffsetCalculator.TidLagFrames;

    private void WriteFrameBox(uint frame)
    {
        _writingFrameBox = true;
        try
        {
            _form.TextBoxFrame.Text = VariableOffsetCalculator.FormatFrameWithAdjustment(
                frame,
                VariableOffsetCalculator.FramesAdjusted(Adjusted, SelectedFps));
        }
        finally
        {
            _writingFrameBox = false;
        }
    }

    private double ReadFrameBoxAdjustment() => VariableOffsetCalculator.AdjustmentMs(
        VariableOffsetCalculator.AdjustmentFrames(_form.TextBoxFrame.Text),
        SelectedFps);

    public int TakeFrameAdjustment()
    {
        int frames = VariableOffsetCalculator.FramesAdjusted(Adjusted, SelectedFps);
        if (frames == 0) return 0;

        Adjusted = 0.0;

        if (ParseInputs(out VariableInfo info) == TimerError.NoError) WriteFrameBox(info.Frame);

        Arm();
        return frames;
    }

    public override void OnTimerStart()
    {
        CurrentOffset = double.MaxValue;
        CurrentTime = 0.0;
        Submitted = false;
        _hasLandingTarget = false;
        _started = true;
        _lastArmLog = "";
        _form.TextBoxFrame.Enabled = true;
        ClearFlash();

        _form.UnlockTrainerId();

        _form.ShowTimingStatus("Timer started", StarterTool.TimerStartLagMs);

        Adjusted = ReadFrameBoxAdjustment();

        if (StartTrainingRound())
        {
            OnDataChange();
            RequestArm();
            return;
        }

        OnDataChange();
        RequestArm();

        _form.FocusTrainerId();
    }

    private bool StartTrainingRound()
    {
        if (!_form.TrainingPanel.IsRunning) return false;
        if (ParseScheduleInputs(out VariableInfo info) != TimerError.NoError) return false;

        uint? frame = _form.TrainingPanel.NextTargetFrame(info);
        if (frame == null) return false;

        WriteFrameBox(frame.Value);
        return true;
    }

    public override void OnTimerStop()
    {
        _armDebounce?.Stop();
        Submitted = false;
        Adjusted = 0.0;
        CurrentOffset = double.MaxValue;
        CurrentTime = 0.0;
        _form.TextBoxFrame.Enabled = false;
        _form.TextBoxFrame.Text = "";
        _form.LabelTimer.Text = 0.0.ToString("F3", CultureInfo.InvariantCulture);

        if (StarterTool.TimerExpired)
        {
            _form.LabelTimer.LetFlashFinish();
        }
        else
        {
            ClearFlash();

            _hasLandingTarget = false;
        }

        if (_started)
        {
            _started = false;

            _form.TrainingPanel.RoundEnded();

            if (!_form.HasLanding && !_form.TrainingPanel.Visible)
            {
                _form.ShowTimingStatus("Timer stopped", StarterTool.TimerStopLagMs);
            }
        }

        OnDataChange();
    }

    public override void OnKeyEvent(Keys key)
    {
        AppSettings settings = StarterTool.Settings;

        if (settings.AddFrame.IsPressed(key) && _form.ButtonPlus.Enabled)
        {
            Nudge(1);
        }
        else if (settings.SubFrame.IsPressed(key) && _form.ButtonMinus.Enabled)
        {
            Nudge(-1);
        }
    }

    public bool TryRecordLanding(double pressTimeMs, double pressLagMs = 0.0)
    {
        if (!_hasLandingTarget) return false;

        double elapsedMs = pressTimeMs - _landingTimerStart;
        double deltaMs = VariableOffsetCalculator.LandingDeltaMs(elapsedMs, _landingTargetMs);

        double window = deltaMs >= 0.0
            ? VariableOffsetCalculator.LandingWindowMs(_landingInfo)
            : VariableOffsetCalculator.EarlyLandingWindowMs(_landingInfo);
        if (Math.Abs(deltaMs) > window) return false;

        _hasLandingTarget = false;

        int? landedFrame = LandedFrame(elapsedMs);
        double chance = VariableOffsetCalculator.HitChance(deltaMs, _landingInfo.Fps);

        LogLanding(elapsedMs, deltaMs, landedFrame, chance, pressLagMs);

        _form.ShowLanding(
            landedFrame,
            _landingTargetFrame,
            deltaMs,
            chance,
            VariableOffsetCalculator.FramesAdjusted(_landingAdjustedMs, _landingInfo.Fps),
            _landingStartLagMs - pressLagMs,
            _landingInfo.Fps);

        StarterTool.Context.RecordHit();
        return true;
    }

    public void CaptureSettings(AppSettings settings)
    {
        settings.Fps = _form.ComboBoxFps.SelectedItem as string ?? settings.Fps;
        settings.Offset = _form.TextBoxOffset.Text;
        settings.VisualOffset = _form.TextBoxVisualOffset.Text;
        settings.Interval = _form.TextBoxInterval.Text;
        settings.NumBeeps = _form.TextBoxBeeps.Text;
        settings.BeepEnabled = _form.CheckBoxBeepEnabled.Checked;
        settings.FlashEnabled = _form.CheckBoxFlashEnabled.Checked;
        settings.Volume = StarterTool.Beeps.Volume;
        settings.BeepSound = StarterTool.Beeps.Sound;
    }

    public override double TimerCallback(double startTimeMs)
    {
        OnDataChange();
        double elapsedMs = Win32.GetTime() - startTimeMs;

        _form.LabelTimer.Sample();

        _form.SampleContextPanel();

        double elapsed = elapsedMs / 1000.0;
        double ret = Math.Min(Math.Max(elapsed, 0.001), CurrentOffset);
        if (ret == CurrentOffset) ret = 0.0;
        CurrentTime = ret;
        return ret;
    }

    public void OnDataChange()
    {
        _form.RefreshTimeColumn();

        TimerError error = ParseInputs(out Info);
        double currentTime = error == TimerError.NoError ? CurrentTime : 0.0;

        bool canAdjust = error == TimerError.NoError
                         && StarterTool.IsTimerRunning
                         && (Submitted
                             ? VariableOffsetCalculator.CanAdjust(Info, currentTime, CurrentOffset)
                             : VariableOffsetCalculator.CanSubmit(Info, currentTime));
        _form.ButtonPlus.Enabled = canAdjust;
        _form.ButtonMinus.Enabled = canAdjust;
    }

    private void RequestArm()
    {
        if (_armDebounce == null) return;

        _armDebounce.Stop();
        _armDebounce.Start();
    }

    public void Arm()
    {
        _armDebounce?.Stop();

        if (!StarterTool.IsTimerRunning) return;

        if (ParseInputs(out Info) != TimerError.NoError
            || !VariableOffsetCalculator.CanSubmit(Info, CurrentTime))
        {
            Disarm();
            return;
        }

        double elapsedMs = Win32.GetTime() - StarterTool.TimerStart;
        double finalBeepMs = VariableOffsetCalculator.BeepOffsetMs(Info, elapsedMs, Adjusted);
        double[] schedule = _form.CheckBoxBeepEnabled.Checked
            ? VariableOffsetCalculator.BeepSchedule(finalBeepMs, Info.Interval, Info.NumBeeps)
            : Array.Empty<double>();

        StarterTool.Beeps.QueueBeeps(schedule);

        _form.LabelTimer.SetSchedule(
            _form.CheckBoxFlashEnabled.Checked
                ? VariableOffsetCalculator.FlashSchedule(
                    VariableOffsetCalculator.FlashTargetMs(Info, Adjusted), Info.Interval, Info.NumBeeps)
                : Array.Empty<double>(),
            Info.Interval,
            StarterTool.TimerStart);

        CurrentOffset = VariableOffsetCalculator.TargetTimeSeconds(Info, Adjusted);

        _landingInfo = Info;
        _landingTimerStart = StarterTool.TimerStart;
        _landingStartLagMs = StarterTool.TimerStartLagMs;
        _landingTargetMs = VariableOffsetCalculator.LandingTargetMs(Info, Adjusted);
        _landingTargetFrame = VariableOffsetCalculator.TargetFrame(Info);
        _landingAdjustedMs = Adjusted;
        _hasLandingTarget = true;

        LogArm();

        _form.TrainingPanel.RoundArmed(
            _landingTargetFrame, Info.Offset, Info.VisualOffset, VariableOffsetCalculator.LandingWindowMs(Info));

        Submitted = true;
        OnDataChange();
    }

    private int? LandedFrame(double elapsedMs)
    {
        int countdownFrame = VariableOffsetCalculator.FrameAtTime(_landingInfo, elapsedMs)
                             - VariableOffsetCalculator.FramesAdjusted(_landingAdjustedMs, _landingInfo.Fps);

        return StarterTool.Context.LandedFrame(countdownFrame);
    }

    private void LogArm()
    {
        string line = string.Format(CultureInfo.InvariantCulture,
            "armed {0}: correction {1:+#;-#;+0} -> effective frame {2}, press due {3:F1} ms "
            + "({4} frames of input lag), final beep {5:F1} ms, offset {6} ms, fps {7}",
            VariableOffsetCalculator.FormatFrameWithAdjustment(
                Info.Frame, VariableOffsetCalculator.FramesAdjusted(Adjusted, Info.Fps)),
            Info.AdvanceCorrection,
            VariableOffsetCalculator.EffectiveFrame(Info),
            _landingTargetMs,
            VariableOffsetCalculator.TidLagFrames,
            CurrentOffset * 1000.0,
            Info.Offset,
            Info.Fps);

        if (line == _lastArmLog) return;

        _lastArmLog = line;
        ContextSession.Log(line);
    }

    private void LogLanding(double elapsedMs, double deltaMs, int? landedFrame, double chance, double pressLagMs)
    {
        int unanchored = VariableOffsetCalculator.LandedFrame(_landingInfo, elapsedMs, _landingAdjustedMs);

        ContextSession.Log(string.Format(CultureInfo.InvariantCulture,
            "landing on {0}: pressed at {1:F1} ms ({2:+0.0;-0.0;0.0} ms off), tool clock frame {3}, "
            + "likely {4} (unanchored {5}), runner-up {6} ({7:P0}), hit chance {8:P0} - "
            + "correction {9:+#;-#;+0}, input lag {10} frames, start lag {11:F1} ms, press lag {12:F1} ms",
            VariableOffsetCalculator.FormatFrameWithAdjustment(
                (uint)Math.Max(_landingTargetFrame, 0),
                VariableOffsetCalculator.FramesAdjusted(_landingAdjustedMs, _landingInfo.Fps)),
            elapsedMs,
            deltaMs,
            VariableOffsetCalculator.FrameAtTime(_landingInfo, elapsedMs)
                - VariableOffsetCalculator.TidLagFrames,
            landedFrame is { } frame ? frame.ToString(CultureInfo.InvariantCulture) : "not anchored",
            unanchored,
            landedFrame is { } marked
                ? VariableOffsetCalculator.AlternateFrame(marked, deltaMs, _landingInfo.Fps)
                    .ToString(CultureInfo.InvariantCulture)
                : "-",
            VariableOffsetCalculator.AlternateChance(deltaMs, _landingInfo.Fps),
            chance,
            _landingInfo.AdvanceCorrection,
            VariableOffsetCalculator.TidLagFrames,
            _landingStartLagMs,
            pressLagMs));
    }

    private void Disarm()
    {
        ClearFlash();

        if (!Submitted) return;

        Submitted = false;
        CurrentOffset = double.MaxValue;
        _hasLandingTarget = false;
        StarterTool.Beeps.ClearPending();
        OnDataChange();
    }

    private void ClearFlash() => _form.LabelTimer.ClearFlash();

    public void Nudge(int direction) => ChangeAudio(direction * FrameStepMultiplier);

    private static int FrameStepMultiplier
    {
        get
        {
            AppSettings settings = StarterTool.Settings;

            if (settings.Multiply3.IsHeld()) return 3;
            if (settings.Multiply2.IsHeld()) return 2;
            return 1;
        }
    }

    public void ChangeAudio(int numFrames)
    {
        if (ParseInputs(out VariableInfo info) != TimerError.NoError) return;

        Adjusted += VariableOffsetCalculator.AdjustmentMs(numFrames, info.Fps);
        WriteFrameBox(info.Frame);
        Arm();
    }

    private TimerError ParseInputs(out VariableInfo info)
    {
        TimerError error = VariableOffsetCalculator.Parse(
            _form.TextBoxFrame.Text,
            _form.ComboBoxFps.SelectedItem as string,
            _form.TextBoxOffset.Text,
            _form.TextBoxVisualOffset.Text,
            _form.TextBoxInterval.Text,
            _form.TextBoxBeeps.Text,
            out info);

        if (error == TimerError.NoError)
        {
            info.AdvanceCorrection = StarterTool.Context.Correction((int)info.Frame) ?? 0;
        }

        return error;
    }

    public void ApplyContextCorrection()
    {
        if (!Submitted) return;

        Arm();
    }

    private TimerError ParseScheduleInputs(out VariableInfo info) => VariableOffsetCalculator.Parse(
        "0",
        _form.ComboBoxFps.SelectedItem as string,
        _form.TextBoxOffset.Text,
        _form.TextBoxVisualOffset.Text,
        _form.TextBoxInterval.Text,
        _form.TextBoxBeeps.Text,
        out info);
}
