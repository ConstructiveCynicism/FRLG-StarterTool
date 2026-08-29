namespace FRLG.StarterTool.App;

public sealed class ThemedTabStrip : Control
{
    private sealed class Tab
    {
        public required string Key;
        public required string Caption;
        public bool Visible = true;
        public Rectangle Bounds;
    }

    private const int Pad = 12;

    private const float DesignerFontPoints = 9F;

    private readonly List<Tab> _tabs = new();
    private string? _selectedKey;
    private string? _hotKey;

    public event EventHandler<string>? TabClicked;

    public ThemedTabStrip()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
    }

    public void Add(string key, string caption) => _tabs.Add(new Tab { Key = key, Caption = caption });

    public bool IsVisible(string key) => Find(key)?.Visible ?? false;

    public void SetVisible(string key, bool visible)
    {
        if (Find(key) is not { } tab || tab.Visible == visible) return;

        tab.Visible = visible;
        Invalidate();
    }

    public string? SelectedKey
    {
        get => _selectedKey;
        set
        {
            if (_selectedKey == value) return;

            _selectedKey = value;
            Invalidate();
        }
    }

    private Tab? Find(string key) => _tabs.Find(t => t.Key == key);

    private Tab? HitTest(Point point) =>
        _tabs.Find(t => t.Visible && t.Bounds.Contains(point));

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;

        using (var background = new SolidBrush(BackColor))
        {
            g.FillRectangle(background, ClientRectangle);
        }

        using var captionFont = new Font(Font, FontStyle.Bold);
        int pad = ZoomLayout.Round(Pad * ZoomLayout.FontPixelFactor(this, DesignerFontPoints));
        int x = 0;
        int bottom = Height - 1;

        foreach (Tab tab in _tabs)
        {
            if (!tab.Visible)
            {
                tab.Bounds = Rectangle.Empty;
                continue;
            }

            int width = TextRenderer.MeasureText(
                g, tab.Caption, captionFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width + 2 * pad;
            tab.Bounds = new Rectangle(x, 0, width, Height);
            x += width;
        }

        using var rule = new Pen(Theme.SectionBorder);

        Tab? selected = _selectedKey == null ? null : Find(_selectedKey);
        if (selected is { Visible: true })
        {
            g.DrawLine(rule, 0, bottom, selected.Bounds.Left, bottom);
            g.DrawLine(rule, selected.Bounds.Right - 1, bottom, Width - 1, bottom);
        }
        else
        {
            g.DrawLine(rule, 0, bottom, Width - 1, bottom);
        }

        foreach (Tab tab in _tabs)
        {
            if (!tab.Visible) continue;

            bool isSelected = ReferenceEquals(tab, selected);
            bool isHot = !isSelected && tab.Key == _hotKey;

            Rectangle face = tab.Bounds;
            if (isSelected)
            {
                using var fill = new SolidBrush(Theme.Surface);
                g.FillRectangle(fill, face);
                g.DrawLine(rule, face.Left, bottom, face.Left, face.Top);
                g.DrawLine(rule, face.Left, face.Top, face.Right - 1, face.Top);
                g.DrawLine(rule, face.Right - 1, face.Top, face.Right - 1, bottom);
            }
            else if (isHot)
            {
                using var fill = new SolidBrush(Theme.Hover);
                g.FillRectangle(fill, new Rectangle(face.Left, face.Top, face.Width, face.Height - 1));
            }

            Color ink = isSelected ? Theme.SectionCaption : isHot ? Theme.Text : Theme.DimText;
            TextRenderer.DrawText(g, tab.Caption, isSelected ? captionFont : Font, face, ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        string? hot = HitTest(e.Location)?.Key;
        if (hot == _hotKey) return;

        _hotKey = hot;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (_hotKey == null) return;

        _hotKey = null;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left) return;
        if (HitTest(e.Location) is not { } tab || tab.Key == _selectedKey) return;

        TabClicked?.Invoke(this, tab.Key);
    }
}
