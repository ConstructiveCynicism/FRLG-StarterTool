using System.Globalization;

namespace FRLG.StarterTool.Core.Tips;

public static class TipAttemptLog
{
    public const string Prefix = "  attempt ";

    private const string NoDelta = "none";

    public static string Format(TipAttempt attempt) => string.Format(CultureInfo.InvariantCulture,
        "{0}delta={1} offset={2} chance={3:F4}",
        Prefix,
        attempt.DeltaMs is { } delta ? delta.ToString("F1", CultureInfo.InvariantCulture) : NoDelta,
        attempt.OffsetMs,
        attempt.HitChance);

    public static bool TryParse(string line, out TipAttempt attempt)
    {
        attempt = new TipAttempt();

        if (line == null) return false;

        string body = line.TrimEnd();
        int at = body.IndexOf(Prefix.Trim(), StringComparison.Ordinal);
        if (at < 0) return false;

        double? delta = null;
        int? offset = null;
        double? chance = null;

        foreach (string field in body[(at + Prefix.Trim().Length)..]
                     .Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            int split = field.IndexOf('=');
            if (split <= 0) return false;

            string key = field[..split];
            string value = field[(split + 1)..];

            switch (key)
            {
                case "delta":
                    if (value == NoDelta) break;
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
                            out double parsedDelta)
                        || double.IsNaN(parsedDelta) || double.IsInfinity(parsedDelta))
                    {
                        return false;
                    }
                    delta = parsedDelta;
                    break;

                case "offset":
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture,
                            out int parsedOffset))
                    {
                        return false;
                    }
                    offset = parsedOffset;
                    break;

                case "chance":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
                            out double parsedChance)
                        || double.IsNaN(parsedChance))
                    {
                        return false;
                    }
                    chance = Math.Clamp(parsedChance, 0.0, 1.0);
                    break;

                default:
                    break;
            }
        }

        if (offset is not { } offsetMs || chance is not { } hitChance) return false;

        attempt = new TipAttempt
        {
            DeltaMs = delta,
            OffsetMs = offsetMs,
            HitChance = hitChance
        };
        return true;
    }
}
