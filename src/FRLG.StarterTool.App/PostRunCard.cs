using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace FRLG.StarterTool.App;

public readonly record struct PostRunCard(string Hit, string OffScreen, string Anchors)
{
    public string Hit { get; } = Hit ?? "";
    public string OffScreen { get; } = OffScreen ?? "";
    public string Anchors { get; } = Anchors ?? "";

    public const int PairGap = 2;

    public const int PairWidth = 2 * StatBoxPanel.BoxWidth + PairGap;

    private const int RowBaseline = 16;

    private const int RowPitch = 22;

    private const float FontSize = StatBoxPanel.StripFontSize;

    public bool Any => Hit.Length > 0 || OffScreen.Length > 0 || Anchors.Length > 0;

    private IEnumerable<(string Caption, string Value)> Rows
    {
        get
        {
            if (Hit.Length > 0) yield return ("HIT", Hit);
            if (OffScreen.Length > 0) yield return ("OFF SCREEN", OffScreen);
            if (Anchors.Length > 0) yield return ("ANCHORS", Anchors);
        }
    }

    public static void Draw(
        Graphics g, in PostRunCard card, int width, int height, in StatBoxPalette palette)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        StatBoxPanel.DrawFrame(g, width, height, palette, divider: null);

        var rows = card.Rows.ToList();
        if (rows.Count == 0) return;

        using var family = new FontFamily(StatBoxPanel.FontFamilyName);

        float scale = StatBoxPanel.ScaleOf(height);
        float size = FontSize * scale;
        float stroke = StatBoxPanel.OutlineThickness * scale;
        float pitch = RowPitch * scale;
        float x = StatBoxPanel.FrameAt(scale) + StatBoxPanel.CellInset * scale;
        float gap = StatBoxPanel.CellGap * scale;
        float top = (height - rows.Count * pitch) / 2f;

        for (int row = 0; row < rows.Count; row++)
        {
            float baseline = top + row * pitch + RowBaseline * scale;

            float captionRight = StatBoxPanel.DrawText(
                g, rows[row].Caption, family, size, x, baseline, palette.Label, palette.Outline,
                stroke, StringAlignment.Near);

            StatBoxPanel.DrawText(
                g, rows[row].Value, family, size, captionRight + gap, baseline, palette.Value,
                palette.Outline, stroke, StringAlignment.Near);
        }
    }

    public static Bitmap Render(in PostRunCard card, int scale, bool pair, in StatBoxPalette palette)
    {
        scale = Math.Clamp(scale, StatBoxPanel.MinRenderScale, StatBoxPanel.MaxRenderScale);
        int width = (pair ? PairWidth : StatBoxPanel.BoxWidth) * scale;
        int height = StatBoxPanel.BoxHeight * scale;

        var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            using Graphics g = Graphics.FromImage(bitmap);
            g.Clear(Color.Transparent);
            Draw(g, card, width, height, palette);
        }
        catch (Exception)
        {
            bitmap.Dispose();
            throw;
        }

        return bitmap;
    }
}
