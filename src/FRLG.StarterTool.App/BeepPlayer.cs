using System.Runtime.InteropServices;

namespace FRLG.StarterTool.App;

public sealed class BeepPlayer : IDisposable
{
    public const int SampleRate = 48000;
    public const int NumChannels = 2;
    public const int BytesPerSample = 2;

    private const int BytesPerFrame = NumChannels * BytesPerSample;

    private IntPtr _waveOut;
    private IntPtr _buffer;
    private IntPtr _header;
    private bool _prepared;
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

    public BeepPlayer()
    {
        RenderBeep();

        var format = new WAVEFORMATEX
        {
            wFormatTag = WAVE_FORMAT_PCM,
            nChannels = NumChannels,
            nSamplesPerSec = SampleRate,
            nAvgBytesPerSec = SampleRate * BytesPerFrame,
            nBlockAlign = BytesPerFrame,
            wBitsPerSample = BytesPerSample * 8,
            cbSize = 0
        };

        int result = waveOutOpen(out _waveOut, WAVE_MAPPER, ref format, IntPtr.Zero, IntPtr.Zero, CALLBACK_NULL);
        if (result != MMSYSERR_NOERROR)
        {
            _waveOut = IntPtr.Zero;
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

    public bool IsAvailable => _waveOut != IntPtr.Zero;

    public void QueueBeeps(IReadOnlyList<double> offsetsMs, int protectedCount = 0)
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
        double maxOffset = offsetsMs.Max();
        int length = (int)Math.Ceiling(maxOffset / 1000.0 * SampleRate) * BytesPerFrame + _beep.Length;
        var pcm = new byte[length];

        _lastBeepStartMs = Win32.GetTime() + maxOffset;

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

            if (_waveOut == IntPtr.Zero) return;

            waveOutReset(_waveOut);
            ReleaseBuffer();

            _buffer = Marshal.AllocHGlobal(pcm.Length);
            Marshal.Copy(pcm, 0, _buffer, pcm.Length);

            var hdr = new WAVEHDR
            {
                lpData = _buffer,
                dwBufferLength = (uint)pcm.Length
            };

            _header = Marshal.AllocHGlobal(Marshal.SizeOf<WAVEHDR>());
            Marshal.StructureToPtr(hdr, _header, false);

            if (waveOutPrepareHeader(_waveOut, _header, (uint)Marshal.SizeOf<WAVEHDR>()) != MMSYSERR_NOERROR)
            {
                ReleaseBuffer();
                return;
            }

            _prepared = true;
            if (waveOutWrite(_waveOut, _header, (uint)Marshal.SizeOf<WAVEHDR>()) != MMSYSERR_NOERROR)
            {
                ReleaseBuffer();
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            CancelDeferredWrite();
            _lastBeepStartMs = double.MinValue;
            _beepStarts.Clear();
            _protectedStarts.Clear();
            _bufferLength = 0;
            if (_waveOut == IntPtr.Zero) return;
            waveOutReset(_waveOut);
            ReleaseBuffer();
        }
    }

    public void ClearPending()
    {
        lock (_lock)
        {
            CancelDeferredWrite();

            if (MutePending()) return;

            if (Win32.GetTime() >= _lastBeepStartMs) return;

            Clear();
        }
    }

    private bool MutePending()
    {
        if (_buffer == IntPtr.Zero || !_prepared || _bufferLength <= 0) return false;

        int position = PositionBytes();
        if (position < 0) return false;

        int from = position + GuardBytes;
        foreach (int start in _beepStarts)
        {
            if (start > from) break;
            from = Math.Max(from, start + _beepBytes);
        }

        for (int at = Math.Min(from, _bufferLength); at < _bufferLength; at += Silence.Length)
        {
            Marshal.Copy(Silence, 0, _buffer + at, Math.Min(Silence.Length, _bufferLength - at));
        }

        _beepStarts.RemoveAll(start => start >= from);
        _protectedStarts.RemoveAll(start => start >= from);
        _lastBeepStartMs = double.MinValue;
        return true;
    }

    private const int GuardBytes = SampleRate / 20 * BytesPerFrame;

    private static readonly byte[] Silence = new byte[64 * 1024];

    private const double WriteMarginMs = 10.0;

    private static double BytesToMs(int bytes) => bytes / (double)BytesPerFrame / SampleRate * 1000.0;

    private int ProtectedRemainderBytes()
    {
        if (_protectedStarts.Count == 0 || !_prepared) return -1;

        int position = PositionBytes();
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

    private int PositionBytes()
    {
        if (_waveOut == IntPtr.Zero) return -1;

        var time = new MMTIME { wType = TIME_BYTES };
        if (waveOutGetPosition(_waveOut, ref time, (uint)Marshal.SizeOf<MMTIME>()) != MMSYSERR_NOERROR) return -1;
        if (time.wType != TIME_BYTES) return -1;

        int position = (int)time.u;
        return position - position % BytesPerFrame;
    }

    public void Preview() => QueueBeeps(new[] { 0.0 });

    private void ReleaseBuffer()
    {
        if (_prepared)
        {
            waveOutUnprepareHeader(_waveOut, _header, (uint)Marshal.SizeOf<WAVEHDR>());
            _prepared = false;
        }

        if (_header != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_header);
            _header = IntPtr.Zero;
        }

        if (_buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = IntPtr.Zero;
        }
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

            if (_waveOut == IntPtr.Zero) return;
            waveOutReset(_waveOut);
            ReleaseBuffer();
            waveOutClose(_waveOut);
            _waveOut = IntPtr.Zero;
        }
    }

    #region winmm interop

    private const int MMSYSERR_NOERROR = 0;
    private const int WAVE_MAPPER = -1;
    private const ushort WAVE_FORMAT_PCM = 1;
    private const uint CALLBACK_NULL = 0;
    private const uint TIME_BYTES = 4;

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
    private struct WAVEHDR
    {
        public IntPtr lpData;
        public uint dwBufferLength;
        public uint dwBytesRecorded;
        public IntPtr dwUser;
        public uint dwFlags;
        public uint dwLoops;
        public IntPtr lpNext;
        public IntPtr reserved;
    }

    [DllImport("winmm.dll")]
    private static extern int waveOutOpen(out IntPtr hWaveOut, int uDeviceID, ref WAVEFORMATEX lpFormat, IntPtr dwCallback, IntPtr dwInstance, uint dwFlags);

    [DllImport("winmm.dll")]
    private static extern int waveOutPrepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, uint uSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutUnprepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, uint uSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutWrite(IntPtr hWaveOut, IntPtr lpWaveOutHdr, uint uSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct MMTIME
    {
        public uint wType;
        public uint u;
        public uint uHigh;
    }

    [DllImport("winmm.dll")]
    private static extern int waveOutReset(IntPtr hWaveOut);

    [DllImport("winmm.dll")]
    private static extern int waveOutGetPosition(IntPtr hWaveOut, ref MMTIME lpInfo, uint uSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutClose(IntPtr hWaveOut);

    #endregion
}
