namespace FRLG.StarterTool.App;

internal sealed class TimerRowList
{
    internal readonly record struct Column(int X, int Width, bool Numeric);

    private sealed class Row
    {
        public required ThemedCheckBox Mark;
        public required ThemedTextBox[] Boxes;
        public required ThemedButton Remove;

        public bool Ticked;
    }

    private const float DesignerFontPoints = 9F;

    private const int MarkX = 2;
    private const int FieldHeight = 23;
    private const int RowPitch = 27;

    private readonly Panel _host;
    private readonly Column[] _columns;
    private readonly int _removeX;
    private readonly List<Row> _rows = new();

    private readonly bool _multi;

    private readonly bool _redRemove;

    private int _selected;
    private bool _enabled = true;

    private bool _filling;

    public event EventHandler? Changed;

    public event EventHandler? SelectionChanged;

    public TimerRowList(Panel host, Column[] columns, int removeX, bool multi = false, bool redRemove = false)
    {
        _host = host;
        _columns = columns;
        _removeX = removeX;
        _multi = multi;
        _redRemove = redRemove;
    }

    public List<int> CheckedIndices
    {
        get
        {
            if (!_multi) return _rows.Count == 0 ? new List<int>() : new List<int> { _selected };

            var ticked = new List<int>();
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Ticked) ticked.Add(i);
            }
            return ticked;
        }
    }

    public int Count => _rows.Count;

    public int SelectedIndex
    {
        get => _selected;
        set => Select(value);
    }

    public string[] Values(int row) => Array.ConvertAll(_rows[row].Boxes, box => box.Text);

    public void SetValue(int row, int column, string text)
    {
        _filling = true;
        try
        {
            _rows[row].Boxes[column].Text = text;
        }
        finally
        {
            _filling = false;
        }
    }

    public void SetRows(IEnumerable<string[]> rows, int selected, IEnumerable<int>? ticked = null)
    {
        _host.SuspendLayout();
        foreach (Row row in _rows) Dispose(row);
        _rows.Clear();

        foreach (string[] values in rows) Build(values);

        _host.ResumeLayout(false);
        _selected = Math.Clamp(selected, 0, Math.Max(0, _rows.Count - 1));

        foreach (int index in ticked ?? Array.Empty<int>())
        {
            if (index >= 0 && index < _rows.Count) _rows[index].Ticked = true;
        }
        if (_multi && _rows.Count > 0 && !_rows.Exists(row => row.Ticked)) _rows[_selected].Ticked = true;

        Relayout();
    }

    public void Add(string[] values)
    {
        Build(values);
        Select(_rows.Count - 1);
        Relayout();
        _host.ScrollControlIntoView(_rows[^1].Boxes[0]);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        foreach (Row row in _rows)
        {
            foreach (ThemedTextBox box in row.Boxes) box.Enabled = enabled;
            row.Remove.Enabled = enabled;
        }
    }

    public void Move(int delta)
    {
        if (_rows.Count == 0) return;

        if (_multi)
        {
            TickOnly(((_selected + delta) % _rows.Count + _rows.Count) % _rows.Count);
            return;
        }
        Select(((_selected + delta) % _rows.Count + _rows.Count) % _rows.Count);
    }

    private void TickOnly(int index)
    {
        index = Math.Clamp(index, 0, _rows.Count - 1);
        _selected = index;
        for (int i = 0; i < _rows.Count; i++) _rows[i].Ticked = i == index;
        ShowMarks();
        _host.ScrollControlIntoView(_rows[index].Boxes[0]);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool AdvanceSingle()
    {
        List<int> ticked = CheckedIndices;
        if (!_multi || ticked.Count != 1 || ticked[0] >= _rows.Count - 1) return false;

        TickOnly(ticked[0] + 1);
        return true;
    }

    private void ShowMarks()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            _rows[i].Mark.Checked = _multi ? _rows[i].Ticked : i == _selected;
        }
    }

    private void Select(int index)
    {
        if (_rows.Count == 0) return;

        index = Math.Clamp(index, 0, _rows.Count - 1);
        bool moved = index != _selected;
        _selected = index;
        ShowMarks();

        if (moved) SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Build(string[] values)
    {
        var row = new Row
        {
            Mark = new ThemedCheckBox { AutoCheck = false, AutoSize = false, Text = "", TabStop = false },
            Boxes = new ThemedTextBox[_columns.Length],
            Remove = new ThemedButton { Glyph = ThemedButton.CrossGlyph, TabStop = false }
        };

        row.Mark.Click += (_, _) =>
        {
            if (!_multi)
            {
                Select(_rows.IndexOf(row));
                return;
            }

            int index = _rows.IndexOf(row);
            Keys modifiers = Control.ModifierKeys;
            if ((modifiers & Keys.Shift) != 0)
            {
                int from = Math.Min(_selected, index), to = Math.Max(_selected, index);
                for (int i = 0; i < _rows.Count; i++) _rows[i].Ticked = i >= from && i <= to;
            }
            else if ((modifiers & Keys.Control) != 0)
            {
                if (row.Ticked && _rows.Count(other => other.Ticked) <= 1) return;

                row.Ticked = !row.Ticked;
                _selected = index;
            }
            else
            {
                TickOnly(index);
                return;
            }

            ShowMarks();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
        if (_redRemove)
        {
            row.Remove.GlyphColor = Color.White;
            row.Remove.Tag = Theme.StopButtonTag;
        }
        row.Remove.Click += (_, _) => RemoveRow(row);

        for (int i = 0; i < _columns.Length; i++)
        {
            var box = new ThemedTextBox
            {
                Numeric = _columns[i].Numeric,
                AutoSize = false,
                TabStop = false,
                Text = i < values.Length ? values[i] : "",
                Enabled = _enabled
            };
            box.TextChanged += (_, _) =>
            {
                if (_filling) return;

                if (!_multi) Select(_rows.IndexOf(row));
                Changed?.Invoke(this, EventArgs.Empty);
            };
            row.Boxes[i] = box;
        }

        row.Remove.Enabled = _enabled;
        _rows.Add(row);

        _host.Controls.Add(row.Mark);
        foreach (ThemedTextBox box in row.Boxes)
        {
            _host.Controls.Add(box);
            (_host.FindForm() as MainForm ?? StarterTool.MainForm)?.WatchNumberField(box);
        }
        _host.Controls.Add(row.Remove);

        Theme.ApplyTo(row.Mark);
        foreach (ThemedTextBox box in row.Boxes) Theme.ApplyTo(box);
        Theme.ApplyTo(row.Remove);
    }

    private void RemoveRow(Row row)
    {
        if (_rows.Count <= 1) return;

        int index = _rows.IndexOf(row);
        if (index < 0) return;

        _rows.RemoveAt(index);
        Dispose(row);

        if (_selected >= _rows.Count || _selected > index) _selected = Math.Max(0, _selected - 1);
        if (_multi && !_rows.Exists(other => other.Ticked)) _rows[_selected].Ticked = true;
        Relayout();
        Changed?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Dispose(Row row)
    {
        _host.Controls.Remove(row.Mark);
        row.Mark.Dispose();
        foreach (ThemedTextBox box in row.Boxes)
        {
            _host.Controls.Remove(box);
            box.Dispose();
        }
        _host.Controls.Remove(row.Remove);
        row.Remove.Dispose();
    }

    public void Relayout()
    {
        float scale = ZoomLayout.FontPixelFactor(_host, DesignerFontPoints);
        int S(int length) => ZoomLayout.Round(length * scale);

        _host.SuspendLayout();

        int originY = _host.AutoScrollPosition.Y;
        int field = S(FieldHeight);
        int mark = S(15);

        for (int i = 0; i < _rows.Count; i++)
        {
            Row row = _rows[i];
            int top = originY + S(i * RowPitch);

            row.Mark.Bounds = new Rectangle(S(MarkX), top + (field - mark) / 2, mark, mark);
            row.Mark.Checked = _multi ? row.Ticked : i == _selected;

            for (int c = 0; c < _columns.Length; c++)
            {
                int left = S(_columns[c].X);
                row.Boxes[c].Bounds = new Rectangle(left, top, S(_columns[c].X + _columns[c].Width) - left, field);
            }

            row.Remove.Bounds = new Rectangle(S(_removeX), top, field, field);
            row.Remove.Visible = _rows.Count > 1;
        }

        _host.ResumeLayout(true);
    }
}
