using System.Drawing.Drawing2D;
using System.Drawing.Text;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public readonly record struct PostRunCard(string Hit, string OffScreen, string Anchors)
{
    public string Hit { get; } = Hit ?? "";
    public string OffScreen { get; } = OffScreen ?? "";
    public string Anchors { get; } = Anchors ?? "";

    public const int PairGap = 2;

    public const int PairWidth = 2 * StatBoxPanel.BoxWidth + PairGap;

    public static int WidthOf(bool pair, StatStripSide side)
    {
        int box = StatBoxPanel.WidthOf(side);
        return pair ? 2 * box + PairGap : box;
    }

    private const int RowBaseline = 16;

    private const int RowPitch = 22;

    private const int CondensedRowPitch = 15;

    private const int CondensedRowBaseline = 11;

    private const float FontSize = StatBoxPanel.StripFontSize;

    private const float CondensedFontSize = 9f;

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
        Graphics g, in PostRunCard card, int width, int height, in StatBoxPalette palette,
        StatStripSide side = StatStripSide.Bottom)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        StatBoxPanel.DrawFrame(g, width, height, palette, divider: null, side);

        var rows = card.Rows.ToList();
        if (rows.Count == 0) return;

        using var family = new FontFamily(StatBoxPanel.FontFamilyName);

        bool condensed = side != StatStripSide.Bottom;
        float scale = StatBoxPanel.ScaleOf(height, side);
        float size = (condensed ? CondensedFontSize : FontSize) * scale;
        float stroke = StatBoxPanel.OutlineThickness * scale;
        float pitch = (condensed ? CondensedRowPitch : RowPitch) * scale;
        float baselineInBand = (condensed ? CondensedRowBaseline : RowBaseline) * scale;
        float x = StatBoxPanel.FrameAt(scale) + StatBoxPanel.CellInset * scale;
        float gap = StatBoxPanel.CellGap * scale;
        float top = (height - rows.Count * pitch) / 2f;

        for (int row = 0; row < rows.Count; row++)
        {
            float baseline = top + row * pitch + baselineInBand;

            float captionRight = StatBoxPanel.DrawText(
                g, rows[row].Caption, family, size, x, baseline, palette.Label, palette.Outline,
                stroke, StringAlignment.Near);

            StatBoxPanel.DrawText(
                g, rows[row].Value, family, size, captionRight + gap, baseline, palette.Value,
                palette.Outline, stroke, StringAlignment.Near);
        }
    }

    public static Bitmap Render(
        in PostRunCard card, int scale, bool pair, in StatBoxPalette palette,
        StatStripSide side = StatStripSide.Bottom)
    {
        scale = Math.Clamp(scale, StatBoxPanel.MinRenderScale, StatBoxPanel.MaxRenderScale);
        int width = WidthOf(pair, side) * scale;
        int height = StatBoxPanel.HeightOf(side) * scale;

        var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            using Graphics g = Graphics.FromImage(bitmap);
            g.Clear(Color.Transparent);
            Draw(g, card, width, height, palette, side);
        }
        catch (Exception)
        {
            bitmap.Dispose();
            throw;
        }

        return bitmap;
    }
}
