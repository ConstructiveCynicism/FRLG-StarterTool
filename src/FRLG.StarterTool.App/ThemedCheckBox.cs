namespace FRLG.StarterTool.App;

public sealed class ThemedCheckBox : CheckBox
{
    private const int DesignGlyphSize = 13;

    private const int DesignTextGap = 4;

    private const int DesignFontHeight = 16;

    private float GlyphScale => Font.Height / (float)DesignFontHeight;

    private int GlyphSize => Math.Max(9, (int)Math.Round(DesignGlyphSize * GlyphScale));

    private int TextGap => Math.Max(1, (int)Math.Round(DesignTextGap * GlyphScale));

    private bool _hot;

    private bool _pressed;

    public bool BoldWhenChecked { get; set; }

    public ThemedCheckBox()
    {
        FlatStyle = FlatStyle.Flat;
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }

    private Font CaptionFont => BoldWhenChecked && Checked && !Font.Bold
        ? (_bold ??= new Font(Font, FontStyle.Bold))
        : Font;

    private Font? _bold;

    protected override void OnFontChanged(EventArgs e)
    {
        _bold?.Dispose();
        _bold = null;
        base.OnFontChanged(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _bold?.Dispose();
        base.Dispose(disposing);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        Size size = base.GetPreferredSize(proposedSize);
        if (Appearance != Appearance.Normal) return size;

        Font font = BoldWhenChecked && !Font.Bold ? (_bold ??= new Font(Font, FontStyle.Bold)) : Font;
        int width = Text.Length == 0
            ? GlyphSize + 1
            : GlyphSize + TextGap + TextRenderer.MeasureText(Text, font).Width;

        return new Size(Math.Max(size.Width, width), Math.Max(size.Height, GlyphSize + 1));
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        if (BoldWhenChecked) Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hot = true;
        base.OnMouseEnter(e);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hot = false;
        _pressed = false;
        base.OnMouseLeave(e);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) _pressed = true;
        base.OnMouseDown(e);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        base.OnMouseUp(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Appearance != Appearance.Normal)
        {
            base.OnPaint(e);
            return;
        }

        Graphics g = e.Graphics;
        using (var background = new SolidBrush(BackColor))
        {
            g.FillRectangle(background, ClientRectangle);
        }

        int glyphLeft = Text.Length == 0 ? Math.Max(0, (Width - GlyphSize) / 2) : 0;
        var glyph = new Rectangle(glyphLeft, Math.Max(0, (Height - GlyphSize) / 2), GlyphSize, GlyphSize);

        DrawGlyph(g, glyph);

        Font font = CaptionFont;
        var text = new Rectangle(GlyphSize + TextGap, TextTop(font, glyph),
            Width - GlyphSize - TextGap, Height);
        TextRenderer.DrawText(g, Text, font, text, CaptionColor,
            TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        if (Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(g, text);
        }
    }

    private static int TextTop(Font font, Rectangle glyph)
    {
        int height = TextRenderer.MeasureText("0", font).Height;
        return glyph.Top + (int)Math.Ceiling((glyph.Height - height) / 2.0);
    }

    private Color CaptionColor => !Enabled ? Theme.DimText
        : BoldWhenChecked && Checked ? Theme.CheckMark
        : ForeColor;

    private void DrawGlyph(Graphics g, Rectangle glyph)
    {
        Color fillColor = !Enabled ? Theme.Window : _hot || _pressed ? Theme.Hover : Theme.Input;
        using (var fill = new SolidBrush(fillColor))
        {
            g.FillRectangle(fill, glyph);
        }
        using (var pen = new Pen(Theme.Border))
        {
            g.DrawRectangle(pen, glyph.X, glyph.Y, glyph.Width - 1, glyph.Height - 1);
        }

        if (!Checked) return;

        var previous = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        float unit = glyph.Width / (float)DesignGlyphSize;
        using (var pen = new Pen(Enabled ? Theme.CheckMark : Theme.DimText, 1.8f * unit))
        {
            g.DrawLines(pen, new[]
            {
                new PointF(glyph.Left + 2.0f * unit, glyph.Top + 6.0f * unit),
                new PointF(glyph.Left + 5.0f * unit, glyph.Top + 9.0f * unit),
                new PointF(glyph.Left + 10.0f * unit, glyph.Top + 3.0f * unit)
            });
        }
        g.SmoothingMode = previous;
    }
}
