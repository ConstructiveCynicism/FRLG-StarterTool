namespace FRLG.StarterTool.Core.Timing;

public static class AtomicClock
{
    public static TimeSpan SyncInterval { get; set; } = TimeSpan.FromMinutes(5);

    public static TimeSpan MinimumTrustedSpan { get; set; } = TimeSpan.FromMinutes(20);

    private static readonly object Gate = new();
    private static Thread? _thread;
    private static volatile bool _running;
    private static NtpSample? _first;
    private static NtpSample? _last;
    private static int _readings;

    public static double Measured
    {
        get
        {
            lock (Gate)
            {
                if (_first is not { } first || _last is not { } last) return 1.0;
                double atomic = last.AtomicSeconds - first.AtomicSeconds;
                return atomic <= 0 ? 1.0 : (last.LocalSeconds - first.LocalSeconds) / atomic;
            }
        }
    }

    public static double MeasuredSpanSeconds
    {
        get
        {
            lock (Gate)
            {
                return _first is { } first && _last is { } last ? last.AtomicSeconds - first.AtomicSeconds : 0;
            }
        }
    }

    public static bool Synced
    {
        get { lock (Gate) return _first != null; }
    }

    public static int Readings
    {
        get { lock (Gate) return _readings; }
    }

    public static bool Trusted
        => MeasuredSpanSeconds >= MinimumTrustedSpan.TotalSeconds && DriftMonitor.IsPlausible(Measured);

    public static void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "Atomic clock sync",
            Priority = ThreadPriority.BelowNormal
        };
        _thread.Start();
    }

    public static void Stop()
    {
        _running = false;
        lock (Gate) Monitor.PulseAll(Gate);
        _thread?.Join(500);
        _thread = null;
    }

    public static NtpSample? SampleNow(int attempts = 4)
    {
        NtpSample? sample = NtpClient.Best(attempts);
        if (sample is { } s) Fold(s);
        return sample;
    }

    public static void Fold(NtpSample sample)
    {
        lock (Gate)
        {
            _first ??= sample;
            _last = sample;
            _readings++;
        }
    }

    public static void Reset()
    {
        lock (Gate)
        {
            _first = null;
            _last = null;
            _readings = 0;
        }
    }

    private static void Loop()
    {
        while (_running)
        {
            NtpSample? sample = null;
            try { sample = NtpClient.Best(); }
            catch (Exception) {  }

            if (sample is { } s) Fold(s);

            TimeSpan wait = sample == null && !Synced ? TimeSpan.FromSeconds(30) : SyncInterval;
            lock (Gate)
            {
                if (_running) Monitor.Wait(Gate, wait);
            }
        }
    }
}
