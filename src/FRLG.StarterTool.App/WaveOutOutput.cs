using System.Runtime.InteropServices;

namespace FRLG.StarterTool.App;

internal sealed class WaveOutOutput : IBeepOutput
{
    private const int BytesPerFrame = BeepPlayer.NumChannels * BeepPlayer.BytesPerSample;

    private IntPtr _waveOut;
    private IntPtr _buffer;
    private IntPtr _header;
    private bool _prepared;
    private int _bufferLength;
    private readonly object _lock = new();

    private const int GuardBytes = BeepPlayer.SampleRate / 20 * BytesPerFrame;

    private static readonly byte[] SilenceBlock = new byte[64 * 1024];

    private WaveOutOutput(IntPtr handle)
    {
        _waveOut = handle;
    }

    public static WaveOutOutput? Open(Action<string> log)
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

        int result = waveOutOpen(out IntPtr handle, WAVE_MAPPER, ref format, IntPtr.Zero, IntPtr.Zero, CALLBACK_NULL);
        if (result != MMSYSERR_NOERROR)
        {
            log($"audio: waveOut open failed, MMRESULT {result} ({BeepPlayer.SampleRate} Hz 16-bit {BeepPlayer.NumChannels}ch)");
            return null;
        }

        log("audio: output waveout, engine period 10 ms");
        return new WaveOutOutput(handle);
    }

    public bool IsOpen => _waveOut != IntPtr.Zero;

    public string Description => "waveout";

    public bool NeedsReopen => false;

    public event Action? DeviceChanged
    {
        add { }
        remove { }
    }

    public bool Write(byte[] pcm)
    {
        lock (_lock)
        {
            if (_waveOut == IntPtr.Zero) return false;

            waveOutReset(_waveOut);
            ReleaseBuffer();

            _buffer = Marshal.AllocHGlobal(pcm.Length);
            Marshal.Copy(pcm, 0, _buffer, pcm.Length);
            _bufferLength = pcm.Length;

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
                return false;
            }

            _prepared = true;
            if (waveOutWrite(_waveOut, _header, (uint)Marshal.SizeOf<WAVEHDR>()) != MMSYSERR_NOERROR)
            {
                ReleaseBuffer();
                return false;
            }

            return true;
        }
    }

    public double StartDelayMs() => double.NaN;

    public void Stop()
    {
        lock (_lock)
        {
            if (_waveOut == IntPtr.Zero) return;
            waveOutReset(_waveOut);
            ReleaseBuffer();
        }
    }

    public int PlayedBytes()
    {
        lock (_lock)
        {
            if (_waveOut == IntPtr.Zero || !_prepared || _bufferLength <= 0) return -1;

            var time = new MMTIME { wType = TIME_BYTES };
            if (waveOutGetPosition(_waveOut, ref time, (uint)Marshal.SizeOf<MMTIME>()) != MMSYSERR_NOERROR) return -1;
            if (time.wType != TIME_BYTES) return -1;

            int position = (int)time.u;
            return position - position % BytesPerFrame;
        }
    }

    public int CommittedBytes()
    {
        int played = PlayedBytes();
        return played < 0 ? -1 : played + GuardBytes;
    }

    public void Silence(int fromByte)
    {
        lock (_lock)
        {
            if (_buffer == IntPtr.Zero || !_prepared) return;
            for (int at = Math.Clamp(fromByte, 0, _bufferLength); at < _bufferLength; at += SilenceBlock.Length)
            {
                Marshal.Copy(SilenceBlock, 0, _buffer + at, Math.Min(SilenceBlock.Length, _bufferLength - at));
            }
        }
    }

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

        _bufferLength = 0;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct MMTIME
    {
        public uint wType;
        public uint u;
        public uint uHigh;
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
    private static extern int waveOutGetPosition(IntPtr hWaveOut, ref MMTIME lpInfo, uint uSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutClose(IntPtr hWaveOut);

    #endregion
}
