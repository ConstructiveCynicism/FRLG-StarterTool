using System.Globalization;

using FRLG.StarterTool.Core.Npc;

namespace FRLG.StarterTool.Core.Troubleshoot;

public enum TroubleshootStage
{
    Fence,

    Lab,
}

public enum MatchQuality
{
    None,

    Movements,

    Exact,
}

public readonly record struct StripLine(string Label, IReadOnlyList<StripToken> Tokens);

public sealed record RowMatch(
    int Index,
    MatchQuality Quality,
    int Advances,
    IReadOnlyList<StripLine> Lines,
    string Label,
    bool WasUsed,
    int Distance = 0)
{
    public string Strip => string.Join("  /  ",
        Lines.Select(l => MovementStrip.Format(l.Tokens)));
}

public sealed record TroubleshootResult
{
    public TroubleshootStage Stage { get; init; }

    public IReadOnlyList<RowMatch> Rows { get; init; } = Array.Empty<RowMatch>();

    public IReadOnlyList<RowMatch> Fits => Rows.Where(r => r.Quality != MatchQuality.None).ToList();

    public int? UsedAdvances { get; init; }

    public RowMatch? Nearest => Rows.Count == 0 ? null : Rows[0];

    public int? ReportedAdvances { get; init; }

    public int? OutByFrames =>
        ReportedAdvances is { } reported && UsedAdvances is { } used ? reported - used : null;

    public int? NearestOutByFrames =>
        Nearest is { } near && UsedAdvances is { } used ? near.Advances - used : null;

    public string Summary { get; init; } = "";

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public static class Troubleshooter
{
    public static TroubleshootResult Fence(RunRecord run, IReadOnlyList<StripToken> report)
    {
        var rows = new List<RowMatch>();
        var notes = new List<string>();

        var live = new HashSet<int>();

        if (run.Taps.Count > 0)
        {
            var tapped = new List<StripToken>(run.Taps.Select(d => new StripToken(d)));

            for (int i = 0; i < run.Fence.Count; i++)
            {
                FenceRow row = run.Fence[i];

                if (Match(row.Strip, tapped, row.OptionalLeadingMoves, exact: false).Quality
                    != MatchQuality.None)
                {
                    live.Add(i);
                }
            }
        }

        for (int i = 0; i < run.Fence.Count; i++)
        {
            FenceRow row = run.Fence[i];
            (MatchQuality quality, int distance) =
                Match(row.Strip, report, row.OptionalLeadingMoves, exact: true);

            rows.Add(new RowMatch(
                i,
                quality,
                row.Advances,
                new[] { new StripLine("", row.Strip) },
                string.Format(CultureInfo.InvariantCulture, "oak {0}", row.OakFrame),
                live.Contains(i),
                distance));
        }

        Sort(rows);

        int? used = Single(rows.Where(r => r.WasUsed));
        int? reported = Single(rows.Where(r => r.Quality != MatchQuality.None));

        if (run.Taps.Count == 0)
        {
            notes.Add("Nothing was reported to the tracker on this run, so every candidate was still "
                + "standing - there is no live answer to be out by.");
        }

        if (OutsideTheField(rows, report.Count(t => !t.IsQuiet), "candidate") is { } outside)
        {
            notes.Add(outside);
        }

        if (run.RefusedTaps > 0)
        {
            notes.Add(string.Format(CultureInfo.InvariantCulture,
                "{0} tap{1} refused during the run as fitting nothing.",
                run.RefusedTaps, run.RefusedTaps == 1 ? " was" : "s were"));
        }

        return new TroubleshootResult
        {
            Stage = TroubleshootStage.Fence,
            Rows = rows,
            UsedAdvances = used,
            ReportedAdvances = reported,
            Summary = Describe(rows, used, run.FrameMs),
            Notes = notes
        };
    }

    public static TroubleshootResult Lab(RunRecord run,
        IReadOnlyList<StripToken>? aide, IReadOnlyList<StripToken>? scientist)
    {
        var rows = new List<RowMatch>();
        var notes = new List<string>();

        for (int i = 0; i < run.Lab.Count; i++)
        {
            LabRow row = run.Lab[i];

            (MatchQuality Quality, int Distance)? left =
                aide == null ? null : Match(row.AideStrip, aide, 0, exact: true);
            (MatchQuality Quality, int Distance)? right =
                scientist == null ? null : Match(row.ScientistStrip, scientist, 0, exact: true);

            rows.Add(new RowMatch(
                i,
                Both(left?.Quality, right?.Quality),
                row.Advances,
                new[]
                {
                    new StripLine("L", row.AideStrip),
                    new StripLine("S", row.ScientistStrip),
                },
                string.Format(CultureInfo.InvariantCulture, "x{0}  lab {1}", row.Members, row.LabFrame),
                row.Focused,
                (left?.Distance ?? 0) + (right?.Distance ?? 0)));
        }

        Sort(rows);

        int? used = Single(rows.Where(r => r.WasUsed));
        int? reported = Single(rows.Where(r => r.Quality != MatchQuality.None));

        if (aide == null && scientist == null)
        {
            notes.Add("Neither NPC was reported, so every box still fits.");
        }
        else if (aide == null || scientist == null)
        {
            notes.Add("Only one of the two was reported - the other is taken as unanswered rather "
                + "than as having stood still. A row of dashes is how to say it did nothing.");
        }

        int seen = (aide?.Count(t => !t.IsQuiet) ?? 0) + (scientist?.Count(t => !t.IsQuiet) ?? 0);

        if (OutsideTheField(rows, seen, "box") is { } outside) notes.Add(outside);

        if (run.LabWindowFrames > 0)
        {
            notes.Add(string.Format(CultureInfo.InvariantCulture,
                "The observable window was {0} frames - {1} slots at {2} frames each.",
                run.LabWindowFrames,
                (run.LabWindowFrames + MovementStrip.QuietIntervalFrames - 1)
                    / MovementStrip.QuietIntervalFrames,
                MovementStrip.QuietIntervalFrames));
        }

        return new TroubleshootResult
        {
            Stage = TroubleshootStage.Lab,
            Rows = rows,
            UsedAdvances = used,
            ReportedAdvances = reported,
            Summary = Describe(rows, used, run.FrameMs),
            Notes = notes
        };
    }

    public static (MatchQuality Quality, int Distance) Compare(IReadOnlyList<StripToken> predicted,
        IReadOnlyList<StripToken> report, int band) => Match(predicted, report, band, exact: true);

    private static (MatchQuality Quality, int Distance) Match(IReadOnlyList<StripToken> predicted,
        IReadOnlyList<StripToken> report, int band, bool exact)
    {
        MatchQuality best = MatchQuality.None;
        int nearest = int.MaxValue;

        for (int offset = 0; offset <= band; offset++)
        {
            (MatchQuality quality, int distance) = At(predicted, report, offset, exact);

            if (distance < nearest) nearest = distance;
            if (quality > best) best = quality;
            if (best == MatchQuality.Exact) break;
        }

        return (best, nearest == int.MaxValue ? 0 : nearest);
    }

    private static (MatchQuality Quality, int Distance) At(IReadOnlyList<StripToken> predicted,
        IReadOnlyList<StripToken> report, int offset, bool exact)
    {
        if (report.Count == 0) return (MatchQuality.Exact, 0);

        int start = 0;
        for (int skipped = 0; skipped < offset && start < predicted.Count; start++)
        {
            if (!predicted[start].IsQuiet) skipped++;
        }

        int distance = Distance(Moves(report), Moves(predicted, start));
        if (distance > 0) return (MatchQuality.None, distance);

        if (!exact) return (MatchQuality.Movements, 0);

        for (int i = 0; i < report.Count; i++)
        {
            int at = start + i;
            if (at >= predicted.Count) return (MatchQuality.Movements, 0);
            if (predicted[at].IsQuiet != report[i].IsQuiet) return (MatchQuality.Movements, 0);
            if (!predicted[at].IsQuiet && predicted[at].Direction != report[i].Direction)
            {
                return (MatchQuality.Movements, 0);
            }
        }

        return (MatchQuality.Exact, 0);
    }

    public static int Distance(IReadOnlyList<Direction> mine, IReadOnlyList<Direction> theirs)
    {
        var previous = new int[theirs.Count + 1];
        var current = new int[theirs.Count + 1];

        for (int j = 0; j <= theirs.Count; j++) previous[j] = j;

        for (int i = 1; i <= mine.Count; i++)
        {
            current[0] = i;

            for (int j = 1; j <= theirs.Count; j++)
            {
                int substitute = previous[j - 1] + (mine[i - 1] == theirs[j - 1] ? 0 : 1);
                current[j] = Math.Min(substitute, Math.Min(previous[j] + 1, current[j - 1] + 1));
            }

            (previous, current) = (current, previous);
        }

        return previous[theirs.Count];
    }

    private static IReadOnlyList<Direction> Moves(IReadOnlyList<StripToken> strip, int from = 0) =>
        strip.Skip(from).Where(t => !t.IsQuiet).Select(t => t.Direction).ToList();

    private static MatchQuality Both(MatchQuality? left, MatchQuality? right) => (left, right) switch
    {
        (null, null) => MatchQuality.Exact,
        (null, { } only) => only,
        ({ } only, null) => only,
        var (a, b) => (MatchQuality)Math.Min((int)a!.Value, (int)b!.Value),
    };

    private static string? OutsideTheField(IReadOnlyList<RowMatch> rows, int reported, string noun)
    {
        if (rows.Count == 0 || reported == 0) return null;
        if (rows.Any(r => r.Quality != MatchQuality.None)) return null;

        int richest = rows.Max(r => r.Lines.Sum(l => l.Tokens.Count(t => !t.IsQuiet)));
        if (richest >= reported) return null;

        return string.Format(CultureInfo.InvariantCulture,
            "No {0} in this field has more than {1} movement{2} and you reported {3}, so what you saw "
            + "is not in it - the field is built from the measured presses, and a run far out of "
            + "context falls outside them.",
            noun, richest, richest == 1 ? "" : "s", reported);
    }

    private static void Sort(List<RowMatch> rows) =>
        rows.Sort((a, b) =>
            a.Quality != b.Quality ? b.Quality.CompareTo(a.Quality)
            : a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance)
            : a.Index.CompareTo(b.Index));

    private static int? Single(IEnumerable<RowMatch> rows)
    {
        int[] distinct = rows.Select(r => r.Advances).Distinct().ToArray();
        return distinct.Length == 1 ? distinct[0] : null;
    }

    private const int TiedFramesListed = 4;

    private static string Describe(IReadOnlyList<RowMatch> rows, int? used, double frameMs)
    {
        if (rows.Count == 0) return "This run recorded no field to check against.";

        RowMatch near = rows[0];

        int[] tied = rows
            .Where(r => r.Quality == near.Quality && r.Distance == near.Distance)
            .Select(r => r.Advances)
            .Distinct()
            .Order()
            .ToArray();

        string aside = near.Quality != MatchQuality.None
            ? "equally close - report more to separate them"
            : string.Format(CultureInfo.InvariantCulture,
                "equally close, and none of them fits - {0} movements out",
                near.Distance);

        string frame = tied.Length switch
        {
            1 => string.Format(CultureInfo.InvariantCulture, "Nearest frame → {0}", near.Advances),

            <= TiedFramesListed => string.Format(CultureInfo.InvariantCulture,
                "Nearest frames → {0} ({1})", string.Join(", ", tied), aside),

            _ => string.Format(CultureInfo.InvariantCulture,
                "Nearest frames → {0} of them, {1} to {2} ({3})",
                tied.Length, tied.Min(), tied.Max(), aside),
        };

        if (used is not { } against)
        {
            RowMatch[] usedRows = rows.Where(r => r.WasUsed).ToArray();

            return frame + "\r\n" + (usedRows.Length switch
            {
                0 => "The run settled on no frame, so there is nothing to be out of context by.",

                _ => string.Format(CultureInfo.InvariantCulture,
                    "The run's own report left {0} frames standing ({1}–{2}), so there is nothing "
                    + "single to be out of context by.",
                    usedRows.Length, usedRows.Min(r => r.Advances), usedRows.Max(r => r.Advances)),
            });
        }

        if (tied.Length > 1)
        {
            return frame + "\r\n" + string.Format(CultureInfo.InvariantCulture,
                "That is {0} to {1} ms out of context - the run used {2}.",
                Math.Round((tied.Min() - against) * frameMs),
                Math.Round((tied.Max() - against) * frameMs),
                against);
        }

        int outBy = near.Advances - against;
        double ms = outBy * frameMs;

        return frame + "\r\n" + (outBy == 0
            ? "This frame was 0 ms out of context - the same one the run used."
            : string.Format(CultureInfo.InvariantCulture,
                "This frame was {0:+#;-#} ms out of context ({1:+#;-#} frame{2}).",
                Math.Round(ms), outBy, Math.Abs(outBy) == 1 ? "" : "s"));
    }
}
