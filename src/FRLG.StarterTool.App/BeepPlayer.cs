using System.Runtime.InteropServices;
using FRLG.StarterTool.Core.Audio;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public sealed class BeepPlayer : IDisposable
{
    public const int SampleRate = 48000;
    public const int NumChannels = 2;
    public const int BytesPerSample = 2;

    private const int BytesPerFrame = NumChannels * BytesPerSample;

    private IBeepOutput? _output;
    private AudioOutput _preferred = AudioOutput.Wasapi;
    private double _periodMs;
    private AudioScheduling _scheduling = AudioScheduling.DeviceClock;
    private ScheduledBeep[] _scheduledBeeps = Array.Empty<ScheduledBeep>();

    private bool UsesDeviceClock => _scheduling == AudioScheduling.DeviceClock && _output is WasapiOutput;
    internal WasapiOutput? Wasapi => _output as WasapiOutput;
    internal short[] Clip => MemoryMarshal.Cast<byte, short>(_beep).ToArray();
    internal double ClipDurationMs => BytesToMs(_beep.Length);
    private bool _reopenPending;

    private double _periodOverrideMs;
    private readonly Action<string> _log;
    private readonly object _lock = new();

    private byte[] _beep = Array.Empty<byte>();

    private double _lastBeepStartMs = double.MinValue;

    private readonly List<int> _beepStarts = new();
    private int _beepBytes;
    private int _bufferLength;

    private readonly List<int> _protectedStarts = new();

    private System.Threading.Timer? _writeTimer;
    private double[]? _pendingOffsetsMs;
    private int _pendingProtectedCount;
    private double _pendingAtMs;
    private int _writeGeneration;
    private int _pendingGeneration;

    private int _volume = 100;
    private string _sound = BeepSounds.Default;
    private short[]? _samples;

    private double _writtenAtMs = double.NaN;

    public double LastWriteLagMs { get; private set; } = double.NaN;

    public string OutputDescription
    {
        get
        {
            lock (_lock)
            {
                return _output is { IsOpen: true }
                    ? $"{_output.Description}, {(UsesDeviceClock ? "DeviceClock" : "Legacy")}" : "none";
            }
        }
    }

    public double? StartLatencyMs()
    {
        lock (_lock)
        {
            if (UsesDeviceClock) return ((WasapiOutput)_output!).ScheduleStartLatencyMs;
            if (_output == null || _bufferLength <= 0 || double.IsNaN(_writtenAtMs)) return null;

            int position = _output.PlayedBytes();
            if (position <= 0 || position >= _bufferLength) return null;

            return Win32.GetTime() - _writtenAtMs - BytesToMs(position);
        }
    }

    public BeepPlayer(Action<string> log)
    {
        _log = log;
        RenderBeep();
    }

    public void Configure(AudioOutput output, double periodMs, AudioScheduling scheduling = AudioScheduling.DeviceClock)
    {
        lock (_lock)
        {
            bool changed = output != _preferred || periodMs != _periodMs || scheduling != _scheduling;
            _scheduling = scheduling;
            _preferred = output;
            _periodMs = periodMs;
            if (changed) _periodOverrideMs = 0;
            if (_output == null || changed) ScheduleReopen();
        }
    }

    public int Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 100);
            RenderBeep();
        }
    }

    public string Sound
    {
        get => _sound;
        set
        {
            _sound = BeepSounds.IsKnown(value) ? value : BeepSounds.Default;
            _samples = null;
            RenderBeep();
        }
    }

    public bool IsAvailable
    {
        get
        {
            lock (_lock)
            {
                return _output is { IsOpen: true };
            }
        }
    }

    public void QueueBeeps(double callerReadMs, IReadOnlyList<double> offsetsMs, int protectedCount = 0)
    {
        lock (_lock)
        {
            if (_scheduling == AudioScheduling.DeviceClock) EnsureOpen();
            if (!UsesDeviceClock)
            {
                QueueLegacy(offsetsMs, protectedCount);
                return;
            }

            CancelDeferredWrite();
            short[] clip = Clip;
            _scheduledBeeps = offsetsMs.Select((offset, index) =>
                new ScheduledBeep(callerReadMs + offset, clip, index < protectedCount)).ToArray();
            _writtenAtMs = Win32.GetTime();
            ((WasapiOutput)_output!).ScheduleBeeps(_scheduledBeeps, _writtenAtMs);
            LastWriteLagMs = _writtenAtMs - callerReadMs;
        }
    }

    private void QueueLegacy(IReadOnlyList<double> offsetsMs, int protectedCount = 0)
    {
        lock (_lock)
        {
            CancelDeferredWrite();

            if (offsetsMs.Count == 0)
            {
                ClearPending();
                return;
            }

            int remainderBytes = ProtectedRemainderBytes();
            if (remainderBytes > 0 && MutePending())
            {
                DeferWrite(offsetsMs, protectedCount, BytesToMs(remainderBytes) + WriteMarginMs);
                return;
            }

            WriteSchedule(offsetsMs, protectedCount);
        }
    }

    private void WriteSchedule(IReadOnlyList<double> offsetsMs, int protectedCount)
    {
        EnsureOpen();

        double maxOffset = offsetsMs.Max();
        int length = (int)Math.Ceiling(maxOffset / 1000.0 * SampleRate) * BytesPerFrame + _beep.Length;
        var pcm = new byte[length];

        double baseMs = Win32.GetTime();
        _lastBeepStartMs = baseMs + maxOffset;

        var starts = new List<int>(offsetsMs.Count);
        var protectedStarts = new List<int>(protectedCount);
        for (int i = 0; i < offsetsMs.Count; i++)
        {
            int destOffset = (int)(offsetsMs[i] / 1000.0 * SampleRate) * BytesPerFrame;
            if (destOffset < 0 || destOffset + _beep.Length > pcm.Length) continue;
            Array.Copy(_beep, 0, pcm, destOffset, _beep.Length);
            starts.Add(destOffset);
            if (i < protectedCount) protectedStarts.Add(destOffset);
        }

        Queue(pcm, starts, protectedStarts);

        LastWriteLagMs = Win32.GetTime() - baseMs;
    }

    private void Queue(byte[] pcm, List<int> starts, List<int> protectedStarts)
    {
        lock (_lock)
        {
            _beepStarts.Clear();
            _beepStarts.AddRange(starts);
            _beepStarts.Sort();
            _protectedStarts.Clear();
            _protectedStarts.AddRange(protectedStarts);
            _beepBytes = _beep.Length;
            _bufferLength = pcm.Length;

            if (_output == null || !_output.Write(pcm))
            {
                _writtenAtMs = double.NaN;
                return;
            }

            _writtenAtMs = Win32.GetTime();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            CancelDeferredWrite();
            _scheduledBeeps = Array.Empty<ScheduledBeep>();
            _lastBeepStartMs = double.MinValue;
            _beepStarts.Clear();
            _protectedStarts.Clear();
            _bufferLength = 0;
            _output?.Stop();
        }
    }

    public void ClearPending()
    {
        lock (_lock)
        {
            CancelDeferredWrite();

            if (UsesDeviceClock)
            {
                _scheduledBeeps = Array.Empty<ScheduledBeep>();
                ((WasapiOutput)_output!).ScheduleBeeps(_scheduledBeeps, Win32.GetTime());
                return;
            }

            if (MutePending()) return;

            if (Win32.GetTime() >= _lastBeepStartMs) return;

            Clear();
        }
    }

    private bool MutePending()
    {
        if (_output == null || _bufferLength <= 0) return false;

        int committed = _output.CommittedBytes();
        if (committed < 0) return false;

        int from = committed;
        foreach (int start in _beepStarts)
        {
            if (start > from) break;
            from = Math.Max(from, start + _beepBytes);
        }

        _output.Silence(Math.Min(from, _bufferLength));

        _beepStarts.RemoveAll(start => start >= from);
        _protectedStarts.RemoveAll(start => start >= from);
        _lastBeepStartMs = double.MinValue;
        return true;
    }

    private const double WriteMarginMs = 10.0;

    private static double BytesToMs(int bytes) => bytes / (double)BytesPerFrame / SampleRate * 1000.0;

    private int ProtectedRemainderBytes()
    {
        if (_protectedStarts.Count == 0 || _output == null) return -1;

        int position = _output.PlayedBytes();
        if (position < 0) return -1;

        foreach (int start in _protectedStarts)
        {
            if (position >= start && position < start + _beepBytes) return start + _beepBytes - position;
        }

        return -1;
    }

    private void DeferWrite(IReadOnlyList<double> offsetsMs, int protectedCount, double delayMs)
    {
        _pendingOffsetsMs = offsetsMs.ToArray();
        _pendingProtectedCount = protectedCount;
        _pendingAtMs = Win32.GetTime();
        _lastBeepStartMs = _pendingAtMs + _pendingOffsetsMs.Max();

        _pendingGeneration = ++_writeGeneration;
        _writeTimer ??= new System.Threading.Timer(WritePending, null, Timeout.Infinite, Timeout.Infinite);
        _writeTimer.Change((int)Math.Max(1.0, Math.Ceiling(delayMs)), Timeout.Infinite);
    }

    private void WritePending(object? state)
    {
        lock (_lock)
        {
            if (_pendingOffsetsMs is not { } offsets || _pendingGeneration != _writeGeneration) return;

            double elapsedMs = Win32.GetTime() - _pendingAtMs;

            var shifted = new List<double>(offsets.Length);
            int protectedCount = 0;
            for (int i = 0; i < offsets.Length; i++)
            {
                double offset = offsets[i] - elapsedMs;
                if (offset < 0.0) continue;
                shifted.Add(offset);
                if (i < _pendingProtectedCount) protectedCount++;
            }

            _pendingOffsetsMs = null;

            if (shifted.Count == 0) return;

            WriteSchedule(shifted, protectedCount);
        }
    }

    private void CancelDeferredWrite()
    {
        _writeGeneration++;
        _pendingOffsetsMs = null;
        _writeTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void Preview() => QueueBeeps(Win32.GetTime(), new[] { 0.0 });

    private void EnsureOpen()
    {
        if (_output is { IsOpen: true, NeedsReopen: false } && !_reopenPending) return;
        Reopen();
    }

    private void Reopen()
    {
        _reopenPending = false;

        double reopenMs = _scheduling == AudioScheduling.DeviceClock ? Win32.GetTime() : 0;
        ScheduledBeep[] future = _scheduling == AudioScheduling.DeviceClock
            ? _scheduledBeeps.Where(beep => beep.TargetMs > reopenMs).ToArray()
            : Array.Empty<ScheduledBeep>();
        IBeepOutput? old = _output;
        _output = null;
        if (old != null)
        {
            old.DeviceChanged -= OnDeviceChanged;
            if (old is WasapiOutput { SuggestedPeriodMs: not 0 } judged) _periodOverrideMs = judged.SuggestedPeriodMs;
            else _periodOverrideMs = 0;
            old.Dispose();
        }

        _beepStarts.Clear();
        _protectedStarts.Clear();
        _bufferLength = 0;
        _lastBeepStartMs = double.MinValue;

        IBeepOutput? output = null;
        bool wasapi = _preferred == AudioOutput.Wasapi && _periodOverrideMs >= 0;
        if (wasapi) output = WasapiOutput.Open(_periodOverrideMs > 0 ? _periodOverrideMs : _periodMs, _log, _scheduling == AudioScheduling.DeviceClock);
        if (output == null)
        {
            if (wasapi) _log("audio: falling back to waveOut");
            output = WaveOutOutput.Open(_log);
        }

        if (output == null)
        {
            _log("audio: no output could be opened; the countdown will be silent until the next write");
            return;
        }

        output.DeviceChanged += OnDeviceChanged;
        _output = output;
        _scheduledBeeps = future;
        if (future.Length > 0)
        {
            double now = Win32.GetTime();
            if (UsesDeviceClock) ((WasapiOutput)output).ScheduleBeeps(future, now, false);
            else WriteSchedule(future.Select(beep => Math.Max(0, beep.TargetMs - now)).ToArray(), 0);
        }
    }

    private void OnDeviceChanged()
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            lock (_lock)
            {
                _reopenPending = true;
                if (_output == null) return;

                bool idle = _pendingOffsetsMs == null
                    && (_bufferLength <= 0 || Win32.GetTime() >= _lastBeepStartMs + BytesToMs(_beepBytes));
                if (UsesDeviceClock || idle) Reopen();
            }
        });
    }

    private void ScheduleReopen()
    {
        _reopenPending = true;
        bool idle = _pendingOffsetsMs == null
            && (_bufferLength <= 0 || Win32.GetTime() >= _lastBeepStartMs + BytesToMs(_beepBytes));
        if (_scheduling == AudioScheduling.DeviceClock || idle) Reopen();
    }

    private void RenderBeep()
    {
        short[] samples = _samples ??= _sound == BeepSounds.Tone
            ? SynthesiseTone()
            : BeepSounds.LoadPcm(_sound, SampleRate, NumChannels) ?? SynthesiseTone();

        double scale = _volume / 100.0;
        var beep = new byte[samples.Length * BytesPerSample];
        for (int i = 0; i < samples.Length; i++)
        {
            var sample = (short)Math.Clamp(samples[i] * scale, short.MinValue, short.MaxValue);
            beep[i * 2] = (byte)(sample & 0xFF);
            beep[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        _beep = beep;
    }

    private static short[] SynthesiseTone()
    {
        const double frequency = 1000.0;
        const double durationSeconds = 0.045;
        const int fadeSamples = 128;

        int frames = (int)(SampleRate * durationSeconds);
        var samples = new short[frames * NumChannels];
        const double amplitude = short.MaxValue * 0.8;

        for (int frame = 0; frame < frames; frame++)
        {
            double envelope = 1.0;
            if (frame < fadeSamples) envelope = frame / (double)fadeSamples;
            else if (frame >= frames - fadeSamples) envelope = (frames - 1 - frame) / (double)fadeSamples;

            var value = (short)(Math.Sin(2.0 * Math.PI * frequency * frame / SampleRate) * amplitude * envelope);
            for (int channel = 0; channel < NumChannels; channel++)
            {
                samples[frame * NumChannels + channel] = value;
            }
        }

        return samples;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            CancelDeferredWrite();
            _writeTimer?.Dispose();
            _writeTimer = null;

            if (_output == null) return;
            _output.DeviceChanged -= OnDeviceChanged;
            _output.Dispose();
            _output = null;
        }
    }
}
