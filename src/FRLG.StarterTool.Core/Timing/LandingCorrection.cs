using FRLG.StarterTool.Core.Training;

namespace FRLG.StarterTool.Core.Timing;

public readonly record struct LandingReport(
    double TargetFrame, int ReportedFrame, int? PredictedFrame, double? DeltaMs,
    int DelayAtMs, int OffsetAtMs, int Step = 1, string? Setup = null);

public readonly record struct LandingAdvice(
    int DelayShiftMs, int OffsetShiftMs, int Runs, int Landings, bool Timed, double SpreadFrames,
    int RecentPresses = 0, double RecentMeanMs = 0.0)
{
    public bool Any => DelayShiftMs != 0 || OffsetShiftMs != 0;

    public bool Scattered => Landings > 1 && SpreadFrames > 1.0;

    public double SingleFrameHitRate => OffsetTuner.HitRate(SpreadFrames, 1);
}

public sealed class LandingCorrection
{
    private readonly List<LandingReport> _reports = new();

    public LandingCorrection(int initialDelayMs, int initialOffsetMs, double fps)
    {
        InitialDelayMs = initialDelayMs;
        InitialOffsetMs = initialOffsetMs;
        Fps = fps > 0.0 ? fps : 60.0;
    }

    public int InitialDelayMs { get; }

    public int InitialOffsetMs { get; }

    public double Fps { get; }

    private double FrameMs => 1000.0 / Fps;

    public const double DelayPriorRuns = 2.0;

    public const double OffsetPriorRuns = 1.0;

    public const double OffsetGain = 0.2;

    public const double MinimumShiftFrames = 0.25;

    private sealed class RunningShift(double priorRuns, double floorGain = 0.0, double start = 0.0)
    {
        public int Count { get; private set; }

        public double Mean { get; private set; } = start;

        public void Observe(double frames)
        {
            double gain = Math.Max(floorGain, 1.0 / (priorRuns + Count + 1));
            Mean += gain * (Math.Clamp(frames, Mean - OutlierFrames, Mean + OutlierFrames) - Mean);
            Count++;
        }
    }

    public const int RecentWindow = 10;

    public OffsetTuner OffsetPosterior { get; } = new();

    public OffsetTuner DelayPosterior { get; } = new();

    public const double OutlierFrames = 3.0;

    public int Count => _reports.Count;

    public IReadOnlyList<LandingReport> Reports => _reports;

    public static void ObserveRobustly(OffsetTuner tuner, double errorFrames)
        => tuner.Observe(Math.Clamp(errorFrames, tuner.Mu - OutlierFrames, tuner.Mu + OutlierFrames));

    private readonly List<Attempt> _attempts = new();

    private readonly record struct Attempt(double DeltaMs, int OffsetAtMs, string? Setup);

    public void ObserveAttempt(double deltaMs, int offsetAtMs, string? setup = null) =>
        _attempts.Add(new Attempt(deltaMs, offsetAtMs, setup));

    public int Attempts => _attempts.Count;

    public void Add(in LandingReport report) => _reports.Add(report);

    public void PrependHistory(Action<LandingCorrection> seed)
    {
        var reports = _reports.ToList();
        var attempts = _attempts.ToList();
        _reports.Clear();
        _attempts.Clear();
        seed(this);
        _reports.AddRange(reports);
        _attempts.AddRange(attempts);
    }

    public void Clear()
    {
        _reports.Clear();
        _attempts.Clear();
    }

    public double OffsetErrorFrames(double deltaMs, int offsetUsed) =>
        (deltaMs - (offsetUsed - InitialOffsetMs)) / 1000.0 * Fps;

    public double OffsetErrorFrames(double targetFrame, int reportedFrame, int offsetUsed, int step = 1) =>
        FramesOff(reportedFrame - targetFrame, step) - (offsetUsed - InitialOffsetMs) / FrameMs;

    public double DelayErrorFrames(int predictedFrame, int reportedFrame, int delayUsed, int step = 1,
        double targetFrame = double.NaN, double? deltaMs = null)
    {
        double frames = -FramesOff(predictedFrame - reportedFrame, step);
        if (deltaMs is { } delta && !double.IsNaN(targetFrame))
        {
            double position = targetFrame - Math.Floor(targetFrame) + delta / FrameMs;
            frames += Math.Round(position, MidpointRounding.AwayFromZero) - position;
        }
        return frames - (delayUsed - InitialDelayMs) / FrameMs;
    }

    public static double FramesOff(double counts, int step) =>
        step <= 1 ? counts : Math.Truncate(counts / step);

    public LandingAdvice? Advise(int delayNowMs, int offsetNowMs, string? setup = null)
    {
        if (_reports.Count == 0) return null;

        bool timed = _reports.Any(report => report.DeltaMs != null);

        double offsetStart = (InitialOffsetMs - offsetNowMs) / FrameMs;
        double delayStart = (InitialDelayMs - delayNowMs) / FrameMs;
        var offset = new RunningShift(OffsetPriorRuns, OffsetGain, offsetStart);
        var delay = new RunningShift(DelayPriorRuns, start: delayStart);
        var spread = new OffsetTuner(offsetStart);
        int runs = 0;
        double loneDelayShift = 0.0;
        double loneOffsetShift = 0.0;

        var recent = new List<double>();
        void ObserveOffset(double frames)
        {
            offset.Observe(frames);
            ObserveRobustly(spread, frames);
            recent.Add((frames - offsetStart) * FrameMs);
        }

        foreach (Attempt attempt in _attempts)
        {
            if (!AudioSetup.Matches(attempt.Setup, setup)) continue;
            ObserveOffset(OffsetErrorFrames(attempt.DeltaMs, attempt.OffsetAtMs));
        }

        foreach (LandingReport report in _reports)
        {
            if (timed && report.DeltaMs == null) continue;

            loneDelayShift = 0.0;
            if (report.DeltaMs is { } delta)
            {
                loneOffsetShift = -delta;

                if (report.PredictedFrame is { } predicted)
                {
                    double frames = DelayErrorFrames(predicted, report.ReportedFrame, report.DelayAtMs,
                        report.Step, report.TargetFrame, delta);
                    loneDelayShift = -(frames + (report.DelayAtMs - InitialDelayMs) / FrameMs) * FrameMs;
                    delay.Observe(frames);
                }
                else
                {
                    double framesOff = (report.TargetFrame - report.ReportedFrame) / Math.Max(report.Step, 1)
                        + delta / FrameMs;
                    loneDelayShift = framesOff * FrameMs;
                    delay.Observe(-framesOff - (report.DelayAtMs - InitialDelayMs) / FrameMs);
                }
            }
            else
            {
                if (!AudioSetup.Matches(report.Setup, setup)) continue;
                loneOffsetShift = FramesOff(report.TargetFrame - report.ReportedFrame, report.Step) * FrameMs;
                ObserveOffset(OffsetErrorFrames(report.TargetFrame, report.ReportedFrame, report.OffsetAtMs, report.Step));
            }
            runs++;
        }
        if (runs == 0 && offset.Count == 0) return null;

        bool firstRun = runs <= 1 && offset.Count <= 1;
        if (firstRun && Math.Max(Math.Abs(loneDelayShift), Math.Abs(loneOffsetShift)) < FrameMs) return null;

        int offsetShift = Shift(offset, InitialOffsetMs, offsetNowMs);
        int delayShift = Shift(delay, InitialDelayMs, delayNowMs);
        if (delayShift == 0 && offsetShift == 0) return null;

        var window = recent.TakeLast(RecentWindow).ToList();
        return new LandingAdvice(
            delayShift, offsetShift, runs, offset.Count, timed,
            spread.Observations > 0 ? spread.MeanSigma : 0.0,
            window.Count, window.Count > 0 ? window.Average() : 0.0);
    }

    private int Shift(RunningShift mean, int initialMs, int nowMs)
    {
        if (mean.Count == 0) return 0;
        int shift = (int)Math.Round(initialMs - mean.Mean * FrameMs) - nowMs;
        return Math.Abs(shift) < MinimumShiftFrames * FrameMs ? 0 : shift;
    }
}
