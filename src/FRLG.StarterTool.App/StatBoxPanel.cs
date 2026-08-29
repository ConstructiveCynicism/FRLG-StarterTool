using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public sealed class StatBoxPanel : Panel
{
    public const int BoxWidth = 196;
    public const int BoxHeight = 72;

    internal const int FrameThickness = 2;

    internal static int FrameAt(float scale) => Math.Max(1, (int)Math.Round(FrameThickness * scale));

    private const int StripTop = 48;

    public const int SideBoxWidth = BoxWidth + SideStripWidth;

    public const int SideBoxHeight = StripTop + FrameThickness;

    internal const int SideStripWidth = 108;

    private const int SideTopBaseline = CaptionBaseline + (SideBoxHeight - StripTop) / 2;

    private const int SideBottomBaseline = ValueBaseline + (SideBoxHeight - StripTop) / 2;

    public static int WidthOf(StatStripSide side) =>
        side == StatStripSide.Bottom ? BoxWidth : SideBoxWidth;

    public static int HeightOf(StatStripSide side) =>
        side == StatStripSide.Bottom ? BoxHeight : SideBoxHeight;

    private const int CaptionBaseline = 20;
    private const int ValueBaseline = 38;
    private const int StripBaseline = 64;

    private const float FontSize = 12f;

    internal const float StripFontSize = 11f;

    internal const float OutlineThickness = 2f;

    internal const int CellInset = 4;

    internal const int CellGap = 6;

    public const string DefaultFillColor = "#3C3C3C";

    public const string DefaultLabelColor = "#4DC6D6";

    public const string DefaultValueColor = "#FFFFFF";

    public const string DefaultOutlineColor = "#000000";

    public const string DefaultFrameColor = "#000000";

    private static Color _labelColor = ParseColor(DefaultLabelColor, DefaultLabelColor);
    private static Color _fillColor = ParseColor(DefaultFillColor, DefaultFillColor);
    private static Color _valueColor = ParseColor(DefaultValueColor, DefaultValueColor);
    private static Color _outlineColor = ParseColor(DefaultOutlineColor, DefaultOutlineColor);
    private static Color _frameColor = ParseColor(DefaultFrameColor, DefaultFrameColor);

    private static void SetColor(ref Color field, Color value)
    {
        if (field == value) return;

        field = value;
        ColorsChanged?.Invoke(null, EventArgs.Empty);
    }

    public static event EventHandler? ColorsChanged;

    public static Color LabelColor
    {
        get => _labelColor;
        set => SetColor(ref _labelColor, value);
    }

    public static Color FillColor
    {
        get => _fillColor;
        set => SetColor(ref _fillColor, value);
    }

    public static Color ValueColor
    {
        get => _valueColor;
        set => SetColor(ref _valueColor, value);
    }

    public static Color OutlineColor
    {
        get => _outlineColor;
        set => SetColor(ref _outlineColor, value);
    }

    public static Color FrameColor
    {
        get => _frameColor;
        set => SetColor(ref _frameColor, value);
    }

    private static readonly string[] StatCaptions = { "HP", "ATK", "DEF", "SPA", "SPD", "SPE" };

    internal const string FontFamilyName = "Microsoft Sans Serif";

    internal static float ScaleOf(int height, StatStripSide side = StatStripSide.Bottom) =>
        height / (float)HeightOf(side);

    private int[] _values = new int[6];
    private string _nature = "";
    private string _trailingCaption = "";
    private string _trailingSample = "";
    private string _trailingValue = "";

    public StatBoxPanel()
    {
        Size = new Size(BoxWidth, BoxHeight);
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
    }

    public void SetTrailingCell(string caption, string widestValue)
    {
        _trailingCaption = caption ?? "";
        _trailingSample = widestValue ?? "";
        Invalidate();
    }

    public string TrailingValue
    {
        get => _trailingValue;
        set
        {
            string text = value ?? "";
            if (_trailingValue == text) return;

            _trailingValue = text;
            Invalidate();
        }
    }

    public void SetValues(int[] values, string nature)
    {
        _values = values;
        _nature = nature;
        Invalidate();
    }

    public void Clear() => SetValues(new int[6], "");

    public void RefreshColors() => Invalidate();

    public StatBoxContent Content =>
        new((int[])_values.Clone(), _nature, _trailingCaption, _trailingSample, _trailingValue);

    protected override void OnPaint(PaintEventArgs e)
    {
        Draw(e.Graphics, Content, Width, Height, StatBoxPalette.Current);
    }

    public static void Draw(
        Graphics g, in StatBoxContent content, int width, int height, in StatBoxPalette palette,
        StatStripSide side = StatStripSide.Bottom)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        bool beside = side != StatStripSide.Bottom;

        DrawFrame(g, width, height, palette, beside ? null : StripTop, side);

        using var family = new FontFamily(FontFamilyName);

        float scale = ScaleOf(height, side);
        int frame = FrameAt(scale);
        float strip = beside ? SideStripWidth * scale : 0;
        float left = frame + (side == StatStripSide.Left ? strip : 0);
        float interior = width - 2 * frame - strip;
        float captionBaseline = (beside ? SideTopBaseline : CaptionBaseline) * scale;
        float valueBaseline = (beside ? SideBottomBaseline : ValueBaseline) * scale;
        int[] values = content.Values;
        float stroke = OutlineThickness * scale;
        for (int stat = 0; stat < 6; stat++)
        {
            float centre = left + interior * (2 * stat + 1) / 12f;
            DrawText(g, StatCaptions[stat], family, FontSize * scale, centre, captionBaseline,
                palette.Label, palette.Outline, stroke, StringAlignment.Center);
            DrawText(
                g,
                (stat < values.Length ? values[stat] : 0).ToString(CultureInfo.InvariantCulture),
                family, FontSize * scale, centre, valueBaseline, palette.Value, palette.Outline,
                stroke, StringAlignment.Center);
        }

        if (beside) DrawSideStrip(g, family, content, width, height, palette, side);
        else DrawStrip(g, family, content, width, height, palette);
    }

    public static Bitmap Render(
        in StatBoxContent content, int scale, in StatBoxPalette palette,
        StatStripSide side = StatStripSide.Bottom)
    {
        scale = Math.Clamp(scale, MinRenderScale, MaxRenderScale);
        int width = WidthOf(side) * scale;
        int height = HeightOf(side) * scale;

        var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            using Graphics g = Graphics.FromImage(bitmap);
            g.Clear(Color.Transparent);
            Draw(g, content, width, height, palette, side);
        }
        catch (Exception)
        {
            bitmap.Dispose();
            throw;
        }

        return bitmap;
    }

    public const int MinRenderScale = 1;

    public const int MaxRenderScale = 8;

    internal static void DrawFrame(
        Graphics g, int width, int height, in StatBoxPalette palette, int? divider = StripTop,
        StatStripSide side = StatStripSide.Bottom)
    {
        var pixels = g.SmoothingMode;
        var blending = g.CompositingMode;
        g.SmoothingMode = SmoothingMode.None;

        g.CompositingMode = CompositingMode.SourceCopy;

        float scale = ScaleOf(height, side);
        using (var border = new SolidBrush(palette.Frame))
        {
            g.FillRectangle(border, 0, 0, width, height);
        }
        using (var fill = new SolidBrush(palette.Fill))
        {
            int frame = FrameAt(scale);
            int interior = width - 2 * frame;
            if (divider == null)
            {
                g.FillRectangle(fill, frame, frame, interior, height - 2 * frame);
            }
            else
            {
                int stripTop = (int)Math.Round(divider.Value * scale);
                g.FillRectangle(fill, frame, frame, interior, stripTop - frame);
                g.FillRectangle(
                    fill,
                    frame,
                    stripTop + frame,
                    interior,
                    height - stripTop - 2 * frame);
            }
        }

        g.SmoothingMode = pixels;
        g.CompositingMode = blending;
    }

    private static void DrawStrip(
        Graphics g, FontFamily family, in StatBoxContent content, int width, int height,
        in StatBoxPalette palette)
    {
        float scale = ScaleOf(height);
        float size = StripFontSize * scale;
        float inset = CellInset * scale;
        float gap = CellGap * scale;
        float baseline = StripBaseline * scale;
        float stroke = OutlineThickness * scale;
        int frame = FrameAt(scale);
        float natureRight = width - frame;

        if (content.TrailingCaption is { Length: > 0 })
        {
            float captionWidth = Measure(g, family, content.TrailingCaption, size, stroke);
            float cellWidth = inset + captionWidth + gap
                              + Measure(g, family, content.TrailingSample, size, stroke) + inset;
            natureRight -= cellWidth;

            float captionX = natureRight + inset;
            DrawText(
                g, content.TrailingCaption, family, size, captionX, baseline, palette.Label,
                palette.Outline, stroke, StringAlignment.Near);
            DrawText(
                g, content.TrailingValue, family, size, captionX + captionWidth + gap, baseline,
                palette.Value, palette.Outline, stroke, StringAlignment.Near);
        }

        float natureCaptionRight = DrawText(
            g, "NATURE", family, size, frame + inset, baseline, palette.Label, palette.Outline,
            stroke, StringAlignment.Near);

        DrawText(
            g, content.Nature, family, size, natureCaptionRight + gap, baseline, palette.Value,
            palette.Outline, stroke, StringAlignment.Near);
    }

    private static void DrawSideStrip(
        Graphics g, FontFamily family, in StatBoxContent content, int width, int height,
        in StatBoxPalette palette, StatStripSide side)
    {
        float scale = ScaleOf(height, side);
        float size = StripFontSize * scale;
        float stroke = OutlineThickness * scale;
        float gap = CellGap * scale;
        int frame = FrameAt(scale);
        float x = (side == StatStripSide.Left ? frame : width - frame - SideStripWidth * scale)
                  + CellInset * scale;

        float captions = Measure(g, family, "NATURE", size, stroke);
        if (content.TrailingCaption is { Length: > 0 })
        {
            captions = Math.Max(captions, Measure(g, family, content.TrailingCaption, size, stroke));
        }

        float valueX = x + captions + gap;

        DrawText(
            g, "NATURE", family, size, x, SideTopBaseline * scale, palette.Label, palette.Outline,
            stroke, StringAlignment.Near);
        DrawText(
            g, content.Nature, family, size, valueX, SideTopBaseline * scale, palette.Value,
            palette.Outline, stroke, StringAlignment.Near);

        if (content.TrailingCaption is not { Length: > 0 }) return;

        DrawText(
            g, content.TrailingCaption, family, size, x, SideBottomBaseline * scale, palette.Label,
            palette.Outline, stroke, StringAlignment.Near);
        DrawText(
            g, content.TrailingValue, family, size, valueX, SideBottomBaseline * scale, palette.Value,
            palette.Outline, stroke, StringAlignment.Near);
    }

    private static float Measure(Graphics g, FontFamily family, string text, float size, float stroke) =>
        string.IsNullOrEmpty(text)
            ? 0f
            : DrawText(
                g, text, family, size, 0, 0, Color.Empty, Color.Empty, stroke, StringAlignment.Near,
                measureOnly: true) + stroke / 2f;

    internal static float DrawText(
        Graphics g, string text, FontFamily family, float size, float x, float baseline, Color colour,
        Color outline, float stroke, StringAlignment alignment, bool measureOnly = false)
    {
        if (string.IsNullOrEmpty(text)) return x;

        float ascent = size * family.GetCellAscent(FontStyle.Bold) / family.GetEmHeight(FontStyle.Bold);

        using var format = new StringFormat(StringFormat.GenericTypographic) { Alignment = alignment };
        using var path = new GraphicsPath();
        path.AddString(text, family, (int)FontStyle.Bold, size, new PointF(x, baseline - ascent), format);

        if (measureOnly) return path.GetBounds().Width;

        using (var pen = new Pen(outline, stroke) { LineJoin = LineJoin.Round })
        {
            g.DrawPath(pen, path);
        }
        using (var brush = new SolidBrush(colour))
        {
            g.FillPath(brush, path);
        }

        return path.GetBounds().Right;
    }

    public static Color ParseColor(string? hex, string fallback)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(hex)) return ColorTranslator.FromHtml(hex.Trim());
        }
        catch (Exception)
        {
        }

        return ColorTranslator.FromHtml(fallback);
    }

    public static string ToHex(Color colour) =>
        string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", colour.R, colour.G, colour.B);
}

public readonly record struct StatBoxPalette(Color Label, Color Value, Color Fill, Color Outline, Color Frame)
{
    public static StatBoxPalette Current =>
        new(StatBoxPanel.LabelColor, StatBoxPanel.ValueColor, StatBoxPanel.FillColor,
            StatBoxPanel.OutlineColor, StatBoxPanel.FrameColor);
}

public readonly record struct StatBoxContent(
    int[] Values, string Nature, string TrailingCaption, string TrailingSample, string TrailingValue)
{
    public int[] Values { get; } = Values ?? new int[6];
    public string Nature { get; } = Nature ?? "";
    public string TrailingCaption { get; } = TrailingCaption ?? "";
    public string TrailingSample { get; } = TrailingSample ?? "";
    public string TrailingValue { get; } = TrailingValue ?? "";

    public bool Equals(StatBoxContent other) =>
        Nature == other.Nature
        && TrailingCaption == other.TrailingCaption
        && TrailingSample == other.TrailingSample
        && TrailingValue == other.TrailingValue
        && ((ReadOnlySpan<int>)(Values ?? Array.Empty<int>()))
            .SequenceEqual(other.Values ?? Array.Empty<int>());

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Nature);
        hash.Add(TrailingCaption);
        hash.Add(TrailingSample);
        hash.Add(TrailingValue);
        foreach (int value in Values ?? Array.Empty<int>()) hash.Add(value);
        return hash.ToHashCode();
    }
}
