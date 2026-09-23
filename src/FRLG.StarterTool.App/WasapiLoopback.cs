using System.Runtime.InteropServices;

namespace FRLG.StarterTool.App;

internal sealed partial class WasapiOutput
{
    private static string ReadDeviceName(IMMDevice device)
    {
        if (device.OpenPropertyStore(0, out IntPtr pointer) < 0) return "Default render device";
        IPropertyStore? store = null;
        IntPtr value = Marshal.AllocCoTaskMem(24);
        try
        {
            for (int i = 0; i < 24; i++) Marshal.WriteByte(value, i, 0);
            store = (IPropertyStore)Marshal.GetObjectForIUnknown(pointer);
            var key = new PROPERTYKEY { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };
            if (store.GetValue(ref key, value) >= 0 && Marshal.ReadInt16(value) == 31)
                return Marshal.PtrToStringUni(Marshal.ReadIntPtr(value, 8)) ?? "Default render device";
            return "Default render device";
        }
        finally
        {
            PropVariantClear(value);
            Marshal.FreeCoTaskMem(value);
            if (store != null) Marshal.FinalReleaseComObject(store);
            Marshal.Release(pointer);
        }
    }

    internal sealed class LoopbackCapture : IDisposable
    {
        private IAudioClient? _client;
        private IAudioCaptureClient? _capture;
        private MixFormat _format;
        private byte[] _packet = Array.Empty<byte>();
        private int _quietFrames;
        private bool _armed;
        private double _lastEndMs = double.NaN;

        public string DeviceName { get; private set; } = "";
        public string FormatDescription => _format.Describe();
        public List<double> Onsets { get; } = new();
        public int InvalidPackets { get; private set; }
        public int Discontinuities { get; private set; }

        public static LoopbackCapture Open()
        {
            var result = new LoopbackCapture();
            try
            {
                result.Initialize();
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        private void Initialize()
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            IMMDevice? device = null;
            IntPtr format = IntPtr.Zero;
            try
            {
                Check(enumerator.GetDefaultAudioEndpoint(DataFlowRender, RoleConsole, out device));
                DeviceName = ReadDeviceName(device);
                _client = (IAudioClient?)Activate(device, IID_IAudioClient)
                    ?? throw new InvalidOperationException("The loopback audio client could not open.");
                Check(_client.GetMixFormat(out format));
                if (!MixFormat.TryParse(format, out _format))
                    throw new InvalidOperationException("The loopback mix format is unsupported.");
                const uint loopback = 0x00020000;
                Check(_client.Initialize(ShareModeShared, loopback, 10000000, 0, format, IntPtr.Zero));
                Guid iid = new("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
                Check(_client.GetService(ref iid, out object capture));
                _capture = (IAudioCaptureClient)capture;
                Check(_client.Start());
                _lastEndMs = Win32.GetTime();
            }
            finally
            {
                if (format != IntPtr.Zero) Marshal.FreeCoTaskMem(format);
                if (device != null) Marshal.FinalReleaseComObject(device);
                Marshal.FinalReleaseComObject(enumerator);
            }
        }

        public void Drain(double threshold)
        {
            Check(_capture!.GetNextPacketSize(out uint available));
            while (available > 0)
            {
                Check(_capture.GetBuffer(out IntPtr data, out uint frames, out uint flags, out _, out ulong qpc));
                try
                {
                    if ((flags & 4) != 0)
                    {
                        InvalidPackets++;
                        _quietFrames = 0;
                        _armed = false;
                    }
                    else
                    {
                        double startMs = Win32.SystemRelativeToMs(TimeSpan.FromTicks((long)qpc), out double drift);
                        if ((flags & 1) != 0)
                        {
                            Discontinuities++;
                            _quietFrames = 0;
                            _armed = false;
                        }
                        else if (startMs - _lastEndMs >= 20)
                        {
                            _armed = true;
                        }
                        int bytes = checked((int)frames * _format.BytesPerFrame);
                        if (_packet.Length < bytes) _packet = new byte[bytes];
                        bool silent = (flags & BufferFlagSilent) != 0;
                        if (!silent) Marshal.Copy(data, _packet, 0, bytes);
                        for (int frame = 0; frame < frames; frame++)
                        {
                            double peak = silent ? 0 : Peak(frame);
                            if (peak < threshold * 0.1)
                            {
                                if (++_quietFrames >= _format.SampleRate / 50) _armed = true;
                            }
                            else _quietFrames = 0;
                            if (_armed && peak >= threshold)
                            {
                                Onsets.Add(startMs + frame * 1000.0 / _format.SampleRate / drift);
                                _armed = false;
                            }
                        }
                        _lastEndMs = startMs + frames * 1000.0 / _format.SampleRate / drift;
                    }
                }
                finally
                {
                    Check(_capture.ReleaseBuffer(frames));
                }
                Check(_capture.GetNextPacketSize(out available));
            }
        }

        private double Peak(int frame)
        {
            double peak = 0;
            int sampleBytes = _format.BitsPerSample / 8;
            for (int channel = 0; channel < _format.Channels; channel++)
            {
                int at = frame * _format.BytesPerFrame + channel * sampleBytes;
                double value;
                if (_format.IsFloat) value = BitConverter.ToSingle(_packet, at);
                else if (sampleBytes == 2) value = BitConverter.ToInt16(_packet, at) / 32768.0;
                else if (sampleBytes == 4) value = BitConverter.ToInt32(_packet, at) / 2147483648.0;
                else
                {
                    int sample = (_packet[at] << 8) | (_packet[at + 1] << 16) | (_packet[at + 2] << 24);
                    value = sample / 2147483648.0;
                }
                peak = Math.Max(peak, Math.Abs(value));
            }
            return peak;
        }

        public void Dispose()
        {
            if (_client != null)
            {
                try { _client.Stop(); } catch (COMException) { }
            }
            if (_capture != null) Marshal.FinalReleaseComObject(_capture);
            if (_client != null) Marshal.FinalReleaseComObject(_client);
            _capture = null;
            _client = null;
        }
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong position, out ulong qpcPosition);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PROPERTYKEY key);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, IntPtr value);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, IntPtr value);
        [PreserveSig] int Commit();
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(IntPtr value);
}
