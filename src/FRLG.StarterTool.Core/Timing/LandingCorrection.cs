using FRLG.StarterTool.Core.Training;

namespace FRLG.StarterTool.Core.Timing;

public readonly record struct LandingReport(
    double TargetFrame, int ReportedFrame, int? PredictedFrame, double? DeltaMs,
    int DelayAtMs, int OffsetAtMs, int Step = 1);

public readonly record struct LandingAdvice(
    int DelayShiftMs, int OffsetShiftMs, int Runs, int Landings, bool Timed, double SpreadFrames)
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

    public OffsetTuner OffsetPosterior { get; } = new();

    public OffsetTuner DelayPosterior { get; } = new();

    public const double OutlierFrames = 3.0;

    public int Count => _reports.Count;

    public IReadOnlyList<LandingReport> Reports => _reports;

    public static void ObserveRobustly(OffsetTuner tuner, double errorFrames)
        => tuner.Observe(Math.Clamp(errorFrames, tuner.Mu - OutlierFrames, tuner.Mu + OutlierFrames));

    private readonly List<(double DeltaMs, int OffsetAtMs)> _attempts = new();

    public void ObserveAttempt(double deltaMs, int offsetAtMs) => _attempts.Add((deltaMs, offsetAtMs));

    public int Attempts => _attempts.Count;

    public void Add(in LandingReport report) => _reports.Add(report);

    public void Clear()
    {
        _reports.Clear();
        _attempts.Clear();
    }

    public double OffsetErrorFrames(double deltaMs, int offsetUsed) =>
        (deltaMs - (offsetUsed - InitialOffsetMs)) / 1000.0 * Fps;

    public double OffsetErrorFrames(double targetFrame, int reportedFrame, int offsetUsed, int step = 1) =>
        FramesOff(reportedFrame - targetFrame, step) - (offsetUsed - InitialOffsetMs) / FrameMs;

    public double DelayErrorFrames(int predictedFrame, int reportedFrame, int delayUsed, int step = 1) =>
        -FramesOff(predictedFrame - reportedFrame, step) - (delayUsed - InitialDelayMs) / FrameMs;

    public static double FramesOff(double counts, int step) =>
        step <= 1 ? counts : Math.Truncate(counts / step);

    public LandingAdvice? Advise(int delayNowMs, int offsetNowMs)
    {
        if (_reports.Count == 0) return null;

        bool timed = _reports.Any(report => report.DeltaMs != null);

        var offsetPosterior = new OffsetTuner();
        var delayPosterior = new OffsetTuner();
        int runs = 0;
        double loneDelayShift = 0.0;
        double loneOffsetShift = 0.0;

        foreach ((double deltaMs, int offsetAtMs) in _attempts)
        {
            ObserveRobustly(offsetPosterior, OffsetErrorFrames(deltaMs, offsetAtMs));
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
                    loneDelayShift = FramesOff(predicted - report.ReportedFrame, report.Step) * FrameMs;
                    ObserveRobustly(delayPosterior,
                        DelayErrorFrames(predicted, report.ReportedFrame, report.DelayAtMs, report.Step));
                }
            }
            else
            {
                loneOffsetShift = FramesOff(report.TargetFrame - report.ReportedFrame, report.Step) * FrameMs;
                ObserveRobustly(offsetPosterior,
                    OffsetErrorFrames(report.TargetFrame, report.ReportedFrame, report.OffsetAtMs, report.Step));
            }
            runs++;
        }
        if (runs == 0 && offsetPosterior.Observations == 0) return null;

        bool firstRun = runs <= 1 && offsetPosterior.Observations <= 1;
        if (firstRun && Math.Max(Math.Abs(loneDelayShift), Math.Abs(loneOffsetShift)) < FrameMs) return null;

        int offsetShift = offsetPosterior.Observations == 0
            ? 0
            : offsetPosterior.RecommendedOffsetMs(InitialOffsetMs, Fps) - offsetNowMs;
        int delayShift = delayPosterior.Observations == 0
            ? 0
            : delayPosterior.RecommendedOffsetMs(InitialDelayMs, Fps) - delayNowMs;
        if (delayShift == 0 && offsetShift == 0) return null;

        return new LandingAdvice(
            delayShift, offsetShift, runs, offsetPosterior.Observations, timed,
            offsetPosterior.Observations > 0 ? offsetPosterior.MeanSigma : 0.0);
    }
}
