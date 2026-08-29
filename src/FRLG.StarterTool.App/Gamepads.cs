using System.Runtime.InteropServices;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public static class Gamepads
{
    public static event Action<InputCode, bool, double>? Changed;

    private const int XInputSlots = 4;

    private const int JoystickSlots = GamepadInput.MaxPads - GamepadInput.FirstJoystickPad;

    private const double ProbeIntervalMs = 1000.0;

    private const int StickPress = 16384;

    private const int StickRelease = 10923;

    private const int TriggerPress = 64;

    private const int TriggerRelease = 40;

    private static readonly bool[][] Down = BuildState();

    private static readonly bool[] Connected = new bool[GamepadInput.MaxPads];

    private static readonly double[] NextProbe = new double[GamepadInput.MaxPads];

    private static readonly JoystickCaps[] Caps = new JoystickCaps[JoystickSlots];

    private static HashSet<uint> _xinputIds = new();

    private static double _xinputIdsRead = double.NegativeInfinity;

    private static Thread? _thread;

    private static volatile bool _running;

    private static bool[][] BuildState()
    {
        var state = new bool[GamepadInput.MaxPads][];
        for (int pad = 0; pad < state.Length; pad++) state[pad] = new bool[GamepadInput.MaxCode + 1];
        return state;
    }

    public static bool IsDown(int pad, int code)
        => pad >= 0 && pad < GamepadInput.MaxPads && code > 0 && code <= GamepadInput.MaxCode && Down[pad][code];

    public static int ConnectedCount
    {
        get
        {
            int count = 0;
            foreach (bool connected in Connected)
            {
                if (connected) count++;
            }
            return count;
        }
    }

    public static void Start()
    {
        if (_thread != null) return;

        _running = true;
        _thread = new Thread(Poll) { IsBackground = true, Name = "GamepadPoll", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    public static void Stop()
    {
        _running = false;
        _thread?.Join(500);
        _thread = null;
    }

    private static void Poll()
    {
        while (_running)
        {
            bool any = false;
            try
            {
                double now = Win32.GetTime();
                for (int slot = 0; slot < XInputSlots; slot++) any |= PollXInput(slot, now);

                bool xinputLive = false;
                for (int slot = 0; slot < XInputSlots; slot++) xinputLive |= Connected[slot];
                for (int slot = 0; slot < JoystickSlots; slot++) any |= PollJoystick(slot, now, xinputLive);
            }
            catch (Exception)
            {
            }

            Thread.Sleep(any ? 1 : 100);
        }
    }

    private static bool PollXInput(int slot, double now)
    {
        if (!Connected[slot] && now < NextProbe[slot]) return false;

        bool ok = Native.XInputGetState((uint)slot, out Native.XInputState state) == 0;
        if (!ok)
        {
            Disconnect(slot, now);
            return false;
        }

        Connected[slot] = true;
        Native.XInputGamepad pad = state.Gamepad;
        ushort buttons = pad.Buttons;

        Set(slot, GamepadInput.DpadUp, (buttons & 0x0001) != 0, now);
        Set(slot, GamepadInput.DpadDown, (buttons & 0x0002) != 0, now);
        Set(slot, GamepadInput.DpadLeft, (buttons & 0x0004) != 0, now);
        Set(slot, GamepadInput.DpadRight, (buttons & 0x0008) != 0, now);
        Set(slot, GamepadInput.Start, (buttons & 0x0010) != 0, now);
        Set(slot, GamepadInput.Back, (buttons & 0x0020) != 0, now);
        Set(slot, GamepadInput.LeftStickClick, (buttons & 0x0040) != 0, now);
        Set(slot, GamepadInput.RightStickClick, (buttons & 0x0080) != 0, now);
        Set(slot, GamepadInput.LeftBumper, (buttons & 0x0100) != 0, now);
        Set(slot, GamepadInput.RightBumper, (buttons & 0x0200) != 0, now);
        Set(slot, GamepadInput.Guide, (buttons & 0x0400) != 0, now);
        Set(slot, GamepadInput.A, (buttons & 0x1000) != 0, now);
        Set(slot, GamepadInput.B, (buttons & 0x2000) != 0, now);
        Set(slot, GamepadInput.X, (buttons & 0x4000) != 0, now);
        Set(slot, GamepadInput.Y, (buttons & 0x8000) != 0, now);

        SetWithHysteresis(slot, GamepadInput.LeftTrigger, pad.LeftTrigger, TriggerPress, TriggerRelease, now);
        SetWithHysteresis(slot, GamepadInput.RightTrigger, pad.RightTrigger, TriggerPress, TriggerRelease, now);

        SetStick(slot, GamepadInput.LeftStickLeft, GamepadInput.LeftStickRight, pad.ThumbLX, now);
        SetStick(slot, GamepadInput.LeftStickDown, GamepadInput.LeftStickUp, pad.ThumbLY, now);
        SetStick(slot, GamepadInput.RightStickLeft, GamepadInput.RightStickRight, pad.ThumbRX, now);
        SetStick(slot, GamepadInput.RightStickDown, GamepadInput.RightStickUp, pad.ThumbRY, now);
        return true;
    }

    private static bool PollJoystick(int slot, double now, bool xinputLive)
    {
        int pad = GamepadInput.FirstJoystickPad + slot;
        if (!Connected[pad] && now < NextProbe[pad]) return false;

        if (!Connected[pad])
        {
            if (Native.joyGetDevCapsW((uint)slot, out Native.JoyCaps caps, Marshal.SizeOf<Native.JoyCaps>()) != 0)
            {
                Disconnect(pad, now);
                return false;
            }
            Caps[slot] = new JoystickCaps(caps, IsXInputDevice(caps.ManufacturerId, caps.ProductId, now));
        }

        if (xinputLive && Caps[slot].IsXInput)
        {
            Disconnect(pad, now);
            return false;
        }

        var info = new Native.JoyInfoEx { Size = (uint)Marshal.SizeOf<Native.JoyInfoEx>(), Flags = Native.JoyReturnAll };
        if (Native.joyGetPosEx((uint)slot, ref info) != 0)
        {
            Disconnect(pad, now);
            return false;
        }

        Connected[pad] = true;
        JoystickCaps range = Caps[slot];

        for (int button = 0; button < Math.Min((int)range.Buttons, GamepadInput.MaxButtons); button++)
        {
            Set(pad, GamepadInput.FirstButton + button + 1, (info.Buttons & (1u << button)) != 0, now);
        }

        bool centred = info.Pov == 0xFFFF || info.Pov > 36000;
        int heading = centred ? -1 : (int)info.Pov;
        Set(pad, GamepadInput.DpadUp, !centred && (heading > 27000 || heading < 9000), now);
        Set(pad, GamepadInput.DpadRight, !centred && heading > 0 && heading < 18000, now);
        Set(pad, GamepadInput.DpadDown, !centred && heading > 9000 && heading < 27000, now);
        Set(pad, GamepadInput.DpadLeft, !centred && heading > 18000, now);

        SetAxis(pad, GamepadInput.LeftStickLeft, GamepadInput.LeftStickRight, range.Normalize(info.X, range.XMin, range.XMax), now);
        SetAxis(pad, GamepadInput.LeftStickUp, GamepadInput.LeftStickDown, range.Normalize(info.Y, range.YMin, range.YMax), now);
        if (range.Axes > 2) SetAxis(pad, GamepadInput.AxisZMinus, GamepadInput.AxisZPlus, range.Normalize(info.Z, range.ZMin, range.ZMax), now);
        if (range.Axes > 3) SetAxis(pad, GamepadInput.AxisRMinus, GamepadInput.AxisRPlus, range.Normalize(info.R, range.RMin, range.RMax), now);
        if (range.Axes > 4) SetAxis(pad, GamepadInput.AxisUMinus, GamepadInput.AxisUPlus, range.Normalize(info.U, range.UMin, range.UMax), now);
        if (range.Axes > 5) SetAxis(pad, GamepadInput.AxisVMinus, GamepadInput.AxisVPlus, range.Normalize(info.V, range.VMin, range.VMax), now);
        return true;
    }

    private static bool IsXInputDevice(ushort vendor, ushort product, double now)
    {
        if (now - _xinputIdsRead >= ProbeIntervalMs)
        {
            _xinputIds = XInputDeviceIds();
            _xinputIdsRead = now;
        }
        return _xinputIds.Contains(((uint)vendor << 16) | product);
    }

    private static HashSet<uint> XInputDeviceIds()
    {
        var ids = new HashSet<uint>();
        try
        {
            foreach (string path in Native.HidInterfacePaths())
            {
                string lower = path.ToLowerInvariant();
                if (!lower.Contains("ig_")) continue;
                int vid = lower.IndexOf("vid_", StringComparison.Ordinal);
                int pid = lower.IndexOf("pid_", StringComparison.Ordinal);
                if (vid < 0 || pid < 0 || vid + 8 > lower.Length || pid + 8 > lower.Length) continue;
                if (!uint.TryParse(lower.AsSpan(vid + 4, 4), System.Globalization.NumberStyles.HexNumber, null, out uint vendor)) continue;
                if (!uint.TryParse(lower.AsSpan(pid + 4, 4), System.Globalization.NumberStyles.HexNumber, null, out uint product)) continue;
                ids.Add((vendor << 16) | product);
            }
        }
        catch (Exception)
        {
        }
        return ids;
    }

    private static void Disconnect(int pad, double now)
    {
        if (Connected[pad])
        {
            Connected[pad] = false;
            bool[] state = Down[pad];
            for (int code = 1; code < state.Length; code++) Set(pad, code, false, now);
        }
        NextProbe[pad] = now + ProbeIntervalMs;
    }

    private static void Set(int pad, int code, bool down, double now)
    {
        if (Down[pad][code] == down) return;

        Down[pad][code] = down;
        Changed?.Invoke(InputCode.Button(pad, code), down, now);
    }

    private static void SetWithHysteresis(int pad, int code, int value, int press, int release, double now)
    {
        if (Down[pad][code]) Set(pad, code, value >= release, now);
        else Set(pad, code, value >= press, now);
    }

    private static void SetStick(int pad, int negative, int positive, short value, double now)
    {
        SetWithHysteresis(pad, negative, -value, StickPress, StickRelease, now);
        SetWithHysteresis(pad, positive, value, StickPress, StickRelease, now);
    }

    private static void SetAxis(int pad, int negative, int positive, double value, double now)
    {
        SetWithHysteresis(pad, negative, (int)(-value * 32767), StickPress, StickRelease, now);
        SetWithHysteresis(pad, positive, (int)(value * 32767), StickPress, StickRelease, now);
    }

    private readonly struct JoystickCaps
    {
        public JoystickCaps(Native.JoyCaps caps, bool xinputDevice)
        {
            Buttons = caps.NumButtons;
            Axes = caps.NumAxes;
            XMin = caps.XMin; XMax = caps.XMax;
            YMin = caps.YMin; YMax = caps.YMax;
            ZMin = caps.ZMin; ZMax = caps.ZMax;
            RMin = caps.RMin; RMax = caps.RMax;
            UMin = caps.UMin; UMax = caps.UMax;
            VMin = caps.VMin; VMax = caps.VMax;

            string name = caps.ProductName ?? "";
            IsXInput = xinputDevice
                       || caps.ManufacturerId == 0x045E
                       || name.Contains("xbox", StringComparison.OrdinalIgnoreCase)
                       || name.Contains("xinput", StringComparison.OrdinalIgnoreCase);
        }

        public uint Buttons { get; }
        public uint Axes { get; }
        public uint XMin { get; }
        public uint XMax { get; }
        public uint YMin { get; }
        public uint YMax { get; }
        public uint ZMin { get; }
        public uint ZMax { get; }
        public uint RMin { get; }
        public uint RMax { get; }
        public uint UMin { get; }
        public uint UMax { get; }
        public uint VMin { get; }
        public uint VMax { get; }
        public bool IsXInput { get; }

        public double Normalize(uint value, uint min, uint max)
        {
            if (max <= min) return 0.0;
            double unit = (value - (double)min) / (max - (double)min);
            return Math.Clamp(unit * 2.0 - 1.0, -1.0, 1.0);
        }
    }

    private static class Native
    {
        public const uint JoyReturnAll = 0xFF;

        private static int _xinputLibrary;

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputGamepad
        {
            public ushort Buttons;
            public byte LeftTrigger;
            public byte RightTrigger;
            public short ThumbLX;
            public short ThumbLY;
            public short ThumbRX;
            public short ThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputState
        {
            public uint PacketNumber;
            public XInputGamepad Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JoyInfoEx
        {
            public uint Size;
            public uint Flags;
            public uint X;
            public uint Y;
            public uint Z;
            public uint R;
            public uint U;
            public uint V;
            public uint Buttons;
            public uint ButtonNumber;
            public uint Pov;
            public uint Reserved1;
            public uint Reserved2;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct JoyCaps
        {
            public ushort ManufacturerId;
            public ushort ProductId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string ProductName;
            public uint XMin;
            public uint XMax;
            public uint YMin;
            public uint YMax;
            public uint ZMin;
            public uint ZMax;
            public uint NumButtons;
            public uint PeriodMin;
            public uint PeriodMax;
            public uint RMin;
            public uint RMax;
            public uint UMin;
            public uint UMax;
            public uint VMin;
            public uint VMax;
            public uint Caps;
            public uint MaxAxes;
            public uint NumAxes;
            public uint MaxButtons;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string RegKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string OemVxD;
        }

        public static uint XInputGetState(uint index, out XInputState state)
        {
            state = default;
            switch (_xinputLibrary)
            {
                case 0:
                    try
                    {
                        uint result = XInputGetState14(index, out state);
                        _xinputLibrary = 1;
                        return result;
                    }
                    catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
                    {
                        _xinputLibrary = 2;
                        goto case 2;
                    }
                case 1:
                    return XInputGetState14(index, out state);
                case 2:
                    try
                    {
                        return XInputGetState910(index, out state);
                    }
                    catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
                    {
                        _xinputLibrary = 3;
                        return 1;
                    }
                default:
                    return 1;
            }
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState14(uint dwUserIndex, out XInputState pState);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState910(uint dwUserIndex, out XInputState pState);

        private static readonly Guid HidInterfaceClass = new("4D1E55B2-F16F-11CF-88CB-001111000030");

        private const uint InterfaceListPresent = 1;

        public static IEnumerable<string> HidInterfacePaths()
        {
            Guid guid = HidInterfaceClass;
            if (CM_Get_Device_Interface_List_SizeW(out uint length, ref guid, null, InterfaceListPresent) != 0 || length == 0)
            {
                return Array.Empty<string>();
            }
            var buffer = new char[length];
            if (CM_Get_Device_Interface_ListW(ref guid, null, buffer, length, InterfaceListPresent) != 0)
            {
                return Array.Empty<string>();
            }
            return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern uint CM_Get_Device_Interface_List_SizeW(out uint pulLen, ref Guid interfaceClassGuid, string? pDeviceID, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern uint CM_Get_Device_Interface_ListW(ref Guid interfaceClassGuid, string? pDeviceID, char[] buffer, uint bufferLen, uint ulFlags);

        [DllImport("winmm.dll")]
        public static extern uint joyGetPosEx(uint uJoyID, ref JoyInfoEx pji);

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        public static extern uint joyGetDevCapsW(nuint uJoyID, out JoyCaps pjc, int cbjc);
    }
}
