using System.Globalization;
using FRLG.StarterTool.Core.Pokemon;
using FRLG.StarterTool.Core.Savestate;
using FRLG.StarterTool.Core.Search;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public sealed class SavestatePanel : Panel
{
    public const int PanelWidth = 594;

    private const int BrowseX = PanelWidth - 62;

    private const int RowHeight = 22;

    private const int ListTop = 52;

    private const int ListHeight = 400;

    private const int EditTop = ListTop + ListHeight + 3;

    public const int PanelHeight = EditTop + 146 + RowHeight;

    private const int StatColumnX = 50;

    private const int StatColumnPitch = 86;
    private const int StatColumnWidth = 60;

    private const int RowLeaderWidth = 48;

    private const int RowButtonWidth = 38;
    private const int RowButtonX = PanelWidth - RowButtonWidth;

    private const int ModeKeep = 0;
    private const int ModeRandom = 1;

    private static readonly string[] StatNames = { "HP", "Atk", "Def", "SpA", "SpD", "Spe" };

    private readonly TextBox _loadBox;
    private readonly TextBox _saveBox;
    private readonly ThemedListView _list;
    private readonly ThemedComboBox _natureCombo;
    private readonly ThemedComboBox _ivCombo;
    private readonly TextBox[] _ivBoxes = new TextBox[6];
    private readonly ThemedCheckBox _evCheck;
    private readonly TextBox[] _evBoxes = new TextBox[6];
    private readonly ThemedComboBox _evSpecies;
    private readonly TextBox _evCount;
    private readonly Label _evTotal;
    private readonly Label _status;
    private readonly Button _buttonApply;
    private readonly Button _buttonRescan;

    private List<SavestateEntry> _entries = new();

    private bool _sticky;

    private readonly Random _random = new();

    private const int RescanDelayMs = 400;

    private readonly System.Windows.Forms.Timer _rescanDelay;

    private int _scanGeneration;

    private bool _scanning;

    public SavestatePanel()
    {
        _loadBox = MakeBox(64, 0, BrowseX - 66, numeric: false);
        _saveBox = MakeBox(64, 26, BrowseX - 66, numeric: false);
        _rescanDelay = new System.Windows.Forms.Timer { Interval = RescanDelayMs };
        _rescanDelay.Tick += (_, _) =>
        {
            _rescanDelay.Stop();
            Rescan();
        };
        _loadBox.TextChanged += (_, _) =>
        {
            _rescanDelay.Stop();
            _rescanDelay.Start();
        };
        _saveBox.TextChanged += (_, _) => Touched();

        var browseLoad = MakeButton("Browse…", BrowseX, 0, 62);
        browseLoad.Click += (_, _) => Browse(_loadBox, "Folder to read GSE states from");
        var browseSave = MakeButton("Browse…", BrowseX, 26, 62);
        browseSave.Click += (_, _) => Browse(_saveBox, "Folder to write edited states to");

        _list = new ThemedListView
        {
            Location = new Point(0, ListTop),
            Size = new Size(PanelWidth, ListHeight),
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = true,
            HideSelection = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Font = new Font("Segoe UI", 8F),
            OwnerDraw = true
        };
        _list.DrawColumnHeader += DrawColumnHeader;
        _list.DrawItem += (_, _) => { };
        _list.DrawSubItem += DrawSubItem;
        _list.Columns.Add("State", 62, HorizontalAlignment.Left);
        _list.Columns.Add("Party", 260, HorizontalAlignment.Left);
        _list.SelectedIndexChanged += (_, _) => Touched();
        _list.HandleCreated += (_, _) => FitLastColumn();

        Controls.Add(MakeLabel("Nature", 0, EditTop, 48, ContentAlignment.MiddleLeft));
        _natureCombo = MakeCombo(50, EditTop, 240);
        _natureCombo.Items.Add("— keep —");
        _natureCombo.Items.Add("Roll from filter");
        foreach (Nature nature in Nature.GetList()) _natureCombo.Items.Add(nature.Name);
        _natureCombo.SelectedIndex = ModeKeep;
        _natureCombo.SelectedIndexChanged += (_, _) => Touched();

        Controls.Add(MakeLabel("IVs", 300, EditTop, 28));
        _ivCombo = MakeCombo(332, EditTop, PanelWidth - 332);
        _ivCombo.Items.Add("— keep —");
        _ivCombo.Items.Add("Roll from filter");
        _ivCombo.Items.Add("Set below");
        _ivCombo.SelectedIndex = ModeKeep;
        _ivCombo.SelectedIndexChanged += (_, _) => Touched();

        for (int stat = 0; stat < 6; stat++)
        {
            Controls.Add(MakeLabel(StatNames[stat], StatColumnX + stat * StatColumnPitch, EditTop + 26, StatColumnWidth));

            _ivBoxes[stat] = MakeBox(StatColumnX + stat * StatColumnPitch, EditTop + 42, StatColumnWidth);
            _ivBoxes[stat].TextAlign = HorizontalAlignment.Center;
            _ivBoxes[stat].Text = "31";
            Controls.Add(_ivBoxes[stat]);

            _evBoxes[stat] = MakeBox(StatColumnX + stat * StatColumnPitch, EditTop + 68, StatColumnWidth);
            _evBoxes[stat].TextAlign = HorizontalAlignment.Center;
            _evBoxes[stat].Text = "0";
            _evBoxes[stat].TextChanged += (_, _) => Touched();
            Controls.Add(_evBoxes[stat]);
        }

        Controls.Add(MakeLabel("IVs", 0, EditTop + 42, RowLeaderWidth, ContentAlignment.MiddleLeft));

        _evCheck = new ThemedCheckBox
        {
            Text = "EVs",
            Location = new Point(0, EditTop + 68),
            Size = new Size(RowLeaderWidth, RowHeight),
            AutoSize = false
        };
        _evCheck.CheckedChanged += (_, _) => Touched();
        Controls.Add(_evCheck);

        var ivMax = MakeButton("Max", RowButtonX, EditTop + 42, RowButtonWidth);
        ivMax.Click += (_, _) => { foreach (TextBox box in _ivBoxes) box.Text = "31"; };
        var evZero = MakeButton("Zero", RowButtonX, EditTop + 68, RowButtonWidth);
        evZero.Click += (_, _) => { foreach (TextBox box in _evBoxes) box.Text = "0"; };

        Controls.Add(MakeLabel("Beat", 0, EditTop + 94, 32, ContentAlignment.MiddleLeft));
        _evSpecies = MakeCombo(34, EditTop + 94, 240);
        foreach (PokemonSpecies species in PokemonSpecies.GetList()) _evSpecies.Items.Add(species.Name);
        _evSpecies.SelectedIndex = 0;

        Controls.Add(MakeLabel("×", 282, EditTop + 94, 14));
        _evCount = MakeBox(300, EditTop + 94, 34);
        _evCount.TextAlign = HorizontalAlignment.Center;
        _evCount.Text = "1";
        Controls.Add(_evCount);

        var addEv = MakeButton("Add", 342, EditTop + 94, 54);
        addEv.Click += (_, _) => AddEvYield();

        _evTotal = MakeLabel("", PanelWidth - 150, EditTop + 94, 150, ContentAlignment.MiddleRight);
        Controls.Add(_evTotal);

        _status = new Label
        {
            Location = new Point(0, EditTop + 118),
            Size = new Size(PanelWidth, 26),
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = Theme.KeepForeColor
        };
        Controls.Add(_status);

        _buttonApply = MakeButton("Apply to Selected", 0, EditTop + 146, 150);
        _buttonApply.Click += (_, _) => ApplyToSelection();

        _buttonRescan = MakeButton("Rescan", 154, EditTop + 146, 80);
        _buttonRescan.Click += (_, _) => Rescan();

        var close = MakeButton("Close", PanelWidth - 80, EditTop + 146, 80);
        close.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        Controls.Add(MakeLabel("Load", 0, 0, 60, ContentAlignment.MiddleLeft));
        Controls.Add(MakeLabel("Save", 0, 26, 60, ContentAlignment.MiddleLeft));
        Controls.Add(_loadBox);
        Controls.Add(_saveBox);
        Controls.Add(browseLoad);
        Controls.Add(browseSave);
        Controls.Add(_list);
        Controls.Add(_natureCombo);
        Controls.Add(_ivCombo);
        Controls.Add(ivMax);
        Controls.Add(evZero);
        Controls.Add(_evSpecies);
        Controls.Add(addEv);
        Controls.Add(_buttonApply);
        Controls.Add(_buttonRescan);
        Controls.Add(close);

        UpdateReadouts();
    }

    public event EventHandler? CloseRequested;

    public Func<FilterPreset>? FilterSource { get; set; }

    public string LoadFolder
    {
        get => _loadBox.Text;
        set => _loadBox.Text = value ?? "";
    }

    public string SaveFolder
    {
        get => _saveBox.Text;
        set => _saveBox.Text = value ?? "";
    }

    public void Rescan()
    {
        _rescanDelay.Stop();
        _sticky = false;

        _status.Text = "Scanning…";
        _status.ForeColor = Theme.Text;

        _scanning = true;
        _buttonRescan.Enabled = false;
        _buttonApply.Enabled = false;

        string folder = LoadFolder;
        int generation = ++_scanGeneration;

        SynchronizationContext? ui = SynchronizationContext.Current;
        if (ui == null)
        {
            ShowScan(generation, Sweep(folder));
            return;
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            List<SavestateEntry> entries = Sweep(folder);
            ui.Post(_ => ShowScan(generation, entries), null);
        });
    }

    private static List<SavestateEntry> Sweep(string folder)
    {
        try
        {
            return SavestateEditor.Scan(folder, SavestateEditor.DefaultTargets);
        }
        catch (IOException)
        {
            return new List<SavestateEntry>();
        }
        catch (UnauthorizedAccessException)
        {
            return new List<SavestateEntry>();
        }
    }

    private void ShowScan(int generation, List<SavestateEntry> entries)
    {
        if (generation != _scanGeneration || IsDisposed) return;

        _scanning = false;
        _buttonRescan.Enabled = true;
        _entries = entries;

        int shared = SharedPrefix(_entries);

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (SavestateEntry entry in _entries)
        {
            var item = new ListViewItem(entry.Name[shared..]) { Tag = entry };
            item.SubItems.Add(entry.Status);
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        FitLastColumn();

        foreach (ListViewItem item in _list.Items)
        {
            item.Selected = item.Tag is SavestateEntry { Editable: true };
        }

        _sticky = false;

        UpdateReadouts();
    }

    private void ApplyToSelection()
    {
        var chosen = _list.SelectedItems.Cast<ListViewItem>()
            .Select(item => item.Tag as SavestateEntry)
            .Where(entry => entry is { Editable: true })
            .Cast<SavestateEntry>()
            .ToList();

        if (chosen.Count == 0)
        {
            Say("Nothing selected that holds a Squirtle, Wartortle, Blastoise or Mewtwo.", hit: false);
            return;
        }

        string destination = SaveFolder.Trim();
        if (destination.Length == 0)
        {
            Say("Set a save folder first.", hit: false);
            return;
        }

        MonEdit edit = BuildEdit();
        if (edit.ChangesNothing)
        {
            Say("Nothing to write: nature and IVs are both on keep and EVs are unticked.", hit: false);
            return;
        }

        if (SamePlace(destination, LoadFolder))
        {
            DialogResult answer = StarterTool.Modal(() => MessageBox.Show(this,
                "The save folder is the load folder, so the states will be overwritten in place.\r\n\r\n"
                + "Go ahead?",
                "Overwrite states", MessageBoxButtons.YesNo, MessageBoxIcon.Warning));
            if (answer != DialogResult.Yes) return;
        }

        int written = 0;
        foreach (SavestateEntry entry in chosen)
        {
            try
            {
                entry.Status = SavestateEditor.Apply(entry, destination, edit, _random);
                written++;
            }
            catch (Exception ex)
            {
                entry.Status = "failed: " + ex.Message;
            }
        }

        foreach (ListViewItem item in _list.Items)
        {
            if (item.Tag is SavestateEntry entry) item.SubItems[1].Text = entry.Status;
        }

        Say(written == chosen.Count
            ? $"Wrote {written} state{(written == 1 ? "" : "s")} to {destination}."
            : $"Wrote {written} of {chosen.Count}; the rest say why in their own row.",
            hit: written == chosen.Count);
    }

    private MonEdit BuildEdit()
    {
        ConstraintRange filter = (FilterSource?.Invoke() ?? new FilterPreset()).Normalize().Primary;

        var edit = new MonEdit
        {
            TargetSpecies = SavestateEditor.DefaultTargets,
            AllowedNatures = filter.Natures,
            IvMinus = ToPack(filter.IvMinus),
            IvNeutral = ToPack(filter.IvNeutral),
            IvPlus = ToPack(filter.IvPlus),
            Ivs = ReadRow(_ivBoxes, 31),
            Evs = ReadRow(_evBoxes, 255),
            EvMode = _evCheck.Checked ? EditMode.Specific : EditMode.Keep
        };

        edit.NatureMode = _natureCombo.SelectedIndex switch
        {
            ModeKeep => EditMode.Keep,
            ModeRandom => EditMode.Random,
            _ => EditMode.Specific
        };
        edit.NatureId = Math.Max(0, _natureCombo.SelectedIndex - 2);

        edit.IvMode = _ivCombo.SelectedIndex switch
        {
            ModeKeep => EditMode.Keep,
            ModeRandom => EditMode.Random,
            _ => EditMode.Specific
        };

        return edit;
    }

    private void AddEvYield()
    {
        List<PokemonSpecies> list = PokemonSpecies.GetList();
        int index = Math.Clamp(_evSpecies.SelectedIndex, 0, list.Count - 1);
        int[] yield = SpeciesTable.EvYield(list[index].Id);

        if (!int.TryParse(_evCount.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
        {
            count = 1;
        }
        count = Math.Clamp(count, 0, 999);

        int[] evs = ReadRow(_evBoxes, 255);
        for (int stat = 0; stat < 6; stat++)
        {
            evs[stat] = Math.Min(255, evs[stat] + yield[stat] * count);
        }
        WriteRow(_evBoxes, evs);

        _evCheck.Checked = true;
    }

    private void Touched()
    {
        _sticky = false;
        UpdateReadouts();
    }

    private void UpdateReadouts()
    {
        bool specificIvs = _ivCombo.SelectedIndex > ModeRandom;
        foreach (TextBox box in _ivBoxes) box.Enabled = specificIvs;
        foreach (TextBox box in _evBoxes) box.Enabled = _evCheck.Checked;

        int[] evs = ReadRow(_evBoxes, 255);
        int total = evs.Sum();
        _evTotal.Text = $"Total {total}/510";
        _evTotal.ForeColor = total > 510 ? Theme.LandingMissText : Theme.Text;

        int editable = _entries.Count(entry => entry.Editable);
        int selected = _list.SelectedItems.Cast<ListViewItem>()
            .Count(item => item.Tag is SavestateEntry { Editable: true });
        _buttonApply.Enabled = selected > 0 && !_scanning;

        if (_sticky || _scanning) return;

        _status.Text = _entries.Count == 0
            ? "No .gqs states in the load folder."
            : $"{editable} of {_entries.Count} states hold an editable mon; {selected} selected.";
        _status.ForeColor = Theme.Text;
    }

    private void Say(string message, bool hit)
    {
        _sticky = true;
        _status.Text = message;
        _status.ForeColor = hit ? Theme.LandingHitText : Theme.LandingMissText;
    }

    private void Browse(TextBox target, string description)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(target.Text) ? target.Text : ""
        };

        if (StarterTool.Modal(() => dialog.ShowDialog(this)) == DialogResult.OK)
        {
            target.Text = dialog.SelectedPath;
        }
    }

    private static bool SamePlace(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static int SharedPrefix(List<SavestateEntry> entries)
    {
        if (entries.Count < 2) return 0;

        string first = entries[0].Name;
        int shared = first.Length;
        foreach (SavestateEntry entry in entries)
        {
            shared = Math.Min(shared, entry.Name.Length);
            for (int at = 0; at < shared; at++)
            {
                if (first[at] != entry.Name[at])
                {
                    shared = at;
                    break;
                }
            }
        }

        foreach (SavestateEntry entry in entries)
        {
            if (entry.Name.Length == shared) return 0;
        }
        return shared;
    }

    private static StatPack ToPack(int[] values) =>
        new(values[0], values[1], values[2], values[3], values[4], values[5]);

    private static int[] ReadRow(TextBox[] boxes, int ceiling)
    {
        var values = new int[6];
        for (int stat = 0; stat < 6; stat++)
        {
            _ = int.TryParse(boxes[stat].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value);
            values[stat] = Math.Clamp(value, 0, ceiling);
        }
        return values;
    }

    private static void WriteRow(TextBox[] boxes, int[] values)
    {
        for (int stat = 0; stat < 6; stat++)
        {
            boxes[stat].Text = values[stat].ToString(CultureInfo.InvariantCulture);
        }
    }

    private static Label MakeLabel(string text, int x, int y, int width,
        ContentAlignment align = ContentAlignment.MiddleCenter) =>
        new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 16),
            TextAlign = align
        };

    private static TextBox MakeBox(int x, int y, int width, bool numeric = true) =>
        new ThemedTextBox
        {
            Numeric = numeric,
            AutoSize = false,
            Location = new Point(x, y),
            Size = new Size(width, RowHeight)
        };

    private static Button MakeButton(string text, int x, int y, int width) =>
        new ThemedButton { Text = text, Location = new Point(x, y), Size = new Size(width, RowHeight) };

    private static ThemedComboBox MakeCombo(int x, int y, int width)
    {
        var combo = new ThemedComboBox
        {
            Location = new Point(x, y),
            Size = new Size(width, RowHeight),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        combo.MatchHeight(RowHeight);
        return combo;
    }

    private const TextFormatFlags CellFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                                              | TextFormatFlags.NoPrefix | TextFormatFlags.Left;

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
        bool selected = e.Item?.Selected == true;
        bool editable = e.Item?.Tag is SavestateEntry { Editable: true };

        Color back = selected ? Theme.Accent : _list.BackColor;
        Color fore = selected ? Theme.AccentText : editable ? _list.ForeColor : Theme.DimText;

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
        bounds.Inflate(-3, 0);
        bounds.Height -= ThemedListView.RuleClearance;
        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", _list.Font, bounds, fore, CellFlags);
    }

    private void FitLastColumn()
    {
        int used = 0;
        for (int i = 0; i < _list.Columns.Count - 1; i++) used += _list.Columns[i].Width;

        ColumnHeader last = _list.Columns[_list.Columns.Count - 1];
        int fill = _list.ClientSize.Width - used;
        if (fill >= 60 && fill != last.Width) last.Width = fill;
    }
}
