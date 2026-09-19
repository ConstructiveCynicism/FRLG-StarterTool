using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FRLG.StarterTool.App;

internal sealed class HidPadReader : IDisposable
{
    public enum PadKind
    {
        None,
        SwitchPro,
        DualShock4,
        DualSense,
    }

    public readonly struct State
    {
        public State(uint buttons, int hat, double lx, double ly, double rx, double ry, double? u, double? v, double time)
        {
            Buttons = buttons;
            Up = hat is 7 or 0 or 1;
            Right = hat is 1 or 2 or 3;
            Down = hat is 3 or 4 or 5;
            Left = hat is 5 or 6 or 7;
            LeftX = lx; LeftY = ly; RightX = rx; RightY = ry;
            U = u; V = v;
            Time = time;
        }

        public uint Buttons { get; }
        public bool Up { get; }
        public bool Right { get; }
        public bool Down { get; }
        public bool Left { get; }

        public double LeftX { get; }
        public double LeftY { get; }

        public double RightX { get; }
        public double RightY { get; }

        public double? U { get; }
        public double? V { get; }

        public double Time { get; }
    }

    private const double SwitchStickTravel = 1400.0;

    private readonly SafeFileHandle _handle;

    private readonly PadKind _kind;

    private readonly object _lock = new();

    private State _state;

    private bool _hasState;

    private volatile bool _alive = true;

    private HidPadReader(string path, PadKind kind, SafeFileHandle handle)
    {
        Path = path;
        _kind = kind;
        _handle = handle;
        new Thread(Read) { IsBackground = true, Name = "HidPadReader", Priority = ThreadPriority.AboveNormal }.Start();
    }

    public string Path { get; }

    public bool Alive => _alive;

    public static PadKind KindOf(ushort vendor, ushort product) => (vendor, product) switch
    {
        (0x057E, 0x2009) => PadKind.SwitchPro,
        (0x054C, 0x05C4) or (0x054C, 0x09CC) => PadKind.DualShock4,
        (0x054C, 0x0CE6) or (0x054C, 0x0DF2) => PadKind.DualSense,
        _ => PadKind.None,
    };

    public static HidPadReader? Open(ushort vendor, ushort product, IEnumerable<string> hidPaths, Func<string, bool> taken)
    {
        PadKind kind = KindOf(vendor, product);
        if (kind == PadKind.None) return null;

        string vid = vendor.ToString("x4", CultureInfo.InvariantCulture);
        string pid = product.ToString("x4", CultureInfo.InvariantCulture);
        string usb = "vid_" + vid + "&pid_" + pid;
        string bluetooth = vid + "_pid&" + pid;

        foreach (string path in hidPaths)
        {
            string lower = path.ToLowerInvariant();
            bool overBluetooth = lower.Contains(bluetooth);
            if (!overBluetooth && !lower.Contains(usb)) continue;

            if (!overBluetooth && kind != PadKind.SwitchPro) continue;
            if (taken(path)) continue;

            SafeFileHandle handle = CreateFileW(path, GenericRead, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                continue;
            }
            return new HidPadReader(path, kind, handle);
        }
        return null;
    }

    public bool TryGetState(out State state)
    {
        lock (_lock)
        {
            state = _state;
            return _hasState;
        }
    }

    public void Dispose()
    {
        _alive = false;
        try
        {
            CancelIoEx(_handle, IntPtr.Zero);
        }
        catch (Exception)
        {
        }
        _handle.Dispose();
    }

    private void Read()
    {
        var buffer = new byte[1024];
        try
        {
            while (_alive)
            {
                if (!ReadFile(_handle, buffer, (uint)buffer.Length, out uint read, IntPtr.Zero)) break;
                if (read < 12) continue;

                double now = Win32.GetTime();
                State? parsed = (_kind, buffer[0]) switch
                {
                    (PadKind.SwitchPro, 0x3F) => ParseSwitchSimple(buffer, now),
                    (PadKind.SwitchPro, 0x30 or 0x31 or 0x32 or 0x33 or 0x21) => ParseSwitchFull(buffer, now),

                    (PadKind.DualShock4 or PadKind.DualSense, 0x01) => ParseSony(buffer, sticks: 1, buttons: 5, triggers: 8, now),

                    (PadKind.DualShock4, 0x11) => ParseSony(buffer, sticks: 3, buttons: 7, triggers: 10, now),

                    (PadKind.DualSense, 0x31) => ParseSony(buffer, sticks: 2, buttons: 9, triggers: 6, now),
                    _ => null,
                };
                if (parsed is not State state) continue;

                lock (_lock)
                {
                    _state = state;
                    _hasState = true;
                }
            }
        }
        catch (Exception)
        {
        }
        _alive = false;
    }

    private static State ParseSwitchSimple(byte[] r, double now)
    {
        uint buttons = (uint)(r[1] | (r[2] << 8));

        static double Axis(byte low, byte high) => Math.Clamp(((low | (high << 8)) - 32768) / 32768.0, -1.0, 1.0);

        return new State(buttons, r[3],
            Axis(r[4], r[5]), Axis(r[6], r[7]), Axis(r[8], r[9]), Axis(r[10], r[11]), null, null, now);
    }

    private static State ParseSwitchFull(byte[] r, double now)
    {
        byte rightSide = r[3], shared = r[4], leftSide = r[5];

        uint buttons = 0;
        if ((rightSide & 0x04) != 0) buttons |= 1u << 0;
        if ((rightSide & 0x08) != 0) buttons |= 1u << 1;
        if ((rightSide & 0x01) != 0) buttons |= 1u << 2;
        if ((rightSide & 0x02) != 0) buttons |= 1u << 3;
        if ((leftSide & 0x40) != 0) buttons |= 1u << 4;
        if ((rightSide & 0x40) != 0) buttons |= 1u << 5;
        if ((leftSide & 0x80) != 0) buttons |= 1u << 6;
        if ((rightSide & 0x80) != 0) buttons |= 1u << 7;
        if ((shared & 0x01) != 0) buttons |= 1u << 8;
        if ((shared & 0x02) != 0) buttons |= 1u << 9;
        if ((shared & 0x08) != 0) buttons |= 1u << 10;
        if ((shared & 0x04) != 0) buttons |= 1u << 11;
        if ((shared & 0x10) != 0) buttons |= 1u << 12;
        if ((shared & 0x20) != 0) buttons |= 1u << 13;

        bool up = (leftSide & 0x02) != 0, right = (leftSide & 0x04) != 0, down = (leftSide & 0x01) != 0, left = (leftSide & 0x08) != 0;
        int hat = up ? (right ? 1 : left ? 7 : 0)
            : down ? (right ? 3 : left ? 5 : 4)
            : right ? 2 : left ? 6 : 8;

        int lx = r[6] | ((r[7] & 0x0F) << 8);
        int ly = (r[7] >> 4) | (r[8] << 4);
        int rx = r[9] | ((r[10] & 0x0F) << 8);
        int ry = (r[10] >> 4) | (r[11] << 4);

        static double Axis(int raw) => Math.Clamp((raw - 2048) / SwitchStickTravel, -1.0, 1.0);

        return new State(buttons, hat, Axis(lx), -Axis(ly), Axis(rx), -Axis(ry), null, null, now);
    }

    private static State ParseSony(byte[] r, int sticks, int buttons, int triggers, double now)
    {
        uint mask = (uint)((r[buttons] >> 4) | (r[buttons + 1] << 4) | ((r[buttons + 2] & 0x03) << 12));

        static double Axis(byte value) => value / 255.0 * 2.0 - 1.0;

        return new State(mask, r[buttons] & 0x0F,
            Axis(r[sticks]), Axis(r[sticks + 1]), Axis(r[sticks + 2]), Axis(r[sticks + 3]),
            Axis(r[triggers]), Axis(r[triggers + 1]), now);
    }

    private const uint GenericRead = 0x80000000;

    private const uint FileShareRead = 1;

    private const uint FileShareWrite = 2;

    private const uint OpenExisting = 3;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string fileName, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(SafeFileHandle handle, byte[] buffer, uint toRead, out uint read, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CancelIoEx(SafeFileHandle handle, IntPtr overlapped);
}
