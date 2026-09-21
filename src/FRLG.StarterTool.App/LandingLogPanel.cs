using System.Globalization;

namespace FRLG.StarterTool.App;

public sealed class LandingLogPanel : Panel
{
    public const int PanelWidth = 390;

    public const int PanelHeight = 346;

    private const int RowHeight = 23;

    private readonly ThemedListView _list;

    public Label Readout { get; }

    public LandingLogPanel()
    {
        var caption = new Label
        {
            Text = "Presses scored against the countdown",
            Location = new Point(0, 0),
            Size = new Size(PanelWidth - 84, RowHeight),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var clear = new ThemedButton
        {
            Text = "Clear",
            Location = new Point(PanelWidth - 80, 0),
            Size = new Size(80, RowHeight)
        };
        clear.Click += (_, _) => Clear();

        _list = new ThemedListView
        {
            Location = new Point(0, 28),
            Size = new Size(PanelWidth, 274),
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Font = new Font("Segoe UI", 8F),
            OwnerDraw = true
        };
        _list.DrawColumnHeader += DrawColumnHeader;
        _list.DrawItem += (_, _) => { };
        _list.DrawSubItem += DrawSubItem;
        _list.Columns.Add("#", 34, HorizontalAlignment.Center);
        _list.Columns.Add("Target", 78, HorizontalAlignment.Center);
        _list.Columns.Add("Landed", 78, HorizontalAlignment.Center);
        _list.Columns.Add("Frames", 62, HorizontalAlignment.Center);
        _list.Columns.Add("ms", 62, HorizontalAlignment.Center);
        _list.Columns.Add("Hit", 60, HorizontalAlignment.Center);
        _list.HandleCreated += (_, _) => FitLastColumn();

        Readout = new Label
        {
            Location = new Point(0, 308),
            Size = new Size(PanelWidth, 36),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Bold),
            Tag = Theme.KeepForeColor
        };

        Controls.Add(caption);
        Controls.Add(clear);
        Controls.Add(_list);
        Controls.Add(Readout);

        BackColorChanged += (_, _) => Recolour();
    }

    public void Clear()
    {
        _list.Items.Clear();
        Readout.Text = "";
    }

    public void Add(string target, string landed, double errorFrames, double deltaMs, double hitChance)
    {
        var item = new ListViewItem((_list.Items.Count + 1).ToString(CultureInfo.InvariantCulture));
        item.SubItems.Add(target);
        item.SubItems.Add(landed);
        item.SubItems.Add(errorFrames.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture));
        item.SubItems.Add(deltaMs.ToString("+0;-0;0", CultureInfo.InvariantCulture));
        item.SubItems.Add(MainForm.FormatChance(hitChance));
        item.Tag = hitChance;
        Colour(item, hitChance);

        _list.Items.Add(item);
        item.EnsureVisible();
        FitLastColumn();
    }

    private static void Colour(ListViewItem item, double hitChance)
    {
        item.BackColor = hitChance > 0.5 ? Theme.LandingHitBack
            : hitChance > 0.0 ? Theme.LandingMaybeBack
            : Theme.LandingMissBack;
        item.ForeColor = Theme.LandingRowText;
    }

    private void Recolour()
    {
        foreach (ListViewItem item in _list.Items)
        {
            if (item.Tag is double chance) Colour(item, chance);
        }
    }

    private const TextFormatFlags CellFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                                              | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter;

    private void DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using (var background = new SolidBrush(Theme.HeaderBack))
        {
            e.Graphics.FillRectangle(background, e.Bounds);
        }
        using (var pen = new Pen(Theme.Border))
        {
            if (e.ColumnIndex < _list.Columns.Count - 1)
            {
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 2, e.Bounds.Right - 1, e.Bounds.Bottom - 3);
            }
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        Rectangle bounds = e.Bounds;
        bounds.Inflate(-2, 0);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", _list.Font, bounds, Theme.Text, CellFlags);
    }

    private void DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        Color back = e.Item?.BackColor ?? _list.BackColor;
        Color fore = e.Item?.ForeColor ?? _list.ForeColor;

        using (var brush = new SolidBrush(back))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }
        using (var pen = new Pen(Theme.GridLine))
        {
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            if (e.ColumnIndex < _list.Columns.Count - 1)
            {
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom - 1);
            }
        }

        Rectangle bounds = e.Bounds;
        bounds.Inflate(-1, 0);
        bounds.Height -= ThemedListView.RuleClearance;
        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", _list.Font, bounds, fore, CellFlags);
    }

    private void FitLastColumn()
    {
        int used = 0;
        for (int i = 0; i < _list.Columns.Count - 1; i++) used += _list.Columns[i].Width;

        ColumnHeader last = _list.Columns[_list.Columns.Count - 1];
        int fill = _list.ClientSize.Width - used;
        if (fill >= 40 && fill != last.Width) last.Width = fill;
    }
}
