namespace FRLG.StarterTool.Core.Voice;

public enum SpokenKind
{
    Digits,

    Number
}

public readonly record struct SpokenId(string Digits, SpokenKind Kind);

public static class SpokenNumber
{
    public const int MaxValue = 65535;

    private static readonly string[] Units =
        { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };

    private static readonly string[] Teens =
        { "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };

    private static readonly string[] Tens =
        { "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

    public static IReadOnlyList<string> Vocabulary { get; } =
        Units.Append("oh").Concat(Teens).Concat(Tens)
            .Concat(new[] { "hundred", "thousand", "and", "double", "triple" })
            .ToArray();

    public static bool TryParse(IReadOnlyList<string> words, out SpokenId id)
    {
        id = default;

        var tokens = new List<string>(words.Count);
        foreach (string word in words)
        {
            string token = word.Trim().ToLowerInvariant();
            if (token.Length == 0) continue;
            if (!Vocabulary.Contains(token)) return false;
            tokens.Add(token);
        }

        if (tokens.Count == 0) return false;

        bool repeated = tokens.Contains("double") || tokens.Contains("triple");
        if (repeated)
        {
            if (!ExpandRepeats(tokens, out tokens)) return false;
        }

        if (tokens.All(token => DigitOf(token) >= 0))
        {
            id = new SpokenId(string.Concat(tokens.Select(token => (char)('0' + DigitOf(token)))), SpokenKind.Digits);
            return true;
        }

        if (!repeated && TryCardinal(tokens, out int value))
        {
            id = new SpokenId(value.ToString(System.Globalization.CultureInfo.InvariantCulture), SpokenKind.Number);
            return true;
        }

        if (TryChunks(tokens, out string digits))
        {
            id = new SpokenId(digits, SpokenKind.Digits);
            return true;
        }

        return false;
    }

    public static bool EndsOpen(IReadOnlyList<string> words)
    {
        string? last = words.LastOrDefault(word => word.Trim().Length > 0)?.Trim().ToLowerInvariant();
        return last != null && (Tens.Contains(last) || last is "double" or "triple" or "and");
    }

    private static int DigitOf(string token) => token == "oh" ? 0 : Array.IndexOf(Units, token);

    private static bool ExpandRepeats(List<string> tokens, out List<string> expanded)
    {
        expanded = new List<string>(tokens.Count + 4);
        for (int i = 0; i < tokens.Count; i++)
        {
            int count = tokens[i] switch { "double" => 2, "triple" => 3, _ => 1 };
            if (count == 1)
            {
                expanded.Add(tokens[i]);
                continue;
            }

            if (i + 1 >= tokens.Count || DigitOf(tokens[i + 1]) < 0) return false;
            for (int n = 0; n < count; n++) expanded.Add(tokens[i + 1]);
            i++;
        }

        return true;
    }

    private enum Last { None, Unit, Teen, Tens, Hundred, Thousand, And }

    private static bool TryCardinal(List<string> tokens, out int value)
    {
        value = 0;
        int total = 0;
        int current = 0;
        bool thousand = false;
        bool hundred = false;
        Last last = Last.None;

        foreach (string token in tokens)
        {
            bool groupStart = last is Last.None or Last.Hundred or Last.Thousand or Last.And;
            int unit = Array.IndexOf(Units, token);
            int teen = Array.IndexOf(Teens, token);
            int tens = Array.IndexOf(Tens, token);

            if (unit >= 1)
            {
                if (!groupStart && last != Last.Tens) return false;
                current += unit;
                last = Last.Unit;
            }
            else if (teen >= 0)
            {
                if (!groupStart) return false;
                current += 10 + teen;
                last = Last.Teen;
            }
            else if (tens >= 0)
            {
                if (!groupStart) return false;
                current += (tens + 2) * 10;
                last = Last.Tens;
            }
            else if (token == "hundred")
            {
                if (last is not (Last.Unit or Last.Teen or Last.Tens) || hundred || current is < 1 or > 99) return false;
                current *= 100;
                hundred = true;
                last = Last.Hundred;
            }
            else if (token == "thousand")
            {
                if (last is not (Last.Unit or Last.Teen or Last.Tens or Last.Hundred) || thousand || current is < 1 or > 999) return false;
                total = current * 1000;
                current = 0;
                thousand = true;
                hundred = false;
                last = Last.Thousand;
            }
            else if (token == "and")
            {
                if (last is not (Last.Hundred or Last.Thousand)) return false;
                last = Last.And;
            }
            else
            {
                return false;
            }
        }

        if (last == Last.And) return false;

        value = total + current;
        return value is >= 1 and <= MaxValue;
    }

    private static bool TryChunks(List<string> tokens, out string digits)
    {
        var text = new System.Text.StringBuilder();
        digits = "";

        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];
            int digit = DigitOf(token);
            int teen = Array.IndexOf(Teens, token);
            int tens = Array.IndexOf(Tens, token);

            if (digit >= 0)
            {
                text.Append((char)('0' + digit));
            }
            else if (teen >= 0)
            {
                text.Append(10 + teen);
            }
            else if (tens >= 0)
            {
                text.Append((char)('2' + tens));
                int next = i + 1 < tokens.Count ? Array.IndexOf(Units, tokens[i + 1]) : -1;
                if (next >= 1)
                {
                    text.Append((char)('0' + next));
                    i++;
                }
                else
                {
                    text.Append('0');
                }
            }
            else
            {
                return false;
            }
        }

        digits = text.ToString();
        return digits.Length > 0;
    }
}
