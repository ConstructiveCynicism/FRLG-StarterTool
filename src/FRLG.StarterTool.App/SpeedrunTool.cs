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

        if (_hookThreadId != 0)
        {
            Win32.PostThreadMessage(_hookThreadId, Win32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _hookThread?.Join(1000);
            _hookThreadId = 0;
        }

        StopTimerThread();
        Beeps?.Dispose();
        SaveSettings();
        Win32.EndTiming();
    }

    public static void SaveSettings()
    {
        if (Settings == null) return;

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

                if (Settings.KeyMethod.IsActivatedByEvent(wParam) && wParam != LastKeyEvent[index]
                    && !IsMasterSwitch(key))
                {
                    bool typing = MainForm.NumberFieldFocused
                                  && ((Win32.IsForeground(MainFormHandle) && MainForm.IsTextEntryKey(key))
                                      || (IsTimerRunning && MainForm.IsNumberKey(key)));

                    bool bound = !typing
                        && (Settings.Start.IsPressed(key) || Settings.Stop.IsPressed(key)
                            || Settings.ToggleLevel.IsPressed(key) || Settings.ExportStats.IsPressed(key)
                            || Settings.AddFrame.IsPressed(key) || Settings.SubFrame.IsPressed(key)
                            || Settings.Multiply2.IsPressed(key) || Settings.Multiply3.IsPressed(key)
                            || ContextDirection(key) != null || ContextFocus(key) != 0
                            || Settings.NpcUndo.IsPressed(key) || Settings.NpcComplete.IsPressed(key)
                            || Settings.NpcMiss.IsPressed(key)
                            || ListAction(key) != null);

                    if (typing)
                    {
                    }
                    else if (Settings.Start.IsPressed(key))
                    {
                        Post(() =>
                        {
                            if (VariableOffset.TryRecordLanding(eventTime, lagMs)) return;

                            if (Context.MarkNextAnchor(eventTime)) return;

                            StartTimer(eventTime, lagMs);
                        });
                    }
                    else if (Settings.Stop.IsPressed(key))
                    {
                        Post(() => StopTimer(false, lagMs));
                    }
                    else if (Settings.ToggleLevel.IsPressed(key))
                    {
                        Post(MainForm.ToggleLevel);
                    }
                    else if (Settings.ExportStats.IsPressed(key))
                    {
                        Post(MainForm.ExportStats);
                    }

                    Direction? tap = typing ? null : ContextDirection(key);
                    if (tap != null)
                    {
                        Post(() =>
                        {
                            if (!MainForm.ReportMovement(tap.Value)) Context.Tap(tap.Value, eventTime);
                        });
                    }

                    int focus = typing ? 0 : ContextFocus(key);
                    if (focus != 0)
                    {
                        Post(() =>
                        {
                            if (!MainForm.ReportFocus(focus)) Context.MoveFocus(focus);
                        });
                    }

                    if (!typing && Settings.NpcUndo.IsPressed(key))
                    {
                        Post(() =>
                        {
                            if (!MainForm.ReportUndo()) Context.Undo();
                        });
                    }

                    if (!typing && Settings.NpcComplete.IsPressed(key))
                    {
                        Post(() => Context.Next());
                    }

                    if (!typing && Settings.NpcMiss.IsPressed(key))
                    {
                        Post(() => Context.Miss());
                    }

                    HotkeyAction? listAction = typing ? null : ListAction(key);
                    if (listAction != null)
                    {
                        Post(() => MainForm.ScrollResults(listAction.Value));
                    }

                    if (!typing) Post(() => CurrentTab.OnKeyEvent(key));

                    if (!bound)
                    {
                        bool extended = (kbd.flags & Win32.LLKHF_EXTENDED) != 0;
                        Post(() => MainForm.HandleGlobalNumpad(key, extended));
                    }
                }

                LastKeyEvent[index] = wParam;
            }
            catch (Exception)
            {
            }
        }

        return Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private static bool IsMasterSwitch(Keys key)
    {
        if (Settings.ToggleGlobalHotkeys.IsPressed(key))
        {
            Post(MainForm.ToggleGlobalHotkeys);
            return true;
        }

        return !Settings.GlobalHotkeysEnabled && !Win32.IsForeground(MainFormHandle);
    }

    private static HotkeyAction? ListAction(Keys key)
    {
        if (Settings.ListUp.IsPressed(key)) return HotkeyAction.ListUp;
        if (Settings.ListDown.IsPressed(key)) return HotkeyAction.ListDown;

        return null;
    }

    private static Direction? ContextDirection(Keys key)
    {
        if (Settings.NpcUp.IsPressed(key)) return Direction.North;
        if (Settings.NpcDown.IsPressed(key)) return Direction.South;
        if (Settings.NpcLeft.IsPressed(key)) return Direction.West;
        if (Settings.NpcRight.IsPressed(key)) return Direction.East;

        return null;
    }

    private static int ContextFocus(Keys key)
    {
        if (Settings.NpcFocusPrev.IsPressed(key)) return -1;
        if (Settings.NpcFocusNext.IsPressed(key)) return 1;

        return 0;
    }

    public static void StartTimer(double? startTimeMs = null, double lagMs = 0.0)
    {
        Beeps.ClearPending();
        StopTimerThread();

        IsTimerRunning = true;
        TimerExpired = false;
        TimerCuesFinish = false;
        TimerStart = startTimeMs ?? Win32.GetTime();
        TimerStartLagMs = startTimeMs != null ? lagMs : 0.0;

        Context.Start();

        CurrentTab.OnTimerStart();

        _timerThreadRunning = true;
        _timerUpdateThread = new Thread(TimerUpdateCallback) { IsBackground = true, Name = "TimerUpdate" };
        _timerUpdateThread.Start();
    }

    public static void StopTimer(bool timerExpired, double lagMs = 0.0, bool letCuesFinish = false)
    {
        if (!IsTimerRunning) return;

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
