using System.Globalization;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public sealed class IgtPanel : Panel
{
    public const int PanelWidth = 402;

    public const int PanelHeight = 367;

    private const int Inner = PanelWidth - 12;

    private const float DesignerFontPoints = 9F;

    private const int DelayerPitch = 26;

    private static readonly string[] FpsPresets = { "59.7275", "59.8261", "60", "59.94", "59.6555", "50" };

    private static readonly TimerRowList.Column[] Columns =
    {
        new(20, 100, Numeric: false),
        new(124, 50, Numeric: true),
        new(178, 66, Numeric: true),
        new(248, 56, Numeric: true),
        new(308, 32, Numeric: true)
    };

    private sealed class DelayerRow
    {
        public required Label Name;
        public required ThemedTextBox Count;
        public required ThemedButton Minus;
        public required ThemedButton Plus;
        public double Delay;
    }

    private readonly ThemedComboBox _game;
    private readonly ThemedComboBox _fps;
    private readonly Panel _delayersHost;
    private readonly List<DelayerRow> _delayers = new();
    private readonly Panel _rowsHost;
    private readonly TimerRowList _rows;
    private readonly ThemedButton _add;
    private readonly ThemedButton _load;
    private readonly ThemedButton _save;

    private IgtDelayersFile _games = new();

    public ThemedButton ButtonPlay { get; }

    public ThemedButton ButtonUndo { get; }

    public event EventHandler<double>? Delayed;

    public IgtPanel()
    {
        var box = new ThemedGroupBox
        {
            Text = "IGT Tracking",
            Location = new Point(0, 0),
            Size = new Size(PanelWidth, PanelHeight)
        };

        box.Controls.Add(new Label
        {
            Text = "Game", Location = new Point(6, 18), Size = new Size(40, 23), TextAlign = ContentAlignment.MiddleLeft
        });
        _game = new ThemedComboBox
        {
            Location = new Point(48, 18), Size = new Size(170, 23), DropDownStyle = ComboBoxStyle.DropDownList
        };
        _game.SelectedIndexChanged += (_, _) => ShowGame();
        box.Controls.Add(_game);

        box.Controls.Add(new Label
        {
            Text = "FPS", Location = new Point(234, 18), Size = new Size(30, 23), TextAlign = ContentAlignment.MiddleLeft
        });
        _fps = new ThemedComboBox
        {
            Location = new Point(266, 18), Size = new Size(130, 23), DropDownStyle = ComboBoxStyle.DropDownList
        };
        _fps.Items.AddRange(FpsPresets);
        box.Controls.Add(_fps);

        _delayersHost = new Panel
        {
            Location = new Point(6, 46),
            Size = new Size(Inner, 5 * DelayerPitch),
            AutoScroll = true
        };
        box.Controls.Add(_delayersHost);

        ButtonPlay = new ThemedButton
        {
            Text = "Play", Location = new Point(6, _delayersHost.Bottom + 4), Size = new Size(120, 26),
            Enabled = false, TabStop = false
        };
        ButtonUndo = new ThemedButton
        {
            Text = "Undo", Location = new Point(130, ButtonPlay.Top), Size = new Size(120, 26),
            Enabled = false, TabStop = false
        };
        box.Controls.Add(ButtonPlay);
        box.Controls.Add(ButtonUndo);

        int headerY = ButtonPlay.Bottom + 6;
        string[] captions = { "Name", "Frame", "Offset", "Interval", "Beeps" };
        for (int i = 0; i < Columns.Length; i++)
        {
            box.Controls.Add(new Label
            {
                Text = captions[i],
                Location = new Point(6 + Columns[i].X - 4, headerY),
                Size = new Size(Columns[i].Width + 8, 18),
                TextAlign = ContentAlignment.MiddleCenter
            });
        }

        _rowsHost = new Panel
        {
            Location = new Point(6, headerY + 20),
            Size = new Size(Inner, 103),
            AutoScroll = true
        };
        box.Controls.Add(_rowsHost);
        _rows = new TimerRowList(_rowsHost, Columns, removeX: 346);

        int footY = _rowsHost.Bottom + 4;
        _add = new ThemedButton { Text = "+ Add Timer", Location = new Point(6, footY), Size = new Size(120, 22), TabStop = false };
        _load = new ThemedButton { Text = "Load Timers", Location = new Point(Inner + 6 - 2 * 100 - 4, footY), Size = new Size(100, 22), TabStop = false };
        _save = new ThemedButton { Text = "Save Timers", Location = new Point(Inner + 6 - 100, footY), Size = new Size(100, 22), TabStop = false };
        _add.Click += (_, _) => _rows.Add(ToRow(new IgtTimerEntry()));
        _load.Click += (_, _) => LoadTimers();
        _save.Click += (_, _) => SaveTimers();
        box.Controls.AddRange(new Control[] { _add, _load, _save });

        Controls.Add(box);
    }

    private static MainForm Form => StarterTool.MainForm;

    public string? Fps => _fps.SelectedItem as string;

    public IgtTimerEntry SelectedEntry =>
        _rows.Count == 0 ? new IgtTimerEntry() : ToEntry(_rows.Values(_rows.SelectedIndex));

    public int DelayerCount => _delayers.Count;

    public void ApplySettings(AppSettings settings)
    {
        _games = TimerLibrary.Default.ReadDelayers();

        _game.Items.Clear();
        foreach (IgtGame game in _games.Games) _game.Items.Add(game.Game);

        int fps = Array.IndexOf(FpsPresets, settings.IgtFps);
        _fps.SelectedIndex = fps >= 0 ? fps : 0;

        int selected = _games.Games.FindIndex(game => game.Game == settings.IgtGame);
        if (_game.Items.Count > 0) _game.SelectedIndex = Math.Max(0, selected);

        _rows.SetRows(settings.IgtTimers.Select(ToRow), settings.IgtSelectedTimer);
    }

    public void CaptureSettings(AppSettings settings)
    {
        settings.IgtGame = _game.SelectedItem as string ?? "";
        settings.IgtFps = Fps ?? settings.IgtFps;
        settings.IgtTimers = Enumerable.Range(0, _rows.Count).Select(i => ToEntry(_rows.Values(i))).ToList();
        settings.IgtSelectedTimer = _rows.SelectedIndex;
    }

    public void MoveSelection(int delta) => _rows.Move(delta);

    public void PressDelayer(int index, int direction)
    {
        if (index < 0 || index >= _delayers.Count) return;
        (direction > 0 ? _delayers[index].Plus : _delayers[index].Minus).PerformClick();
    }

    public void ResetCounts()
    {
        foreach (DelayerRow row in _delayers) row.Count.Text = "0";
    }

    public void Relayout()
    {
        ClampToWidth();
        _rows.Relayout();
        LayoutDelayers();
    }

    private void ClampToWidth()
    {
        foreach (Control child in Controls)
        {
            if (child.Right > Width) child.Width = Width - child.Left;
        }
    }

    private static string[] ToRow(IgtTimerEntry entry) =>
        new[] { entry.Name, entry.Frame, entry.Offsets, entry.Interval, entry.NumBeeps };

    private static IgtTimerEntry ToEntry(string[] row) => new()
    {
        Name = row[0],
        Frame = row[1],
        Offsets = row[2],
        Interval = row[3],
        NumBeeps = row[4]
    };

    private void ShowGame()
    {
        _delayersHost.SuspendLayout();
        foreach (DelayerRow row in _delayers)
        {
            foreach (Control control in new Control[] { row.Name, row.Count, row.Minus, row.Plus })
            {
                _delayersHost.Controls.Remove(control);
                control.Dispose();
            }
        }
        _delayers.Clear();

        IgtGame? game = _games.Games.Find(candidate => candidate.Game == _game.SelectedItem as string);
        foreach (IgtDelayer delayer in game?.Delayers ?? new List<IgtDelayer>())
        {
            var row = new DelayerRow
            {
                Name = new Label { Text = delayer.Name + ":", TextAlign = ContentAlignment.MiddleLeft },
                Count = new ThemedTextBox
                {
                    Text = "0", AutoSize = false, Enabled = false, TextAlign = HorizontalAlignment.Center, TabStop = false
                },
                Minus = new ThemedButton { Glyph = "−", TabStop = false },
                Plus = new ThemedButton { Glyph = "+", TabStop = false },
                Delay = delayer.Delay
            };
            row.Minus.Click += (_, _) => Count(row, -1);
            row.Plus.Click += (_, _) => Count(row, +1);

            _delayers.Add(row);
            foreach (Control control in new Control[] { row.Name, row.Count, row.Minus, row.Plus })
            {
                _delayersHost.Controls.Add(control);
                Theme.ApplyTo(control);
            }
        }

        _delayersHost.ResumeLayout(false);
        LayoutDelayers();
    }

    private void Count(DelayerRow row, int direction)
    {
        int.TryParse(row.Count.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count);
        row.Count.Text = (count + direction).ToString(CultureInfo.InvariantCulture);
        Delayed?.Invoke(this, direction * row.Delay);
    }

    private void LayoutDelayers()
    {
        float scale = ZoomLayout.FontPixelFactor(_delayersHost, DesignerFontPoints);
        int S(int length) => ZoomLayout.Round(length * scale);

        _delayersHost.SuspendLayout();
        int originY = _delayersHost.AutoScrollPosition.Y;
        int field = S(23);

        for (int i = 0; i < _delayers.Count; i++)
        {
            DelayerRow row = _delayers[i];
            int top = originY + S(i * DelayerPitch);

            row.Name.Bounds = new Rectangle(S(2), top, S(250), field);
            row.Count.Bounds = new Rectangle(S(256), top, S(36), field);
            row.Minus.Bounds = new Rectangle(S(296), top, field, field);
            row.Plus.Bounds = new Rectangle(S(296) + field + S(4), top, field, field);
        }

        _delayersHost.ResumeLayout(true);
    }

    private void LoadTimers()
    {
        string? path = Form.BrowseOpen("Load timers", "Timers (*.json)|*.json|All files (*.*)|*.*");
        if (path == null) return;

        try
        {
            List<IgtTimerEntry>? timers = TimerLibrary.ReadIgtTimers(path);
            if (timers == null)
            {
                Form.Fail($"\"{Path.GetFileName(path)}\" holds no timers.", "Load timers");
                return;
            }

            _rows.SetRows(timers.Select(ToRow), 0);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Form.Fail($"Could not read \"{Path.GetFileName(path)}\": {e.Message}", "Load timers");
        }
    }

    private void SaveTimers()
    {
        string? path = Form.BrowseSave("Save timers", "Timers (*.json)|*.json|All files (*.*)|*.*", "igt timers");
        if (path == null) return;

        try
        {
            TimerLibrary.WriteIgtTimers(
                path, Enumerable.Range(0, _rows.Count).Select(i => ToEntry(_rows.Values(i))));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Form.Fail($"Could not write \"{Path.GetFileName(path)}\": {e.Message}", "Save timers");
        }
    }
}
