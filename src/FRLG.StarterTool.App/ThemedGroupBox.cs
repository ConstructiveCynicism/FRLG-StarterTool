namespace FRLG.StarterTool.App;

public sealed class ThemedGroupBox : GroupBox
{
    private const int TextInset = 9;

    public ThemedGroupBox()
    {
        FlatStyle = FlatStyle.Flat;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;

        using (var background = new SolidBrush(BackColor))
        {
            g.FillRectangle(background, ClientRectangle);
        }

        using var captionFont = new Font(Font, FontStyle.Bold);

        Size caption = Text.Length == 0
            ? Size.Empty
            : TextRenderer.MeasureText(g, Text, captionFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);

        int top = caption.Height / 2;
        using (var pen = new Pen(Theme.SectionBorder))
        {
            g.DrawRectangle(pen, new Rectangle(0, top, Width - 1, Height - 1 - top));
        }

        if (Text.Length == 0) return;

        using (var background = new SolidBrush(BackColor))
        {
            g.FillRectangle(background, new Rectangle(TextInset - 3, top, caption.Width + 6, 1));
        }
        TextRenderer.DrawText(g, Text, captionFont, new Point(TextInset, 0), ForeColor, TextFormatFlags.NoPadding);
    }
}
