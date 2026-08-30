using System.Globalization;
using System.Runtime.InteropServices;
using FRLG.StarterTool.Core.Npc;
using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Timing;

namespace FRLG.StarterTool.App;

public static class StarterTool
{
    private const int TimerResolutionMs = 15;

    public static MainForm MainForm = null!;
    public static BeepPlayer Beeps = null!;
    public static VariableOffsetTimer VariableOffset = null!;
    public static AppSettings Settings = null!;

    public static StatServer StatServer = null!;

    public static readonly ContextSession Context = new();

    public static TimeFormat TimeFormat => Settings?.TimeFormat ?? TimeFormat.Seconds;

    public static SettingsForm? SettingsForm;

    private static volatile int _modalDepth;

    public static volatile bool IsTimerRunning;

    public static bool TimerExpired;

    public static bool TimerCuesFinish;

    public static double TimerStart;

    public static double TimerStartLagMs;

    public static double TimerStopLagMs;

    private static Thread? _timerUpdateThread;
    private static volatile bool _timerThreadRunning;

    private static Win32.Proc _keyboardCallback = null!;
    private static IntPtr _keyboardHook;

    private static Thread? _hookThread;
    private static uint _hookThreadId;

    public static IntPtr MainFormHandle { get; private set; }

    private static readonly int[] LastKeyEvent = new int[256];

    public static BaseTimer CurrentTab => VariableOffset;

    public static void Init(MainForm mainForm)
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        Win32.InitTiming();

        Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;

        bool firstRun = !File.Exists(SettingsStore.DefaultPath);

        Settings = SettingsStore.Load(SettingsStore.DefaultPath, out string? loadError);
        if (loadError != null)
        {
            MessageBox.Show(mainForm,
                "The settings could not be loaded and have been reset to their default values.\n" + loadError,
                "Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        if (firstRun) Settings.ZoomPercent = DefaultZoomPercent();

        Win32.SetDrift(Settings.ClockDrift);
        DriftMonitor.Start();
        if (Settings.AtomicClockSync) AtomicClock.Start();

        MainForm = mainForm;
        ApplyTheme();

        StatServer = new StatServer();
        StatServer.Start(Settings);
        Beeps = new BeepPlayer();
        VariableOffset = new VariableOffsetTimer(mainForm);
        VariableOffset.OnInit();
        mainForm.ApplySettings(Settings);

        MainFormHandle = mainForm.Handle;
        StartHookThread();

        Gamepads.Changed += GamepadChanged;
        Gamepads.Start();
    }

    private static int DefaultZoomPercent() =>
        (Screen.PrimaryScreen?.Bounds.Height ?? 1080) < 1440 ? 75 : 100;

    private static void StartHookThread()
    {
        using var installed = new ManualResetEventSlim();

        _hookThread = new Thread(() =>
        {
            _hookThreadId = Win32.GetCurrentThreadId();

            _keyboardCallback = Keycallback;
            _keyboardHook = Win32.SetHook(Win32.WH_KEYBOARD_LL, _keyboardCallback);
            installed.Set();

            Win32.RunMessageLoop();

            if (_keyboardHook != IntPtr.Zero)
            {
                Win32.UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }
        })
        {
            IsBackground = true,
            Name = "KeyboardHook",
            Priority = ThreadPriority.Highest
        };
        _hookThread.Start();

        installed.Wait(1000);
    }

    public static void Destroy()
    {
        StatServer?.Dispose();
        Gamepads.Stop();

        if (_hookThreadId != 0)
        {
            Win32.PostThreadMessage(_hookThreadId, Win32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _hookThread?.Join(1000);
            _hookThreadId = 0;
        }

        StopTimerThread();
        Beeps?.Dispose();

        DriftMonitor.Stop();
        AtomicClock.Stop();
        if (Settings != null) Settings.ClockDrift = ChooseDrift(runLocal: false, out _);
        SaveSettings();
        Win32.EndTiming();
    }

    private static bool _settingsCleared;

    public static void ClearSettings()
    {
        _settingsCleared = true;
        try
        {
            File.Delete(SettingsStore.DefaultPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _settingsCleared = false;
            MessageBox.Show(MainForm, "The settings file could not be deleted.\n" + e.Message,
                "Clear settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Restart();
    }

    public static void SaveSettings()
    {
        if (Settings == null || _settingsCleared) return;

        try
        {
            VariableOffset?.CaptureSettings(Settings);
            MainForm?.CaptureSettings(Settings);
        }
        catch (ObjectDisposedException)
        {
        }

        SettingsStore.Save(SettingsStore.DefaultPath, Settings, out _);
    }

    public const double DriftCheckLeadMs = 500.0;

    public const double DriftCheckMinShiftMs = 0.2;

    public const double DriftDisagreementPpm = 3.0;

    public static double ChooseDrift(bool runLocal, out string source)
    {
        double session = DriftMonitor.Measured;
        bool sessionTrusted = DriftMonitor.Trusted;

        if (AtomicClock.Trusted)
        {
            double atomic = AtomicClock.Measured;
            double against = sessionTrusted ? session : (DriftMonitor.RunRate ?? session);
            if (Math.Abs(DriftMonitor.ToPpm(atomic) - DriftMonitor.ToPpm(against)) >= DriftDisagreementPpm)
            {
                source = "atomic";
                return atomic;
            }
        }

        if (runLocal && DriftMonitor.RunRate is { } run)
        {
            source = "run";
            return run;
        }

        if (sessionTrusted)
        {
            source = "session";
            return session;
        }

        source = "saved";
        return Win32.Drift;
    }

    public static double CheckClockDrift(out string source)
    {
        source = "";
        if (!IsTimerRunning) return 0.0;

        double oldDrift = Win32.Drift;
        double newDrift = ChooseDrift(runLocal: true, out source);
        if (newDrift == oldDrift) return 0.0;
        double elapsedMs = Win32.GetTime() - TimerStart;
        double shiftMs = elapsedMs * (1.0 - oldDrift / newDrift);
        if (Math.Abs(shiftMs) < DriftCheckMinShiftMs) return 0.0;

        Win32.SetDrift(newDrift);
        TimerStart = Win32.GetTime() - elapsedMs * oldDrift / newDrift;
        if (Settings != null) Settings.ClockDrift = newDrift;
        return shiftMs;
    }

    public static void ShowSettings()
    {
        if (SettingsForm != null)
        {
            SettingsForm.Activate();
            return;
        }

        bool reopen;
        do
        {
            using var form = new SettingsForm(Settings);
            SettingsForm = form;
            bool unpinned = SuspendAlwaysOnTop();
            try
            {
                form.ShowDialog(MainForm);
            }
            finally
            {
                SettingsForm = null;
                RestoreAlwaysOnTop(unpinned);
            }

            reopen = form.ReopenForZoom;
        }
        while (reopen);

        SaveSettings();
    }

    public static void ApplyTheme()
    {
        Theme.Dark = Settings.DarkMode;

        Win32.SetAppDarkMode(Theme.Dark);

        foreach (Form form in Application.OpenForms)
        {
            Theme.Apply(form);
        }
    }

    public static void Post(Action work)
    {
        if (MainForm is not { IsHandleCreated: true }) return;

        try
        {
            MainForm.BeginInvoke(work);
        }
        catch (Exception e) when (e is ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    public static T Modal<T>(Func<T> show)
    {
        _modalDepth++;
        bool unpinned = SuspendAlwaysOnTop();
        try
        {
            return show();
        }
        finally
        {
            _modalDepth--;
            RestoreAlwaysOnTop(unpinned);
        }
    }

    private static bool SuspendAlwaysOnTop()
    {
        if (MainForm is not { TopMost: true }) return false;

        MainForm.TopMost = false;
        return true;
    }

    private static void RestoreAlwaysOnTop(bool suspended)
    {
        if (suspended && MainForm is { IsDisposed: false }) MainForm.TopMost = true;
    }

    private static IntPtr Keycallback(int nCode, int wParam, IntPtr lParam)
    {
        if (nCode >= 0 && SettingsForm == null && _modalDepth == 0)
        {
            double eventTime = Win32.GetTime();

            try
            {
                var kbd = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
                var key = (Keys)kbd.vkCode;
                int index = (int)key & 0xFF;

                double lagMs = Win32.EventLagMs(kbd.time);
                eventTime -= lagMs;

                bool extended = (kbd.flags & Win32.LLKHF_EXTENDED) != 0;

                if (Settings.KeyMethod.IsActivatedByEvent(wParam) && wParam != LastKeyEvent[index])
                {
                    Dispatch(InputPress.Capture(InputCode.Key((int)key), Settings), extended, eventTime, lagMs);
                }

                LastKeyEvent[index] = wParam;
            }
            catch (Exception)
            {
            }
        }

        return Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private static void GamepadChanged(InputCode input, bool pressed, double time)
    {
        if (SettingsForm != null || _modalDepth != 0 || Settings == null) return;
        if (!Settings.KeyMethod.IsActivatedByEdge(pressed)) return;

        try
        {
            Dispatch(InputPress.Capture(input, Settings), extended: false, time, 0.0);
        }
        catch (Exception)
        {
        }
    }

    private static void Dispatch(InputPress press, bool extended, double eventTime, double lagMs)
    {
        if (IsMasterSwitch(press)) return;

        Keys key = press.Key;

        Keys entryKey = press.IsKeyboard ? MainForm.TranslateNumpad(key, extended) : Keys.None;

            bool aliased = entryKey != key && entryKey != Keys.None
                                           && key is not (>= Keys.D0 and <= Keys.D9);

            bool claimed = IsBound(press) || (aliased && IsBound(press.As(InputCode.Key((int)entryKey))));

            bool clearing = entryKey == Keys.Decimal;

            bool starting = IsIdleStart(press);

            _idleStartPress = starting && press.IsKeyboard && press.Foreground ? key : Keys.None;

            bool typing = press.IsKeyboard && MainForm.NumberFieldFocused
                          && ((press.Foreground && MainForm.IsTextEntryKey(key))
                              || (IsTimerRunning && MainForm.IsNumberKey(entryKey)))
                          && !(clearing && claimed)
                          && !starting;

            bool bound = !typing && claimed;

            if (typing)
            {
            }
            else if (Settings.Start.IsPressed(press))
            {
                Post(() =>
                {
                    if (VariableOffset.TryRecordLanding(eventTime, lagMs)) return;

                    if (Context.MarkNextAnchor(eventTime)) return;

                    if (Context.Stage == ContextStage.Lab && !Context.HitConfirmed
                        && VariableOffset.LandingWindowOpen)
                    {
                        ContextSession.Log(string.Format(CultureInfo.InvariantCulture,
                            "start press ignored at {0:F1} ms - landing still owed",
                            eventTime - TimerStart));
                        return;
                    }

                    StartTimer(eventTime, lagMs);
                });
            }
            else if (Settings.Stop.IsPressed(press))
            {
                Post(() => StopTimer(false, lagMs));
            }
            else if (Settings.ToggleLevel.IsPressed(press))
            {
                Post(MainForm.ToggleLevel);
            }
            else if (Settings.ExportStats.IsPressed(press))
            {
                Post(MainForm.ExportStats);
            }

            Direction? tap = typing ? null : ContextDirection(press);
            if (tap != null)
            {
                Post(() =>
                {
                    if (!MainForm.ReportMovement(tap.Value)) Context.Tap(tap.Value, eventTime);
                });
            }

            int focus = typing ? 0 : ContextFocus(press);
            if (focus != 0)
            {
                Post(() =>
                {
                    if (!MainForm.ReportFocus(focus)) Context.MoveFocus(focus);
                });
            }

            if (!typing && Settings.NpcUndo.IsPressed(press))
            {
                Post(() =>
                {
                    if (!MainForm.ReportUndo()) Context.Undo();
                });
            }

            if (!typing && Settings.NpcComplete.IsPressed(press))
            {
                Post(() => Context.Next());
            }

            if (!typing && Settings.NpcMiss.IsPressed(press))
            {
                Post(() => Context.Miss());
            }

            HotkeyAction? listAction = typing ? null : ListAction(press);
            if (listAction != null)
            {
                Post(() => MainForm.ScrollResults(listAction.Value));
            }

            if (!typing) Post(() => CurrentTab.OnKeyEvent(press));

            if (!bound && press.IsKeyboard)
            {
                Post(() => MainForm.HandleGlobalNumpad(key, extended));
            }
    }

    private static bool IsMasterSwitch(InputPress press)
    {
        if (Settings.ToggleGlobalHotkeys.IsPressed(press))
        {
            Post(MainForm.ToggleGlobalHotkeys);
            return true;
        }

        return !Settings.GlobalHotkeysEnabled && !press.Foreground;
    }

    private static bool IsIdleStart(InputPress press) => !IsTimerRunning && Settings.Start.IsPressed(press);

    private static volatile Keys _idleStartPress;

    public static bool TakeIdleStart(Keys key)
    {
        if (key == Keys.None || _idleStartPress != key) return false;

        _idleStartPress = Keys.None;
        return true;
    }

    public static bool IsBoundKey(Keys key)
        => key != Keys.None && IsBound(InputPress.Capture(InputCode.Key((int)key), Settings));

    public static bool IsBound(InputPress press) =>
        Settings.Start.IsPressed(press) || Settings.Stop.IsPressed(press)
        || Settings.ToggleLevel.IsPressed(press) || Settings.ExportStats.IsPressed(press)
        || Settings.AddFrame.IsPressed(press) || Settings.SubFrame.IsPressed(press)
        || Settings.Multiply2.IsPressed(press) || Settings.Multiply3.IsPressed(press)
        || ContextDirection(press) != null || ContextFocus(press) != 0
        || Settings.NpcUndo.IsPressed(press) || Settings.NpcComplete.IsPressed(press)
        || Settings.NpcMiss.IsPressed(press)
        || ListAction(press) != null;

    private static HotkeyAction? ListAction(InputPress press)
    {
        if (Settings.ListUp.IsPressed(press)) return HotkeyAction.ListUp;
        if (Settings.ListDown.IsPressed(press)) return HotkeyAction.ListDown;

        return null;
    }

    private static Direction? ContextDirection(InputPress press)
    {
        if (Settings.NpcUp.IsPressed(press)) return Direction.North;
        if (Settings.NpcDown.IsPressed(press)) return Direction.South;
        if (Settings.NpcLeft.IsPressed(press)) return Direction.West;
        if (Settings.NpcRight.IsPressed(press)) return Direction.East;

        return null;
    }

    private static int ContextFocus(InputPress press)
    {
        if (Settings.NpcFocusPrev.IsPressed(press)) return -1;
        if (Settings.NpcFocusNext.IsPressed(press)) return 1;

        return 0;
    }

    public static void StartTimer(double? startTimeMs = null, double lagMs = 0.0)
    {
        Beeps.ClearPending();
        StopTimerThread();

        DriftMonitor.BeginRun();

        IsTimerRunning = true;
        TimerExpired = false;
        TimerCuesFinish = false;
        TimerStart = startTimeMs ?? Win32.GetTime();
        TimerStartLagMs = startTimeMs != null ? lagMs : 0.0;

        if (VariableOffset.StartsEncounterRun) Context.Reset();
        else Context.Start();

        CurrentTab.OnTimerStart();

        _timerThreadRunning = true;
        _timerUpdateThread = new Thread(TimerUpdateCallback) { IsBackground = true, Name = "TimerUpdate" };
        _timerUpdateThread.Start();
    }

    public static void StopTimer(bool timerExpired, double lagMs = 0.0, bool letCuesFinish = false)
    {
        if (!IsTimerRunning)
        {
            if (!timerExpired) VariableOffset.ResetEncounterRun();
            return;
        }

        if (!timerExpired)
        {
            if (!letCuesFinish) Beeps.ClearPending();
            StopTimerThread();
        }

        IsTimerRunning = false;
        TimerExpired = timerExpired;
        TimerCuesFinish = letCuesFinish;
        TimerStopLagMs = lagMs;
        CurrentTab.OnTimerStop();

        if (!timerExpired)
        {
            Context.LogStop();
            Context.Reset();
        }
        else
        {
            Context.TimerStopped();
        }
    }

    private static void StopTimerThread()
    {
        _timerThreadRunning = false;
        _timerUpdateThread = null;
    }

    private static void TimerUpdateCallback()
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

        Thread self = Thread.CurrentThread;
        double currentTime;

        do
        {
            if (!_timerThreadRunning || !ReferenceEquals(_timerUpdateThread, self)) return;

            double tick = 0.0;
            try
            {
                MainForm.Invoke(() =>
                {
                    tick = CurrentTab.TimerCallback(TimerStart);
                    MainForm.LabelTimer.Text = TimeText.Format(tick, TimeFormat);
                });
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }

            currentTime = tick;
            Thread.Sleep(TimerResolutionMs);
        } while (currentTime > 0.0);

        try
        {
            MainForm.Invoke(() => StopTimer(true));
        }
        catch (Exception e) when (e is ObjectDisposedException or InvalidOperationException)
        {
        }
    }
}
