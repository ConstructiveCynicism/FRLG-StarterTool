namespace FRLG.StarterTool.App;

internal static class GdiCache
{
    private const int Bound = 256;

    private static readonly Dictionary<(Color Color, float Width), Pen> Pens = new();
    private static readonly Dictionary<Color, SolidBrush> Brushes = new();

    public static Pen Pen(Color color, float width)
    {
        if (Pens.TryGetValue((color, width), out Pen? pen)) return pen;

        if (Pens.Count >= Bound)
        {
            foreach (Pen old in Pens.Values) old.Dispose();
            Pens.Clear();
        }

        pen = new Pen(color, width);
        Pens[(color, width)] = pen;
        return pen;
    }

    public static SolidBrush Brush(Color color)
    {
        if (Brushes.TryGetValue(color, out SolidBrush? brush)) return brush;

        if (Brushes.Count >= Bound)
        {
            foreach (SolidBrush old in Brushes.Values) old.Dispose();
            Brushes.Clear();
        }

        brush = new SolidBrush(color);
        Brushes[color] = brush;
        return brush;
    }
}
