using System.Globalization;

namespace FRLG.StarterTool.Core.Timing;

public sealed class StartLatencyWatch
{
    public const int BaselineCount = 10;

    public const int StepCount = 3;

    public const double StepMarginMs = 3.0;

    private readonly List<double> _baseline = new();
    private readonly List<double> _recent = new();
    private int _side;

    public void Reset()
    {
        _baseline.Clear();
        _recent.Clear();
        _side = 0;
    }

    public string? Add(double latencyMs)
    {
        if (double.IsNaN(latencyMs)) return null;

        if (_baseline.Count < BaselineCount)
        {
            _baseline.Add(latencyMs);
            return null;
        }

        double lo = _baseline.Min(), hi = _baseline.Max();
        int side = latencyMs > hi + StepMarginMs ? 1 : latencyMs < lo - StepMarginMs ? -1 : 0;

        if (_recent.Count > 0 && SideOf(_recent[^1], lo, hi) != side) _recent.Clear();
        _recent.Add(latencyMs);
        if (_recent.Count > StepCount) _recent.RemoveAt(0);
        if (_recent.Count < StepCount || side == _side) return null;

        int was = _side;
        _side = side;
        double moved = Median(_recent) - Median(_baseline);
        string readings = string.Join(", ", _recent.Select(r => r.ToString("F1", CultureInfo.InvariantCulture)));

        if (side == 0)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "start latency back in its band ({0:F1}-{1:F1} ms): {2} ms - the step {3} has ended",
                lo, hi, readings, was > 0 ? "up" : "down");
        }

        return string.Format(CultureInfo.InvariantCulture,
            "start latency stepped {0}: {1} ms against {2:F1}-{3:F1} ms over the first {4} arms since the output opened - "
            + "the beeps sound about {5:F0} ms {6} than they did, unless they are placed on the device clock",
            side > 0 ? "up" : "down", readings, lo, hi, BaselineCount, Math.Abs(moved), side > 0 ? "later" : "earlier");
    }

    private static int SideOf(double ms, double lo, double hi)
        => ms > hi + StepMarginMs ? 1 : ms < lo - StepMarginMs ? -1 : 0;

    private static double Median(List<double> values)
    {
        double[] sorted = values.Order().ToArray();
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
