using System.Globalization;
using System.Text.RegularExpressions;

namespace FRLG.StarterTool.Core.Timing;

public readonly record struct HistoryLanding(int TargetFrame, double DeltaMs, int? PredictedFrame,
    int? ReportedFrame, int DelayAtMs, int OffsetAtMs, double Fps, int Step);

public static class LandingHistory
{
    private static readonly Regex Armed = new(
        @"armed (\d+):.*offset (-?\d+) ms, delay (-?\d+) ms, fps ([\d.]+)", RegexOptions.CultureInvariant);

    private static readonly Regex Landing = new(
        @"landing on (\d+): pressed at [\d.]+ ms \(([+-]?[\d.]+) ms off\)(?:.*?likely (\d+))?", RegexOptions.CultureInvariant);

    private static readonly Regex Reported = new(
        @"landing reported: starter press landed on frame (\d+)", RegexOptions.CultureInvariant);

    public static IReadOnlyList<HistoryLanding> Parse(IEnumerable<string> lines)
    {
        var found = new List<HistoryLanding>();
        int step = 1;
        (int Delay, int Offset, double Fps)? armed = null;

        foreach (string line in lines)
        {
            if (line.Contains("wireless adapter on", StringComparison.Ordinal)) step = 2;

            if (Armed.Match(line) is { Success: true } a)
            {
                armed = (Int(a.Groups[3]), Int(a.Groups[2]),
                    double.Parse(a.Groups[4].Value, CultureInfo.InvariantCulture));
                continue;
            }

            if (Landing.Match(line) is { Success: true } l && armed is { } boxes)
            {
                found.Add(new HistoryLanding(
                    Int(l.Groups[1]),
                    double.Parse(l.Groups[2].Value, CultureInfo.InvariantCulture),
                    l.Groups[3].Success ? Int(l.Groups[3]) : null,
                    null, boxes.Delay, boxes.Offset, boxes.Fps, step));
                continue;
            }

            if (found.Count == 0) continue;

            if (Reported.Match(line) is { Success: true } r)
            {
                found[^1] = found[^1] with { ReportedFrame = Int(r.Groups[1]) };
            }
            else if (line.Contains("starter press marked missed", StringComparison.Ordinal))
            {
                found.RemoveAt(found.Count - 1);
            }
        }

        return found;
    }

    public static void Seed(LandingCorrection correction, IEnumerable<HistoryLanding> landings)
    {
        foreach (HistoryLanding landing in landings)
        {
            correction.ObserveAttempt(landing.DeltaMs, landing.OffsetAtMs);
            if (landing.ReportedFrame is { } reported)
            {
                correction.Add(new LandingReport(landing.TargetFrame, reported, landing.PredictedFrame,
                    landing.DeltaMs, landing.DelayAtMs, landing.OffsetAtMs, landing.Step));
            }
        }
    }

    private static int Int(Group group) => int.Parse(group.Value, CultureInfo.InvariantCulture);
}
