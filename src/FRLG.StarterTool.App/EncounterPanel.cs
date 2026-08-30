using System.Globalization;
using FRLG.StarterTool.Core.Encounters;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public sealed class EncounterPanel : Panel
{
    private const int PanelWidth = 600;

    private const int PanelHeight = 640;

    private const int BoxInset = 6;

    private const int BoxTop = 18;

    private const int BoxBottomPad = 6;

    private const int SectionGap = 6;

    private const int TopBandHeight = 100;

    private const int OffsetWidth = 186;

    private const int FileLeft = OffsetWidth + SectionGap;
    private const int FileWidth = PanelWidth - FileLeft;

    private const int OptionLabelWidth = 52;

    private const int OptionBoxWidth =
        (PanelWidth - 2 * BoxInset - 2 * (OptionLabelWidth + 6) - 24) / 2;

    private const int OptionColumn2 = BoxInset + OptionLabelWidth + 6 + OptionBoxWidth + 24;

    private int _seedsHeight;

    private const int StatusHeight = 74;

    private const int VisibleRows = 5;

    private const int RowHeight = 20;

    private const int RowPitch = 22;

    private static readonly (int X, int Width)[] Columns =
    {
        (0, 200),
        (204, 60),
        (268, 60),
        (332, 66),
        (402, 60),
        (466, 68),
    };

    private const int RemoveX = 540;
    private const int ScrollX = 571;

    private static readonly string[] Headings =
        { "Path", "Rate", "Tiles", "Immunity", "Repel", "Encounters" };

    private const int Fields = 6;

    private readonly Label[] _headings = new Label[Headings.Length];
    private readonly TextBox[][] _slots = new TextBox[VisibleRows][];
    private readonly ThemedButton[] _remove = new ThemedButton[VisibleRows];
    private readonly VScrollBar _scroll;

    private readonly List<string[]> _rows = new();

    private int _top;

    private bool _binding;

    private readonly Label _labelDelay;

    private readonly TextBox _delay;
    private readonly Label _labelIntroFrame;
    private readonly TextBox _introFrame;
    private readonly Label _labelTitleFrame;
    private readonly TextBox _titleFrame;

    private readonly ListBox _routeList;

    private readonly ThemedButton _buttonRouteUpdate;
    private readonly ThemedButton _buttonRouteSave;
    private readonly ThemedButton _buttonRouteImport;
    private readonly ThemedButton _buttonRouteExport;
    private readonly ThemedButton _buttonRouteDelete;

    private readonly ThemedGroupBox _boxOffset;

    private readonly ThemedGroupBox _boxFile;
    private readonly ThemedGroupBox _boxSettings;
    private readonly ThemedGroupBox _boxTiles;
    private readonly ThemedGroupBox _boxSeeds;

    private readonly ThemedButton _buttonSearch;
    private readonly ThemedButton _buttonAdd;
    private readonly Label _labelCombo;
    private readonly ThemedComboBox _combo;

    private List<TitleCombo> _combos = new();

    private bool _settingOptions;

    private readonly Label _labelGame;
    private readonly ThemedComboBox _game;
    private readonly Label _labelButtons;
    private readonly ThemedComboBox _buttons;
    private readonly Label _labelSound;
    private readonly ThemedComboBox _sound;
    private readonly Label _labelIntro;
    private readonly ThemedComboBox _intro;
    private readonly Label _labelTitle;
    private readonly ThemedComboBox _title;
    private readonly ThemedListView _results;
    private readonly Label _status;

    private List<EncounterMatch> _matches = new();
    private List<EncounterPath> _searched = new();

    private List<EncounterNeighbourRow>? _around;

    private EncounterMatch? _picked;

    private bool _restoring;

    private (int Seed, int Frame, int Pass)? _highlight;

    private const int AroundFillColumn = 5;
    private List<TitleVariant> _skipped = new();
    private CancellationTokenSource? _cancel;

    private int _pickedSeed = -1;

    private int _pickedOffset;
    private int _pickedPass;

    private bool _writingPresses;

    private readonly List<EncounterRoutePreset> _routes = new();

    private bool _fillingRoutes;

    private int _routeIndex;

    public int Cycles { get; set; } = TitleSeedTable.CycleOffset;

    public event EventHandler? RoutesChanged;

    public EncounterPanel()
    {
        Size = new Size(PanelWidth, PanelHeight);

        _boxOffset = AddSection("Offset", 0, 0, OffsetWidth, TopBandHeight);

        const int offsetFieldX = BoxInset + 66 + 4;
        const int offsetFieldWidth = 92;

        _labelDelay = AddCaption(_boxOffset, "Delay (ms)", BoxInset, BoxTop + 2, 66);
        _delay = AddNumberBox(_boxOffset, offsetFieldX, BoxTop, offsetFieldWidth);

        int pressRow = BoxTop + 26;
        _labelIntroFrame = AddCaption(_boxOffset, "Intro", BoxInset, pressRow + 2, 66);
        _introFrame = AddNumberBox(_boxOffset, offsetFieldX, pressRow, offsetFieldWidth);

        pressRow += 26;
        _labelTitleFrame = AddCaption(_boxOffset, "Title", BoxInset, pressRow + 2, 66);
        _titleFrame = AddNumberBox(_boxOffset, offsetFieldX, pressRow, offsetFieldWidth);
        _titleFrame.TextChanged += (_, _) =>
        {
            if (_writingPresses) return;
            _pickedSeed = -1;
        };

        _boxFile = AddSection("File", FileLeft, 0, FileWidth, TopBandHeight);

        _routeList = new ListBox
        {
            Location = new Point(BoxInset, BoxTop),
            Size = new Size(FileWidth - 2 * BoxInset, TopBandHeight - BoxTop - 22 - 4 - BoxBottomPad),
            IntegralHeight = false,
            SelectionMode = SelectionMode.One,
        };
        _routeList.DoubleClick += (_, _) => RenameSelectedRoute();
        _routeList.SelectedIndexChanged += (_, _) => RouteSelected();
        _boxFile.Controls.Add(_routeList);

        int routeButtonY = _routeList.Bottom + 4;
        int routeButtonWidth = (FileWidth - 2 * BoxInset - 4 * 4) / 5;
        ThemedButton RouteButton(string text, int slot, Action click)
        {
            var button = new ThemedButton
            {
                Text = text,
                Location = new Point(BoxInset + slot * (routeButtonWidth + 4), routeButtonY),
                Size = new Size(routeButtonWidth, 22),
            };
            button.Click += (_, _) => click();
            _boxFile.Controls.Add(button);
            return button;
        }

        _buttonRouteUpdate = RouteButton("Update", 0, UpdateSelectedRoute);
        _buttonRouteSave = RouteButton("Save", 1, SaveRouteAs);
        _buttonRouteImport = RouteButton("Import", 2, ImportRoute);
        _buttonRouteExport = RouteButton("Export", 3, ExportSelectedRoute);
        _buttonRouteDelete = RouteButton("Delete", 4, DeleteRoute);
        FillRoutes("");

        _boxSettings = AddSection("Game Settings", 0, TopBandHeight + SectionGap, PanelWidth, TopBandHeight);

        int optionRow = BoxTop;
        _labelGame = AddCaption(_boxSettings, "Game", BoxInset, optionRow + 4, OptionLabelWidth);
        _game = AddOptionBox(BoxInset + OptionLabelWidth + 6, optionRow, OptionBoxWidth);
        _game.Items.AddRange(new object[] { "FireRed", "LeafGreen" });
        _game.SelectedIndex = 0;
        _game.SelectedIndexChanged += (_, _) => OptionsChanged();

        _labelCombo = AddCaption(_boxSettings, "Combo", OptionColumn2, optionRow + 4, OptionLabelWidth);
        _combo = AddOptionBox(OptionColumn2 + OptionLabelWidth + 6, optionRow, OptionBoxWidth);

        optionRow += 26;
        _labelButtons = AddCaption(_boxSettings, "Buttons", BoxInset, optionRow + 4, OptionLabelWidth);
        _buttons = AddOptionBox(BoxInset + OptionLabelWidth + 6, optionRow, OptionBoxWidth);
        _buttons.Items.AddRange(new object[] { "Help", "L=A" });
        _buttons.SelectedIndex = 0;
        _buttons.SelectedIndexChanged += (_, _) => { FillCombos(Variant.Combo); OptionsChanged(); };
        FillCombos(null);
        _combo.SelectedIndexChanged += (_, _) => OptionsChanged();

        _labelSound = AddCaption(_boxSettings, "Sound", OptionColumn2, optionRow + 4, OptionLabelWidth);
        _sound = AddOptionBox(OptionColumn2 + OptionLabelWidth + 6, optionRow, OptionBoxWidth);
        _sound.Items.AddRange(new object[] { "Mono", "Stereo", "Any" });
        _sound.SelectedIndex = 0;
        _sound.SelectedIndexChanged += (_, _) => OptionsChanged();

        optionRow += 26;

        _labelIntro = AddCaption(_boxSettings, "Intro", BoxInset, optionRow + 4, OptionLabelWidth);
        _intro = AddOptionBox(BoxInset + OptionLabelWidth + 6, optionRow, OptionBoxWidth);
        _intro.Items.AddRange(new object[] { "Played", "Skip 477", "Skip 990", "Any" });
        _intro.SelectedIndex = 0;
        _intro.SelectedIndexChanged += (_, _) => OptionsChanged();

        _labelTitle = AddCaption(_boxSettings, "Title", OptionColumn2, optionRow + 4, OptionLabelWidth);
        _title = AddOptionBox(OptionColumn2 + OptionLabelWidth + 6, optionRow, OptionBoxWidth);
        _title.Items.AddRange(new object[] { "Either", "Played", "Skipped" });
        _title.SelectedIndex = 0;
        _title.SelectedIndexChanged += (_, _) => OptionsChanged();

        _boxSettings.Height = _title.Bottom + BoxBottomPad;

        _boxTiles = AddSection("Tiles", 0, _boxSettings.Bottom + SectionGap, PanelWidth, 0);

        for (int column = 0; column < Headings.Length; column++)
        {
            var heading = new Label
            {
                Text = Headings[column],
                Location = new Point(BoxInset + Columns[column].X, BoxTop),
                Size = new Size(Columns[column].Width, 16),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            _boxTiles.Controls.Add(heading);
            _headings[column] = heading;
        }

        int rowsTop = BoxTop + 18;
        for (int row = 0; row < VisibleRows; row++)
        {
            int top = rowsTop + row * RowPitch;
            int slot = row;
            _slots[row] = new TextBox[Fields];
            for (int field = 0; field < Fields; field++)
            {
                TextBox box = AddBox(field, top, field == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Center);
                int which = field;
                box.TextChanged += (_, _) => SlotEdited(slot, which, box.Text);
                box.MouseWheel += (_, e) => ScrollBy(e.Delta);
                _slots[row][field] = box;
            }

            int boxHeight = _slots[row][0].Height;
            _remove[row] = new ThemedButton
            {
                Glyph = ThemedButton.CrossGlyph,
                Location = new Point(BoxInset + RemoveX, top),
                Size = new Size(boxHeight + 2, boxHeight),
                Tag = Theme.NudgeButtonTag,
            };
            _remove[row].Click += (_, _) => RemovePath(_top + slot);
            _boxTiles.Controls.Add(_remove[row]);
        }

        _scroll = new VScrollBar
        {
            Location = new Point(BoxInset + ScrollX, rowsTop),
            Size = new Size(17, VisibleRows * RowPitch - 2),
            Minimum = 0,
            SmallChange = 1,
            LargeChange = VisibleRows,
        };
        _scroll.ValueChanged += (_, _) =>
        {
            if (_top == _scroll.Value) return;
            _top = _scroll.Value;
            Bind();
        };
        _boxTiles.Controls.Add(_scroll);

        int tileButtonRow = rowsTop + VisibleRows * RowPitch + 4;
        _buttonSearch = new ThemedButton
        {
            Text = "Search",
            Location = new Point(BoxInset, tileButtonRow),
            Size = new Size(90, 24),
        };
        _buttonSearch.Click += (_, _) => Search();
        _boxTiles.Controls.Add(_buttonSearch);

        _buttonAdd = new ThemedButton
        {
            Text = "+ Path",
            Location = new Point(BoxInset + ScrollX + 17 - 70, tileButtonRow),
            Size = new Size(70, 24),
        };
        _buttonAdd.Click += (_, _) => AddPath();
        _boxTiles.Controls.Add(_buttonAdd);
        _boxTiles.Height = _buttonAdd.Bottom + BoxBottomPad;

        int seedsTop = _boxTiles.Bottom + SectionGap;
        _seedsHeight = PanelHeight - seedsTop;
        _boxSeeds = AddSection("Seeds", 0, seedsTop, PanelWidth, _seedsHeight);

        _results = new ThemedListView
        {
            Location = new Point(BoxInset, BoxTop),
            Size = new Size(PanelWidth - 2 * BoxInset, _seedsHeight - BoxTop - StatusHeight - 4 - BoxBottomPad),
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Font = new Font("Segoe UI", 8F),
            OwnerDraw = true,
        };
        _results.DrawColumnHeader += DrawColumnHeader;
        _results.DrawItem += (_, _) => { };
        _results.DrawSubItem += DrawSubItem;
        _results.Columns.Add("Frame", 46, HorizontalAlignment.Center);
        _results.Columns.Add("Loops", 44, HorizontalAlignment.Center);
        _results.Columns.Add("Time", 46, HorizontalAlignment.Center);
        _results.Columns.Add("Win", 34, HorizontalAlignment.Center);
        _results.Columns.Add("Seed", 44, HorizontalAlignment.Center);
        _results.Columns.Add("Enc", 34, HorizontalAlignment.Center);
        _results.Columns.Add("Rate", 44, HorizontalAlignment.Center);
        _results.Columns.Add("Where", 78, HorizontalAlignment.Left);
        _results.HandleCreated += (_, _) => FitLastColumn();
        _results.SelectedIndexChanged += (_, _) => ShowSelected();
        _results.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Escape || _around is null) return;
            RestoreResults();
            e.Handled = true;
        };
        _boxSeeds.Controls.Add(_results);

        _status = new Label
        {
            Location = new Point(BoxInset, _seedsHeight - BoxBottomPad - StatusHeight),
            Size = new Size(PanelWidth - 2 * BoxInset, StatusHeight),
            TextAlign = ContentAlignment.TopLeft,
        };
        _boxSeeds.Controls.Add(_status);

        LoadRoute(EncounterPath.DefaultRoute);
    }

    private ThemedGroupBox AddSection(string text, int x, int y, int width, int height)
    {
        var box = new ThemedGroupBox
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
        };
        Controls.Add(box);
        return box;
    }

    private Label AddCaption(Control parent, string text, int x, int y, int width)
    {
        var label = new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 16),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        parent.Controls.Add(label);
        return label;
    }

    private ThemedComboBox AddOptionBox(int x, int y, int width)
    {
        var box = new ThemedComboBox
        {
            Location = new Point(x, y),
            Size = new Size(width, RowHeight),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _boxSettings.Controls.Add(box);
        return box;
    }

    private TextBox AddNumberBox(Control parent, int x, int y, int width)
    {
        var box = new ThemedTextBox
        {
            Numeric = true,
            Location = new Point(x, y),
            Size = new Size(width, RowHeight),
            TextAlign = HorizontalAlignment.Center,
        };
        parent.Controls.Add(box);
        return box;
    }

    private TextBox AddBox(int column, int top, HorizontalAlignment align = HorizontalAlignment.Center)
    {
        var box = new ThemedTextBox
        {
            Numeric = column > 0,
            Location = new Point(BoxInset + Columns[column].X, top),
            Size = new Size(Columns[column].Width, RowHeight),
            TextAlign = align,
        };
        _boxTiles.Controls.Add(box);
        return box;
    }

    private void Bind()
    {
        _binding = true;
        try
        {
            int max = Math.Max(0, _rows.Count - VisibleRows);
            _top = Math.Clamp(_top, 0, max);

            for (int slot = 0; slot < VisibleRows; slot++)
            {
                int index = _top + slot;
                bool held = index < _rows.Count;
                for (int field = 0; field < Fields; field++)
                {
                    _slots[slot][field].Text = held ? _rows[index][field] : "";
                    _slots[slot][field].Enabled = held;
                }
                _remove[slot].Enabled = held;
            }

            _scroll.Maximum = Math.Max(0, _rows.Count - 1);
            _scroll.LargeChange = VisibleRows;
            _scroll.Enabled = _rows.Count > VisibleRows;
            if (_scroll.Value != _top) _scroll.Value = _top;
        }
        finally
        {
            _binding = false;
        }
    }

    private void SlotEdited(int slot, int field, string text)
    {
        if (_binding) return;

        int index = _top + slot;
        if (index >= _rows.Count) return;

        _rows[index][field] = text;
    }

    private void ScrollBy(int wheelDelta)
    {
        if (!_scroll.Enabled) return;

        int step = wheelDelta > 0 ? -1 : 1;
        int max = Math.Max(0, _rows.Count - VisibleRows);
        int next = Math.Clamp(_top + step, 0, max);
        if (next == _top) return;

        _top = next;
        Bind();
    }

    private void AddPath()
    {
        _rows.Add(new[] { "", "21", "", "", "", "" });
        _top = Math.Max(0, _rows.Count - VisibleRows);
        Bind();

        int slot = _rows.Count - 1 - _top;
        _slots[slot][0].Focus();
    }

    private void RemovePath(int index)
    {
        if (index < 0 || index >= _rows.Count) return;

        _rows.RemoveAt(index);
        Bind();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollBy(e.Delta);
    }

    public string SaveRoute()
    {
        var lines = new List<string>();
        foreach (EncounterPath path in ReadRoute(strict: false))
        {
            lines.Add(string.Join(',', path.Name.Replace(',', ' '), path.Rate, path.Tiles,
                path.NewMap ? 1 : 0, path.MinSteps, path.RepelTiles,
                path.TargetEncounters?.ToString(CultureInfo.InvariantCulture) ?? ""));
        }
        return string.Join('\n', lines);
    }

    public TitleVariant Variant
    {
        get => new(
            _buttons.SelectedIndex == 1 ? TitleButtonMode.LEqualsA : TitleButtonMode.Help,
            _sound.SelectedIndex == 1 ? TitleSoundMode.Stereo : TitleSoundMode.Mono,
            _intro.SelectedIndex switch
            {
                1 => TitleIntro.Skip477,
                2 => TitleIntro.Skip990,
                _ => TitleIntro.Played,
            },
            _title.SelectedIndex switch
            {
                1 => TitleAnimation.PlayedOut,
                2 => TitleAnimation.SpedUp,
                _ => TitleAnimation.Either,
            },
            _combo.SelectedIndex >= 1 && _combo.SelectedIndex - 1 < _combos.Count ? _combos[_combo.SelectedIndex - 1] : null,
            _game.SelectedIndex == 1 ? TitleGame.LeafGreen : TitleGame.FireRed);
        set
        {
            bool was = _settingOptions;
            _settingOptions = true;
            try
            {
                _game.SelectedIndex = value.Game == TitleGame.LeafGreen ? 1 : 0;
                _buttons.SelectedIndex = value.Buttons == TitleButtonMode.LEqualsA ? 1 : 0;
                FillCombos(value.Combo);
                _sound.SelectedIndex = value.Sound == TitleSoundMode.Stereo ? 1 : 0;
                _intro.SelectedIndex = value.Intro switch
                {
                    TitleIntro.Skip477 => 1,
                    TitleIntro.Skip990 => 2,
                    _ => 0,
                };
                _title.SelectedIndex = value.Animation switch
                {
                    TitleAnimation.PlayedOut => 1,
                    TitleAnimation.SpedUp => 2,
                    _ => 0,
                };
            }
            finally
            {
                _settingOptions = was;
            }
        }
    }

    private void OptionsChanged()
    {
        if (_settingOptions || _matches.Count == 0 || _cancel is not null) return;
        Search();
    }

    private void FillCombos(TitleCombo? keep)
    {
        TitleButtonMode mode = _buttons.SelectedIndex == 1 ? TitleButtonMode.LEqualsA : TitleButtonMode.Help;
        _combos = TitleCombos.All(mode).ToList();

        bool was = _settingOptions;
        _settingOptions = true;
        try
        {
            _combo.BeginUpdate();
            _combo.Items.Clear();
            _combo.Items.Add("Any");
            foreach (TitleCombo combo in _combos) _combo.Items.Add(combo.Short);
            _combo.EndUpdate();

            int index = keep is TitleCombo wanted ? _combos.IndexOf(wanted) : -1;
            _combo.SelectedIndex = index + 1;
        }
        finally
        {
            _settingOptions = was;
        }
    }

    public bool SoundAny
    {
        get => _sound.SelectedIndex == 2;
        set
        {
            bool was = _settingOptions;
            _settingOptions = true;
            try { if (value) _sound.SelectedIndex = 2; else if (_sound.SelectedIndex == 2) _sound.SelectedIndex = 0; }
            finally { _settingOptions = was; }
        }
    }

    public bool IntroAny
    {
        get => _intro.SelectedIndex == 3;
        set
        {
            bool was = _settingOptions;
            _settingOptions = true;
            try { if (value) _intro.SelectedIndex = 3; else if (_intro.SelectedIndex == 3) _intro.SelectedIndex = 0; }
            finally { _settingOptions = was; }
        }
    }

    private string SoundKey => SoundAny ? "any" : Variant.SoundKey;

    private string IntroKey => IntroAny ? "any" : Variant.IntroKey;

    public int DelayMs
    {
        get => SignedNumber(_delay.Text) ?? 0;
        set => WritePress(_delay, value == 0 ? "" : value.ToString(CultureInfo.InvariantCulture));
    }

    public int IntroFrame
    {
        get => ParsePress(_introFrame.Text).Frame;
        set => WritePress(_introFrame, value, IntroWindow);
    }

    public int IntroWindow
    {
        get => ParsePress(_introFrame.Text).Window;
        set => WritePress(_introFrame, IntroFrame, value);
    }

    public int TitleFrame
    {
        get => ParsePress(_titleFrame.Text).Frame;
        set => WritePress(_titleFrame, value, TitleWindow);
    }

    public int TitleWindow
    {
        get => ParsePress(_titleFrame.Text).Window;
        set => WritePress(_titleFrame, TitleFrame, value);
    }

    private static (int Frame, int Window) ParsePress(string text)
    {
        string value = (text ?? "").Trim();
        int dash = value.IndexOf('-');
        if (dash <= 0) return (Number(value) ?? 0, 1);

        int? first = Number(value[..dash]);
        if (first is null) return (0, 1);

        int? last = Number(value[(dash + 1)..]);
        if (last is null || last <= first) return (first.Value, 1);
        return (first.Value, Math.Min(last.Value - first.Value + 1, MaxWindow));
    }

    private const int MaxWindow = 60;

    private void WritePress(TextBox box, int frame, int window) => WritePress(
        box,
        frame <= 0 ? "" : new ManipPress("", frame, Math.Clamp(window, 1, MaxWindow)).Frames);

    private void WritePress(TextBox box, string text)
    {
        _writingPresses = true;
        try
        {
            box.Text = text;
        }
        finally
        {
            _writingPresses = false;
        }
    }

    private void FillPresses(PressFrame press)
    {
        _writingPresses = true;
        try
        {
            if (press.Variant.IntroSkipped)
            {
                WritePress(_introFrame, TitleSeedTable.IntroFrameOf(press.Variant), press.IntroWindow);
            }
            else
            {
                _introFrame.Text = "";
            }

            WritePress(_titleFrame, ResetFrame(press), press.Window);
            _pickedSeed = press.Seed;
            _pickedOffset = press.Offset;
            _pickedPass = press.Pass;
        }
        finally
        {
            _writingPresses = false;
        }
    }

    public List<EncounterRoutePreset> Routes => _routes.Select(route => route.Clone()).ToList();

    public EncounterRoutePreset? FindRoute(string name)
    {
        foreach (EncounterRoutePreset route in _routes)
        {
            if (EncounterRoutePreset.NameEquals(route.Name, name)) return route.Clone();
        }
        return null;
    }

    public string ActiveRoute => _routeIndex >= 0 && _routeIndex < _routes.Count ? _routes[_routeIndex].Name : "";

    public void SetRoutes(IEnumerable<EncounterRoutePreset> routes, string active)
    {
        _routes.Clear();
        foreach (EncounterRoutePreset route in routes) _routes.Add(route.Clone().Normalize());
        FillRoutes(active);
    }

    private void FillRoutes(string active)
    {
        _fillingRoutes = true;
        try
        {
            _routeList.BeginUpdate();
            _routeList.Items.Clear();
            foreach (EncounterRoutePreset route in _routes) _routeList.Items.Add(route.Name);
            _routeList.EndUpdate();

            int index = -1;
            for (int i = 0; i < _routes.Count; i++)
            {
                if (EncounterRoutePreset.NameEquals(_routes[i].Name, active)) index = i;
            }
            _routeList.SelectedIndex = index;
            _routeIndex = index;
        }
        finally
        {
            _fillingRoutes = false;
        }

        EnableRouteButtons();
    }

    private void EnableRouteButtons()
    {
        bool any = _routeIndex >= 0 && _routeIndex < _routes.Count;
        _buttonRouteUpdate.Enabled = any;
        _buttonRouteExport.Enabled = any;
        _buttonRouteDelete.Enabled = any;
    }

    private void RouteSelected()
    {
        if (_fillingRoutes) return;

        _routeIndex = _routeList.SelectedIndex;
        EnableRouteButtons();
        LoadSelectedRoute();
    }

    private void LoadSelectedRoute()
    {
        if (_routeIndex < 0 || _routeIndex >= _routes.Count) return;

        LoadPreset(_routes[_routeIndex]);
    }

    private void SaveRouteAs()
    {
        string? name = PromptForName("Save route", "Route name:", ActiveRoute);
        if (name is null) return;

        EncounterRoutePreset preset = CaptureRoute(name);
        int existing = _routes.FindIndex(route => EncounterRoutePreset.NameEquals(route.Name, name));
        if (existing >= 0)
        {
            if (!Confirm($"A route named \"{_routes[existing].Name}\" already exists. Overwrite it?", "Save route")) return;

            _routes[existing] = preset;
        }
        else
        {
            _routes.Add(preset);
        }

        FillRoutes(name);
        _status.Text = existing >= 0 ? $"Route \"{name}\" updated." : $"Route \"{name}\" saved.";
        RoutesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSelectedRoute()
    {
        if (_routeIndex < 0 || _routeIndex >= _routes.Count) return;

        string name = _routes[_routeIndex].Name;
        _routes[_routeIndex] = CaptureRoute(name);
        FillRoutes(name);
        _status.Text = $"Route \"{name}\" updated.";
        RoutesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenameSelectedRoute()
    {
        if (_routeIndex < 0 || _routeIndex >= _routes.Count) return;

        EncounterRoutePreset active = _routes[_routeIndex];
        string? name = PromptForName("Rename route", "New name for this route:", active.Name);
        if (name is null || EncounterRoutePreset.NameEquals(name, active.Name)) return;

        int clash = _routes.FindIndex(route => EncounterRoutePreset.NameEquals(route.Name, name));
        if (clash >= 0)
        {
            if (!Confirm($"A route named \"{_routes[clash].Name}\" already exists. Overwrite it?", "Rename route")) return;

            _routes.RemoveAt(clash);
        }

        active.Name = name;
        FillRoutes(name);
        _status.Text = $"Route \"{name}\" renamed.";
        RoutesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DeleteRoute()
    {
        if (_routeIndex < 0 || _routeIndex >= _routes.Count) return;

        string name = _routes[_routeIndex].Name;
        if (!Confirm($"Delete the route \"{name}\"?", "Delete route")) return;

        _routes.RemoveAt(_routeIndex);
        FillRoutes("");
        _status.Text = $"Route \"{name}\" deleted.";
        RoutesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ImportRoute()
    {
        Form owner = FindForm() ?? throw new InvalidOperationException("The planner has no window.");
        using var dialog = new OpenFileDialog
        {
            Title = "Import route",
            Filter = "Route (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (StarterTool.Modal(() => dialog.ShowDialog(owner)) != DialogResult.OK) return;

        string path = dialog.FileName;
        EncounterRoutePreset? preset;
        try
        {
            preset = PresetFile.Read<EncounterRoutePreset>(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _status.Text = $"Could not read \"{Path.GetFileName(path)}\": {ex.Message}";
            return;
        }
        if (preset == null)
        {
            _status.Text = $"\"{Path.GetFileName(path)}\" holds no route.";
            return;
        }

        preset.Normalize();
        if (preset.Name.Length == 0) preset.Name = Path.GetFileNameWithoutExtension(path);

        int existing = _routes.FindIndex(route => EncounterRoutePreset.NameEquals(route.Name, preset.Name));
        if (existing >= 0)
        {
            if (!Confirm($"A route named \"{_routes[existing].Name}\" already exists. Overwrite it?", "Import route")) return;

            _routes[existing] = preset;
        }
        else
        {
            _routes.Add(preset);
        }

        FillRoutes(preset.Name);
        LoadPreset(preset);
        RoutesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ExportSelectedRoute()
    {
        if (_routeIndex < 0 || _routeIndex >= _routes.Count) return;

        EncounterRoutePreset route = _routes[_routeIndex];
        Form owner = FindForm() ?? throw new InvalidOperationException("The planner has no window.");
        using var dialog = new SaveFileDialog
        {
            Title = "Export route",
            Filter = "Route (*.json)|*.json|All files (*.*)|*.*",
            FileName = MainForm.PresetFileName(route.Name),
            OverwritePrompt = true,
        };
        if (StarterTool.Modal(() => dialog.ShowDialog(owner)) != DialogResult.OK) return;

        try
        {
            PresetFile.Write(dialog.FileName, route);
            _status.Text = $"Route \"{route.Name}\" exported to {Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = $"Could not write \"{Path.GetFileName(dialog.FileName)}\": {ex.Message}";
        }
    }

    private string? PromptForName(string title, string prompt, string initialValue)
    {
        Form owner = FindForm() ?? throw new InvalidOperationException("The planner has no window.");
        using var dialog = new TextPromptDialog(title, prompt, initialValue);
        if (StarterTool.Modal(() => dialog.ShowDialog(owner)) != DialogResult.OK) return null;

        return dialog.Value.Length == 0 ? null : dialog.Value;
    }

    private bool Confirm(string message, string title)
    {
        Form owner = FindForm() ?? throw new InvalidOperationException("The planner has no window.");
        return StarterTool.Modal(() => MessageBox.Show(
            owner, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question)) == DialogResult.Yes;
    }

    private EncounterRoutePreset CaptureRoute(string name) => new EncounterRoutePreset
    {
        Name = name,
        Route = SaveRoute(),
        Game = Variant.GameKey,
        Buttons = Variant.ButtonsKey,
        Sound = SoundKey,
        Intro = IntroKey,
        Title = Variant.AnimationKey,
        Combo = Variant.ComboKey,
        DelayMs = DelayMs,
        IntroFrame = IntroFrame,
        IntroWindow = IntroWindow,
        TitleFrame = TitleFrame,
        TitleWindow = TitleWindow,
        Seed = _pickedSeed,
        Offset = _pickedOffset,
        Pass = _pickedPass,
    }.Normalize();

    private void LoadPreset(EncounterRoutePreset preset)
    {
        LoadRoute(preset.Route);
        Variant = TitleVariant.Parse(preset.Buttons, preset.Sound, preset.Intro, preset.Title, preset.Combo, preset.Game);
        SoundAny = preset.Sound == "any";
        IntroAny = preset.Intro == "any";
        DelayMs = preset.DelayMs;
        IntroFrame = preset.IntroFrame;
        IntroWindow = preset.IntroWindow;
        TitleFrame = preset.TitleFrame;
        TitleWindow = preset.TitleWindow;
        _pickedSeed = preset.Seed;
        _pickedOffset = preset.Offset;
        _pickedPass = preset.Pass;

        _status.Text = DescribePresses(preset);

        if (preset.Seed >= 0)
        {
            _highlight = (preset.Seed, preset.TitleFrame, preset.Pass);
            Search();
        }
    }

    private static string DescribePresses(EncounterRoutePreset preset)
    {
        List<ManipPress> presses = preset.Presses();
        if (presses.Count == 0) return $"Route \"{preset.Name}\" loaded - no presses set; pick a row or type a Title frame.";

        string list = string.Join(", ", presses.Select(press => $"{press.Name} on frame {press.Frames}"));
        string seed = preset.Seed >= 0 ? $" for Trainer ID {preset.Seed:X4}" : "";
        string delay = preset.DelayMs == 0 ? "" : $", delay {preset.DelayMs:+#;-#} ms";
        return $"Route \"{preset.Name}\" loaded: {list}{seed}{delay}.";
    }

    public void LoadRoute(string saved)
    {
        if (string.IsNullOrWhiteSpace(saved))
        {
            LoadRoute(EncounterPath.DefaultRoute);
            return;
        }

        var paths = new List<EncounterPath>();
        foreach (string line in saved.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = line.Split(',');
            if (fields.Length < 7) continue;

            paths.Add(new EncounterPath(
                fields[0],
                Number(fields[1]) ?? 21,
                Number(fields[2]) ?? 0,
                fields[3] == "1",
                Number(fields[4]) ?? 0,
                Number(fields[5]) ?? 0,
                Number(fields[6])));
        }

        if (paths.Count == 0) paths.AddRange(EncounterPath.DefaultRoute);
        LoadRoute(paths);
    }

    private void LoadRoute(IReadOnlyList<EncounterPath> paths)
    {
        _rows.Clear();
        foreach (EncounterPath path in paths)
        {
            _rows.Add(new[]
            {
                path.Name,
                path.Rate.ToString(CultureInfo.InvariantCulture),
                path.Tiles.ToString(CultureInfo.InvariantCulture),
                path.MinSteps.ToString(CultureInfo.InvariantCulture),
                path.RepelTiles == 0 ? "" : path.RepelTiles.ToString(CultureInfo.InvariantCulture),
                path.TargetEncounters?.ToString(CultureInfo.InvariantCulture) ?? "",
            });
        }
        _top = 0;
        Bind();

        ClearRows();
        _matches = new List<EncounterMatch>();
        _status.Text = "";
    }

    private List<EncounterPath> ReadRoute(bool strict)
    {
        var paths = new List<EncounterPath>();
        foreach (string[] row in _rows)
        {
            string name = row[0].Trim();
            int tiles = Number(row[2]) ?? 0;
            if (strict && (name.Length == 0 || tiles <= 0)) continue;
            if (!strict && name.Length == 0 && tiles == 0 && row[5].Length == 0) continue;

            int rate = Number(row[1]) ?? 21;

            int immune = Number(row[3]) ?? EncounterPath.DefaultMinSteps(rate);

            paths.Add(new EncounterPath(
                name, rate, tiles, NewMap: true, immune,
                Math.Min(tiles, Number(row[4]) ?? 0),
                Number(row[5])));
        }
        return paths;
    }

    private static int? Number(string text) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
        && value >= 0 ? value : null;

    private static int? SignedNumber(string text) =>
        int.TryParse(text.Trim(), NumberStyles.Integer | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value)
            ? value : null;

    private async void Search()
    {
        if (_cancel is not null)
        {
            _highlight = null;
            return;
        }

        List<EncounterPath> route = ReadRoute(strict: true);
        if (route.Count == 0)
        {
            _status.Text = "Nothing to search: give at least one path a name and a tile count.";
            _highlight = null;
            return;
        }

        List<TitleVariant> variants = Variants();
        var missing = variants.Where(v => !TitleSeedTable.HasRta(v)).ToList();
        variants.RemoveAll(v => !TitleSeedTable.HasRta(v));
        if (variants.Count == 0)
        {
            ClearRows();
            _matches = new List<EncounterMatch>();
            TitleVariant variant = missing[0];
            _status.Text = variant.Intro switch
            {
                TitleIntro.Skip990 => $"No RTA table for {variant} yet - the 990 skip is a table of its own per pair of options.",
                TitleIntro.Skip477 when variant.OwnsTable =>
                    $"No table for {variant} yet - that combo reads a seed of its own off the 477 skip on every frame (2026-08-25), "
                    + "a table nobody has swept for this pair.",
                TitleIntro.Skip477 when variant.Combo is not null && TitleSeedTable.IntroSkipShiftOf(variant with { Combo = null }) is not null =>
                    $"No 477 term for {variant} - the term is per combo, and this one read a different seed on every frame "
                    + "off its boot (2026-08-25), so it would be a table of its own that nobody has swept. Measured at 477: "
                    + "SELECT or START skipping with A→START, START→A, A→L or START alone; A skipping with START→L / L→START; L skipping with A→START.",
                TitleIntro.Skip477 => $"No 477 term for {variant} yet - the skip's constant is the button mode's "
                                      + "(Help +6004, L=A +6015, on either sound), and this pair's has not been booted.",
                _ when variant.Table.Combo is not null =>
                    $"No table for {variant} yet - that combo reads a seed of its own (L first, A or L alone, or a skip made with A or L) "
                    + "and has not been swept for this pair.",
                _ when variant.Game == TitleGame.LeafGreen =>
                    $"No RTA table for {variant} yet - LeafGreen's seeds are a table of their own per pair of options "
                    + "(no title lag frames, the read elsewhere in its frame; 2026-08-28), swept on the LeafGreen ROM. Help + stereo ships.",
                _ => $"No RTA table for {variant} yet - that pair of options has not been swept "
                     + "off a save set to it. All four button x sound pairs ship.",
            };
            NoHighlight();
            return;
        }

        _cancel = new CancellationTokenSource();
        _buttonSearch.Enabled = false;
        _status.Text = "Searching every reachable seed...";
        ClearRows();
        _matches = new List<EncounterMatch>();

        try
        {
            CancellationToken token = _cancel.Token;
            int cycles = Cycles;
            EncounterSearchResult result = await Task.Run(
                () => EncounterSearch.Search(route, cycles: cycles, protocol: TitleProtocol.Rta,
                    variants: variants, cancellationToken: token),
                token);
            _skipped = missing;

            _searched = route;
            _matches = result.Matches;
            ShowResults(result);
            ApplyHighlight();
        }
        catch (OperationCanceledException)
        {
            _status.Text = "";
        }
        finally
        {
            _highlight = null;
            _cancel.Dispose();
            _cancel = null;
            _buttonSearch.Enabled = true;
        }
    }

    private List<TitleVariant> Variants()
    {
        TitleVariant chosen = Variant;
        TitleSoundMode[] sounds = SoundAny
            ? new[] { TitleSoundMode.Mono, TitleSoundMode.Stereo }
            : new[] { chosen.Sound };
        TitleIntro[] intros = IntroAny
            ? new[] { TitleIntro.Played, TitleIntro.Skip477, TitleIntro.Skip990 }
            : new[] { chosen.Intro };

        var variants = new List<TitleVariant>();
        foreach (TitleSoundMode sound in sounds)
        {
            foreach (TitleIntro intro in intros)
            {
                variants.Add(chosen with { Sound = sound, Intro = intro });
            }
        }
        return variants;
    }

    private void ShowResults(EncounterSearchResult result)
    {
        FillRows(result.Matches);

        if (result.Matches.Count == 0)
        {
            string rows = Variant.Animation switch
            {
                TitleAnimation.PlayedOut => " on the rows from 268 (Title: plays out)",
                TitleAnimation.SpedUp => " on the rows before 268 (Title: sped up)",
                _ => "",
            };
            string combo = Variant.Combo is TitleCombo chosen
                ? $" that {chosen.Short} has the presses for ({chosen.Count} button{(chosen.Count == 1 ? "" : "s")}: "
                  + (chosen.Count == 3 ? "intro skipped and title sped up"
                     : chosen.Count == 2 ? "intro skipped or title sped up, not both"
                     : "intro played, title played out") + ")"
                : "";
            _status.Text = result.SeedsMatched == 0
                ? "No seed runs that route, on any of the sampled main streams."
                : $"That route is reachable, but no measured press frame reaches it{rows}{combo}.";
            return;
        }

        string capped = result.Truncated
            ? $" of {result.TotalMatches.ToString("N0", CultureInfo.InvariantCulture)}"
            : "";
        string skipped = _skipped.Count == 0
            ? ""
            : $" Not swept, so not searched: {string.Join("; ", _skipped)}.";
        string under = Variant.Combo is TitleCombo chosenCombo
            ? $" under {chosenCombo.Short}"
            : " under each table's own combo";
        _status.Text =
            $"{result.Matches.Count.ToString("N0", CultureInfo.InvariantCulture)}{capped} press frames{under}, "
            + $"{result.SeedsMatched.ToString("N0", CultureInfo.InvariantCulture)} of 65536 seeds. "
            + "Pick a row for the press - it fills Intro and Title above." + skipped;
    }

    private void FillRows(IReadOnlyList<EncounterMatch> matches)
    {
        _results.BeginUpdate();
        ClearRows();

        _results.Columns[1].Text = "Loops";

        foreach (EncounterMatch match in matches)
        {
            var item = new ListViewItem(ResetFrame(match.Press).ToString(CultureInfo.InvariantCulture));
            item.SubItems.Add(match.Press.Pass.ToString(CultureInfo.InvariantCulture));
            item.SubItems.Add(Wait(match.Press));
            item.SubItems.Add(Windows(match.Press));
            item.SubItems.Add(match.Press.Seed.ToString("X4", CultureInfo.InvariantCulture));
            item.SubItems.Add(match.Total.ToString(CultureInfo.InvariantCulture));
            item.SubItems.Add((match.Rate * 100).ToString("0", CultureInfo.InvariantCulture) + "%");
            item.SubItems.Add(Where(match.PathCounts));
            _results.Items.Add(item);
        }

        _results.EndUpdate();
    }

    private void ClearRows()
    {
        _around = null;
        _picked = null;
        ApplyColumns(around: false);
        _results.Items.Clear();
    }

    private static readonly (string Text, int Width, HorizontalAlignment Align)[] SearchColumns =
    {
        ("Frame", 46, HorizontalAlignment.Center), ("Loops", 44, HorizontalAlignment.Center),
        ("Time", 46, HorizontalAlignment.Center), ("Win", 34, HorizontalAlignment.Center),
        ("Seed", 44, HorizontalAlignment.Center), ("Enc", 34, HorizontalAlignment.Center),
        ("Rate", 44, HorizontalAlignment.Center), ("Where", 78, HorizontalAlignment.Left),
    };

    private static readonly (string Text, int Width, HorizontalAlignment Align)[] AroundColumns =
    {
        ("Win", 76, HorizontalAlignment.Center), ("Frame", 78, HorizontalAlignment.Center),
        ("Seed", 48, HorizontalAlignment.Center), ("Enc", 34, HorizontalAlignment.Center),
        ("Rate", 44, HorizontalAlignment.Center), ("Where", 78, HorizontalAlignment.Left),
    };

    private void ApplyColumns(bool around)
    {
        var shape = around ? AroundColumns : SearchColumns;
        for (int i = 0; i < _results.Columns.Count; i++)
        {
            ColumnHeader column = _results.Columns[i];
            if (i < shape.Length)
            {
                column.Text = shape[i].Text;
                column.TextAlign = shape[i].Align;
                column.Width = shape[i].Width;
            }
            else
            {
                column.Width = 0;
            }
        }
        FitLastColumn();
    }

    private static int ResetFrame(PressFrame press) =>
        TitleSeedTable.IntroAnchorOf(press.Variant.Intro) + press.WaitFrames;

    private static string Windows(PressFrame press)
    {
        string window = press.Window.ToString(CultureInfo.InvariantCulture);
        return press.IntroWindow > 0
            ? press.IntroWindow.ToString(CultureInfo.InvariantCulture) + "→" + window
            : window;
    }

    private static string Wait(PressFrame press)
    {
        double seconds = ResetFrame(press) / TitleSeedTable.FramesPerSecond;
        return seconds < 90
            ? seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s"
            : (seconds / 60).ToString("0.0", CultureInfo.InvariantCulture) + "m";
    }

    private string Where(IReadOnlyList<int> counts)
    {
        var parts = new List<string>();
        for (int path = 0; path < counts.Count && path < _searched.Count; path++)
        {
            if (counts[path] == 0) continue;
            parts.Add(counts[path] == 1
                ? _searched[path].Name
                : $"{_searched[path].Name} x{counts[path]}");
        }
        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }

    private void ShowSelected()
    {
        if (_restoring) return;
        if (_around is not null)
        {
            ShowAroundSelected();
            return;
        }
        if (_results.SelectedIndices.Count == 0 || _results.SelectedIndices[0] >= _matches.Count) return;

        EncounterMatch match = _matches[_results.SelectedIndices[0]];

        FillPresses(match.Press);

        if (match.Press.Protocol == TitleProtocol.Rta)
        {
            ShowSelectedRta(match);
            return;
        }

        string loops = match.Press.Pass == 0
            ? "on the first title screen"
            : $"after {match.Press.Pass} title-screen loop{(match.Press.Pass == 1 ? "" : "s")}";

        _status.Text =
            $"Press {match.Press.Offset} frames in, {loops} - {Wait(match.Press)} from the reset. "
            + $"Trainer ID {match.Press.Seed:X4} (counter {match.Press.Recorded:X4}), "
            + $"wild seed {match.WildSeed:X4}. "
            + $"{match.Total} encounter{(match.Total == 1 ? "" : "s")}: {Where(match.PathCounts)}"
            + $" - on {match.Rate * 100:0}% of sampled main streams.";
    }

    private void ShowSelectedRta(EncounterMatch match)
    {
        DescribeRta(match);
        BeginInvoke(new Action(() => ShowAround(match)));
    }

    private void DescribeRta(EncounterMatch match)
    {
        PressFrame press = match.Press;
        TitleVariant variant = press.Variant;
        IReadOnlyList<TitleButton> order = TitleCombos.Of(press).Order;
        int at = 0;

        string loops = press.Pass == 0
            ? ""
            : $", {press.Pass} loop{(press.Pass == 1 ? "" : "s")} ({Wait(press)} from the reset; arithmetic, up to {press.Band} cycles off)";
        string settings = $"{variant.PairTable}, intro {(variant.IntroSkipped ? "skipped at " + TitleSeedTable.IntroFrameOf(variant.Intro) : "played")}"
            + $", title {(press.SeedPressFrame is null ? "played out" : "sped up")}, {TitleCombos.Of(press).Short}{loops}."
            + $" Trainer ID {press.Seed:X4}.";

        string intro = "";
        if (variant.IntroSkipped)
        {
            int first = TitleSeedTable.IntroFrameOf(variant);
            int last = first + press.IntroWindow - 1;
            string frames = press.IntroWindow > 1 ? $"{first}-{last}" : first.ToString(CultureInfo.InvariantCulture);
            intro = $"\nSkip: {TitleCombo.Name(order[at++])} on {frames} from reset.";
        }

        int frame = ResetFrame(press);
        string window = press.Window > 1 ? $"{frame}-{frame + press.Window - 1}" : frame.ToString(CultureInfo.InvariantCulture);
        string title = press.SeedPressFrame is null
            ? $"\nTitle (played out): {TitleCombo.Name(order[at])} on {window} from reset."
            : $"\nTitle (speed-up): {TitleCombo.Name(order[at])} on {window} from reset, {TitleCombo.Name(order[at + 1])} next frame.";

        string flag = TitleCombos.IsSwept(press) || TitleCombos.IsMeasured(press)
            ? ""
            : $" Table swept with {TitleCombos.Swept(press).Short}; this combo is unmeasured.";

        _status.Text = settings + intro + title + flag + " Esc: back to the search.";
    }

    private void ShowAround(EncounterMatch match)
    {
        if (_matches.Count == 0 || !_matches.Contains(match)) return;

        List<EncounterNeighbourRow> rows = Neighbours(match);
        _results.BeginUpdate();
        _results.Items.Clear();
        _picked = match;
        _around = rows;
        ApplyColumns(around: true);
        foreach (EncounterNeighbourRow row in rows)
        {
            var item = new ListViewItem(row.Window);
            item.SubItems.Add(row.Frames);
            item.SubItems.Add(row.Seed.ToString("X4", CultureInfo.InvariantCulture));
            item.SubItems.Add(row.Encounters.ToString(CultureInfo.InvariantCulture));
            item.SubItems.Add((row.Rate * 100).ToString("0", CultureInfo.InvariantCulture) + "%");
            item.SubItems.Add(row.Where);
            while (item.SubItems.Count < _results.Columns.Count) item.SubItems.Add("");
            _results.Items.Add(item);
        }
        _results.EndUpdate();
    }

    private void RestoreResults()
    {
        EncounterMatch? picked = _picked;
        FillRows(_matches);
        int index = picked is null ? -1 : _matches.IndexOf(picked);
        if (index < 0) return;
        _restoring = true;
        try
        {
            _results.Items[index].Selected = true;
            _results.EnsureVisible(index);
        }
        finally
        {
            _restoring = false;
        }
        if (picked!.Press.Protocol == TitleProtocol.Rta) DescribeRta(picked);
    }

    private void ApplyHighlight()
    {
        if (_highlight is not (int seed, int frame, int pass)) return;
        _highlight = null;

        int index = -1;
        for (int i = 0; i < _matches.Count && i < _results.Items.Count; i++)
        {
            PressFrame press = _matches[i].Press;
            if (press.Seed != seed) continue;
            if (index < 0) index = i;
            if (ResetFrame(press) == frame && press.Pass == pass)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            _status.Text += $" The route's Trainer ID {seed:X4} is not on these rows - its own presses stand, "
                + "but the table or the route has moved since it was saved.";
            return;
        }

        _restoring = true;
        try
        {
            _results.Items[index].Selected = true;
            _results.Items[index].Focused = true;
            _results.EnsureVisible(index);
        }
        finally
        {
            _restoring = false;
        }
        _status.Text += $" The route's Trainer ID {seed:X4} is the highlighted row.";
    }

    private void NoHighlight()
    {
        if (_highlight is not (int seed, _, _)) return;
        _highlight = null;
        _status.Text += $" The route's Trainer ID {seed:X4} is on a table that is not searched here.";
    }

    private void ShowAroundSelected()
    {
        if (_around is null || _results.SelectedIndices.Count == 0 || _results.SelectedIndices[0] >= _around.Count) return;
        EncounterNeighbourRow row = _around[_results.SelectedIndices[0]];
        string kept = row.Chance > 0 ? "keeps the picked encounters" : "loses the picked encounters";
        _status.Text = $"{row.Window}: title press on frame {row.Frames}, Trainer ID {row.Seed:X4} - {row.Encounters} encounter{(row.Encounters == 1 ? "" : "s")}"
            + $" ({row.Where}) on {row.Rate * 100:0}% of sampled streams; {kept}. Esc: back to the search.";
    }

    private List<EncounterNeighbourRow> Neighbours(EncounterMatch match)
    {
        PressFrame press = match.Press;
        int frame = ResetFrame(press);
        var groups = new List<(int First, int Last, int Seed)>();
        for (int delta = -AroundFrames; delta < press.Window + AroundFrames; delta++)
        {
            if (TitleSeedTable.SeedAt(press.Offset + delta, press.Pass, press.Cycles, press.Variant) is not int seed) continue;
            if (groups.Count > 0 && groups[^1].Seed == seed && groups[^1].Last == delta - 1)
            {
                groups[^1] = (groups[^1].First, delta, seed);
                continue;
            }
            groups.Add((delta, delta, seed));
        }

        string skip = press.Variant.IntroSkipped ? "0 → " : "";
        var rows = new List<EncounterNeighbourRow>();
        foreach ((int first, int last, int seed) in groups)
        {
            EncounterOutcome outcome = EncounterSearch.Evaluate(_searched, seed);
            bool same = outcome.PathCounts.SequenceEqual(match.PathCounts);
            int early = Offset(first, press.Window);
            int late = Offset(last, press.Window);
            string label = early == late ? Delta(early) : Delta(early) + ".." + Delta(late);
            string frames = first == last
                ? (frame + first).ToString(CultureInfo.InvariantCulture)
                : $"{frame + first}-{frame + last}";
            rows.Add(new EncounterNeighbourRow(skip + label, frames, seed, outcome.ModeTotal, outcome.ModeRate,
                Where(outcome.ModePathCounts), same ? outcome.Rate : 0.0));
        }
        return rows;
    }

    private const int AroundFrames = 5;

    private static int Offset(int delta, int window) =>
        delta < 0 ? delta : delta < window ? 0 : delta - window + 1;

    private static string Delta(int frames) =>
        frames > 0 ? "+" + frames.ToString(CultureInfo.InvariantCulture) : frames.ToString(CultureInfo.InvariantCulture);

    private const TextFormatFlags CellFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                                              | TextFormatFlags.NoPrefix;

    private static TextFormatFlags Align(HorizontalAlignment alignment) => alignment switch
    {
        HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
        HorizontalAlignment.Right => TextFormatFlags.Right,
        _ => TextFormatFlags.Left,
    };

    private void DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using (var background = new SolidBrush(Theme.HeaderBack))
        {
            e.Graphics.FillRectangle(background, e.Bounds);
        }
        using (var pen = new Pen(Theme.Border))
        {
            if (e.ColumnIndex < _results.Columns.Count - 1)
            {
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 2, e.Bounds.Right - 1, e.Bounds.Bottom - 3);
            }
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        Rectangle bounds = e.Bounds;
        bounds.Inflate(-2, 0);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", _results.Font, bounds, Theme.Text,
            CellFlags | Align(e.Header?.TextAlign ?? HorizontalAlignment.Left));
    }

    private void DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        bool selected = e.Item?.Selected == true;
        Color back = selected ? Theme.Accent : _results.BackColor;
        Color fore = selected ? Theme.AccentText : _results.ForeColor;
        if (!selected && _around is not null && e.ItemIndex >= 0 && e.ItemIndex < _around.Count)
        {
            double chance = _around[e.ItemIndex].Chance;
            back = chance > 0.5 ? Theme.LandingHitBack : chance > 0.0 ? Theme.LandingMaybeBack : Theme.LandingMissBack;
            fore = Theme.LandingRowText;
        }

        using (var brush = new SolidBrush(back))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }
        using (var pen = new Pen(Theme.GridLine))
        {
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            if (e.ColumnIndex < _results.Columns.Count - 1)
            {
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom - 1);
            }
        }

        Rectangle bounds = e.Bounds;
        bounds.Inflate(-3, 0);
        bounds.Height -= ThemedListView.RuleClearance;
        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", _results.Font, bounds, fore,
            CellFlags | Align(_results.Columns[e.ColumnIndex].TextAlign));
    }

    private void FitLastColumn()
    {
        int fillIndex = _around is null ? _results.Columns.Count - 1 : AroundFillColumn;
        int used = 0;
        for (int i = 0; i < fillIndex; i++) used += _results.Columns[i].Width;

        ColumnHeader last = _results.Columns[fillIndex];
        int fill = _results.ClientSize.Width - used;
        if (fill >= 60 && fill != last.Width) last.Width = fill;
    }

    public void Cancel() => _cancel?.Cancel();
}
