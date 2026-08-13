using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;

namespace FRLG.StarterTool.App;

public sealed class StatBoxPanel : Panel
{
    public const int BoxWidth = 196;
    public const int BoxHeight = 72;

    private const int FrameThickness = 2;

    private int Frame => Math.Max(1, (int)Math.Round(FrameThickness * BoxScale));

    private const int StripTop = 48;

    private const int CaptionBaseline = 20;
    private const int ValueBaseline = 38;
    private const int StripBaseline = 64;

    private const float FontSize = 12f;

    private const float StripFontSize = 11f;

    private const int CellInset = 4;

    private const int CellGap = 6;

    public const string DefaultFillColor = "#3C3C3C";

    private static Color _fillColor = ParseFillColor(DefaultFillColor);

    public static Color FillColor
    {
        get => _fillColor;
        set
        {
            if (_fillColor == value) return;

            _fillColor = value;
            FillColorChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static event EventHandler? FillColorChanged;

    private static readonly string[] StatCaptions = { "HP", "ATK", "DEF", "SPA", "SPD", "SPE" };

    public const string DefaultLabelColor = "#4DC6D6";

    private static Color _labelColor = ParseColor(DefaultLabelColor);

    public static Color LabelColor
    {
        get => _labelColor;
        set
        {
            if (_labelColor == value) return;

            _labelColor = value;
            LabelColorChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static event EventHandler? LabelColorChanged;

    private float BoxScale => Height / (float)BoxHeight;

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

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        DrawFrame(g);

        using var family = new FontFamily("Microsoft Sans Serif");

        float scale = BoxScale;
        int frame = Frame;
        int interior = Width - 2 * frame;
        for (int stat = 0; stat < 6; stat++)
        {
            float centre = frame + interior * (2 * stat + 1) / 12f;
            DrawText(g, StatCaptions[stat], family, FontSize * scale, centre, CaptionBaseline * scale,
                LabelColor, StringAlignment.Center);
            DrawText(
                g,
                _values[stat].ToString(CultureInfo.InvariantCulture),
                family, FontSize * scale, centre, ValueBaseline * scale, Color.White, StringAlignment.Center);
        }

        DrawStrip(g, family);
    }

    private void DrawFrame(Graphics g)
    {
        var pixels = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;

        using (var black = new SolidBrush(Color.Black))
        {
            g.FillRectangle(black, ClientRectangle);
        }
        using (var fill = new SolidBrush(FillColor))
        {
            int frame = Frame;
            int stripTop = (int)Math.Round(StripTop * BoxScale);
            int interior = Width - 2 * frame;
            g.FillRectangle(fill, frame, frame, interior, stripTop - frame);
            g.FillRectangle(
                fill,
                frame,
                stripTop + frame,
                interior,
                Height - stripTop - 2 * frame);
        }

        g.SmoothingMode = pixels;
    }

    private void DrawStrip(Graphics g, FontFamily family)
    {
        float scale = BoxScale;
        float size = StripFontSize * scale;
        float inset = CellInset * scale;
        float gap = CellGap * scale;
        float baseline = StripBaseline * scale;
        float natureRight = Width - Frame;

        if (_trailingCaption.Length > 0)
        {
            float captionWidth = Measure(g, family, _trailingCaption, size);
            float cellWidth = inset + captionWidth + gap + Measure(g, family, _trailingSample, size) + inset;
            natureRight -= cellWidth;

            float captionX = natureRight + inset;
            DrawText(
                g, _trailingCaption, family, size, captionX, baseline, LabelColor, StringAlignment.Near);
            DrawText(
                g, _trailingValue, family, size, captionX + captionWidth + gap, baseline,
                Color.White, StringAlignment.Near);
        }

        float natureCaptionRight = DrawText(
            g, "NATURE", family, size, Frame + inset, baseline, LabelColor,
            StringAlignment.Near);

        DrawText(
            g, _nature, family, size, natureCaptionRight + gap, baseline, Color.White,
            StringAlignment.Near);
    }

    private static float Measure(Graphics g, FontFamily family, string text, float size) =>
        string.IsNullOrEmpty(text)
            ? 0f
            : DrawText(g, text, family, size, 0, 0, Color.Empty, StringAlignment.Near, measureOnly: true) + 1f;

    private static float DrawText(
        Graphics g, string text, FontFamily family, float size, float x, float baseline, Color colour,
        StringAlignment alignment, bool measureOnly = false)
    {
        if (string.IsNullOrEmpty(text)) return x;

        float ascent = size * family.GetCellAscent(FontStyle.Bold) / family.GetEmHeight(FontStyle.Bold);

        using var format = new StringFormat(StringFormat.GenericTypographic) { Alignment = alignment };
        using var path = new GraphicsPath();
        path.AddString(text, family, (int)FontStyle.Bold, size, new PointF(x, baseline - ascent), format);

        if (measureOnly) return path.GetBounds().Width;

        using (var pen = new Pen(Color.Black, 2f) { LineJoin = LineJoin.Round })
        {
            g.DrawPath(pen, path);
        }
        using (var brush = new SolidBrush(colour))
        {
            g.FillPath(brush, path);
        }

        return path.GetBounds().Right;
    }

    public static Color ParseColor(string? hex) => ParseColor(hex, DefaultLabelColor);

    public static Color ParseFillColor(string? hex) => ParseColor(hex, DefaultFillColor);

    private static Color ParseColor(string? hex, string fallback)
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
