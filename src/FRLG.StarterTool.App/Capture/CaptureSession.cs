using System.Globalization;
using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Video;

namespace FRLG.StarterTool.App.Capture;

internal sealed record CaptureResult(
    IReadOnlyList<CapturedFrame> Frames, int TargetIndex, double PressMs, double DueMs, double FrameMs, int DelayMs,
    bool Calibration, bool Pressed, string RouteName, string Note)
{
    public Action? ReleaseFrames { get; init; }
}

internal sealed class CaptureSession : IDisposable
{
    private const double MarginMs = 500.0;

    private const double PressMarginMs = 250.0;

    private const double CalibrationSpanMs = 1000.0;

    private const double CalibrationSkipMs = 1000.0;

    private const double CalibrationTimeoutMs = 15000.0;

    private double MinIntervalMs => _frameMs / 2.0 - 1.0;

    private const double LiveIntervalMs = 1000.0;

    private const double PreviewIntervalMs = 100.0;

    private const long PreviewPoolBytes = 64L * 1024 * 1024;

    private readonly FrameBufferPool _pool;
    private readonly FrameBufferPool _previewPool = new(PreviewPoolBytes);
    private readonly FrameRing _ring;
    private readonly object _lock = new();
    private long _frameBytes;
    private int _claimed;
    private int _lost;
    private string _spanNote = "";
    private IFrameSource? _source;
    private string _sourceKey = "";
    private Rectangle _crop;
    private bool _downscale;
    private long _seq;
    private System.Threading.Timer? _deliver;
    private string _routeName = "";
    private double _pressMs = double.NaN;
    private int _delayMs;
    private double _dueMs;
    private double _frameMs;
    private bool _pressed;
    private bool _calibrating;
    private bool _whiteSeen;
    private bool _armed;
    private Action<Bitmap>? _preview;
    private double _lastPreviewMs;
    private Action<Bitmap>? _live;
    private double _lastLiveMs;

    public CaptureSession()
    {
        long budget = FrameRing.BudgetFor(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
        _pool = new FrameBufferPool(budget);
        _ring = new FrameRing(budget, frame => _pool.Return(frame.Bgra));
    }

    public event Action<CaptureResult>? Ready;

    public bool IsOpen => _source != null;

    public string? SourceError => _source?.Error;

    public string Status
    {
        get
        {
            IFrameSource? source = _source;
            if (source == null) return "Off";
            string status = source.Error ?? "Capturing " + source.DisplayName;
            return status;
        }
    }

    private const double GameFps = 59.7275;

    public void Refresh(AppSettings settings, bool routeSelected)
    {
        _crop = new Rectangle(settings.VideoCropX, settings.VideoCropY, settings.VideoCropWidth, settings.VideoCropHeight);
        _downscale = settings.VideoDownscale;

        bool wanted = (settings.VideoEnabled && routeSelected) || _preview != null;
        string key = wanted ? settings.VideoSourceKind + "\n" + settings.VideoSourceId : "";
        if (key == _sourceKey) return;

        CloseSource();
        _sourceKey = key;
        if (!wanted)
        {
            _pool.Clear();
            return;
        }

        IFrameSource? source = CaptureSourceInfo.Open(settings.VideoSourceKind, settings.VideoSourceId);
        if (source == null) return;
        source.FrameArrived += OnFrame;
        _source = source;
        source.Start();
        UpdateActive();
        ContextSession.Log(string.Format(CultureInfo.InvariantCulture, "capture: opened {0} \"{1}\", budget {2} MB",
            settings.VideoSourceKind, source.DisplayName, _ring.BudgetBytes >> 20));
    }

    public void SetPreview(Action<Bitmap>? preview, AppSettings settings, bool routeSelected)
    {
        _preview = preview;
        _sourceKey = "\0";
        Refresh(settings, routeSelected);
        UpdateActive();
    }

    public void SetLive(Action<Bitmap>? live)
    {
        if (ReferenceEquals(_live, live)) return;
        _live = live;
        UpdateActive();
    }

    public void Arm(string routeName, double dueMs, double earlyMs, double lateMs, int delayMs, double fps)
    {
        lock (_lock)
        {
            _deliver?.Dispose();
            _deliver = null;
            _routeName = routeName;
            _pressMs = dueMs;
            _dueMs = dueMs;
            _frameMs = 1000.0 / (fps > 0 ? fps : 59.7275);
            _pressed = false;
            _delayMs = delayMs;
            _calibrating = delayMs == 0;
            _whiteSeen = false;
            _armed = _source != null;
            _claimed = 0;
            _lost = 0;
            _spanNote = "";
        }
        UpdateActive();

        if (!_armed)
        {
            _ring.Disarm();
            return;
        }

        if (_calibrating)
        {
            _ring.Disarm();
        }
        else
        {
            _ring.KeepWindow(dueMs - earlyMs - MarginMs + delayMs, dueMs + lateMs + MarginMs + delayMs, MinIntervalMs);
        }
    }

    public void Pressed(double pressMs) => Settle(pressMs, pressed: true);

    public void Missed(double dueMs) => Settle(dueMs, pressed: false);

    public void Disarm()
    {
        lock (_lock)
        {
            _armed = false;
            _deliver?.Dispose();
            _deliver = null;
        }
        _ring.Disarm();
        UpdateActive();
    }

    private void UpdateActive()
    {
        IFrameSource? source = _source;
        if (source == null) return;
        bool armed;
        lock (_lock) armed = _armed;
        source.Active = armed || _preview != null || _live != null;
    }

    private double FittedSpanMs(double wantedMs)
    {
        long frameBytes = Interlocked.Read(ref _frameBytes);
        if (frameBytes <= 0) return wantedMs;
        double frames = _ring.BudgetBytes / (double)frameBytes;
        return Math.Min(wantedMs, Math.Max(frames - 1, 1) * _frameMs / 2.0);
    }

    private void Settle(double stampMs, bool pressed)
    {
        lock (_lock)
        {
            if (!_armed || _deliver != null) return;
            int delayMs = _delayMs;
            _pressMs = stampMs;
            _pressed = pressed;

            double now = Win32.GetTime();
            double marginMs = FittedSpanMs(2 * PressMarginMs) / 2.0;
            double spanMs = FittedSpanMs(CalibrationSpanMs);
            if (_calibrating)
            {
                _ring.KeepRolling(spanMs, stampMs + CalibrationSkipMs, MinIntervalMs);
                if (spanMs < CalibrationSpanMs) _spanNote = string.Format(CultureInfo.InvariantCulture, "memory holds {0:F0} ms", spanMs);
            }
            else if (stampMs + delayMs - marginMs > now)
            {
                _ring.KeepWindow(stampMs + delayMs - marginMs, stampMs + delayMs + marginMs, MinIntervalMs);
                if (marginMs < PressMarginMs) _spanNote = string.Format(CultureInfo.InvariantCulture, "memory holds ±{0:F0} ms", marginMs);
            }

            double waitMs = _calibrating
                ? stampMs + CalibrationTimeoutMs - now
                : stampMs + delayMs + marginMs + 100 - now;
            int dueIn = (int)Math.Clamp(Math.Ceiling(waitMs), 0, int.MaxValue);
            _deliver = new System.Threading.Timer(_ => Deliver(delayMs, whiteSeen: false), null, dueIn, Timeout.Infinite);
        }
    }

    private void OnFrame(double stampMs, IRawFrame raw)
    {
        bool downscale = _downscale;
        Rectangle clip = WindowFrameSource.Clip(_crop, raw.Width, raw.Height);
        Interlocked.Exchange(ref _frameBytes, downscale ? GamePixels.Bytes : (long)clip.Width * clip.Height * 4);

        bool preview = _preview != null && stampMs - _lastPreviewMs >= PreviewIntervalMs;
        bool live = _live != null && stampMs - _lastLiveMs >= LiveIntervalMs;
        try
        {
            if (live)
            {
                _lastLiveMs = stampMs;
                raw.CopyRegion(_crop, downscale, _previewPool.Rent, (bytes, size) =>
                {
                    var shown = new CapturedFrame(0, stampMs, size.Width, size.Height, bytes);
                    StarterTool.Post(() =>
                    {
                        try
                        {
                            if (_live is { } show) show(ToBitmap(shown));
                        }
                        finally
                        {
                            _previewPool.Return(bytes);
                        }
                    });
                });
            }

            if (_ring.Claim(stampMs))
            {
                Interlocked.Increment(ref _claimed);
                if (!raw.CopyRegion(_crop, downscale, _pool.Rent, (bytes, size) => Keep(stampMs, bytes, size)))
                {
                    Interlocked.Increment(ref _lost);
                }
            }

            if (preview)
            {
                _lastPreviewMs = stampMs;
                raw.CopyRegion(Rectangle.Empty, false, _previewPool.Rent, (bytes, size) =>
                {
                    var full = new CapturedFrame(0, stampMs, size.Width, size.Height, bytes);
                    StarterTool.Post(() =>
                    {
                        try
                        {
                            if (_preview is { } show) show(ToBitmap(full));
                        }
                        finally
                        {
                            _previewPool.Return(bytes);
                        }
                    });
                });
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref _lost);
        }
    }

    private void Keep(double stampMs, byte[] bytes, Size size)
    {
        var frame = new CapturedFrame(Interlocked.Increment(ref _seq), stampMs, size.Width, size.Height, bytes);
        if (!_ring.Add(frame))
        {
            _pool.Return(bytes);
            return;
        }
        CheckWhite(frame);
    }

    private void CheckWhite(CapturedFrame frame)
    {
        lock (_lock)
        {
            if (!_armed || !_calibrating || !_pressed || _whiteSeen || frame.StampMs <= _pressMs) return;
        }
        if (!WhiteFade.IsWhite(frame)) return;

        lock (_lock)
        {
            if (!_armed || _whiteSeen) return;
            _whiteSeen = true;
            int delayMs = _delayMs;
            _deliver?.Dispose();
            _deliver = new System.Threading.Timer(_ =>
            {
                _ring.Freeze();
                Deliver(delayMs, whiteSeen: true);
            }, null, (int)(WhiteFade.PlateauMs + 100), Timeout.Infinite);
        }
    }

    private void Deliver(int delayMs, bool whiteSeen)
    {
        CaptureResult result;
        lock (_lock)
        {
            if (!_armed) return;
            _armed = false;
            _deliver?.Dispose();
            _deliver = null;

            List<CapturedFrame> frames = _ring.TakeAll();
            int target;
            string note;
            if (_calibrating)
            {
                target = whiteSeen ? WhiteFade.LastBeforeComplete(frames, _pressMs) : -1;
                note = target >= 0
                    ? "Calibration: fade found"
                    : "Calibration: no fade";
                if (target < 0) target = FrameRing.IndexAt(frames, _pressMs + delayMs);
            }
            else
            {
                target = FrameRing.IndexAt(frames, _pressMs + delayMs);
                note = _pressed ? "" : "No title press - guessing";
            }
            if (_spanNote.Length > 0) note = note.Length > 0 ? note + " · " + _spanNote : _spanNote[..1].ToUpperInvariant() + _spanNote[1..];

            int released = 0;
            result = new CaptureResult(frames, target, _pressMs, _dueMs, _frameMs, delayMs, _calibrating && whiteSeen,
                _pressed, _routeName, note)
            {
                ReleaseFrames = () =>
                {
                    if (Interlocked.Exchange(ref released, 1) != 0) return;
                    foreach (CapturedFrame frame in frames) _pool.Return(frame.Bgra);
                },
            };
        }
        _ring.Disarm();
        UpdateActive();
        int lost = Interlocked.CompareExchange(ref _lost, 0, 0);
        string rate = _source?.FramesPerSecond is double fps
            ? string.Format(CultureInfo.InvariantCulture, ", source {0:0.##} fps", fps)
            : "";

        StarterTool.Post(() =>
        {
            CapturedFrame? shown = result.TargetIndex >= 0 ? result.Frames[result.TargetIndex] : null;
            double longestGap = 0;
            for (int i = 1; i < result.Frames.Count; i++)
            {
                longestGap = Math.Max(longestGap, result.Frames[i].StampMs - result.Frames[i - 1].StampMs);
            }
            ContextSession.Log(string.Format(CultureInfo.InvariantCulture,
                "capture: {0} {1:F1} ms, delay {2} ms, frame {3}, {4} frames kept, {5} lost, longest gap {6:F1} ms{9}{7}{8}",
                result.Pressed ? "press" : "due", result.PressMs, result.DelayMs,
                shown == null ? "none" : (shown.StampMs - result.PressMs).ToString("F1", CultureInfo.InvariantCulture) + " ms after",
                result.Frames.Count, lost, longestGap,
                result.Calibration ? " (calibration)" : "",
                result.Note.Length > 0 ? " - " + result.Note : "", rate));
            Ready?.Invoke(result);
        });
    }

    public static double TargetSlotMs(CaptureResult result)
    {
        if (!result.Calibration || result.TargetIndex < 0) return result.PressMs + result.DelayMs;

        IReadOnlyList<CapturedFrame> frames = result.Frames;
        double on = frames[result.TargetIndex].StampMs;
        double off = result.TargetIndex + 1 < frames.Count ? frames[result.TargetIndex + 1].StampMs : on + result.FrameMs;
        return Math.Min((on + off) / 2.0, on + result.FrameMs / 2.0);
    }

    public static Bitmap ToBitmap(CapturedFrame frame)
    {
        var bitmap = new Bitmap(frame.Width, frame.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, frame.Width, frame.Height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        try
        {
            for (int y = 0; y < frame.Height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(frame.Bgra, y * frame.Stride, data.Scan0 + y * data.Stride, frame.Stride);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return bitmap;
    }

    private void CloseSource()
    {
        IFrameSource? source = _source;
        _source = null;
        if (source == null) return;
        source.FrameArrived -= OnFrame;
        source.Dispose();
    }

    public void Dispose()
    {
        Disarm();
        CloseSource();
    }
}
