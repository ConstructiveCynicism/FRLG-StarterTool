using System.Globalization;
using FRLG.StarterTool.Core.Npc;
using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Timing;
using FRLG.StarterTool.Core.Encounters;

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
        "59.6555",
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

    private double _countdownStartMs = double.MaxValue;

    private double _driftCheckMs = double.MaxValue;

    private bool _driftChecked;

    private System.Windows.Forms.Timer? _armDebounce;

    private readonly CueRun _beepRun = new();

    private readonly CueRun _flashRun = new();

    private System.Windows.Forms.Timer? _landingWindowClose;

    private bool _writingFrameBox;

    private EncounterRun? _encounter;

    private bool _encounterDone;

    private double _encounterLastTargetTime = double.NegativeInfinity;

    private const double EncounterResetWindowMinMs = 1000.0;

    private const double EncounterResetWindowMaxMs = 10000.0;

    private bool _started;

    private bool _audioStartPending;

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
        _form.TextBoxDelayOffset.Text = settings.DelayOffset;
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

            if (StarterTool.TakeIdleStart(e.KeyCode)) return;

            Arm();
            e.SuppressKeyPress = true;
        };

        _armDebounce = new System.Windows.Forms.Timer { Interval = ArmDelayMs };
        _armDebounce.Tick += (_, _) =>
        {
            _armDebounce!.Stop();
            Arm();
        };

        _landingWindowClose = new System.Windows.Forms.Timer();
        _landingWindowClose.Tick += (_, _) => CloseLandingWindow();

        _form.TextBoxFrame.TextChanged += (_, _) =>
        {
            if (!_writingFrameBox) Adjusted = ReadFrameBoxAdjustment();

            OnDataChange();
            RequestArm();
        };

        foreach (Control control in new Control[]
                 { _form.TextBoxOffset, _form.TextBoxVisualOffset, _form.TextBoxDelayOffset,
                   _form.TextBoxInterval, _form.TextBoxBeeps })
        {
            control.TextChanged += (_, _) =>
            {
                OnDataChange();
                RequestArm();
            };
        }

        _form.CheckBoxBeepEnabled.CheckedChanged += (_, _) => Arm();
        _form.CheckBoxFlashEnabled.CheckedChanged += (_, _) => Arm();

        _form.ComboBoxEncounterRoute.SelectedIndexChanged += (_, _) => _encounterDone = false;
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

    public int DelayOffsetMs =>
        int.TryParse(_form.TextBoxDelayOffset.Text, NumberStyles.Integer | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out int delay)
            ? delay
            : 0;

    public bool TrainingUsesVisualOffset =>
        _form.CheckBoxFlashEnabled.Checked && !_form.CheckBoxBeepEnabled.Checked;

    public bool OffsetsShared =>
        _form.CheckBoxBeepEnabled.Checked && _form.CheckBoxFlashEnabled.Checked
        && OffsetMs == VisualOffsetMs;

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
        _landingWindowClose?.Stop();
        _countdownStartMs = double.MaxValue;
        _driftCheckMs = double.MaxValue;
        _driftChecked = false;
        _beepRun.Reset();
        _flashRun.Reset();
        _started = true;
        _audioStartPending = false;
        _lastArmLog = "";
        ClearFlash();

        if (StartsEncounterRun)
        {
            StartEncounterRun();
            return;
        }

        _form.TextBoxFrame.Enabled = true;

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

        bool encounterStopped = _encounter != null;
        if (_encounter != null) StopEncounterRun();

        if (!StarterTool.TimerExpired) _encounterDone = false;

        Submitted = false;
        Adjusted = 0.0;
        CurrentOffset = double.MaxValue;
        _countdownStartMs = double.MaxValue;
        _beepRun.Reset();
        _flashRun.Reset();
        CurrentTime = 0.0;
        _form.TextBoxFrame.Enabled = false;
        _form.TextBoxFrame.Text = "";
        _form.LabelTimer.Text = TimeText.Format(0.0, StarterTool.TimeFormat);

        if (StarterTool.TimerExpired || StarterTool.TimerCuesFinish)
        {
            _form.LabelTimer.LetFlashFinish();
        }
        else
        {
            ClearFlash();
        }

        if (StarterTool.TimerExpired)
        {
            ArmLandingWindowClose();
        }
        else
        {
            _landingWindowClose?.Stop();

            _hasLandingTarget = false;
        }

        if (_started)
        {
            _started = false;

            _form.TrainingPanel.RoundEnded();

            if (!_form.HasLanding && !_form.TrainingTabUp && !encounterStopped)
            {
                _form.ShowTimingStatus("Timer stopped", StarterTool.TimerStopLagMs);
            }
        }

        OnDataChange();
    }

    public override void OnKeyEvent(InputPress press)
    {
        AppSettings settings = StarterTool.Settings;

        if (settings.AddFrame.IsPressed(press) && _form.ButtonPlus.Enabled)
        {
            Nudge(1);
        }
        else if (settings.SubFrame.IsPressed(press) && _form.ButtonMinus.Enabled)
        {
            Nudge(-1);
        }
    }

    public bool TryRecordLanding(double pressTimeMs, double pressLagMs = 0.0)
    {
        if (_encounter != null) return RecordEncounterPress(pressTimeMs, pressLagMs);

        if (!_hasLandingTarget) return false;

        double elapsedMs = pressTimeMs - _landingTimerStart;
        double deltaMs = VariableOffsetCalculator.LandingDeltaMs(elapsedMs, _landingTargetMs);

        double window = deltaMs >= 0.0
            ? VariableOffsetCalculator.LandingWindowMs(_landingInfo)
            : VariableOffsetCalculator.EarlyLandingWindowMs(_landingInfo);
        if (Math.Abs(deltaMs) > window) return false;

        _hasLandingTarget = false;
        _landingWindowClose?.Stop();

        int countdownFrame = CountdownFrameAt(elapsedMs);
        int? landedFrame = StarterTool.Context.LandedFrame(countdownFrame);
        double rawChance = VariableOffsetCalculator.HitChance(deltaMs, _landingInfo.Fps);
        double chance = FrameWindow.HitChance(deltaMs, _landingInfo.Fps,
            StarterTool.Settings?.NpcContextWindowMs ?? 0.0);

        LogLanding(elapsedMs, deltaMs, landedFrame, chance, pressLagMs);

        _form.ShowLanding(
            landedFrame,
            _landingTargetFrame,
            deltaMs,
            chance,
            VariableOffsetCalculator.FramesAdjusted(_landingAdjustedMs, _landingInfo.Fps),
            _landingStartLagMs - pressLagMs,
            _landingInfo.Fps,
            rawChance);

        StarterTool.Context.RecordHit(countdownFrame, deltaMs, chance,
            TrainingUsesVisualOffset ? VisualOffsetMs : OffsetMs);
        return true;
    }

    public bool LandingWindowOpen =>
        _hasLandingTarget
        && Win32.GetTime() < _landingTimerStart
            + VariableOffsetCalculator.LandingCloseMs(_landingInfo, _landingAdjustedMs);

    private void ArmLandingWindowClose()
    {
        if (_landingWindowClose == null || !_hasLandingTarget) return;

        double remainingMs = _landingTimerStart
            + VariableOffsetCalculator.LandingCloseMs(_landingInfo, _landingAdjustedMs)
            - Win32.GetTime();

        if (remainingMs <= 0.0)
        {
            CloseLandingWindow();
            return;
        }

        _landingWindowClose.Interval = (int)Math.Ceiling(remainingMs);
        _landingWindowClose.Start();
    }

    private void CloseLandingWindow()
    {
        _landingWindowClose?.Stop();

        if (_encounter != null)
        {
            FinishEncounterRun();
            return;
        }

        StarterTool.Context.Unpressed();
    }

    public EncounterRoutePreset? EncounterRoute =>
        _form.ComboBoxEncounterRoute.SelectedIndex > 0
            ? _form.EncounterPanel.FindRoute(_form.ComboBoxEncounterRoute.SelectedItem as string ?? "")
            : null;

    public bool StartsEncounterRun =>
        (!_encounterDone || RestartsEncounterRun)
        && !_form.TrainingPanel.IsRunning
        && EncounterRoute is { } route
        && route.Presses().Count > 0
        && ParseScheduleInputs(out _) == TimerError.NoError;

    private bool RestartsEncounterRun
    {
        get
        {
            if (!_encounterDone) return false;
            double sinceMs = StarterTool.TimerStart - _encounterLastTargetTime;
            return sinceMs >= EncounterResetWindowMinMs && sinceMs <= EncounterResetWindowMaxMs;
        }
    }

    public bool EncounterRunLive => _encounter != null;

    private void StartEncounterRun()
    {
        EncounterRoutePreset route = EncounterRoute!;
        if (ParseScheduleInputs(out VariableInfo info) != TimerError.NoError) return;

        if (_encounterDone) ContextSession.Log("encounter manip reset - Start inside the reset window runs it again");
        _encounterDone = false;

        _encounter = new EncounterRun(route, info);
        _encounterLastTargetTime = StarterTool.TimerStart + _encounter.LastPressMs;

        double elapsedMs = Win32.GetTime() - StarterTool.TimerStart;
        double[] beeps = _form.CheckBoxBeepEnabled.Checked
            ? _encounter.BeepSchedule(elapsedMs)
            : Array.Empty<double>();
        StarterTool.Beeps.QueueBeeps(beeps);

        double[] flashes = _form.CheckBoxFlashEnabled.Checked
            ? _encounter.FlashSchedule()
            : Array.Empty<double>();
        _form.LabelTimer.SetSchedule(flashes, info.Interval, StarterTool.TimerStart);

        double firstCueMs = double.MaxValue;
        if (beeps.Length > 0) firstCueMs = Math.Min(firstCueMs, beeps[0] + elapsedMs);
        if (flashes.Length > 0) firstCueMs = Math.Min(firstCueMs, flashes[0]);
        _driftCheckMs = firstCueMs == double.MaxValue ? double.MaxValue : firstCueMs - StarterTool.DriftCheckLeadMs;

        CurrentOffset = _encounter.EndSeconds;

        ContextSession.Log(_encounter.ArmLog());
        _form.ShowManipTab();
        _form.ShowEncounterLanding(_encounter.Rows(), _encounter.Status(), null);
        OnDataChange();
    }

    private bool RecordEncounterPress(double pressTimeMs, double pressLagMs)
    {
        if (_encounter == null) return false;

        double elapsedMs = pressTimeMs - StarterTool.TimerStart;
        EncounterRun.Target? target = _encounter.Owed(elapsedMs);
        if (target == null)
        {
            ContextSession.Log(string.Format(CultureInfo.InvariantCulture,
                "start press ignored at {0:F1} ms - reset seed manip still owed a press", elapsedMs));
            return true;
        }

        _encounter.Score(target, elapsedMs);
        ContextSession.Log(string.Format(CultureInfo.InvariantCulture,
            "encounter {0} press at {1:F1} ms ({2:+0.0;-0.0;0.0} ms off frame {3}), landed frame {4}, hit chance {5:P0} - press lag {6:F1} ms",
            target.Press.Name, elapsedMs, target.DeltaMs, target.Press.Frames, target.LandedFrame, target.Chance, pressLagMs));

        _form.ShowEncounterLanding(_encounter.Rows(), _encounter.Status(), _encounter.WorstChance);

        if (!_encounter.AnyOwed && !StarterTool.IsTimerRunning) FinishEncounterRun();
        return true;
    }

    private void StopEncounterRun()
    {
        if (_encounter == null) return;

        if (StarterTool.TimerExpired)
        {
            if (!_encounter.AnyOwed)
            {
                FinishEncounterRun();
                return;
            }

            double remainingMs = StarterTool.TimerStart + _encounter.CloseMs - Win32.GetTime();
            if (remainingMs <= 0.0 || _landingWindowClose == null)
            {
                FinishEncounterRun();
                return;
            }

            _landingWindowClose.Interval = (int)Math.Ceiling(remainingMs);
            _landingWindowClose.Start();
            return;
        }

        _landingWindowClose?.Stop();
        ContextSession.Log("encounter manip stopped by hand - still pending");
        _encounter = null;
        _encounterDone = false;
        _form.ShowEncounterLanding(Array.Empty<EncounterLandingRow>(),
            "Reset seed manip stopped - Start runs it again", null);
    }

    public void ResetEncounterRun()
    {
        if (_encounter != null || !_encounterDone) return;

        _encounterDone = false;
        ContextSession.Log("encounter manip reset by Stop - Start runs it again");
        _form.ShowEncounterLanding(Array.Empty<EncounterLandingRow>(),
            "Reset seed manip reset - Start runs it again", null);
    }

    private void FinishEncounterRun()
    {
        if (_encounter == null) return;

        _landingWindowClose?.Stop();
        _encounter.MissRest();
        _form.ShowEncounterLanding(_encounter.Rows(), _encounter.Status(), _encounter.WorstChance);
        ContextSession.Log("encounter manip closed - " + _encounter.Status());

        _encounter = null;
        _encounterDone = true;
    }

    public void CaptureSettings(AppSettings settings)
    {
        settings.Fps = _form.ComboBoxFps.SelectedItem as string ?? settings.Fps;
        settings.Offset = _form.TextBoxOffset.Text;
        settings.VisualOffset = _form.TextBoxVisualOffset.Text;
        settings.DelayOffset = _form.TextBoxDelayOffset.Text;
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

        StarterTool.Context.FireCue(elapsedMs);

        CloseTrackingAtCountdown(elapsedMs);

        CheckClockDrift(elapsedMs);

        LogAudioStart();

        _form.LabelTimer.Sample();

        _form.SampleContextPanel();

        double elapsed = elapsedMs / 1000.0;
        double ret = Math.Min(Math.Max(elapsed, 0.001), CurrentOffset);
        if (ret == CurrentOffset) ret = 0.0;
        CurrentTime = ret;
        return ret;
    }

    private void CloseTrackingAtCountdown(double elapsedMs)
    {
        if (!Submitted || elapsedMs < _countdownStartMs) return;

        StarterTool.Context.Miss(automatic: true);
    }

    private void CheckClockDrift(double elapsedMs)
    {
        if (_driftChecked || elapsedMs < _driftCheckMs) return;
        _driftChecked = true;

        double oldPpm = DriftMonitor.ToPpm(Win32.Drift);
        double shiftMs = StarterTool.CheckClockDrift(out string source);
        if (shiftMs == 0.0) return;

        ContextSession.Log(string.Format(CultureInfo.InvariantCulture,
            "clock drift {0:+0.0;-0.0} ppm ({1}) adopted (was {2:+0.0;-0.0}) {3:0.0} s before the first cue - target moved {4:+0.00;-0.00} ms",
            DriftMonitor.ToPpm(Win32.Drift), source, oldPpm, StarterTool.DriftCheckLeadMs / 1000.0, shiftMs));

        if (_encounter == null)
        {
            Arm();
            return;
        }

        _encounterLastTargetTime = StarterTool.TimerStart + _encounter.LastPressMs;
        double sinceStartMs = Win32.GetTime() - StarterTool.TimerStart;
        StarterTool.Beeps.QueueBeeps(_form.CheckBoxBeepEnabled.Checked
            ? _encounter.BeepSchedule(sinceStartMs)
            : Array.Empty<double>());
        _form.LabelTimer.SetSchedule(
            _form.CheckBoxFlashEnabled.Checked ? _encounter.FlashSchedule() : Array.Empty<double>(),
            _encounter.Interval,
            StarterTool.TimerStart);
    }

    public void OnDataChange()
    {
        _form.RefreshTimeColumn();

        TimerError error = ParseInputs(out Info);
        double currentTime = error == TimerError.NoError ? CurrentTime : 0.0;

        bool canAdjust = error == TimerError.NoError
                         && StarterTool.IsTimerRunning
                         && (Submitted
                             ? VariableOffsetCalculator.CanAdjust(currentTime, CurrentOffset)
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

        if (_encounter != null) return;

        if (ParseInputs(out Info) != TimerError.NoError)
        {
            Disarm();
            return;
        }

        double elapsedMs = Win32.GetTime() - StarterTool.TimerStart;

        bool full = VariableOffsetCalculator.CanSubmit(Info, CurrentTime);
        double finalBeepMs = VariableOffsetCalculator.BeepOffsetMs(Info, elapsedMs, Adjusted);

        if (!full && (!Submitted || finalBeepMs < 0.0))
        {
            Disarm();
            return;
        }

        int beepsPlayed = full ? 0 : _beepRun.Played(elapsedMs);
        int flashesPlayed = full ? 0 : _flashRun.Played(elapsedMs);

        double[] schedule = _form.CheckBoxBeepEnabled.Checked
            ? ArmRemaining(full, finalBeepMs, beepsPlayed)
            : Array.Empty<double>();

        QueueAudio(schedule);

        double audioLagMs = Win32.GetTime() - (StarterTool.TimerStart + elapsedMs);
        _audioStartPending = schedule.Length > 0;

        double[] flashes = _form.CheckBoxFlashEnabled.Checked
            ? ArmRemaining(full, VariableOffsetCalculator.FlashTargetMs(Info, Adjusted), flashesPlayed, elapsedMs)
            : Array.Empty<double>();

        _form.LabelTimer.SetSchedule(flashes, CueSpacing(flashes, Info.Interval), StarterTool.TimerStart);

        _beepRun.Set(FromTimerStart(schedule, elapsedMs), beepsPlayed);
        _flashRun.Set(flashes, flashesPlayed);

        CurrentOffset = VariableOffsetCalculator.TargetTimeSeconds(Info, Adjusted);

        _landingInfo = Info;
        _landingTimerStart = StarterTool.TimerStart;
        _landingStartLagMs = StarterTool.TimerStartLagMs;
        _landingTargetMs = VariableOffsetCalculator.LandingTargetMs(Info, Adjusted);
        _landingTargetFrame = VariableOffsetCalculator.TargetFrame(Info);
        _landingAdjustedMs = Adjusted;
        _hasLandingTarget = true;

        _countdownStartMs = VariableOffsetCalculator.CountdownStartMs(
            Info,
            Adjusted,
            _form.CheckBoxBeepEnabled.Checked,
            _form.CheckBoxFlashEnabled.Checked)
            - VariableOffsetCalculator.CueGuardMs;

        _driftCheckMs = _countdownStartMs + VariableOffsetCalculator.CueGuardMs - StarterTool.DriftCheckLeadMs;

        LogArm(full ? null : schedule.Length, audioLagMs);

        _form.TrainingPanel.RoundArmed(
            _landingTargetFrame, Info.Offset, Info.VisualOffset, VariableOffsetCalculator.LandingWindowMs(Info));

        Submitted = true;
        OnDataChange();
    }

    private double[] ArmRemaining(bool full, double finalCueMs, int played, double floorMs = 0.0) =>
        full
            ? VariableOffsetCalculator.BeepSchedule(finalCueMs, Info.Interval, Info.NumBeeps)
            : VariableOffsetCalculator.RemainingSchedule(
                finalCueMs, Info.Interval, (int)Info.NumBeeps - played, floorMs);

    private static double CueSpacing(double[] schedule, uint intervalMs)
        => schedule.Length >= 2 ? schedule[^1] - schedule[^2] : intervalMs;

    private static double[] FromTimerStart(double[] offsetsMs, double elapsedMs)
    {
        var times = new double[offsetsMs.Length];
        for (int i = 0; i < offsetsMs.Length; i++)
        {
            times[i] = offsetsMs[i] + elapsedMs;
        }

        return times;
    }

    private void QueueAudio(IReadOnlyList<double> countdown)
    {
        double[] cue = CueSchedule();
        if (cue.Length == 0)
        {
            StarterTool.Beeps.QueueBeeps(countdown);
            return;
        }

        var both = new List<double>(cue.Length + countdown.Count);
        both.AddRange(cue);
        both.AddRange(countdown);

        StarterTool.Beeps.QueueBeeps(both, cue.Length);
    }

    private void QueueCueOnly()
    {
        double[] cue = CueSchedule();

        if (cue.Length > 0) StarterTool.Beeps.QueueBeeps(cue, cue.Length);
        else StarterTool.Beeps.ClearPending();
    }

    private double[] CueSchedule()
    {
        if (!_form.CheckBoxBeepEnabled.Checked) return Array.Empty<double>();
        if (StarterTool.Context.CuePressMs is not { } cue) return Array.Empty<double>();
        if (ParseScheduleInputs(out VariableInfo info) != TimerError.NoError) return Array.Empty<double>();

        double elapsedMs = Win32.GetTime() - StarterTool.TimerStart;
        double finalBeepMs = cue + VariableOffsetCalculator.TidLagFrames / info.Fps * 1000.0
            + info.Offset - elapsedMs;

        return VariableOffsetCalculator.BeepSchedule(finalBeepMs, info.Interval, info.NumBeeps);
    }

    private int CountdownFrameAt(double elapsedMs) =>
        VariableOffsetCalculator.FrameAtTime(_landingInfo, elapsedMs)
        - VariableOffsetCalculator.FramesAdjusted(_landingAdjustedMs, _landingInfo.Fps);

    private void LogArm(int? salvagedBeeps, double audioLagMs)
    {
        string line = string.Format(CultureInfo.InvariantCulture,
            "armed {0}: correction {1:+#;-#;+0} -> effective frame {2}, press due {3:F1} ms "
            + "({4} frames of input lag), final beep {5:F1} ms, offset {6} ms, delay {7} ms, fps {8}",
            VariableOffsetCalculator.FormatFrameWithAdjustment(
                Info.Frame, VariableOffsetCalculator.FramesAdjusted(Adjusted, Info.Fps)),
            Info.AdvanceCorrection,
            VariableOffsetCalculator.EffectiveFrame(Info),
            _landingTargetMs,
            VariableOffsetCalculator.TidLagFrames,
            CurrentOffset * 1000.0,
            Info.Offset,
            Info.DelayOffset,
            Info.Fps);

        if (salvagedBeeps is { } beeps)
        {
            line += string.Format(CultureInfo.InvariantCulture,
                " - re-cut mid-countdown, {0} of {1} beeps left", beeps, Info.NumBeeps);
        }

        if (line == _lastArmLog) return;

        _lastArmLog = line;
        ContextSession.Log(line);

        IntPtr handle = StarterTool.MainFormHandle;
        ContextSession.Log(string.Format(CultureInfo.InvariantCulture,
            "audio: written {0:F1} ms after arm (buffer {1:F1} ms of it), timer resolution {2:F2} ms, window {3}{4}",
            audioLagMs,
            StarterTool.Beeps.LastWriteLagMs,
            Win32.CurrentTimerResolutionMs(),
            Win32.IsForeground(handle) ? "foreground" : "background",
            Win32.IsMinimized(handle) ? ", minimised" : ""));
    }

    private void LogAudioStart()
    {
        if (!_audioStartPending) return;
        if (StarterTool.Beeps.StartLatencyMs() is not { } latencyMs) return;

        _audioStartPending = false;
        ContextSession.Log(string.Format(CultureInfo.InvariantCulture,
            "audio: device started {0:F1} ms after the write", latencyMs));
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
        _countdownStartMs = double.MaxValue;
        _beepRun.Reset();
        _flashRun.Reset();
        QueueCueOnly();
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
            _form.TextBoxDelayOffset.Text,
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
        if (_encounter != null) return;

        if (!Submitted)
        {
            QueueCueOnly();
            return;
        }

        Arm();
    }

    private TimerError ParseScheduleInputs(out VariableInfo info) => VariableOffsetCalculator.Parse(
        "0",
        _form.ComboBoxFps.SelectedItem as string,
        _form.TextBoxOffset.Text,
        _form.TextBoxVisualOffset.Text,
        _form.TextBoxDelayOffset.Text,
        _form.TextBoxInterval.Text,
        _form.TextBoxBeeps.Text,
        out info);

    private sealed class CueRun
    {
        private double[] _times = Array.Empty<double>();

        private int _delivered;

        public int Played(double elapsedMs)
        {
            int played = _delivered;
            foreach (double time in _times)
            {
                if (time <= elapsedMs + VariableOffsetCalculator.CueGuardMs) played++;
            }

            return played;
        }

        public void Set(double[] times, int played)
        {
            _times = times;
            _delivered = played;
        }

        public void Reset() => Set(Array.Empty<double>(), 0);
    }
}
