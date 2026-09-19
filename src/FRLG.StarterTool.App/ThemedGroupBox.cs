namespace FRLG.StarterTool.App;

public sealed class ThemedGroupBox : GroupBox
{
    private const int TextInset = 9;

    public ThemedGroupBox()
    {
        FlatStyle = FlatStyle.Flat;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is not ScrollBar bar) return;
        bar.VisibleChanged += (_, _) => Invalidate();
        bar.LocationChanged += (_, _) => Invalidate();
        bar.SizeChanged += (_, _) =>
        {
            CentreThumb(bar);
            Invalidate();
        };
        CentreThumb(bar);
    }

    private static int Skew(ScrollBar bar)
    {
        int across = bar is VScrollBar ? bar.Width : bar.Height;
        return across >= 16 || across % 2 == 0 ? 1 : 0;
    }

    private static void CentreThumb(ScrollBar bar)
    {
        Region? old = bar.Region;
        bar.Region = Skew(bar) == 0 ? null : new Region(Shown(bar, Point.Empty));
        old?.Dispose();
    }

    private static Rectangle Shown(ScrollBar bar, Point at)
    {
        int skew = Skew(bar);
        return bar is VScrollBar
            ? new Rectangle(at.X + skew, at.Y, bar.Width - skew, bar.Height)
            : new Rectangle(at.X, at.Y + skew, bar.Width, bar.Height - skew);
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

        using (var pen = new Pen(Theme.Border))
        {
            foreach (Control child in Controls)
            {
                if (child is not ScrollBar { Visible: true } bar) continue;
                Rectangle r = Shown(bar, bar.Location);
                g.DrawRectangle(pen, new Rectangle(r.X - 2, r.Y - 2, r.Width + 3, r.Height + 3));
            }
        }

        if (Text.Length == 0) return;

        using (var background = new SolidBrush(BackColor))
        {
            g.FillRectangle(background, new Rectangle(TextInset - 3, top, caption.Width + 6, 1));
        }
        TextRenderer.DrawText(g, Text, captionFont, new Point(TextInset, 0), ForeColor, TextFormatFlags.NoPadding);
    }
}
