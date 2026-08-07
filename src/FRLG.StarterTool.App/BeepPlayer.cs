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

    public void QueueBeeps(IReadOnlyList<double> offsetsMs)
    {
        if (offsetsMs.Count == 0)
        {
            Clear();
            return;
        }

        double maxOffset = offsetsMs.Max();
        int length = (int)Math.Ceiling(maxOffset / 1000.0 * SampleRate) * BytesPerFrame + _beep.Length;
        var pcm = new byte[length];

        _lastBeepStartMs = Win32.GetTime() + maxOffset;

        foreach (double offset in offsetsMs)
        {
            int destOffset = (int)(offset / 1000.0 * SampleRate) * BytesPerFrame;
            if (destOffset < 0 || destOffset + _beep.Length > pcm.Length) continue;
            Array.Copy(_beep, 0, pcm, destOffset, _beep.Length);
        }

        Queue(pcm);
    }

    private void Queue(byte[] pcm)
    {
        lock (_lock)
        {
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
            _lastBeepStartMs = double.MinValue;
            if (_waveOut == IntPtr.Zero) return;
            waveOutReset(_waveOut);
            ReleaseBuffer();
        }
    }

    public void ClearPending()
    {
        lock (_lock)
        {
            if (Win32.GetTime() >= _lastBeepStartMs) return;

            Clear();
        }
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

    [DllImport("winmm.dll")]
    private static extern int waveOutReset(IntPtr hWaveOut);

    [DllImport("winmm.dll")]
    private static extern int waveOutClose(IntPtr hWaveOut);

    #endregion
}
