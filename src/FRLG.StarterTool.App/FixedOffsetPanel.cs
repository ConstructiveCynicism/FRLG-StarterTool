using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Timing;

namespace FRLG.StarterTool.App;

public sealed class FixedOffsetPanel : Panel
{
    public const int PanelWidth = 402;

    public const int PanelHeight = 367;

    private const int Inner = PanelWidth - 12;

    private static readonly TimerRowList.Column[] Columns =
    {
        new(20, 100, Numeric: false),
        new(124, 106, Numeric: false),
        new(234, 58, Numeric: true),
        new(296, 44, Numeric: true)
    };

    private readonly Panel _rowsHost;
    private readonly TimerRowList _rows;
    private readonly ThemedButton _unitButton;
    private readonly ThemedButton _addButton;
    private readonly ThemedCheckBox _adjustAll;

    private readonly ListBox _files;
    private readonly ThemedButton _update;
    private readonly ThemedButton _save;
    private readonly ThemedButton _import;
    private readonly ThemedButton _export;
    private readonly ThemedButton _delete;

    private FixedTargetUnit _unit = FixedTargetUnit.Milliseconds;
    private string _activeSet = "";
    private bool _fillingFiles;
    private bool _running;

    public Label Readout { get; }

    public event EventHandler? SelectionChanged;

    public event EventHandler? Changed;

    public FixedOffsetPanel()
    {
        var timers = new ThemedGroupBox
        {
            Text = "Timers",
            Location = new Point(0, 0),
            Size = new Size(PanelWidth, 209)
        };

        Label Header(string text, TimerRowList.Column column) => new()
        {
            Text = text,
            Location = new Point(6 + column.X, 18),
            Size = new Size(column.Width, 20),
            TextAlign = ContentAlignment.MiddleCenter
        };

        timers.Controls.Add(Header("Name", Columns[0]));
        _unitButton = new ThemedButton
        {
            Location = new Point(6 + Columns[1].X, 17),
            Size = new Size(Columns[1].Width, 22),
            TabStop = false
        };
        _unitButton.Click += (_, _) => FlipUnit();
        timers.Controls.Add(_unitButton);
        timers.Controls.Add(Header("Interval", Columns[2]));
        timers.Controls.Add(Header("Beeps", Columns[3]));

        _rowsHost = new Panel
        {
            Location = new Point(6, 42),
            Size = new Size(Inner, 5 * 27),
            AutoScroll = true
        };
        timers.Controls.Add(_rowsHost);

        _rows = new TimerRowList(_rowsHost, Columns, removeX: 344, multi: true, redRemove: true);
        _rows.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        _rows.SelectionChanged += (_, _) =>
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, EventArgs.Empty);
        };

        _addButton = new ThemedButton
        {
            Text = "+ Add Timer",
            Location = new Point(6, _rowsHost.Bottom + 4),
            Size = new Size(120, 22),
            TabStop = false
        };
        _addButton.Click += (_, _) =>
        {
            FixedTimerEntry entry = _rows.Count > 0 ? ToEntry(_rows.Values(_rows.Count - 1)) : new FixedTimerEntry();
            entry.Name = "Timer";
            _rows.Add(ToRow(entry));
        };
        timers.Controls.Add(_addButton);

        _adjustAll = new ThemedCheckBox
        {
            Text = "Adjust all offsets",
            AutoSize = true,
            Checked = true,
            Location = new Point(_addButton.Right + 12, _addButton.Top + 3),
            TabStop = false
        };
        timers.Controls.Add(_adjustAll);

        Readout = new Label
        {
            Location = new Point(0, timers.Bottom + 4),
            Size = new Size(PanelWidth, 36),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Bold),
            Tag = Theme.KeepForeColor
        };

        var file = new ThemedGroupBox
        {
            Text = "File",
            Location = new Point(0, Readout.Bottom + 4),
            Size = new Size(PanelWidth, PanelHeight - Readout.Bottom - 4)
        };

        _files = new ListBox
        {
            Location = new Point(6, 18),
            Size = new Size(Inner, file.Height - 18 - 22 - 4 - 6),
            IntegralHeight = false,
            SelectionMode = SelectionMode.One
        };
        file.Controls.Add(_files);

        int buttonWidth = (Inner - 4 * 4) / 5;
        ThemedButton FileButton(string text, int slot) => new()
        {
            Text = text,
            Location = new Point(6 + slot * (buttonWidth + 4), _files.Bottom + 4),
            Size = new Size(buttonWidth, 22),
            TabStop = false
        };

        _update = FileButton("Update", 0);
        _save = FileButton("Save", 1);
        _import = FileButton("Import", 2);
        _export = FileButton("Export", 3);
        _delete = FileButton("Delete", 4);
        file.Controls.AddRange(new Control[] { _update, _save, _import, _export, _delete });

        _update.Click += (_, _) => UpdateActive();
        _save.Click += (_, _) => SaveAs();
        _import.Click += (_, _) => Import();
        _export.Click += (_, _) => Export();
        _delete.Click += (_, _) => DeleteActive();

        _files.SelectedIndexChanged += (_, _) =>
        {
            if (_fillingFiles || _files.SelectedItem is not string name) return;

            TimerSet? set = Library.Find(name);
            if (set == null) return;

            _activeSet = set.Name;
            LoadSet(set, 0);
            RefreshFileButtons();
            Changed?.Invoke(this, EventArgs.Empty);
        };
        _files.DoubleClick += (_, _) => RenameActive();

        Controls.Add(timers);
        Controls.Add(Readout);
        Controls.Add(file);

        ShowUnit();
    }

    private static TimerLibrary Library => TimerLibrary.Default;

    private static MainForm Form => StarterTool.MainForm;

    private static double Fps => StarterTool.VariableOffset?.SelectedFps ?? 60.0;

    public FixedTargetUnit Unit => _unit;

    public bool AdjustAll
    {
        get => _adjustAll.Checked;
        set => _adjustAll.Checked = value;
    }

    public string ActiveSet => _activeSet;

    public int SelectedIndex => _rows.SelectedIndex;

    public List<FixedTimerEntry> CheckedEntries =>
        _rows.CheckedIndices.Select(index => ToEntry(_rows.Values(index))).ToList();

    public TimerSet CaptureSet(string name = "")
    {
        var set = new TimerSet { Name = name, TargetUnit = _unit };
        for (int i = 0; i < _rows.Count; i++) set.Timers.Add(ToEntry(_rows.Values(i)));
        return set;
    }

    public void LoadSet(TimerSet set, int selected, IEnumerable<int>? ticked = null)
    {
        _unit = set.TargetUnit;
        ShowUnit();

        List<FixedTimerEntry> timers = set.Timers.Count > 0
            ? set.Timers
            : new List<FixedTimerEntry> { new() };
        _rows.SetRows(timers.Select(ToRow), selected, ticked ?? new[] { selected });
        _rows.SetEnabled(!_running);
    }

    public void ApplySettings(AppSettings settings)
    {
        _activeSet = settings.FixedActiveSet;
        AdjustAll = settings.FixedAdjustAll;
        LoadSet(settings.FixedTimers, settings.FixedSelectedTimer, settings.FixedCheckedTimers);
        FillFiles();
    }

    public void CaptureSettings(AppSettings settings)
    {
        settings.FixedTimers = CaptureSet();
        settings.FixedSelectedTimer = _rows.SelectedIndex;
        settings.FixedCheckedTimers = _rows.CheckedIndices;
        settings.FixedActiveSet = _activeSet;
        settings.FixedAdjustAll = AdjustAll;
    }

    public void SetRunning(bool running)
    {
        _running = running;
        _rows.SetEnabled(!running);
        _addButton.Enabled = !running;
        _unitButton.Enabled = !running;
        _files.Enabled = !running;
        _save.Enabled = !running;
        _import.Enabled = !running;
        RefreshFileButtons();
    }

    public void AdvanceAfterRun()
    {
        if (!_running) _rows.AdvanceSingle();
    }

    public void MoveSelection(int delta)
    {
        if (!_running) _rows.Move(delta);
    }

    public void Relayout()
    {
        ClampToWidth();
        _rows.Relayout();
    }

    private void ClampToWidth()
    {
        foreach (Control child in Controls)
        {
            if (child.Right > Width) child.Width = Width - child.Left;
        }
    }

    private static string[] ToRow(FixedTimerEntry entry) =>
        new[] { entry.Name, entry.Offsets, entry.Interval, entry.NumBeeps };

    private static FixedTimerEntry ToEntry(string[] row) => new()
    {
        Name = row[0],
        Offsets = row[1],
        Interval = row[2],
        NumBeeps = row[3]
    };

    private void ShowUnit() =>
        _unitButton.Text = _unit == FixedTargetUnit.Frames ? "Target (frames)" : "Target (ms)";

    private void FlipUnit()
    {
        FixedTargetUnit to = _unit == FixedTargetUnit.Frames ? FixedTargetUnit.Milliseconds : FixedTargetUnit.Frames;
        for (int i = 0; i < _rows.Count; i++)
        {
            _rows.SetValue(i, 1, FixedOffsetCalculator.ConvertOffsets(_rows.Values(i)[1], _unit, to, Fps));
        }

        _unit = to;
        ShowUnit();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void FillFiles()
    {
        _fillingFiles = true;
        try
        {
            _files.BeginUpdate();
            _files.Items.Clear();
            int index = -1;
            foreach (TimerSet set in Library.ReadSets())
            {
                if (TimerSet.NameEquals(set.Name, _activeSet)) index = _files.Items.Count;
                _files.Items.Add(set.Name);
            }
            _files.EndUpdate();
            _files.SelectedIndex = index;
            if (index < 0) _activeSet = "";
        }
        finally
        {
            _fillingFiles = false;
        }

        RefreshFileButtons();
    }

    private void RefreshFileButtons()
    {
        bool any = _files.SelectedIndex >= 0 && !_running;
        _update.Enabled = any;
        _export.Enabled = any;
        _delete.Enabled = any;
    }

    private void UpdateActive()
    {
        if (_activeSet.Length == 0) return;
        Guarded("Update Timers", () => Library.Write(CaptureSet(_activeSet)));
    }

    private void SaveAs()
    {
        string? name = Form.PromptForName("Save Timers", "Name for these timers:", _activeSet);
        if (string.IsNullOrWhiteSpace(name)) return;

        TimerSet? existing = Library.Find(name);
        if (existing != null
            && !Form.Confirm($"Timers named \"{existing.Name}\" already exist. Overwrite them?", "Save Timers"))
        {
            return;
        }

        if (!Guarded("Save Timers", () => Library.Write(CaptureSet(existing?.Name ?? name.Trim())))) return;
        _activeSet = existing?.Name ?? name.Trim();
        FillFiles();
    }

    private void RenameActive()
    {
        if (_activeSet.Length == 0 || _running) return;

        string? name = Form.PromptForName("Rename Timers", "New name for these timers:", _activeSet);
        if (string.IsNullOrWhiteSpace(name) || TimerSet.NameEquals(name, _activeSet)) return;

        TimerSet? clash = Library.Find(name);
        if (clash != null
            && !Form.Confirm($"Timers named \"{clash.Name}\" already exist. Overwrite them?", "Rename Timers"))
        {
            return;
        }

        if (!Guarded("Rename Timers", () => Library.Rename(_activeSet, name.Trim()))) return;
        _activeSet = name.Trim();
        FillFiles();
    }

    private void DeleteActive()
    {
        if (_activeSet.Length == 0) return;
        if (!Form.Confirm($"Delete the timers \"{_activeSet}\"?", "Delete Timers")) return;

        if (!Guarded("Delete Timers", () => Library.Delete(_activeSet))) return;
        _activeSet = "";
        FillFiles();
    }

    private void Import()
    {
        string? path = Form.BrowseOpen("Import timers", "Timers (*.json)|*.json|All files (*.*)|*.*");
        if (path == null) return;

        TimerSet? set = null;
        if (!Guarded("Import timers", () => set = TimerLibrary.ReadImport(path))) return;
        if (set == null)
        {
            Form.Fail($"\"{Path.GetFileName(path)}\" holds no timers.", "Import timers");
            return;
        }

        TimerSet? existing = Library.Find(set.Name);
        if (existing != null
            && !Form.Confirm($"Timers named \"{existing.Name}\" already exist. Overwrite them?", "Import timers"))
        {
            return;
        }

        if (existing != null) set.Name = existing.Name;
        if (!Guarded("Import timers", () => Library.Write(set))) return;

        _activeSet = set.Name;
        LoadSet(set, 0);
        FillFiles();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Export()
    {
        TimerSet? set = _activeSet.Length == 0 ? null : Library.Find(_activeSet);
        if (set == null) return;

        string? path = Form.BrowseSave("Export timers", "Timers (*.json)|*.json|All files (*.*)|*.*", set.Name);
        if (path == null) return;

        Guarded("Export timers", () => PresetFile.Write(path, set));
    }

    private static bool Guarded(string title, Action work)
    {
        try
        {
            work();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Form.Fail(e.Message, title);
            return false;
        }
    }
}
