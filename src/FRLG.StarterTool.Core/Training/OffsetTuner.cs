namespace FRLG.StarterTool.Core.Training;

public sealed class OffsetTuner
{
    public const double PriorNu = 4.0;

    public const double PriorBeta = 2.345;

    public double Mu { get; private set; }

    public double Nu { get; private set; } = PriorNu;

    public double Beta { get; private set; } = PriorBeta;

    public double Alpha => Nu / 2.0;

    public int Observations => (int)(Nu - PriorNu);

    public double MeanSigma => Math.Sqrt(MeanSigmaApprox);

    public double SdSigma
    {
        get
        {
            double variance = MeanSigmaSquared - MeanSigmaApprox;
            return variance > 0.0 ? Math.Sqrt(variance) : 0.0;
        }
    }

    private double MeanSigmaSquared => Beta / (Alpha - 1.0);

    private double MeanSigmaApprox => 2.0 * Beta / (Nu - 1.0);

    public void Observe(double errorFrames)
    {
        double previousMu = Mu;

        Mu = (Nu * Mu + errorFrames) / (Nu + 1.0);
        Beta += Nu * Math.Pow(errorFrames - previousMu, 2) / (2.0 * Nu + 2.0);
        Nu += 1.0;
    }

    public int RecommendedOffsetMs(int initialOffsetMs, double fps) =>
        (int)Math.Round(initialOffsetMs - 1000.0 * Mu / fps);

    public static double HitRate(double sigma, int frames)
    {
        if (frames <= 0) return 0.0;
        if (sigma <= 0.0) return 1.0;

        return Erf(frames / (2.0 * sigma * Math.Sqrt(2.0)));
    }

    public static double Erf(double x)
    {
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        int sign = x < 0.0 ? -1 : 1;
        x = Math.Abs(x);

        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return sign * y;
    }
}
