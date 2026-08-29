namespace FRLG.StarterTool.App;

public sealed class ThemedButton : Button
{
    protected override bool ShowFocusCues => false;

    private bool _hot;

    private bool _pressed;

    public string? Glyph { get; set; }

    public const string CrossGlyph = "×";

    public Color? GlyphColor { get; set; }

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

    private Color FaceColor
    {
        get
        {
            if (!Enabled) return BackColor;
            if (_pressed && FlatAppearance.MouseDownBackColor != Color.Empty) return FlatAppearance.MouseDownBackColor;
            if (_hot && FlatAppearance.MouseOverBackColor != Color.Empty) return FlatAppearance.MouseOverBackColor;
            return BackColor;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (string.IsNullOrEmpty(Glyph) && Text.Length > 0)
        {
            Rectangle inner = ClientRectangle;
            inner.Inflate(-1, -1);
            using (var background = new SolidBrush(FaceColor))
            {
                e.Graphics.FillRectangle(background, inner);
            }

            Color caption = Enabled ? ForeColor : Theme.Dark ? Theme.DimText : SystemColors.GrayText;
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, caption,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        if (string.IsNullOrEmpty(Glyph)) return;

        if (Glyph == CrossGlyph) DrawCross(e.Graphics);
        else DrawGlyph(e.Graphics, Glyph);
    }

    private void DrawCross(Graphics g)
    {
        Color colour = GlyphColor
            ?? (Enabled ? ForeColor : Theme.Dark ? Theme.DimText : SystemColors.GrayText);

        float arm = Math.Max(3f, Font.Height * 0.26f);
        float thickness = Math.Max(1.4f, arm * 0.34f);
        float cx = (ClientRectangle.Width - 1) / 2f;
        float cy = (ClientRectangle.Height - 1) / 2f;

        var previous = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var pen = new Pen(colour, thickness)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
        })
        {
            g.DrawLine(pen, cx - arm, cy - arm, cx + arm, cy + arm);
            g.DrawLine(pen, cx + arm, cy - arm, cx - arm, cy + arm);
        }
        g.SmoothingMode = previous;
    }

    private void DrawGlyph(Graphics g, string glyph)
    {
        Color colour = GlyphColor
            ?? (Enabled ? ForeColor : Theme.Dark ? Theme.DimText : SystemColors.GrayText);

        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddString(glyph, Font.FontFamily, (int)Font.Style,
            g.DpiY * Font.SizeInPoints / 72f, PointF.Empty, StringFormat.GenericTypographic);

        RectangleF ink = path.GetBounds();
        if (ink.Width <= 0f || ink.Height <= 0f) return;

        using (var move = new System.Drawing.Drawing2D.Matrix())
        {
            move.Translate(
                ClientRectangle.Width / 2f - (ink.Left + ink.Width / 2f),
                ClientRectangle.Height / 2f - (ink.Top + ink.Height / 2f));
            path.Transform(move);
        }

        var previous = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var brush = new SolidBrush(colour))
        {
            g.FillPath(brush, path);
        }
        g.SmoothingMode = previous;
    }
}
