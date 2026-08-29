using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FRLG.StarterTool.App;

public static class Win32
{
    public const int WH_KEYBOARD_LL = 0x000D;

    public const int WM_SETREDRAW = 0x000B;
    public const int WM_PAINT = 0x000F;
    public const int WM_NCPAINT = 0x0085;
    public const int WM_NCCALCSIZE = 0x0083;
    public const int WM_THEMECHANGED = 0x031A;
    public const int WM_PRINTCLIENT = 0x0318;

    public const int LVM_GETHEADER = 0x1000 + 31;

    public const int WM_NOTIFY = 0x004E;

    public const int HDN_BEGINTRACKA = -300 - 6;
    public const int HDN_BEGINTRACKW = -300 - 26;
    public const int HDN_DIVIDERDBLCLICKA = -300 - 5;
    public const int HDN_DIVIDERDBLCLICKW = -300 - 25;

    [StructLayout(LayoutKind.Sequential)]
    public struct NMHDR
    {
        public IntPtr hwndFrom;
        public IntPtr idFrom;
        public int code;
    }
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;
    public const int WM_MOUSEWHEEL = 0x020A;

    public const int LLKHF_EXTENDED = 0x01;

    public const uint WM_QUIT = 0x0012;

    private static double _frequency = 1.0;
    private static double _frequencyDrifted = 1.0;
    private static long _originTicks;
    private static double _originMs;
    private static bool _highResolutionTimer;
    private static double _tickGranularityMs = 16.0;

    public delegate IntPtr Proc(int nCode, int wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public static IntPtr SetHook(int id, Proc proc)
    {
        using Process curProcess = Process.GetCurrentProcess();
        using ProcessModule curModule = curProcess.MainModule!;
        return SetWindowsHookEx(id, proc, GetModuleHandle(curModule.ModuleName), 0);
    }

    public static Keys GetAsyncKey(params Keys[] keys)
    {
        foreach (Keys key in keys)
        {
            if (GetAsyncKeyState(key) != 0)
            {
                return key;
            }
        }

        return Keys.None;
    }

    public static bool IsKeyDown(Keys key) => key != Keys.None && (GetAsyncKeyState(key) & 0x8000) != 0;

    public static void InitTiming()
    {
        QueryPerformanceFrequency(out long freq);
        _frequency = freq / 1000.0;
        _frequencyDrifted = _frequency * Drift;

        _highResolutionTimer = timeBeginPeriod(1) == 0;

        _tickGranularityMs = MeasureTickGranularity();
    }

    public static double TickGranularityMs => _tickGranularityMs;

    private static double MeasureTickGranularity()
    {
        double deadline = GetTime() + 100.0;
        uint first = GetTickCount();
        while (GetTickCount() == first)
        {
            if (GetTime() > deadline) return 16.0;
        }

        uint second = GetTickCount();
        double start = GetTime();
        while (GetTickCount() == second)
        {
            if (GetTime() > deadline) return 16.0;
        }

        double step = GetTime() - start;
        return step > 0.5 && step < 40.0 ? step : 16.0;
    }

    public static void EndTiming()
    {
        if (!_highResolutionTimer) return;

        timeEndPeriod(1);
        _highResolutionTimer = false;
    }

    public static double GetTime()
    {
        QueryPerformanceCounter(out long timeStamp);
        return _originMs + (timeStamp - _originTicks) / _frequencyDrifted;
    }

    public static double Drift { get; private set; } = 1.0;

    public static void SetDrift(double drift)
    {
        double next = Core.Timing.DriftMonitor.IsPlausible(drift) ? drift : 1.0;
        QueryPerformanceCounter(out long now);
        _originMs += (now - _originTicks) / _frequencyDrifted;
        _originTicks = now;
        Drift = next;
        _frequencyDrifted = _frequency * next;
    }

    public static double EventTime(double now, uint eventTick) => now - EventLagMs(eventTick);

    public static double CurrentTimerResolutionMs()
    {
        try
        {
            if (NtQueryTimerResolution(out _, out _, out uint current) != 0) return double.NaN;
            return current / 10000.0;
        }
        catch (Exception)
        {
            return double.NaN;
        }
    }

    public static bool IsMinimized(IntPtr hWnd) => hWnd != IntPtr.Zero && IsIconic(hWnd);

    public static double EventLagMs(uint eventTick)
    {
        double gapMs = unchecked(GetTickCount() - eventTick);
        if (gapMs <= 0.0 || gapMs >= 250.0) return 0.0;

        double lagMs = gapMs - _tickGranularityMs;
        return lagMs > 0.0 ? lagMs : 0.0;
    }

    public static void RunMessageLoop()
    {
        while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    public static bool IsForeground(IntPtr hWnd) => hWnd != IntPtr.Zero && GetForegroundWindow() == hWnd;

    public static void SetDarkTitleBar(IntPtr hWnd, bool dark)
    {
        int value = dark ? 1 : 0;
        if (DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE_PRE_20H1, ref value, sizeof(int));
        }

        RedrawFrame(hWnd);
    }

    public static void InitDarkModeSupport()
    {
        IntPtr uxtheme = LoadLibrary("uxtheme.dll");
        if (uxtheme == IntPtr.Zero) return;

        if (Environment.OSVersion.Version.Build >= 18362)
        {
            _setPreferredAppMode = GetProcAddress(uxtheme, 135);
        }

        _flushMenuThemes = GetProcAddress(uxtheme, 136);
        _allowDarkModeForWindow = GetProcAddress(uxtheme, 133);
    }

    public static void SetAppDarkMode(bool dark)
    {
        if (_setPreferredAppMode != IntPtr.Zero)
        {
            Marshal.GetDelegateForFunctionPointer<SetPreferredAppModeFn>(_setPreferredAppMode)(
                dark ? PreferredAppModeForceDark : PreferredAppModeForceLight);
        }

        if (_flushMenuThemes != IntPtr.Zero)
        {
            Marshal.GetDelegateForFunctionPointer<FlushMenuThemesFn>(_flushMenuThemes)();
        }
    }

    public static void SetDarkScrollBars(IntPtr hWnd, bool dark)
    {
        if (hWnd == IntPtr.Zero) return;

        if (_allowDarkModeForWindow != IntPtr.Zero)
        {
            Marshal.GetDelegateForFunctionPointer<AllowDarkModeForWindowFn>(_allowDarkModeForWindow)(hWnd, dark);
        }

        SetWindowTheme(hWnd, dark ? "DarkMode_Explorer" : "Explorer", null);

        SendMessage(hWnd, WM_THEMECHANGED, IntPtr.Zero, IntPtr.Zero);

        RedrawFrame(hWnd);
    }

    public static IntPtr GetComboBoxList(IntPtr comboHandle)
    {
        if (comboHandle == IntPtr.Zero) return IntPtr.Zero;

        var info = new COMBOBOXINFO { cbSize = Marshal.SizeOf<COMBOBOXINFO>() };
        return GetComboBoxInfo(comboHandle, ref info) ? info.hwndList : IntPtr.Zero;
    }

    public static void DisableVisualStyles(IntPtr hWnd)
    {
        if (hWnd != IntPtr.Zero) SetWindowTheme(hWnd, "", "");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct COMBOBOXINFO
    {
        public int cbSize;
        public RECT rcItem;
        public RECT rcButton;
        public int stateButton;
        public IntPtr hwndCombo;
        public IntPtr hwndItem;
        public IntPtr hwndList;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetComboBoxInfo(IntPtr hWnd, ref COMBOBOXINFO info);

    private static IntPtr _allowDarkModeForWindow;
    private static IntPtr _setPreferredAppMode;
    private static IntPtr _flushMenuThemes;

    private const int PreferredAppModeForceDark = 2;
    private const int PreferredAppModeForceLight = 3;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetPreferredAppModeFn(int mode);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool AllowDarkModeForWindowFn(IntPtr hWnd, bool allow);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void FlushMenuThemesFn();

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? subAppName, string? subIdList);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", EntryPoint = "GetProcAddress")]
    private static extern IntPtr GetProcAddress(IntPtr hModule, IntPtr ordinal);

    private static IntPtr GetProcAddress(IntPtr hModule, int ordinal) =>
        GetProcAddress(hModule, new IntPtr(ordinal));

    public static void RedrawFrame(IntPtr hWnd) =>
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

    private const int SWP_NOSIZE = 0x0001;
    private const int SWP_NOMOVE = 0x0002;
    private const int SWP_NOZORDER = 0x0004;
    private const int SWP_FRAMECHANGED = 0x0020;
    private const int SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_PRE_20H1 = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern int SendMessage(IntPtr hWnd, int wMsg, bool wParam, int lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool erase);

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWindowsHookEx(int idHook, Proc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, int wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(Keys vKey);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryTimerResolution(out uint minimum, out uint maximum, out uint current);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll")]
    public static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

    [DllImport("kernel32.dll")]
    public static extern bool QueryPerformanceFrequency(out long lpFrequency);

    [DllImport("kernel32.dll")]
    public static extern uint GetTickCount();

    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint uPeriod);
}
