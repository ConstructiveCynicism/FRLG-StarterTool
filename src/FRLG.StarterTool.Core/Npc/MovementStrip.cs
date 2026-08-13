namespace FRLG.StarterTool.Core.Npc;

public readonly record struct StripMove(int Frame, Direction Direction, bool Complete = true);

public readonly record struct StripToken(Direction Direction, bool Complete = true)
{
    public static readonly StripToken Quiet = new(Direction.None);

    public bool IsQuiet => Direction == Direction.None;

    public override string ToString() =>
        IsQuiet ? "-" : Directions.Letter(Direction) + (Complete ? "" : "~");
}

public static class MovementStrip
{
    public const int QuietIntervalFrames = 32;

    public static int SlotOf(int frame) => Math.Max(0, (frame - 1) / QuietIntervalFrames);

    public static IReadOnlyList<StripToken> Layout(IEnumerable<StripMove> moves)
    {
        var tokens = new List<StripToken>();

        foreach (StripMove move in moves)
        {
            for (int slot = tokens.Count; slot < SlotOf(move.Frame); slot++)
            {
                tokens.Add(StripToken.Quiet);
            }

            tokens.Add(new StripToken(move.Direction, move.Complete));
        }

        return tokens;
    }

    public static string Format(IEnumerable<StripToken> tokens) =>
        string.Join(" ", tokens.Select(t => t.ToString()));

    public static bool TryParse(string? text, out List<StripToken> tokens, out char offending)
    {
        tokens = new List<StripToken>();
        offending = '\0';

        foreach (char c in text ?? "")
        {
            if (char.IsWhiteSpace(c) || c is ',' or '.' or '/' or '|') continue;

            if (c is '-' or '_' or '–' or '—')
            {
                tokens.Add(StripToken.Quiet);
                continue;
            }

            if (c == '~')
            {
                if (tokens.Count == 0 || tokens[^1].IsQuiet)
                {
                    offending = c;
                    return false;
                }

                tokens[^1] = tokens[^1] with { Complete = false };
                continue;
            }

            Direction direction = Directions.FromLetter(c);
            if (direction == Direction.None)
            {
                offending = c;
                return false;
            }

            tokens.Add(new StripToken(direction));
        }

        return true;
    }
}
