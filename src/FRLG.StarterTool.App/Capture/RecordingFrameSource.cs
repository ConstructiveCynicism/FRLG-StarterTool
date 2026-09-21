using FRLG.StarterTool.Core.Video;

namespace FRLG.StarterTool.App.Capture;

internal sealed class RecordingFrameSource : IFrameSource
{
    public const string Extension = "*.mkv";

    public const string InactiveError = "Recording Inactive";

    private const long StaleAfterMs = 12000;

    private const double BehindMs = 20000.0;

    private readonly string _folder;
    private readonly object _lock = new();
    private System.Threading.Timer? _watch;
    private MkvTail? _tail;
    private double _startMs;
    private double _offsetMs;
    private bool _aligned;
    private byte[] _bgra = Array.Empty<byte>();
    private long _lastKeyframeTicks;
    private volatile bool _scanning;
    private bool _disposed;

    public RecordingFrameSource(string folder)
    {
        _folder = folder;
    }

    public string DisplayName
    {
        get
        {
            MkvTail? tail = _tail;
            return tail == null ? _folder : System.IO.Path.GetFileName(tail.Path);
        }
    }

    public string? Error { get; private set; } = "Not started";

    public event Action<double, IRawFrame>? FrameArrived;

    public bool Active { get; set; }

    public double? FramesPerSecond => _tail?.FramesPerSecond;

    public bool Aligned
    {
        get { lock (_lock) return _aligned; }
    }

    public string FileName => DisplayName;

    public void Start()
    {
        _watch = new System.Threading.Timer(_ => Watch(), null, 0, 2000);
    }

    private void Watch()
    {
        try
        {
            string? newest = null;
            DateTime newestUtc = DateTime.MinValue;
            if (Directory.Exists(_folder))
            {
                foreach (string path in Directory.EnumerateFiles(_folder, Extension))
                {
                    DateTime created = File.GetCreationTimeUtc(path);
                    if (created <= newestUtc) continue;
                    newestUtc = created;
                    newest = path;
                }
            }

            lock (_lock)
            {
                if (_disposed) return;
                if (newest == null)
                {
                    Error = Directory.Exists(_folder) ? "No .mkv recording in the folder" : "Recording folder not found";
                    return;
                }
                if (_tail != null && string.Equals(_tail.Path, newest, StringComparison.OrdinalIgnoreCase))
                {
                    string? error = _tail.Error ?? (InactiveLocked() ? InactiveError : null);
                    if (error == InactiveError && Error != InactiveError)
                    {
                        ContextSession.Log("capture: recording \"" + System.IO.Path.GetFileName(newest) + "\" is not being written");
                    }
                    Error = error;
                    return;
                }
                if (_scanning) return;

                _tail?.Dispose();
                _startMs = Win32.GetTime() - (DateTime.UtcNow - newestUtc).TotalMilliseconds;
                _offsetMs = 0;
                _aligned = false;
                _tail = new MkvTail(newest);
                _tail.KeyframeRead += OnKeyframe;
                _tail.Start();
                Error = null;
            }
            ContextSession.Log("capture: following recording \"" + System.IO.Path.GetFileName(newest) + "\"");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Error = "Cannot read the recording folder";
        }
    }

    private bool InactiveLocked()
    {
        MkvTail tail = _tail!;
        long grew = tail.LastGrowthTicks;
        if (grew == 0) return false;
        if (Environment.TickCount64 - grew > StaleAfterMs) return true;
        double last = tail.LastPtsMs;
        return !double.IsNaN(last) && Win32.GetTime() - (_startMs + _offsetMs + last) > BehindMs;
    }

    public void Align(double byMs)
    {
        lock (_lock)
        {
            _offsetMs += byMs;
            _aligned = true;
        }
    }

    public bool Holds(double stampMs)
    {
        MkvTail? tail = _tail;
        if (tail == null) return false;
        double last = tail.LastPtsMs;
        lock (_lock) return !double.IsNaN(last) && _startMs + _offsetMs + last >= stampMs;
    }

    public int Scan(double fromMs, double toMs, Func<bool> more, out double lastMs)
    {
        lastMs = double.NaN;
        double last = double.NaN;
        MkvTail? tail;
        double baseMs;
        lock (_lock)
        {
            tail = _tail;
            baseMs = _startMs + _offsetMs;
            if (tail == null || _disposed) return 0;
            _scanning = true;
        }
        try
        {
            int delivered = tail.Decode(fromMs - baseMs, toMs - baseMs, (ptsMs, picture) =>
            {
                last = baseMs + ptsMs;
                FrameArrived?.Invoke(last, new RawFrame(this, picture));
                return more();
            });
            lastMs = last;
            return delivered;
        }
        finally
        {
            _scanning = false;
        }
    }

    private void OnKeyframe(double ptsMs)
    {
        if (!Active || _scanning) return;
        long now = Environment.TickCount64;
        if (now - _lastKeyframeTicks < 500) return;
        _lastKeyframeTicks = now;

        MkvTail? tail = _tail;
        double baseMs;
        lock (_lock) baseMs = _startMs + _offsetMs;
        tail?.Decode(ptsMs, ptsMs, (at, picture) =>
        {
            FrameArrived?.Invoke(baseMs + at, new RawFrame(this, picture));
            return false;
        });
    }

    private sealed class RawFrame : IRawFrame
    {
        private readonly RecordingFrameSource _source;
        private readonly MkvTail.Picture _picture;
        private bool _converted;

        public RawFrame(RecordingFrameSource source, MkvTail.Picture picture)
        {
            _source = source;
            _picture = picture;
        }

        public int Width => _picture.Width;

        public int Height => _picture.Height;

        public bool CopyRegion(Rectangle region, bool gamePixels, Func<int, byte[]> rent, Action<byte[], Size> done)
        {
            Rectangle clip = WindowFrameSource.Clip(region, Width, Height);
            if (clip.IsEmpty) return false;

            int stride = Width * 4;
            if (!_converted)
            {
                if (_source._bgra.Length != stride * Height) _source._bgra = new byte[stride * Height];
                _picture.ToBgra(_source._bgra);
                _converted = true;
            }
            byte[] whole = _source._bgra;

            byte[] bytes;
            Size size;
            if (gamePixels)
            {
                bytes = rent(GamePixels.Bytes);
                GamePixels.Sample(whole, stride, clip.X, clip.Y, clip.Width, clip.Height, bytes);
                size = new Size(GamePixels.Width, GamePixels.Height);
            }
            else
            {
                bytes = rent(clip.Width * clip.Height * 4);
                for (int y = 0; y < clip.Height; y++)
                {
                    Buffer.BlockCopy(whole, (clip.Y + y) * stride + clip.X * 4, bytes, y * clip.Width * 4, clip.Width * 4);
                }
                size = clip.Size;
            }
            done(bytes, size);
            return true;
        }
    }

    public void Dispose()
    {
        MkvTail? tail;
        lock (_lock)
        {
            _disposed = true;
            tail = _tail;
            _tail = null;
        }
        _watch?.Dispose();
        tail?.Dispose();
    }
}
