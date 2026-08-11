namespace FRLG.StarterTool.Core.Npc;

public enum Direction
{
    None = 0,
    South = 1,
    North = 2,
    West = 3,
    East = 4,
}

public static class Directions
{
    public static readonly Direction[] Standard =
    {
        Direction.South, Direction.North, Direction.West, Direction.East,
    };

    public static readonly Direction[] UpAndDown =
    {
        Direction.South, Direction.North,
    };

    public static readonly Direction[] LeftAndRight =
    {
        Direction.West, Direction.East,
    };

    public static void MoveCoords(Direction direction, ref int x, ref int y)
    {
        switch (direction)
        {
            case Direction.South: y += 1; break;
            case Direction.North: y -= 1; break;
            case Direction.West: x -= 1; break;
            case Direction.East: x += 1; break;
        }
    }

    public static string Letter(Direction direction) => direction switch
    {
        Direction.South => "S",
        Direction.North => "N",
        Direction.West => "W",
        Direction.East => "E",
        _ => "-",
    };

    public static Direction FromLetter(char c) => char.ToUpperInvariant(c) switch
    {
        'S' or 'D' => Direction.South,
        'N' or 'U' => Direction.North,
        'W' or 'L' => Direction.West,
        'E' or 'R' => Direction.East,
        _ => Direction.None,
    };

    public static bool TryParse(string? text, out List<Direction> directions, out char offending)
    {
        directions = new List<Direction>();
        offending = '\0';

        foreach (char c in text ?? "")
        {
            if (char.IsWhiteSpace(c) || c is ',' or '-' or '>' or '.' or '/') continue;

            Direction direction = FromLetter(c);
            if (direction == Direction.None)
            {
                offending = c;
                return false;
            }
            directions.Add(direction);
        }

        return true;
    }

    public static string Format(IEnumerable<Direction> directions) =>
        string.Join(" ", directions.Select(Letter));
}
