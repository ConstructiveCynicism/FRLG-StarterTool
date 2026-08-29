namespace FRLG.StarterTool.App;

public sealed class ThemedComboBox : ComboBox
{
    public void RefreshDrawMode()
    {
        if (DrawMode != DrawMode.OwnerDrawFixed)
        {
            DrawMode = DrawMode.OwnerDrawFixed;
        }

        Invalidate();
        RefreshScrollBars();
    }

    public void MatchHeight(int height)
    {
        if (height <= 0) return;

        if (DrawMode != DrawMode.OwnerDrawFixed) DrawMode = DrawMode.OwnerDrawFixed;

        for (int item = height; item > 1; item--)
        {
            ItemHeight = item;
            if (Height <= height) break;
        }

        Height = height;
    }

    public void RefreshScrollBars()
    {
        if (!IsHandleCreated) return;

        Win32.SetDarkScrollBars(Win32.GetComboBoxList(Handle), Theme.Dark);
    }

    private string _prefix = "";

    private int _prefixTime;

    private const int PrefixTimeoutMs = 1000;

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar))
        {
            if (e.KeyChar == '\b' && _prefix.Length > 0)
            {
                _prefix = _prefix.Substring(0, _prefix.Length - 1);
                _prefixTime = Environment.TickCount;
                if (_prefix.Length > 0) Select(_prefix);
                e.Handled = true;
                return;
            }

            base.OnKeyPress(e);
            return;
        }

        if (Environment.TickCount - _prefixTime > PrefixTimeoutMs) _prefix = "";
        _prefixTime = Environment.TickCount;

        string extended = _prefix + e.KeyChar;
        if (!Select(extended))
        {
            extended = e.KeyChar.ToString();
            Select(extended);
        }
        _prefix = extended;

        e.Handled = true;
    }

    private bool Select(string prefix)
    {
        int index = FindString(prefix);
        if (index < 0) return false;

        SelectedIndex = index;
        return true;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        RefreshScrollBars();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool listRow = (e.State & DrawItemState.ComboBoxEdit) == 0;
        bool selected = listRow && (e.State & DrawItemState.Selected) != 0;

        Color back = selected ? Theme.Accent : Theme.Input;
        Color fore = selected ? Theme.AccentText : Enabled ? Theme.Text : Theme.DimText;

        using (var background = new SolidBrush(back))
        {
            e.Graphics.FillRectangle(background, e.Bounds);
        }

        if (e.Index >= 0)
        {
            TextRenderer.DrawText(
                e.Graphics,
                GetItemText(Items[e.Index]),
                Font,
                e.Bounds,
                fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Win32.WM_MOUSEWHEEL && !DroppedDown)
        {
            if (Parent is { } parent && parent.IsHandleCreated)
            {
                Win32.SendMessage(parent.Handle, Win32.WM_MOUSEWHEEL, m.WParam, m.LParam);
            }

            return;
        }

        base.WndProc(ref m);

        if (m.Msg == Win32.WM_PAINT)
        {
            PaintButton();
        }
    }

    private void PaintButton()
    {
        int width = SystemInformation.HorizontalScrollBarArrowWidth;
        var button = new Rectangle(Width - width - 1, 1, width, Height - 2);

        using Graphics g = Graphics.FromHwnd(Handle);
        using (var background = new SolidBrush(Theme.Surface))
        {
            g.FillRectangle(background, button);
        }

        using (var arrow = new SolidBrush(Enabled ? Theme.Text : Theme.DimText))
        {
            Rectangle interior = Rectangle.FromLTRB(button.Left + 1, button.Top, Width - 1, button.Bottom);
            float cx = (interior.Left + interior.Right) / 2f;
            float cy = (interior.Top + interior.Bottom) / 2f;

            var previous = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillPolygon(arrow, new[]
            {
                new PointF(cx - 4.5f, cy - 2.5f),
                new PointF(cx + 4.5f, cy - 2.5f),
                new PointF(cx, cy + 2.5f)
            });
            g.SmoothingMode = previous;
        }

        using var pen = new Pen(Theme.Border);
        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        g.DrawLine(pen, button.Left, 1, button.Left, Height - 2);
    }
}
