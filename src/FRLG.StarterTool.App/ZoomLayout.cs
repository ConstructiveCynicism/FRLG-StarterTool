namespace FRLG.StarterTool.App;

public static class ZoomLayout
{
    private const int ClearTypeBleed = 2;

    internal readonly record struct FontSpec(FontFamily Family, float Size, FontStyle Style, GraphicsUnit Unit)
    {
        internal static FontSpec Of(Font font) => new(font.FontFamily, font.Size, font.Style, font.Unit);

        internal Font Scaled(float factor) => new(Family, Size * factor, Style, Unit);
    }

    public sealed class Baseline
    {
        internal readonly Dictionary<Control, Rectangle> Bounds = new();
        internal readonly Dictionary<Control, FontSpec> Fonts = new();
        internal readonly Dictionary<ColumnHeader, int> Columns = new();
        internal readonly Dictionary<Control, bool> AutoSizes = new();

        internal readonly List<Font> Created = new();

        internal FontSpec FormFont;
        internal Size ClientSize;

        public int ClientWidth => ClientSize.Width;
    }

    public static Baseline Capture(Form root)
    {
        var baseline = new Baseline { FormFont = FontSpec.Of(root.Font), ClientSize = root.ClientSize };
        Collect(root, baseline);
        return baseline;
    }

    private static void Collect(Control parent, Baseline baseline)
    {
        foreach (Control child in parent.Controls)
        {
            baseline.Bounds[child] = child.Bounds;

            baseline.Fonts[child] = FontSpec.Of(child.Font);
            baseline.AutoSizes[child] = child.AutoSize;

            if (child is ListView list)
            {
                foreach (ColumnHeader column in list.Columns) baseline.Columns[column] = column.Width;
            }

            Collect(child, baseline);
        }
    }

    public static void Apply(Form root, Baseline baseline, float factor, params Control[] keepNativeSize)
    {
        var replaced = new List<Font>(baseline.Created);
        baseline.Created.Clear();

        Scale(root, baseline, factor, keepNativeSize);
        root.Font = Take(baseline, baseline.FormFont, factor);

        foreach (ColumnHeader column in baseline.Columns.Keys)
        {
            column.Width = Round(baseline.Columns[column] * factor);
        }

        foreach (Font font in replaced) font.Dispose();
    }

    private static void Scale(Control parent, Baseline baseline, float factor, Control[] keepNativeSize)
    {
        foreach (Control child in parent.Controls)
        {
            if (!baseline.Bounds.TryGetValue(child, out Rectangle bounds)) continue;

            bool nativeHeight = Array.IndexOf(keepNativeSize, child) >= 0;

            child.Font = Take(baseline, baseline.Fonts[child], factor);

            if (child is TextBoxBase text) text.AutoSize = factor == 1F && baseline.AutoSizes[child];

            int left = Round(bounds.Left * factor);
            int top = Round(bounds.Top * factor);
            int width = Round(bounds.Right * factor) - left;
            int height = nativeHeight ? bounds.Height : Round(bounds.Bottom * factor) - top;

            child.Bounds = new Rectangle(left, top, width, height);

            if (child is ThemedComboBox combo) combo.MatchHeight(height);

            if (factor != 1F && child is Label caption && !baseline.AutoSizes[child])
            {
                int grownHeight = Math.Max(height, caption.Font.Height);
                int grownWidth = Math.Max(
                    width, TextRenderer.MeasureText(caption.Text, caption.Font).Width + ClearTypeBleed);

                if (grownHeight != height || grownWidth != width)
                {
                    int shift = (grownWidth - width) switch
                    {
                        0 => 0,
                        var overhang => caption.TextAlign switch
                        {
                            ContentAlignment.TopCenter or ContentAlignment.MiddleCenter
                                or ContentAlignment.BottomCenter => overhang / 2,
                            ContentAlignment.TopRight or ContentAlignment.MiddleRight
                                or ContentAlignment.BottomRight => overhang,
                            _ => 0
                        }
                    };

                    caption.Bounds = new Rectangle(
                        Math.Max(0, left - shift),
                        Math.Max(0, top + height - grownHeight),
                        grownWidth,
                        grownHeight);
                }
            }

            Scale(child, baseline, factor, keepNativeSize);
        }
    }

    private static Font Take(Baseline baseline, FontSpec spec, float factor)
    {
        Font scaled = spec.Scaled(factor);
        baseline.Created.Add(scaled);
        return scaled;
    }

    public static int Round(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
