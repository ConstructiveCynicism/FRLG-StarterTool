using System.Globalization;
using System.Runtime.InteropServices;

namespace FRLG.StarterTool.App;

internal sealed class WasapiOutput : IBeepOutput
{
    private const int BytesPerFrame = BeepPlayer.NumChannels * BeepPlayer.BytesPerSample;

    private IAudioClient _client = null!;
    private IAudioRenderClient _render = null!;
    private IAudioClock? _clock;
    private AutoResetEvent _event = null!;
    private Thread? _feed;
    private volatile bool _stopping;
    private readonly object _lock = new();

    private uint _bufferFrames;
    private uint _targetFrames;
    private double _periodMs;
    private MixFormat _mix;
    private byte[]? _scratch;

    private byte[]? _pcm;
    private double _sourceFrame;

    private long _totalFedFrames;
    private long _anchorFrames;
    private ulong _clockFrequency;

    private DeviceWatch? _watch;
    private volatile bool _needsReopen;
    private readonly Action<string> _log;

    private double _defaultPeriodMs = 10.0;

    private const double ValidateMs = 5000.0;
    private const double ValidateJitterPeriods = 1.5;
    private const double ValidateRate = 0.0003;
    private const double ValidateSettleMs = 100.0;

    public double SuggestedPeriodMs { get; private set; }

    public event Action? DeviceChanged;

    private WasapiOutput(Action<string> log)
    {
        _log = log;
    }

    public bool IsOpen => _feed != null && !_stopping;

    public string Description { get; private set; } = "wasapi";

    public bool NeedsReopen => _needsReopen;

    public static WasapiOutput? Open(double periodMs, Action<string> log)
    {
        var output = new WasapiOutput(log);
        try
        {
            if (output.Initialize(periodMs)) return output;
        }
        catch (Exception e)
        {
            log($"audio: WASAPI open failed, {e.GetType().Name}: {e.Message}");
        }

        output.Dispose();
        return null;
    }

    private bool Initialize(double periodMs)
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        try
        {
            int hr = enumerator.GetDefaultAudioEndpoint(DataFlowRender, RoleConsole, out IMMDevice device);
            if (hr < 0)
            {
                _log($"audio: WASAPI has no default render device (0x{hr:X8})");
                return false;
            }

            try
            {
                if (!InitializeOn(device, periodMs)) return false;
            }
            finally
            {
                Marshal.FinalReleaseComObject(device);
            }

            _watch = new DeviceWatch(this);
            enumerator.RegisterEndpointNotificationCallback(_watch);
            _watch.Enumerator = enumerator;
            enumerator = null!;
            return true;
        }
        finally
        {
            if (enumerator != null) Marshal.FinalReleaseComObject(enumerator);
        }
    }

    private bool InitializeOn(IMMDevice device, double periodMs)
    {
        IntPtr ownFormat = AllocOwnFormat();
        IntPtr mixFormat = IntPtr.Zero;
        try
        {
            if (Activate(device, IID_IAudioClient3) is IAudioClient3 client3)
            {
                int hr = client3.GetMixFormat(out mixFormat);
                if (hr >= 0 && MixFormat.TryParse(mixFormat, out MixFormat mix))
                {
                    if (TryLowLatency(client3, mixFormat, periodMs, mix, "mix " + mix.Describe())) return true;
                }
                else if (hr >= 0)
                {
                    _log("audio: WASAPI mix format is one the feed cannot write, converting in the engine instead");
                }

                Marshal.FinalReleaseComObject(client3);
            }
            else
            {
                _log("audio: IAudioClient3 unavailable (Windows 10 or later), trying the engine's default period");
            }

            if (Activate(device, IID_IAudioClient) is IAudioClient client)
            {
                int hr = client.Initialize(ShareModeShared, EventFlag | ConvertFlags, 0, 0, ownFormat, IntPtr.Zero);
                if (hr >= 0)
                {
                    hr = client.GetDevicePeriod(out long defaultPeriod, out _);
                    double period = hr >= 0 ? defaultPeriod / 10000.0 : 10.0;
                    _defaultPeriodMs = period;
                    return Finish(client, period, MixFormat.Own, (uint)Math.Round(period / 1000.0 * BeepPlayer.SampleRate),
                        $"audio: output wasapi shared, engine period {period.ToString("F2", CultureInfo.InvariantCulture)} ms (the default; no low-latency path), pcm16 2ch, engine converting");
                }

                _log($"audio: WASAPI Initialize refused (0x{hr:X8})");
                Marshal.FinalReleaseComObject(client);
            }

            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(ownFormat);
            if (mixFormat != IntPtr.Zero) Marshal.FreeCoTaskMem(mixFormat);
        }
    }

    private bool TryLowLatency(IAudioClient3 client, IntPtr format, double periodMs, MixFormat mix, string formatNote)
    {
        int hr = client.GetSharedModeEnginePeriod(format, out uint defaultFrames, out uint fundamental, out uint minFrames, out uint maxFrames);
        if (hr < 0)
        {
            _log($"audio: WASAPI GetSharedModeEnginePeriod refused (0x{hr:X8}) for {formatNote}");
            return false;
        }

        uint periodFrames = minFrames;
        if (periodMs > 0 && fundamental > 0)
        {
            double asked = periodMs / 1000.0 * mix.SampleRate;
            periodFrames = (uint)Math.Round(asked / fundamental) * fundamental;
            periodFrames = Math.Clamp(periodFrames, minFrames, maxFrames);
        }

        hr = client.InitializeSharedAudioStream(EventFlag, periodFrames, format, IntPtr.Zero);
        if (hr < 0)
        {
            _log($"audio: WASAPI InitializeSharedAudioStream refused (0x{hr:X8}) at {mix.FramesToMs(periodFrames):F2} ms for {formatNote}");
            return false;
        }

        double period = mix.FramesToMs(periodFrames);
        _defaultPeriodMs = mix.FramesToMs(defaultFrames);
        return Finish(client, period, mix, periodFrames, string.Format(CultureInfo.InvariantCulture,
            "audio: output wasapi shared, engine period {0:F2} ms (driver min {1:F2}, default {2:F2}), {3}",
            period, mix.FramesToMs(minFrames), mix.FramesToMs(defaultFrames), formatNote));
    }

    private bool Finish(IAudioClient client, double periodMs, MixFormat mix, uint periodFrames, string line)
    {
        _client = client;
        _mix = mix;
        _periodMs = periodMs;

        int hr = client.GetBufferSize(out _bufferFrames);
        if (hr < 0 || _bufferFrames == 0)
        {
            _log($"audio: WASAPI GetBufferSize refused (0x{hr:X8})");
            return false;
        }

        _targetFrames = Math.Clamp(periodFrames * 2, 1, _bufferFrames);

        Guid renderIid = IID_IAudioRenderClient;
        hr = client.GetService(ref renderIid, out object render);
        if (hr < 0)
        {
            _log($"audio: WASAPI has no render client (0x{hr:X8})");
            return false;
        }
        _render = (IAudioRenderClient)render;

        Guid clockIid = IID_IAudioClock;
        if (client.GetService(ref clockIid, out object clock) >= 0 && clock is IAudioClock audioClock
            && audioClock.GetFrequency(out _clockFrequency) >= 0 && _clockFrequency > 0)
        {
            _clock = audioClock;
        }

        _event = new AutoResetEvent(false);
        hr = client.SetEventHandle(_event.SafeWaitHandle.DangerousGetHandle());
        if (hr < 0)
        {
            _log($"audio: WASAPI SetEventHandle refused (0x{hr:X8})");
            return false;
        }

        if (!_mix.IsOwn) _scratch = new byte[_bufferFrames * _mix.BytesPerFrame];

        lock (_lock)
        {
            Fill();
        }

        hr = client.Start();
        if (hr < 0)
        {
            _log($"audio: WASAPI Start refused (0x{hr:X8})");
            return false;
        }

        Description = string.Format(CultureInfo.InvariantCulture, "wasapi {0:F2} ms", periodMs);
        _log(line + string.Format(CultureInfo.InvariantCulture, ", buffer {0:F2} ms fed {1:F2} ms ahead",
            _mix.FramesToMs(_bufferFrames), _mix.FramesToMs(_targetFrames)));

        _feed = new Thread(FeedLoop) { IsBackground = true, Name = "BeepFeed", Priority = ThreadPriority.Highest };
        _feed.Start();
        return true;
    }

    private static object? Activate(IMMDevice device, Guid iid)
    {
        int hr = device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out object client);
        return hr >= 0 ? client : null;
    }

    private static IntPtr AllocOwnFormat()
    {
        var format = new WAVEFORMATEX
        {
            wFormatTag = WAVE_FORMAT_PCM,
            nChannels = BeepPlayer.NumChannels,
            nSamplesPerSec = BeepPlayer.SampleRate,
            nAvgBytesPerSec = BeepPlayer.SampleRate * BytesPerFrame,
            nBlockAlign = BytesPerFrame,
            wBitsPerSample = BeepPlayer.BytesPerSample * 8,
            cbSize = 0
        };
        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<WAVEFORMATEX>());
        Marshal.StructureToPtr(format, ptr, false);
        return ptr;
    }

    private void FeedLoop()
    {
        IntPtr task = IntPtr.Zero;
        try
        {
            uint taskIndex = 0;
            task = AvSetMmThreadCharacteristicsW("Pro Audio", ref taskIndex);
        }
        catch (Exception)
        {
        }

        try
        {
            double startedMs = Win32.GetTime();
            double baseClock = double.NaN, basePull = double.NaN, windowStartMs = double.NaN;

            while (!_stopping)
            {
                _event.WaitOne(1000);
                if (_stopping) break;

                lock (_lock)
                {
                    if (_stopping) break;
                    try
                    {
                        Fill();

                        if (_clock != null)
                        {
                            double now = Win32.GetTime();
                            if (double.IsNaN(baseClock))
                            {
                                if (now - startedMs >= ValidateSettleMs)
                                {
                                    (baseClock, basePull) = ClockAndPull();
                                    windowStartMs = now;
                                }
                            }
                            else if (now - windowStartMs >= ValidateMs && !Playing())
                            {
                                Judge(baseClock, basePull);
                                if (_needsReopen) break;
                                (baseClock, basePull) = ClockAndPull();
                                windowStartMs = now;
                            }
                        }
                    }
                    catch (COMException e)
                    {
                        Invalidate($"WASAPI stream lost (0x{e.HResult:X8}), the output will be reopened");
                        break;
                    }
                }
            }
        }
        finally
        {
            if (task != IntPtr.Zero)
            {
                try { AvRevertMmThreadCharacteristics(task); } catch (Exception) { }
            }
        }
    }

    private void Fill()
    {
        Check(_client.GetCurrentPadding(out uint padding));
        if (padding >= _targetFrames) return;
        uint frames = _targetFrames - padding;

        int copyFrames = _pcm == null ? 0 : _mix.FramesAvailable(_pcm, _sourceFrame, (int)frames);

        Check(_render.GetBuffer(frames, out IntPtr data));

        uint flags = 0;
        if (copyFrames <= 0)
        {
            flags = BufferFlagSilent;
        }
        else if (_mix.IsOwn)
        {
            Marshal.Copy(_pcm!, (int)_sourceFrame * BytesPerFrame, data, copyFrames * BytesPerFrame);
            int rest = ((int)frames - copyFrames) * BytesPerFrame;
            if (rest > 0) Zero(data + copyFrames * BytesPerFrame, rest);
        }
        else
        {
            _mix.Convert(_pcm!, _sourceFrame, copyFrames, _scratch!);
            int written = copyFrames * _mix.BytesPerFrame;
            Marshal.Copy(_scratch!, 0, data, written);
            int rest = ((int)frames - copyFrames) * _mix.BytesPerFrame;
            if (rest > 0) Zero(data + written, rest);
        }

        Check(_render.ReleaseBuffer(frames, flags));

        _totalFedFrames += frames;
        if (copyFrames > 0) _sourceFrame += copyFrames * _mix.SourceStep;
    }

    private (double clock, double pull) ClockAndPull()
    {
        double clock = double.NaN;
        if (_clock != null && _clock.GetPosition(out ulong position, out _) >= 0)
        {
            clock = position * (double)_mix.SampleRate / _clockFrequency;
        }
        Check(_client.GetCurrentPadding(out uint padding));
        return (clock, _totalFedFrames - padding);
    }

    private bool Playing() => _pcm != null && _mix.FramesAvailable(_pcm, _sourceFrame, 1) > 0;

    private void Judge(double baseClock, double basePull)
    {
        (double clock, double pull) = ClockAndPull();
        double clockFrames = clock - baseClock;
        double pullFrames = pull - basePull;
        if (double.IsNaN(clockFrames) || clockFrames <= 0) return;

        double periodFrames = _periodMs * _mix.SampleRate / 1000.0;
        double allowance = ValidateJitterPeriods * periodFrames + ValidateRate * clockFrames;
        if (Math.Abs(pullFrames - clockFrames) <= allowance) return;
        double ratio = pullFrames / clockFrames;
        double windowMs = clockFrames * 1000.0 / _mix.SampleRate;

        double next;
        string then;
        if (_periodMs < _defaultPeriodMs - 0.01)
        {
            next = Math.Min(_periodMs * 2.0, _defaultPeriodMs);
            then = string.Format(CultureInfo.InvariantCulture, "reopening at {0:F2} ms", next);
        }
        else
        {
            next = -1;
            then = "giving WASAPI up for this session";
        }

        SuggestedPeriodMs = next;
        Invalidate(string.Format(CultureInfo.InvariantCulture,
            "WASAPI period {0:F2} ms is unfit - the engine pulled {1:+0.00;-0.00}% ({2:+0.0;-0.0} ms) against the device clock over {3:F0} ms - {4}",
            _periodMs, (ratio - 1.0) * 100.0, (pullFrames - clockFrames) * 1000.0 / _mix.SampleRate, windowMs, then));
    }

    private static readonly byte[] SilenceBlock = new byte[16 * 1024];

    private static void Zero(IntPtr at, int bytes)
    {
        for (int done = 0; done < bytes; done += SilenceBlock.Length)
        {
            Marshal.Copy(SilenceBlock, 0, at + done, Math.Min(SilenceBlock.Length, bytes - done));
        }
    }

    private static void Check(int hr)
    {
        if (hr < 0) throw new COMException("WASAPI", hr);
    }

    private void Invalidate(string reason)
    {
        if (_needsReopen) return;
        _needsReopen = true;
        _log("audio: " + reason);
        try
        {
            DeviceChanged?.Invoke();
        }
        catch (Exception)
        {
        }
    }

    public bool Write(byte[] pcm)
    {
        lock (_lock)
        {
            if (!IsOpen) return false;

            _pcm = pcm;
            _sourceFrame = 0;
            _anchorFrames = _totalFedFrames;

            try
            {
                Fill();
            }
            catch (COMException e)
            {
                Invalidate($"WASAPI write failed (0x{e.HResult:X8}), the output will be reopened");
                return false;
            }

            return true;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _pcm = null;
            _sourceFrame = 0;
        }
    }

    public int PlayedBytes()
    {
        lock (_lock)
        {
            if (_pcm == null || !IsOpen) return -1;

            double playedFrames;
            if (_clock != null && _clock.GetPosition(out ulong position, out _) >= 0)
            {
                playedFrames = position * (double)_mix.SampleRate / _clockFrequency;
            }
            else if (_client.GetCurrentPadding(out uint padding) >= 0)
            {
                playedFrames = _totalFedFrames - padding;
            }
            else
            {
                return -1;
            }

            double relative = playedFrames - _anchorFrames;
            if (relative <= 0) return 0;
            long bytes = (long)(relative * _mix.SourceStep) * BytesPerFrame;
            return (int)Math.Min(bytes, _pcm.Length);
        }
    }

    public int CommittedBytes()
    {
        lock (_lock)
        {
            if (_pcm == null || !IsOpen) return -1;
            return (int)Math.Min(((long)_sourceFrame + 2) * BytesPerFrame, _pcm.Length);
        }
    }

    public void Silence(int fromByte)
    {
        lock (_lock)
        {
            if (_pcm == null) return;
            int from = Math.Clamp(fromByte, 0, _pcm.Length);
            Array.Clear(_pcm, from, _pcm.Length - from);
        }
    }

    public void Dispose()
    {
        _stopping = true;
        try { _event?.Set(); } catch (ObjectDisposedException) { }
        if (_feed != null && _feed != Thread.CurrentThread) _feed.Join(2000);
        _feed = null;

        lock (_lock)
        {
            if (_watch != null)
            {
                try
                {
                    _watch.Enumerator?.UnregisterEndpointNotificationCallback(_watch);
                    if (_watch.Enumerator != null) Marshal.FinalReleaseComObject(_watch.Enumerator);
                }
                catch (Exception) { }
                _watch = null;
            }

            try { _client?.Stop(); } catch (Exception) { }
            if (_clock != null) { Marshal.FinalReleaseComObject(_clock); _clock = null; }
            if (_render != null) { Marshal.FinalReleaseComObject(_render); _render = null!; }
            if (_client != null) { Marshal.FinalReleaseComObject(_client); _client = null!; }
            _event?.Dispose();
            _pcm = null;
        }
    }

    private sealed class DeviceWatch : IMMNotificationClient
    {
        private readonly WasapiOutput _owner;
        public IMMDeviceEnumerator? Enumerator;

        public DeviceWatch(WasapiOutput owner)
        {
            _owner = owner;
        }

        public void OnDeviceStateChanged(string deviceId, int newState) { }
        public void OnDeviceAdded(string deviceId) { }
        public void OnDeviceRemoved(string deviceId) { }

        public void OnDefaultDeviceChanged(int flow, int role, string? deviceId)
        {
            if (flow != DataFlowRender || role != RoleConsole) return;
            _owner.Invalidate("default device changed, the output will be reopened");
        }

        public void OnPropertyValueChanged(string deviceId, PROPERTYKEY key) { }
    }

    private readonly struct MixFormat
    {
        public readonly int SampleRate;
        public readonly int Channels;
        public readonly int BitsPerSample;
        public readonly bool IsFloat;
        public readonly bool IsOwn;

        public int BytesPerFrame => IsOwn ? WasapiOutput.BytesPerFrame : Channels * BitsPerSample / 8;

        public double SourceStep => BeepPlayer.SampleRate / (double)SampleRate;

        public double FramesToMs(uint frames) => frames * 1000.0 / SampleRate;

        public int FramesAvailable(byte[] pcm, double sourceFrame, int maxFrames)
        {
            int sourceFrames = pcm.Length / WasapiOutput.BytesPerFrame;
            if (SourceStep == 1.0)
            {
                return Math.Clamp(sourceFrames - (int)sourceFrame, 0, maxFrames);
            }

            double last = sourceFrames - 2;
            if (sourceFrame > last) return 0;
            return (int)Math.Min(Math.Floor((last - sourceFrame) / SourceStep) + 1, maxFrames);
        }

        public static readonly MixFormat Own = new(BeepPlayer.SampleRate, BeepPlayer.NumChannels, 16, false, true);

        private MixFormat(int sampleRate, int channels, int bits, bool isFloat, bool isOwn)
        {
            SampleRate = sampleRate;
            Channels = channels;
            BitsPerSample = bits;
            IsFloat = isFloat;
            IsOwn = isOwn;
        }

        public static bool TryParse(IntPtr format, out MixFormat mix)
        {
            mix = default;
            if (format == IntPtr.Zero) return false;

            var wf = Marshal.PtrToStructure<WAVEFORMATEX>(format);
            bool isFloat;
            int bits = wf.wBitsPerSample;
            if (wf.wFormatTag == WAVE_FORMAT_EXTENSIBLE && wf.cbSize >= 22)
            {
                var sub = Marshal.PtrToStructure<Guid>(format + 24);
                if (sub == SubFormatPcm) isFloat = false;
                else if (sub == SubFormatFloat) isFloat = true;
                else return false;
            }
            else if (wf.wFormatTag == WAVE_FORMAT_PCM) isFloat = false;
            else if (wf.wFormatTag == WAVE_FORMAT_IEEE_FLOAT) isFloat = true;
            else return false;

            if (isFloat && bits != 32) return false;
            if (!isFloat && bits != 16 && bits != 24 && bits != 32) return false;
            if (wf.nChannels < 1 || wf.nChannels > 16) return false;

            mix = new MixFormat((int)wf.nSamplesPerSec, wf.nChannels, bits, isFloat, false);
            return true;
        }

        public string Describe() => string.Format(CultureInfo.InvariantCulture, "{0} Hz {1}{2} {3}ch",
            SampleRate, IsFloat ? "float" : "pcm", BitsPerSample, Channels);

        public void Convert(byte[] pcm, double sourceFrame, int frames, byte[] dest)
        {
            int outFrame = BytesPerFrame;
            int outSample = BitsPerSample / 8;
            double step = SourceStep;
            for (int f = 0; f < frames; f++)
            {
                double at = sourceFrame + f * step;
                int whole = (int)at;
                int src = whole * WasapiOutput.BytesPerFrame;
                short left = (short)(pcm[src] | (pcm[src + 1] << 8));
                short right = (short)(pcm[src + 2] | (pcm[src + 3] << 8));
                if (step != 1.0)
                {
                    double t = at - whole;
                    int next = src + WasapiOutput.BytesPerFrame;
                    short left2 = (short)(pcm[next] | (pcm[next + 1] << 8));
                    short right2 = (short)(pcm[next + 2] | (pcm[next + 3] << 8));
                    left = (short)(left + (left2 - left) * t);
                    right = (short)(right + (right2 - right) * t);
                }
                int dst = f * outFrame;

                if (Channels == 1)
                {
                    WriteSample(dest, dst, (short)((left + right) / 2), outSample);
                    continue;
                }

                WriteSample(dest, dst, left, outSample);
                WriteSample(dest, dst + outSample, right, outSample);
                for (int c = 2; c < Channels; c++)
                {
                    Array.Clear(dest, dst + c * outSample, outSample);
                }
            }
        }

        private void WriteSample(byte[] dest, int at, short value, int outSample)
        {
            if (IsFloat)
            {
                float v = value / 32768f;
                BitConverter.TryWriteBytes(dest.AsSpan(at, 4), v);
                return;
            }

            switch (BitsPerSample)
            {
                case 16:
                    dest[at] = (byte)(value & 0xFF);
                    dest[at + 1] = (byte)((value >> 8) & 0xFF);
                    break;
                case 24:
                    dest[at] = 0;
                    dest[at + 1] = (byte)(value & 0xFF);
                    dest[at + 2] = (byte)((value >> 8) & 0xFF);
                    break;
                default:
                    dest[at] = 0;
                    dest[at + 1] = 0;
                    dest[at + 2] = (byte)(value & 0xFF);
                    dest[at + 3] = (byte)((value >> 8) & 0xFF);
                    break;
            }
        }
    }

    private const int DataFlowRender = 0;
    private const int RoleConsole = 0;
    private const uint CLSCTX_ALL = 0x17;
    private const int ShareModeShared = 0;

    private const uint EventFlag = 0x00040000;

    private const uint ConvertFlags = 0x80000000  | 0x08000000 ;

    private const uint BufferFlagSilent = 0x2;

    private const ushort WAVE_FORMAT_PCM = 1;
    private const ushort WAVE_FORMAT_IEEE_FLOAT = 3;
    private const ushort WAVE_FORMAT_EXTENSIBLE = 0xFFFE;

    private static readonly Guid SubFormatPcm = new("00000001-0000-0010-8000-00aa00389b71");
    private static readonly Guid SubFormatFloat = new("00000003-0000-0010-8000-00aa00389b71");

    private static readonly Guid IID_IAudioClient = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
    private static readonly Guid IID_IAudioClient3 = new("7ED4EE07-8E67-4CD4-8C1A-2B7A5987AD42");
    private static readonly Guid IID_IAudioRenderClient = new("F294ACFC-3146-4483-A7BF-ADDCA7C260E2");
    private static readonly Guid IID_IAudioClock = new("CD63314F-3FBA-4a1b-812C-EF96358728E7");

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

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        [PreserveSig] int OpenPropertyStore(int access, out IntPtr properties);
        [PreserveSig] int GetId(out IntPtr id);
        [PreserveSig] int GetState(out int state);
    }

    [ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMNotificationClient
    {
        void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int newState);
        void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
        void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
        void OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string? deviceId);
        void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, PROPERTYKEY key);
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

    [ComImport, Guid("7ED4EE07-8E67-4CD4-8C1A-2B7A5987AD42"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient3 : IAudioClient
    {
        [PreserveSig] new int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);
        [PreserveSig] new int GetBufferSize(out uint frames);
        [PreserveSig] new int GetStreamLatency(out long latency);
        [PreserveSig] new int GetCurrentPadding(out uint frames);
        [PreserveSig] new int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
        [PreserveSig] new int GetMixFormat(out IntPtr format);
        [PreserveSig] new int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] new int Start();
        [PreserveSig] new int Stop();
        [PreserveSig] new int Reset();
        [PreserveSig] new int SetEventHandle(IntPtr handle);
        [PreserveSig] new int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);

        [PreserveSig] int IsOffloadCapable(int category, out bool offloadCapable);
        [PreserveSig] int SetClientProperties(IntPtr properties);
        [PreserveSig] int GetBufferSizeLimits(IntPtr format, bool eventDriven, out long minBufferDuration, out long maxBufferDuration);

        [PreserveSig] int GetSharedModeEnginePeriod(IntPtr format, out uint defaultPeriodFrames, out uint fundamentalPeriodFrames, out uint minPeriodFrames, out uint maxPeriodFrames);
        [PreserveSig] int GetCurrentSharedModeEnginePeriod(out IntPtr format, out uint currentPeriodFrames);
        [PreserveSig] int InitializeSharedAudioStream(uint streamFlags, uint periodFrames, IntPtr format, IntPtr sessionGuid);
    }

    [ComImport, Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioRenderClient
    {
        [PreserveSig] int GetBuffer(uint frames, out IntPtr data);
        [PreserveSig] int ReleaseBuffer(uint frames, uint flags);
    }

    [ComImport, Guid("CD63314F-3FBA-4a1b-812C-EF96358728E7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClock
    {
        [PreserveSig] int GetFrequency(out ulong frequency);
        [PreserveSig] int GetPosition(out ulong position, out ulong qpcPosition);
        [PreserveSig] int GetCharacteristics(out uint characteristics);
    }

    [DllImport("avrt.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr AvSetMmThreadCharacteristicsW(string taskName, ref uint taskIndex);

    [DllImport("avrt.dll")]
    private static extern bool AvRevertMmThreadCharacteristics(IntPtr handle);
}
