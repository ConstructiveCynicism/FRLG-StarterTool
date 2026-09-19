namespace FRLG.StarterTool.Core.Video;

public sealed class FrameRing
{
    public const long DefaultBudgetBytes = 768L * 1024 * 1024;

    public const long MinBudgetBytes = 128L * 1024 * 1024;

    public static long BudgetFor(long physicalBytes) =>
        physicalBytes <= 0 ? DefaultBudgetBytes : Math.Clamp(physicalBytes / 32, MinBudgetBytes, DefaultBudgetBytes);

    private enum Mode { Off, Window, Rolling }

    private readonly object _lock = new();
    private readonly List<CapturedFrame> _frames = new();
    private readonly long _budgetBytes;
    private readonly Action<CapturedFrame>? _released;
    private double _lastClaimMs = double.NegativeInfinity;
    private long _bytes;
    private Mode _mode;
    private double _fromMs;
    private double _toMs;
    private double _spanMs;
    private double _minIntervalMs;
    private bool _frozen;

    public FrameRing(long budgetBytes = DefaultBudgetBytes, Action<CapturedFrame>? released = null)
    {
        _budgetBytes = Math.Max(budgetBytes, 1);
        _released = released;
    }

    public long BudgetBytes => _budgetBytes;

    public void KeepWindow(double fromMs, double toMs, double minIntervalMs = 0)
    {
        lock (_lock)
        {
            ClearLocked();
            _mode = Mode.Window;
            _fromMs = Math.Min(fromMs, toMs);
            _toMs = Math.Max(fromMs, toMs);
            _minIntervalMs = Math.Max(minIntervalMs, 0);
        }
    }

    public void KeepRolling(double spanMs, double fromMs = double.NegativeInfinity, double minIntervalMs = 0)
    {
        lock (_lock)
        {
            ClearLocked();
            _mode = Mode.Rolling;
            _spanMs = Math.Max(spanMs, 1.0);
            _fromMs = fromMs;
            _minIntervalMs = Math.Max(minIntervalMs, 0);
        }
    }

    public void Disarm()
    {
        lock (_lock)
        {
            ClearLocked();
            _mode = Mode.Off;
        }
    }

    public void Freeze()
    {
        lock (_lock) _frozen = true;
    }

    public bool Frozen
    {
        get { lock (_lock) return _frozen; }
    }

    public bool Wants(double stampMs)
    {
        lock (_lock) return WantsLocked(stampMs);
    }

    public bool Claim(double stampMs)
    {
        lock (_lock)
        {
            if (!WantsLocked(stampMs)) return false;
            _lastClaimMs = stampMs;
            return true;
        }
    }

    public bool Add(CapturedFrame frame)
    {
        lock (_lock)
        {
            if (!InsideLocked(frame.StampMs)) return false;

            int at = _frames.Count;
            while (at > 0 && _frames[at - 1].StampMs > frame.StampMs) at--;
            _frames.Insert(at, frame);
            _bytes += frame.Bytes;

            if (_mode == Mode.Rolling)
            {
                double oldest = _frames[^1].StampMs - _spanMs;
                while (_frames.Count > 1 && _frames[0].StampMs < oldest) RemoveOldestLocked();
            }
            while (_frames.Count > 1 && _bytes > _budgetBytes) RemoveOldestLocked();
            return true;
        }
    }

    public int Count
    {
        get { lock (_lock) return _frames.Count; }
    }

    public List<CapturedFrame> Snapshot()
    {
        lock (_lock) return new List<CapturedFrame>(_frames);
    }

    public List<CapturedFrame> TakeAll()
    {
        lock (_lock)
        {
            var frames = new List<CapturedFrame>(_frames);
            _frames.Clear();
            _bytes = 0;
            return frames;
        }
    }

    public static int IndexAt(IReadOnlyList<CapturedFrame> frames, double stampMs)
    {
        if (frames.Count == 0) return -1;
        int lo = 0, hi = frames.Count - 1, found = 0;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (frames[mid].StampMs <= stampMs)
            {
                found = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return found;
    }

    private bool WantsLocked(double stampMs) =>
        stampMs - Math.Max(_lastClaimMs, _frames.Count == 0 ? double.NegativeInfinity : _frames[^1].StampMs) >= _minIntervalMs
        && InsideLocked(stampMs);

    private bool InsideLocked(double stampMs) => !_frozen && _mode switch
    {
        Mode.Window => stampMs >= _fromMs && stampMs <= _toMs,
        Mode.Rolling => stampMs >= _fromMs,
        _ => false,
    };

    private void RemoveOldestLocked()
    {
        CapturedFrame oldest = _frames[0];
        _bytes -= oldest.Bytes;
        _frames.RemoveAt(0);
        _released?.Invoke(oldest);
    }

    private void ClearLocked()
    {
        if (_released != null)
        {
            foreach (CapturedFrame frame in _frames) _released(frame);
        }
        _frames.Clear();
        _bytes = 0;
        _frozen = false;
        _lastClaimMs = double.NegativeInfinity;
    }
}
