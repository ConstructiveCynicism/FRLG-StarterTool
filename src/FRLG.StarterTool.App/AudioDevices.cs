using System.Runtime.InteropServices;

namespace FRLG.StarterTool.App;

internal static class AudioDevices
{
    public readonly record struct Endpoint(string Id, string Name);

    public static List<Endpoint> List(bool capture)
    {
        var endpoints = new List<Endpoint>();
        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? collection = null;
        try
        {
            enumerator = CreateEnumerator();
            if (enumerator.EnumAudioEndpoints(capture ? DataFlowCapture : DataFlowRender, DeviceStateActive, out collection) < 0)
            {
                return endpoints;
            }

            collection.GetCount(out uint count);
            for (uint i = 0; i < count; i++)
            {
                if (collection.Item(i, out IMMDevice device) < 0) continue;
                try
                {
                    string? id = IdOf(device);
                    if (id != null) endpoints.Add(new Endpoint(id, NameOf(device) ?? id));
                }
                finally
                {
                    Marshal.FinalReleaseComObject(device);
                }
            }
        }
        catch (Exception e) when (e is COMException or InvalidCastException)
        {
        }
        finally
        {
            if (collection != null) Marshal.FinalReleaseComObject(collection);
            if (enumerator != null) Marshal.FinalReleaseComObject(enumerator);
        }

        return endpoints;
    }

    public static IMMDevice? Resolve(string? id, bool capture, out bool fellBack)
    {
        fellBack = false;
        var enumerator = CreateEnumerator();
        try
        {
            if (!string.IsNullOrEmpty(id))
            {
                if (enumerator.GetDevice(id, out IMMDevice chosen) >= 0)
                {
                    if (chosen.GetState(out int state) >= 0 && state == DeviceStateActive) return chosen;
                    Marshal.FinalReleaseComObject(chosen);
                }

                fellBack = true;
            }

            int flow = capture ? DataFlowCapture : DataFlowRender;
            return enumerator.GetDefaultAudioEndpoint(flow, RoleConsole, out IMMDevice device) >= 0 ? device : null;
        }
        finally
        {
            Marshal.FinalReleaseComObject(enumerator);
        }
    }

    public static string? IdOf(IMMDevice device)
    {
        if (device.GetId(out IntPtr idPtr) < 0 || idPtr == IntPtr.Zero) return null;
        string? id = Marshal.PtrToStringUni(idPtr);
        Marshal.FreeCoTaskMem(idPtr);
        return id;
    }

    public static string? NameOf(IMMDevice device)
    {
        if (device.OpenPropertyStore(StgmRead, out IPropertyStore? store) < 0 || store == null) return null;
        try
        {
            var key = new PROPERTYKEY { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };
            var value = new PROPVARIANT();
            if (store.GetValue(ref key, ref value) < 0) return null;
            try
            {
                return value.vt == VT_LPWSTR && value.data != IntPtr.Zero ? Marshal.PtrToStringUni(value.data) : null;
            }
            finally
            {
                PropVariantClear(ref value);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(store);
        }
    }

    public sealed class Capture : IDisposable
    {
        public const int SampleRate = 16000;

        private IAudioClient? _client;
        private IAudioCaptureClient? _capture;

        public string DeviceName { get; }

        private Capture(IAudioClient client, IAudioCaptureClient capture, string deviceName)
        {
            _client = client;
            _capture = capture;
            DeviceName = deviceName;
        }

        public static Capture? Open(string? id, Action<string> log)
        {
            IMMDevice? device = Resolve(id, capture: true, out bool fellBack);
            if (device == null)
            {
                log("voice: no recording device");
                return null;
            }

            IntPtr format = IntPtr.Zero;
            try
            {
                string name = NameOf(device) ?? "unnamed";
                if (fellBack) log("voice: the chosen recording device is not connected, using the default");

                var iid = IID_IAudioClient;
                if (device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out object activated) < 0 || activated is not IAudioClient client)
                {
                    log($"voice: \"{name}\" refused an audio client");
                    return null;
                }

                format = Marshal.AllocHGlobal(Marshal.SizeOf<WAVEFORMATEX>());
                Marshal.StructureToPtr(new WAVEFORMATEX
                {
                    wFormatTag = 1,
                    nChannels = 1,
                    nSamplesPerSec = SampleRate,
                    nAvgBytesPerSec = SampleRate * 2,
                    nBlockAlign = 2,
                    wBitsPerSample = 16,
                    cbSize = 0
                }, format, false);

                int hr = client.Initialize(0, ConvertFlags, 2_000_000, 0, format, IntPtr.Zero);
                if (hr < 0)
                {
                    log($"voice: \"{name}\" refused a 16 kHz mono capture stream (0x{hr:X8})");
                    Marshal.FinalReleaseComObject(client);
                    return null;
                }

                var captureIid = IID_IAudioCaptureClient;
                if (client.GetService(ref captureIid, out object service) < 0 || service is not IAudioCaptureClient captureClient)
                {
                    log($"voice: \"{name}\" has no capture service");
                    Marshal.FinalReleaseComObject(client);
                    return null;
                }

                hr = client.Start();
                if (hr < 0)
                {
                    log($"voice: \"{name}\" would not start (0x{hr:X8})");
                    Marshal.FinalReleaseComObject(captureClient);
                    Marshal.FinalReleaseComObject(client);
                    return null;
                }

                log($"voice: listening on \"{name}\"");
                return new Capture(client, captureClient, name);
            }
            finally
            {
                if (format != IntPtr.Zero) Marshal.FreeHGlobal(format);
                Marshal.FinalReleaseComObject(device);
            }
        }

        public bool Drain(Action<byte[], int> sink)
        {
            if (_capture == null) return false;

            while (true)
            {
                int hr = _capture.GetNextPacketSize(out uint packet);
                if (hr < 0) return false;
                if (packet == 0) return true;

                hr = _capture.GetBuffer(out IntPtr data, out uint frames, out uint flags, out _, out _);
                if (hr < 0) return false;

                int bytes = (int)frames * 2;
                var chunk = new byte[bytes];
                if ((flags & BufferFlagSilent) == 0 && data != IntPtr.Zero) Marshal.Copy(data, chunk, 0, bytes);
                _capture.ReleaseBuffer(frames);
                sink(chunk, bytes);
            }
        }

        public void Dispose()
        {
            try { _client?.Stop(); } catch (COMException) { }
            if (_capture != null) { Marshal.FinalReleaseComObject(_capture); _capture = null; }
            if (_client != null) { Marshal.FinalReleaseComObject(_client); _client = null; }
        }
    }

    private const int DataFlowRender = 0;
    private const int DataFlowCapture = 1;
    private const int RoleConsole = 0;
    private const int DeviceStateActive = 1;
    private const int StgmRead = 0;
    private const uint CLSCTX_ALL = 0x17;
    private const ushort VT_LPWSTR = 31;
    private const uint BufferFlagSilent = 0x2;

    private const uint ConvertFlags = 0x80000000 | 0x08000000;

    private static readonly Guid IID_IAudioClient = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
    private static readonly Guid IID_IAudioCaptureClient = new("C8ADBD64-E71E-48a0-A4DE-185C395CD317");

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROPVARIANT
    {
        public ushort vt;
        public ushort reserved1;
        public ushort reserved2;
        public ushort reserved3;
        public IntPtr data;
        public IntPtr data2;
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT value);

    internal static object CreateEnumeratorObject() =>
        Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"), throwOnError: true)!)!;

    private static IMMDeviceEnumerator CreateEnumerator() => (IMMDeviceEnumerator)CreateEnumeratorObject();

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore? properties);
        [PreserveSig] int GetId(out IntPtr id);
        [PreserveSig] int GetState(out int state);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PROPERTYKEY key);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint frames);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }
}
