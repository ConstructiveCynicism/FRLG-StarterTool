using System.Diagnostics;

namespace FRLG.StarterTool.Core.Timing;

public static class DriftMonitor
{
    public static TimeSpan SampleInterval { get; set; } = TimeSpan.FromSeconds(10);

    public static TimeSpan MinimumTrustedSpan { get; set; } = TimeSpan.FromMinutes(10);

    public const double MaxDriftPpm = 1000.0;

    public const int RunMinIntervals = 3;

    public const double RunAgreementPpm = 1.0;

    public const double RunMaxPpm = 50.0;

    private static readonly object Gate = new();
    private static readonly List<double> Rates = [];
    private static readonly List<double> RunRates = [];
    private static Thread? _thread;
    private static volatile bool _running;
    private static long _lastLocal;
    private static long _lastSystem;
    private static double _keptSpanSeconds;
    private static double _runSpanSeconds;
    private static int _dropped;

    public static double Measured
    {
        get { lock (Gate) return Median(Rates); }
    }

    public static double MeasuredSpanSeconds
    {
        get { lock (Gate) return _keptSpanSeconds; }
    }

    public static int Dropped
    {
        get { lock (Gate) return _dropped; }
    }

    public static bool Trusted
        => MeasuredSpanSeconds >= MinimumTrustedSpan.TotalSeconds && IsPlausible(Measured);

    public static double RunSpanSeconds
    {
        get { lock (Gate) return _runSpanSeconds; }
    }

    public static double? RunRate
    {
        get
        {
            lock (Gate)
            {
                if (RunRates.Count < RunMinIntervals) return null;
                double spread = ToPpm(RunRates.Max()) - ToPpm(RunRates.Min());
                if (spread > RunAgreementPpm) return null;
                double rate = Median(RunRates);
                return Math.Abs(ToPpm(rate)) <= RunMaxPpm ? rate : null;
            }
        }
    }

    public static void BeginRun()
    {
        lock (Gate)
        {
            RunRates.Clear();
            _runSpanSeconds = 0;
        }
    }

    public static bool IsPlausible(double drift)
        => !double.IsNaN(drift) && Math.Abs(drift - 1.0) * 1e6 <= MaxDriftPpm;

    public static double ToPpm(double drift) => (drift - 1.0) * 1e6;

    public static void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "Clock drift monitor",
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

    public static bool SampleNow()
    {
        long before = Stopwatch.GetTimestamp();
        long system = DateTime.UtcNow.Ticks;
        long after = Stopwatch.GetTimestamp();
        if ((after - before) * 1000.0 / Stopwatch.Frequency > 0.05) return false;
        long local = before + (after - before) / 2;

        lock (Gate)
        {
            if (_lastLocal == 0)
            {
                _lastLocal = local;
                _lastSystem = system;
                return true;
            }

            double localSeconds = (local - _lastLocal) / (double)Stopwatch.Frequency;
            double systemSeconds = (system - _lastSystem) / (double)TimeSpan.TicksPerSecond;
            _lastLocal = local;
            _lastSystem = system;

            if (systemSeconds < 1.0 || localSeconds <= 0) return false;
            return Fold(localSeconds / systemSeconds, systemSeconds);
        }
    }

    public static bool Fold(double rate, double seconds)
    {
        lock (Gate)
        {
            if (!IsPlausible(rate))
            {
                _dropped++;
                return false;
            }

            Rates.Add(rate);
            _keptSpanSeconds += seconds;
            RunRates.Add(rate);
            _runSpanSeconds += seconds;
            return true;
        }
    }

    public static void Reset()
    {
        lock (Gate)
        {
            Rates.Clear();
            RunRates.Clear();
            _lastLocal = 0;
            _lastSystem = 0;
            _keptSpanSeconds = 0;
            _runSpanSeconds = 0;
            _dropped = 0;
        }
    }

    private static double Median(List<double> rates)
    {
        if (rates.Count == 0) return 1.0;
        double[] sorted = rates.Order().ToArray();
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    private static void Loop()
    {
        while (_running)
        {
            try { SampleNow(); }
            catch (Exception) {  }

            lock (Gate)
            {
                if (_running) Monitor.Wait(Gate, SampleInterval);
            }
        }
    }
}
