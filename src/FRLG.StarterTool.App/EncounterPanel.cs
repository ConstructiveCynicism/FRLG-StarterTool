using System.Globalization;
using FRLG.StarterTool.Core.Encounters;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public sealed class EncounterPanel : Panel
{
    private const int PanelWidth = 600;

    private const int PanelHeight = 666;

    private const int BoxInset = 6;

    private const int BoxTop = 18;

    private const int BoxBottomPad = 6;

    private const int SectionGap = 6;

    private const int TopBandHeight = 126;

    private const int OffsetWidth = 186;

    private const int FileLeft = OffsetWidth + SectionGap;
    private const int FileWidth = PanelWidth - FileLeft;

    private static readonly int[] OptionFields = { 86, 86, 86, 86 };

    private const int OptionCaptionGap = 6;

    private int _seedsHeight;

    private const int StatusHeight = 108;

    private const int VisibleRows = 5;

    private const int RowHeight = 20;

    private const int RowPitch = 22;

    private static readonly (int X, int Width)[] Columns =
    {
        (0, 150),
        (154, 52),
        (210, 52),
        (266, 60),
        (330, 66),
        (400, 56),
        (460, 74),
    };

    private const int RemoveX = 540;
    private const int ScrollX = 571;

    private static readonly string[] Headings =
        { "Path", "Rate", "Tiles", "Patches", "Immunity", "Repel", "Encounters" };

    private const int Fields = 7;

    private readonly Label[] _headings = new Label[Headings.Length];
    private readonly TextBox[][] _slots = new TextBox[VisibleRows][];
    private readonly ThemedButton[] _remove = new ThemedButton[VisibleRows];
    private readonly VScrollBar _scroll;

    private readonly List<string[]> _rows = new();

    private int _top;

    private bool _binding;

    private readonly Label _labelDelay;

    private readonly TextBox _delay;
    private readonly Label _labelOffsetMs;
    private readonly TextBox _offsetMs;
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

    private bool _settingOptions;

    private readonly Label _labelGame;
    private readonly ThemedComboBox _game;
    private readonly Label _labelButtons;
    private readonly ThemedComboBox _buttons;
    private readonly Label _labelSaves;
    private readonly ThemedComboBox _saves;
    private readonly Label _labelMax;
    private readonly TextBox _maxSeconds;
    private readonly ThemedListView _results;
    private readonly ManipDetail _status;

    private readonly ThemedGroupBox _boxTileTable;

    private readonly ThemedListView _tileList;
    private readonly ThemedButton _buttonBack;
    private readonly ThemedButton _buttonScan;
    private readonly HScrollBar _tileScroll;

    private int _tileSeed;

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

        const int offsetFieldX = BoxInset + 66 + 6;
        const int offsetFieldWidth = OffsetWidth - BoxInset - offsetFieldX;

        _labelDelay = AddCaption(_boxOffset, "Delay (ms)", BoxInset, BoxTop + 2, 66);
        _delay = AddNumberBox(_boxOffset, offsetFieldX, BoxTop, offsetFieldWidth);

        int offsetRow = BoxTop + 26;
        _labelOffsetMs = AddCaption(_boxOffset, "Offset (ms)", BoxInset, offsetRow + 2, 66);
        _offsetMs = AddNumberBox(_boxOffset, offsetFieldX, offsetRow, offsetFieldWidth);

        int pressRow = BoxTop + 52;
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
        string[] captions = { "Game", "Buttons", "Save", "Max Time (s)" };
        var captionWidths = new int[captions.Length];
        int used = 0;
        for (int i = 0; i < captions.Length; i++)
        {
            captionWidths[i] = TextRenderer.MeasureText(captions[i], Font).Width + 2;
            used += captionWidths[i] + OptionCaptionGap + OptionFields[i];
        }
        int cellGap = (PanelWidth - 2 * BoxInset - used) / (captions.Length - 1);
        var cellX = new int[captions.Length];
        for (int i = 0, x = BoxInset; i < captions.Length; i++)
        {
            cellX[i] = x;
            x += captionWidths[i] + OptionCaptionGap + OptionFields[i] + cellGap;
        }
        int FieldX(int cell) => cellX[cell] + captionWidths[cell] + OptionCaptionGap;

        _labelGame = AddCaption(_boxSettings, captions[0], cellX[0], optionRow + 4, captionWidths[0]);
        _game = AddOptionBox(FieldX(0), optionRow, OptionFields[0]);
        _game.Items.AddRange(new object[] { "FireRed", "LeafGreen" });
        _game.SelectedIndex = 0;
        _game.SelectedIndexChanged += (_, _) => OptionsChanged();

        _labelButtons = AddCaption(_boxSettings, captions[1], cellX[1], optionRow + 4, captionWidths[1]);
        _buttons = AddOptionBox(FieldX(1), optionRow, OptionFields[1]);
        _buttons.Items.AddRange(new object[] { "Help", "L=A", "Either" });
        _buttons.SelectedIndex = 0;
        _buttons.SelectedIndexChanged += (_, _) => OptionsChanged();

        _labelSaves = AddCaption(_boxSettings, captions[2], cellX[2], optionRow + 4, captionWidths[2]);
        _saves = AddOptionBox(FieldX(2), optionRow, OptionFields[2]);
        _saves.Items.AddRange(new object[] { "Multi", "Single", "Either" });
        _saves.SelectedIndex = 0;
        _saves.SelectedIndexChanged += (_, _) => OptionsChanged();

        _labelMax = AddCaption(_boxSettings, captions[3], cellX[3], optionRow + 4, captionWidths[3]);
        _maxSeconds = AddNumberBox(_boxSettings, FieldX(3), optionRow, OptionFields[3]);
        _maxSeconds.AutoSize = false;
        _maxSeconds.Height = _game.Height;
        _maxSeconds.Leave += (_, _) => MaxSecondsCommitted();
        _maxSeconds.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            MaxSecondsCommitted();
            e.Handled = e.SuppressKeyPress = true;
        };

        _boxSettings.Height = _game.Bottom + BoxBottomPad;

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

        int rowsBottom = _remove[VisibleRows - 1].Bottom;
        _scroll = new VScrollBar
        {
            Location = new Point(BoxInset + ScrollX, rowsTop + 2),
            Size = new Size(17, rowsBottom - rowsTop - 4),
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

        _boxTileTable = AddSection("Tiles Table", 0, _boxTiles.Top, PanelWidth, _boxTiles.Height);
        _boxTileTable.Visible = false;

        _tileList = new ThemedListView
        {
            Location = new Point(BoxInset, BoxTop),
            Size = new Size(PanelWidth - 2 * BoxInset, tileButtonRow - 4 - BoxTop),
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Font = new Font("Segoe UI", 8F),
            OwnerDraw = true,
        };
        _tileList.DrawColumnHeader += DrawColumnHeader;
        _tileList.DrawItem += (_, _) => { };
        _tileList.DrawSubItem += DrawSubItem;
        _tileList.Columns.Add("Path", 120, HorizontalAlignment.Left);
        _tileList.Columns.Add("Tiles", 44, HorizontalAlignment.Center);
        _tileList.Columns.Add("Enc", 40, HorizontalAlignment.Center);
        _tileList.Columns.Add("%", 44, HorizontalAlignment.Center);
        _tileList.Columns.Add("Encounter On Tile", 200, HorizontalAlignment.Left);
        _tileList.HandleCreated += (_, _) => _tileList.BeginInvoke(() =>
        {
            FitLastColumn(_tileList, _tileList.Columns.Count - 1);
            FitTileScroll();
        });
        _tileList.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Escape || _around is null) return;
            RestoreResults();
            e.Handled = true;
        };
        _boxTileTable.Controls.Add(_tileList);

        _buttonBack = new ThemedButton
        {
            Text = "Back",
            Location = new Point(BoxInset, tileButtonRow),
            Size = new Size(90, 24),
        };
        _buttonBack.Click += (_, _) => RestoreResults();
        _boxTileTable.Controls.Add(_buttonBack);

        _buttonScan = new ThemedButton
        {
            Text = "Scan",
            Location = new Point(_buttonBack.Right + 4, tileButtonRow),
            Size = new Size(90, 24),
        };
        _buttonScan.Click += (_, _) => ScanTiles();
        _boxTileTable.Controls.Add(_buttonScan);

        _tileScroll = new HScrollBar
        {
            Location = new Point(_buttonScan.Right + 8, tileButtonRow + 3),
            Size = new Size(PanelWidth - BoxInset - _buttonScan.Right - 8, 17),
            Minimum = 0,
            SmallChange = 8,
            Visible = false,
        };
        _tileScroll.ValueChanged += (_, _) => _tileList.Invalidate();
        _boxTileTable.Controls.Add(_tileScroll);

        int seedsTop = _boxTiles.Bottom + SectionGap;
        _seedsHeight = PanelHeight - seedsTop;
        _boxSeeds = AddSection("Seeds", 0, seedsTop, PanelWidth, _seedsHeight);

        _results = new ThemedListView
        {
            Location = new Point(BoxInset, BoxTop),
            Size = new Size(PanelWidth - 2 * BoxInset, _seedsHeight - BoxTop - StatusHeight - SectionGap - BoxBottomPad),
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
        _results.Columns.Add("Win", 52, HorizontalAlignment.Center);
        _results.Columns.Add("Seed", 44, HorizontalAlignment.Center);
        _results.Columns.Add("Enc", 34, HorizontalAlignment.Center);
        _results.Columns.Add("Rate", 44, HorizontalAlignment.Center);
        _results.Columns.Add("Where", 78, HorizontalAlignment.Left);
        _results.HandleCreated += (_, _) => FitLastColumn();
        _results.SelectedIndexChanged += (_, _) => ShowSelected();
        _results.DoubleClick += (_, _) => SearchAroundSelected();
        _results.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Escape || _around is null) return;
            RestoreResults();
            e.Handled = true;
        };
        _boxSeeds.Controls.Add(_results);

        _status = new ManipDetail
        {
            Location = new Point(BoxInset, _seedsHeight - BoxBottomPad - StatusHeight),
            Size = new Size(PanelWidth - 2 * BoxInset, StatusHeight),
            Font = new Font("Segoe UI", 8F),
        };
        _status.RowClicked += ToggleCue;
        _introFrame.TextChanged += (_, _) => _status.SetCued(CuedRows());
        _titleFrame.TextChanged += (_, _) => _status.SetCued(CuedRows());
        _boxSeeds.Controls.Add(_status);

        LoadRoute(EncounterPath.PlannerDefault);
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
        _rows.Add(new[] { "", "21", "", "1", "", "", "" });
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
                path.TargetEncounters?.ToString(CultureInfo.InvariantCulture) ?? "",
                path.Patches));
        }
        return string.Join('\n', lines);
    }

    public TitleVariant Variant
    {
        get => new(
            _buttons.SelectedIndex == 1 ? TitleButtonMode.LEqualsA : TitleButtonMode.Help,
            TitleSoundMode.Mono,
            Game: _game.SelectedIndex == 1 ? TitleGame.LeafGreen : TitleGame.FireRed,
            Saves: _saves.SelectedIndex == 1 ? TitleSaves.Single : TitleSaves.Multi);
        set
        {
            bool was = _settingOptions;
            _settingOptions = true;
            try
            {
                _game.SelectedIndex = value.Game == TitleGame.LeafGreen ? 1 : 0;
                _buttons.SelectedIndex = value.Buttons == TitleButtonMode.LEqualsA ? 1 : 0;
                _saves.SelectedIndex = value.Saves == TitleSaves.Single ? 1 : 0;
            }
            finally
            {
                _settingOptions = was;
            }
        }
    }

    public bool SavesEither
    {
        get => _saves.SelectedIndex == 2;
        set
        {
            bool was = _settingOptions;
            _settingOptions = true;
            try { if (value) _saves.SelectedIndex = 2; else if (_saves.SelectedIndex == 2) _saves.SelectedIndex = 0; }
            finally { _settingOptions = was; }
        }
    }

    public string SavesKey => SavesEither ? "either" : Variant.SavesKey;

    public bool ButtonsEither
    {
        get => _buttons.SelectedIndex == 2;
        set
        {
            bool was = _settingOptions;
            _settingOptions = true;
            try { if (value) _buttons.SelectedIndex = 2; else if (_buttons.SelectedIndex == 2) _buttons.SelectedIndex = 0; }
            finally { _settingOptions = was; }
        }
    }

    public string ButtonsKey => ButtonsEither ? "either" : Variant.ButtonsKey;

    public int MaxSeconds
    {
        get => Number(_maxSeconds.Text) ?? 0;
        set
        {
            bool was = _settingOptions;
            _settingOptions = true;
            try { _maxSeconds.Text = value <= 0 ? "" : value.ToString(CultureInfo.InvariantCulture); }
            finally { _settingOptions = was; }
            _searchedMaxSeconds = MaxSeconds;
        }
    }

    private int _searchedMaxSeconds;

    private void MaxSecondsCommitted()
    {
        if (MaxSeconds == _searchedMaxSeconds) return;
        _searchedMaxSeconds = MaxSeconds;
        OptionsChanged();
    }

    private void OptionsChanged()
    {
        if (_settingOptions || _matches.Count == 0 || _cancel is not null) return;
        Search();
    }

    public int DelayMs
    {
        get => SignedNumber(_delay.Text) ?? 0;
        set => WritePress(_delay, value == 0 ? "" : value.ToString(CultureInfo.InvariantCulture));
    }

    public int? OffsetMs
    {
        get => SignedNumber(_offsetMs.Text);
        set => WritePress(_offsetMs, value is int offset ? offset.ToString(CultureInfo.InvariantCulture) : "");
    }

    public int IntroFrame
    {
        get => PressAt(_introFrame, 0).Frame;
        set => WriteIntroBox(value, IntroWindow, LoopFrame, LoopWindow, IntroExtra);
    }

    public int IntroWindow
    {
        get => PressAt(_introFrame, 0).Window;
        set => WriteIntroBox(IntroFrame, value, LoopFrame, LoopWindow, IntroExtra);
    }

    public int LoopFrame
    {
        get => PressAt(_introFrame, 1).Frame;
        set => WriteIntroBox(IntroFrame, IntroWindow, value, LoopWindow, IntroExtra);
    }

    public int LoopWindow
    {
        get => PressAt(_introFrame, 1).Window;
        set => WriteIntroBox(IntroFrame, IntroWindow, LoopFrame, value, IntroExtra);
    }

    public string IntroExtra
    {
        get => ExtraOf(_introFrame, 2);
        set => WriteIntroBox(IntroFrame, IntroWindow, LoopFrame, LoopWindow, value);
    }

    private static string[] Places(string text) => (text ?? "").Split(ManipPress.Separators);

    private static (int Frame, int Window) PressAt(TextBox box, int place)
    {
        string[] places = Places(box.Text);
        return place < places.Length ? ParsePress(places[place]) : (0, 1);
    }

    private static string ExtraOf(TextBox box, int from) =>
        ManipPress.FormatList(ManipPress.ParseList("", string.Join(',', Places(box.Text).Skip(from))));

    private void WriteIntroBox(int introFrame, int introWindow, int loopFrame, int loopWindow, string extra)
    {
        var places = new List<string> { Press(introFrame, introWindow) };
        if (loopFrame > 0) places.Add(Press(loopFrame, loopWindow));
        places.AddRange(ManipPress.ParseList("", extra).Select(press => press.Frames));
        WritePress(_introFrame, places.Count == 1 ? places[0] : string.Join(",", places));
    }

    public int TitleFrame
    {
        get => PressAt(_titleFrame, 0).Frame;
        set => WriteTitleBox(value, TitleWindow, TitleExtra);
    }

    public int TitleWindow
    {
        get => PressAt(_titleFrame, 0).Window;
        set => WriteTitleBox(TitleFrame, value, TitleExtra);
    }

    public string TitleExtra
    {
        get => ExtraOf(_titleFrame, 1);
        set => WriteTitleBox(TitleFrame, TitleWindow, value);
    }

    private void WriteTitleBox(int frame, int window, string extra)
    {
        var places = new List<string> { Press(frame, window) };
        places.AddRange(ManipPress.ParseList("", extra).Select(press => press.Frames));
        WritePress(_titleFrame, places.Count == 1 ? places[0] : string.Join(",", places));
    }

    private static (int Frame, int Window) ParsePress(string text) =>
        ManipPress.Parse("", text) is ManipPress press ? (press.Frame, press.Window) : (0, 1);

    private const int MaxWindow = ManipPress.MaxWindow;

    private static string Press(int frame, int window) =>
        frame <= 0 ? "" : new ManipPress("", frame, Math.Clamp(window, 1, MaxWindow)).Frames;

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
            if (TitleRecipes.Find(press.Variant) is TitleRecipe recipe)
            {
                WritePress(_introFrame, string.Join(",", recipe.Steps.Where(step => step.Cued).Select(step => step.Press.Frames)));
            }
            else
            {
                WriteIntroBox(
                    press.Variant.IntroSkipped ? TitleSeedTable.IntroFrameOf(press.Variant) : 0, press.IntroWindow,
                    press.Variant.LoopSkipped ? TitleSeedTable.LoopFrameOf(press.Variant) : 0, TitleSeedTable.LoopWindowOf(press.Variant),
                    "");
            }

            WriteTitleBox(ResetFrame(press), press.Window, "");
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

    public (int Seed, int Offset, int Pass) PickedSeed
    {
        get => (_pickedSeed, _pickedOffset, _pickedPass);
        set => (_pickedSeed, _pickedOffset, _pickedPass) = value;
    }

    public bool LoadActiveRoute()
    {
        if (_routeIndex < 0 || _routeIndex >= _routes.Count) return false;

        LoadSelectedRoute();
        return true;
    }

    public void FindPickedSeed()
    {
        if (_pickedSeed < 0) return;

        _highlight = (_pickedSeed, TitleFrame, _pickedPass);
        Search();
    }

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

            _routes[existing] = KeepReference(_routes[existing], preset);
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
        _routes[_routeIndex] = KeepReference(_routes[_routeIndex], CaptureRoute(name));
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
        if (clash >= 0 && !Confirm($"A route named \"{_routes[clash].Name}\" already exists. Overwrite it?", "Rename route")) return;

        if (!OnDisk(() =>
            {
                if (clash >= 0) PresetLibrary.Default.DeleteRoute(_routes[clash].Name);
                PresetLibrary.Default.RenameRoute(active.Name, name);
            }, "rename")) return;
        if (clash >= 0) _routes.RemoveAt(clash);

        active.Name = name;
        FillRoutes(name);
        _status.Text = $"Route \"{name}\" renamed.";
        RoutesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DeleteRoute()
    {
        if (_routeIndex < 0 || _routeIndex >= _routes.Count) return;

        string name = _routes[_routeIndex].Name;
        if (!Confirm($"Delete the route \"{name}\" and its reference pictures?", "Delete route")) return;
        if (!OnDisk(() => PresetLibrary.Default.DeleteRoute(name), "delete")) return;

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
            Title = "Import route - a route's .json, alone or in its folder of references",
            Filter = "Route (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (StarterTool.Modal(() => dialog.ShowDialog(owner)) != DialogResult.OK) return;

        string path = dialog.FileName;
        EncounterRoutePreset? preset;
        Dictionary<string, byte[]> pictures;
        try
        {
            preset = PresetLibrary.ReadImport(path, out pictures);
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

        int existing = _routes.FindIndex(route => EncounterRoutePreset.NameEquals(route.Name, preset.Name));
        if (existing >= 0 && !Confirm($"A route named \"{_routes[existing].Name}\" already exists. Overwrite it?", "Import route")) return;

        if (!OnDisk(() => PresetLibrary.Default.InstallRoute(preset, pictures), "import")) return;
        if (existing >= 0)
        {
            _routes[existing] = preset;
        }
        else
        {
            _routes.Add(preset);
        }

        FillRoutes(preset.Name);
        LoadPreset(preset);
        int count = preset.References.Count;
        _status.Text = count > 0
            ? $"Route \"{preset.Name}\" imported with {count} reference picture{(count == 1 ? "" : "s")}."
            : $"Route \"{preset.Name}\" imported.";
        RoutesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ExportSelectedRoute()
    {
        if (_routeIndex < 0 || _routeIndex >= _routes.Count) return;

        EncounterRoutePreset route = _routes[_routeIndex];
        Form owner = FindForm() ?? throw new InvalidOperationException("The planner has no window.");
        using var dialog = new FolderBrowserDialog
        {
            Description = $"Export \"{route.Name}\": pick where its folder goes",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
        };
        if (StarterTool.Modal(() => dialog.ShowDialog(owner)) != DialogResult.OK) return;

        try
        {
            PresetLibrary.Default.Sync(Array.Empty<FilterPreset>(), new[] { route });
            string folder = PresetLibrary.Default.ExportRoute(route, dialog.SelectedPath);
            _status.Text = $"Route \"{route.Name}\" exported to {folder}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = $"Could not export \"{route.Name}\": {ex.Message}";
        }
    }

    private bool OnDisk(Action change, string verb)
    {
        try
        {
            change();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = $"Could not {verb} the route's files: {ex.Message}";
            return false;
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

    private static EncounterRoutePreset KeepReference(EncounterRoutePreset old, EncounterRoutePreset updated)
    {
        updated.References = old.References.Select(reference => reference.Clone()).ToList();
        updated.VideoDelayMs = old.VideoDelayMs;
        updated.ReferenceZoom = old.ReferenceZoom;
        updated.ReferenceCenterX = old.ReferenceCenterX;
        updated.ReferenceCenterY = old.ReferenceCenterY;
        return updated;
    }

    public bool EditCapture(string routeName, Action<EncounterRoutePreset> edit)
    {
        int index = _routes.FindIndex(route => EncounterRoutePreset.NameEquals(route.Name, routeName));
        if (index < 0) return false;

        edit(_routes[index]);
        _routes[index].Normalize();
        RoutesChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private EncounterRoutePreset CaptureRoute(string name) => new EncounterRoutePreset
    {
        Name = name,
        Route = SaveRoute(),
        Game = Variant.GameKey,
        Buttons = ButtonsKey,
        Saves = SavesKey,
        MaxSeconds = MaxSeconds,
        Sound = "any",
        Intro = "any",
        Title = "either",
        Combo = "any",
        DelayMs = DelayMs,
        OffsetMs = OffsetMs,
        IntroFrame = IntroFrame,
        IntroWindow = IntroWindow,
        LoopFrame = LoopFrame,
        LoopWindow = LoopWindow,
        TitleFrame = TitleFrame,
        TitleWindow = TitleWindow,
        IntroExtra = IntroExtra,
        TitleExtra = TitleExtra,
        Seed = _pickedSeed,
        Offset = _pickedOffset,
        Pass = _pickedPass,
        ManipRows = _lastTable.Seed == _pickedSeed && _pickedSeed >= 0 ? new List<string>(_lastTable.Rows) : new(),
        ManipSettings = _lastTable.Seed == _pickedSeed && _pickedSeed >= 0 ? new List<string>(_lastTable.Settings) : new(),
    }.Normalize();

    private (int Seed, List<string> Rows, List<string> Settings) _lastTable = (-1, new(), new());

    private void KeepManipTable(int seed, IReadOnlyList<(string Frames, string Inputs)> rows, IReadOnlyList<string> settings)
    {
        List<string> packed = rows.Select(row => row.Frames + "\t" + row.Inputs).ToList();
        _lastTable = (seed, packed, settings.ToList());
        if (_routeIndex < 0 || _routeIndex >= _routes.Count) return;

        EncounterRoutePreset route = _routes[_routeIndex];
        if (route.Seed != seed || (route.ManipRows.SequenceEqual(packed) && route.ManipSettings.SequenceEqual(settings))) return;
        route.ManipRows = packed;
        route.ManipSettings = settings.ToList();
        RoutesChanged?.Invoke(this, EventArgs.Empty);
    }

    public (List<(string Frames, string Inputs)> Rows, List<string> Settings)? ManipTableFor(string routeName)
    {
        foreach (EncounterRoutePreset route in _routes)
        {
            if (EncounterRoutePreset.NameEquals(route.Name, routeName)) return route.ManipTable();
        }
        return null;
    }

    private void LoadPreset(EncounterRoutePreset preset)
    {
        LoadRoute(preset.Route);
        Variant = TitleVariant.Parse(preset.Buttons, null, game: preset.Game, saves: preset.Saves);
        ButtonsEither = preset.Buttons == "either";
        SavesEither = preset.Saves == "either";
        MaxSeconds = preset.MaxSeconds;
        DelayMs = preset.DelayMs;
        OffsetMs = preset.OffsetMs;
        IntroFrame = preset.IntroFrame;
        IntroWindow = preset.IntroWindow;
        LoopFrame = preset.LoopFrame;
        LoopWindow = preset.LoopWindow;
        TitleFrame = preset.TitleFrame;
        TitleWindow = preset.TitleWindow;
        IntroExtra = preset.IntroExtra;
        TitleExtra = preset.TitleExtra;
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
        string offset = preset.OffsetMs is int offsetMs ? $", offset {offsetMs:+#;-#;+0} ms" : "";
        return $"Route \"{preset.Name}\" loaded: {list}{seed}{delay}{offset}.";
    }

    public void LoadRoute(string saved)
    {
        if (string.IsNullOrWhiteSpace(saved))
        {
            LoadRoute(EncounterPath.PlannerDefault);
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
                Number(fields[6]),
                fields.Length > 7 ? Number(fields[7]) ?? 1 : 1));
        }

        if (paths.Count == 0) paths.AddRange(EncounterPath.PlannerDefault);
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
                path.Patches.ToString(CultureInfo.InvariantCulture),
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
            if (!strict && name.Length == 0 && tiles == 0 && row[6].Length == 0) continue;

            int rate = Number(row[1]) ?? 21;

            int immune = Number(row[4]) ?? EncounterPath.DefaultMinSteps(rate);

            paths.Add(new EncounterPath(
                name, rate, tiles, NewMap: true, immune,
                Math.Min(tiles, Number(row[5]) ?? 0),
                Number(row[6]),
                Number(row[3]) ?? 1));
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

        _cancel = new CancellationTokenSource();
        _buttonSearch.Enabled = false;
        FitStatus(0);
        _status.Text = "Searching every reachable seed...";
        ClearRows();
        _matches = new List<EncounterMatch>();

        try
        {
            CancellationToken token = _cancel.Token;
            int cycles = Cycles;
            TitleVariant chosen = Variant;
            TitleButtonMode? buttons = ButtonsEither ? null : chosen.Buttons;
            TitleSaves? saves = SavesEither ? null : chosen.Saves;
            _searchedMaxSeconds = MaxSeconds;
            int maxFrame = MaxSeconds > 0 ? (int)Math.Floor(MaxSeconds * TitleSeedTable.FramesPerSecond) : 0;

            var unswept = new List<TitleVariant>();
            EncounterSearchResult? result = await Task.Run(
                () =>
                {
                    List<TitleVariant> variants = TitleSeedTable.Spanning(chosen.Game, buttons, saves, unswept);
                    return variants.Count == 0
                        ? null
                        : EncounterSearch.Search(route, cycles: cycles, protocol: TitleProtocol.Rta,
                            variants: variants, cancellationToken: token, maxResetFrame: maxFrame);
                },
                token);
            _skipped = unswept;

            if (result is null)
            {
                _status.Text = $"No tables yet for {(chosen.Game == TitleGame.LeafGreen ? "LeafGreen" : "FireRed")}"
                    + (saves is null ? "" : $" with {(chosen.Saves == TitleSaves.Single ? "a single save" : "two saves")}")
                    + (buttons is null ? "" : $" in {(chosen.Buttons == TitleButtonMode.LEqualsA ? "L=A" : "Help")} mode")
                    + " - that boot has not been swept.";
                NoHighlight();
                return;
            }

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

    private void ShowResults(EncounterSearchResult result)
    {
        FillRows(result.Matches);

        string skipped = _skipped.Count == 0
            ? ""
            : $" Not swept, so not searched: {string.Join("; ", _skipped.Select(v => Setup(v) + (SavesEither ? (v.Saves == TitleSaves.Single ? " single" : " multi") : "")))}.";
        string within = MaxSeconds > 0 ? $" within {MaxSeconds} s of the reset" : "";

        if (result.Matches.Count == 0)
        {
            _status.Text = (result.SeedsMatched == 0
                ? "No seed runs that route, on any of the sampled main streams."
                : $"That route is reachable, but no measured press frame reaches it{within}.") + skipped;
            return;
        }

        string capped = result.Truncated
            ? $" of {result.TotalMatches.ToString("N0", CultureInfo.InvariantCulture)}"
            : "";
        _status.Text =
            $"{result.Matches.Count.ToString("N0", CultureInfo.InvariantCulture)}{capped} press frames{within}, "
            + $"{result.SeedsMatched.ToString("N0", CultureInfo.InvariantCulture)} of 65536 seeds, over every sound mode, intro, "
            + "title press and combo the save has a table for. Pick a row for its inputs - it fills Intro and Title above. "
            + "Double-click: its tile table and the frames around it." + skipped;
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
        FitLastColumn();
    }

    private void ClearRows()
    {
        _around = null;
        _picked = null;
        HideTiles();
        ApplyColumns(around: false);
        _results.Items.Clear();
    }

    private static readonly (string Text, int Width, HorizontalAlignment Align)[] SearchColumns =
    {
        ("Frame", 46, HorizontalAlignment.Center), ("Loops", 44, HorizontalAlignment.Center),
        ("Time", 46, HorizontalAlignment.Center), ("Win", 52, HorizontalAlignment.Center),
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

    private static int ResetFrame(PressFrame press) => press.ResetFrame;

    private static string Windows(PressFrame press)
    {
        string window = press.Window.ToString(CultureInfo.InvariantCulture);
        if (TitleRecipes.Find(press.Variant) is TitleRecipe recipe)
        {
            int tightest = recipe.Windows.DefaultIfEmpty(0).Min();
            return tightest > 0 ? tightest.ToString(CultureInfo.InvariantCulture) + "→" + window : window;
        }
        if (press.Variant.LoopSkipped)
        {
            window = TitleSeedTable.LoopWindowOf(press.Variant).ToString(CultureInfo.InvariantCulture) + "→" + window;
        }
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

    private static string Setup(TitleVariant variant) =>
        (variant.Buttons == TitleButtonMode.LEqualsA ? "L=A" : "Help") + " "
        + (variant.Sound == TitleSoundMode.Stereo ? "Stereo" : "Mono");

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
        DescribeRta(match);
    }

    private void SearchAroundSelected()
    {
        if (_around is not null) return;
        if (_results.SelectedIndices.Count == 0 || _results.SelectedIndices[0] >= _matches.Count) return;

        EncounterMatch match = _matches[_results.SelectedIndices[0]];
        if (match.Press.Protocol != TitleProtocol.Rta) return;

        FillPresses(match.Press);
        ShowAround(match);
        ShowTiles(match.Press.Seed);
        DescribeRta(match);
    }

    private void DescribeRta(EncounterMatch match)
    {
        PressFrame press = match.Press;
        TitleVariant variant = press.Variant;
        IReadOnlyList<TitleButton> order = TitleCombos.Of(press).Order;
        bool exact = variant.Combo is not null;
        int at = 0;

        var rows = new List<(string Frames, string Inputs)>();
        TitleRecipe? recipe = TitleRecipes.Find(variant);
        if (recipe is not null)
        {
            foreach (RecipeStep step in recipe.Steps)
            {
                rows.Add((step.Frame > 0 ? step.Press.Frames : step.Label, step.Text));
            }
        }
        else if (variant.IntroSkipped)
        {
            var skip = new ManipPress("", TitleSeedTable.IntroFrameOf(variant), Math.Max(press.IntroWindow, 1));
            rows.Add((skip.Frames, $"Hold {Button(order[at++])} - skips the intro"));
        }
        if (recipe is null && variant.LoopSkipped)
        {
            var skip = new ManipPress("", TitleSeedTable.LoopFrameOf(variant), Math.Max(TitleSeedTable.LoopWindowOf(variant), 1));
            rows.Add((skip.Frames, $"Hold {Button(order[at++])} - skips the loop's intro"));
        }
        else if (recipe is null && variant.OnLoop)
        {
            rows.Add(("loop", ""));
        }

        var title = new ManipPress("", ResetFrame(press), press.Window);
        if (recipe is not null)
        {
            foreach ((int offset, string text) in recipe.EntrySteps(press.SeedPressFrame is not null))
            {
                var edge = new ManipPress("", title.Frame + offset, title.Window);
                rows.Add((edge.Frames, offset == 0 && press.SeedPressFrame is null ? text.Replace(" - enters", " - title clears") : text));
            }
        }
        else if (press.SeedPressFrame is null)
        {
            rows.Add((title.Frames, $"Hold {Button(order[at])} - title clears"));
        }
        else
        {
            TitleButton first = order[at], second = order[at + 1];
            bool either = !exact && first == TitleButton.Start && second == TitleButton.A;
            var entry = new ManipPress("", title.Frame + 1, title.Window);
            rows.Add((title.Frames, either ? "Hold Start/A - speeds the title up" : $"Hold {Button(first)} - speeds the title up"));
            rows.Add((entry.Frames, either ? "Hold A/Start, the other one - enters" : $"Hold {Button(second)} - enters"));
        }

        var settings = new List<string>
        {
            variant.Buttons == TitleButtonMode.LEqualsA ? "L=A" : "Help",
            match.EitherSound ? "Mono/Stereo" : variant.Sound == TitleSoundMode.Stereo ? "Stereo" : "Mono",
            variant.Saves == TitleSaves.Single ? "Single Save" : "Multi Save",
            $"Seed {press.Seed:X4}",
        };

        int titleRows = recipe is not null ? recipe.EntrySteps(press.SeedPressFrame is not null).Count : press.SeedPressFrame is null ? 1 : 2;
        _detailRows = rows.Select((row, i) => (row.Frames, Title: i >= rows.Count - titleRows, First: i == rows.Count - titleRows)).ToList();
        FitStatus(rows.Count);
        _status.ShowManip(rows, settings, CuedRows(), "");
        KeepManipTable(press.Seed, rows, settings);
    }

    private List<(string Frames, bool Title, bool First)> _detailRows = new();

    private List<bool> CuedRows() => _detailRows.Select(row =>
        ManipPress.Parse("", row.Frames) is ManipPress press
        && ManipPress.ParseList("", (row.Title ? _titleFrame : _introFrame).Text).Any(p => p.Frame == press.Frame)).ToList();

    private void ToggleCue(int index)
    {
        if (index < 0 || index >= _detailRows.Count) return;
        (string frames, bool title, bool first) = _detailRows[index];
        if (ManipPress.Parse("", frames) is not ManipPress press) return;

        TextBox box = title ? _titleFrame : _introFrame;
        List<ManipPress> places = ManipPress.ParseList("", box.Text);
        if (places.RemoveAll(p => p.Frame == press.Frame) == 0)
        {
            if (title && first) places.Insert(0, press); else places.Add(press);
        }
        if (!title) places = places.OrderBy(p => p.Frame).ToList();
        WritePress(box, ManipPress.FormatList(places));
        _status.SetCued(CuedRows());
    }

    private int _statusGrown;

    private int _statusHeightSet;

    private void FitStatus(int rows)
    {
        if (_statusGrown != 0 && _status.Height != _statusHeightSet) _statusGrown = 0;

        int row = _status.Font.Height + 2;
        int grow = Math.Max(0, rows - 4) * row;
        grow = Math.Min(grow, Math.Max(0, _results.Height + _statusGrown - 6 * row));
        int change = grow - _statusGrown;
        if (change == 0) return;

        _results.Height -= change;
        _status.SetBounds(_status.Left, _status.Top - change, _status.Width, _status.Height + change);
        _statusGrown = grow;
        _statusHeightSet = _status.Height;
    }

    private static string Button(TitleButton button) => button switch
    {
        TitleButton.Start => "Start",
        TitleButton.Select => "Select",
        TitleButton.L => "L",
        _ => "A",
    };

    private void ShowTiles(int titleSeed, int samples = EncounterSearch.DefaultSamples)
    {
        IReadOnlyList<EncounterPathTiles> tiles = EncounterSearch.TilesOf(_searched, titleSeed, samples);
        _tileSeed = titleSeed;

        _tileList.BeginUpdate();
        _tileList.Items.Clear();
        for (int path = 0; path < _searched.Count && path < tiles.Count; path++)
        {
            EncounterPathTiles one = tiles[path];
            var item = new ListViewItem(_searched[path].Name);
            item.SubItems.Add(_searched[path].Tiles.ToString(CultureInfo.InvariantCulture));
            item.SubItems.Add(one.ModeCount.ToString(CultureInfo.InvariantCulture));
            item.SubItems.Add(Percent(one.ModeShare));
            item.SubItems.Add(one.Tiles.Count == 0
                ? "none"
                : string.Join(", ", one.Tiles.Select(tile => tile.Share >= 0.995
                    ? tile.Tile.ToString(CultureInfo.InvariantCulture)
                    : $"{tile.Tile} ({Percent(tile.Share)})")));
            _tileList.Items.Add(item);
        }
        _tileList.EndUpdate();

        bool scanned = samples > EncounterSearch.DefaultSamples;
        _boxTileTable.Text = scanned
            ? $"Wild Seed {titleSeed:X4} - {samples} streams"
            : $"Wild Seed {titleSeed:X4}";
        _buttonScan.Enabled = !scanned;
        if (!_boxTileTable.Visible)
        {
            _boxTileTable.Visible = true;
            _boxTiles.Visible = false;
            FitLastColumn(_tileList, _tileList.Columns.Count - 1);
        }
        FitTileScroll();
    }

    private static string Percent(double share) => share > 0 && share < 0.005
        ? "<1%"
        : (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    private void ScanTiles()
    {
        if (!_boxTileTable.Visible || _around is null) return;
        Cursor.Current = Cursors.WaitCursor;
        try
        {
            ShowTiles(_tileSeed, EncounterSearch.ScanSamples);
        }
        finally
        {
            Cursor.Current = Cursors.Default;
        }
    }

    private void FitTileScroll()
    {
        int last = _tileList.Columns.Count - 1;
        int left = 0;
        for (int i = 0; i < last; i++) left += _tileList.Columns[i].Width;
        int room = Math.Min(_tileList.Columns[last].Width, _tileList.ClientSize.Width - left) - 6;
        int widest = 0;
        foreach (ListViewItem item in _tileList.Items)
        {
            if (item is null || item.SubItems.Count <= last) continue;
            widest = Math.Max(widest, TextRenderer.MeasureText(item.SubItems[last].Text, _tileList.Font, Size.Empty,
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width);
        }

        int overhang = widest - room;
        if (overhang <= 0)
        {
            _tileScroll.Value = 0;
            _tileScroll.Visible = false;
            return;
        }
        overhang += 8;
        int large = Math.Max(1, room / 2);
        _tileScroll.Maximum = overhang + large - 1;
        _tileScroll.LargeChange = large;
        if (_tileScroll.Value > overhang) _tileScroll.Value = overhang;
        _tileScroll.Visible = true;
    }

    private void HideTiles()
    {
        if (!_boxTileTable.Visible) return;
        _boxTiles.Visible = true;
        _boxTileTable.Visible = false;
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
        FitLastColumn();
    }

    private void RestoreResults()
    {
        if (_around is null) return;
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
        ShowTiles(row.Seed);
        _status.Text = $"{row.Window}: title press on frame {row.Frames}, Trainer ID {row.Seed:X4} - {row.Encounters} encounter{(row.Encounters == 1 ? "" : "s")}"
            + $" ({row.Where}) on {row.Rate * 100:0}% of sampled streams; {kept}. The tile table above is this seed's. Back or Esc: the search.";
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
        ListView list = sender as ListView ?? _results;
        using (var background = new SolidBrush(Theme.HeaderBack))
        {
            e.Graphics.FillRectangle(background, e.Bounds);
        }
        using (var pen = new Pen(Theme.Border))
        {
            if (e.ColumnIndex < list.Columns.Count - 1)
            {
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 2, e.Bounds.Right - 1, e.Bounds.Bottom - 3);
            }
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        Rectangle bounds = e.Bounds;
        bounds.Inflate(-2, 0);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", list.Font, bounds, Theme.Text,
            CellFlags | Align(e.Header?.TextAlign ?? HorizontalAlignment.Left));
    }

    private void DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        ListView list = sender as ListView ?? _results;
        bool selected = e.Item?.Selected == true;
        Color back = selected ? Theme.Accent : list.BackColor;
        Color fore = selected ? Theme.AccentText : list.ForeColor;
        if (!selected && list == _results && _around is not null && e.ItemIndex >= 0 && e.ItemIndex < _around.Count)
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
            if (e.ColumnIndex < list.Columns.Count - 1)
            {
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom - 1);
            }
        }

        Rectangle bounds = e.Bounds;
        bounds.Inflate(-3, 0);
        bounds.Height -= ThemedListView.RuleClearance;
        int shift = list == _tileList && e.ColumnIndex == list.Columns.Count - 1 && _tileScroll.Visible ? _tileScroll.Value : 0;
        if (shift > 0)
        {
            Region clip = e.Graphics.Clip;
            e.Graphics.SetClip(bounds);
            bounds.X -= shift;
            bounds.Width = short.MaxValue;
            TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", list.Font, bounds, fore,
                (CellFlags & ~TextFormatFlags.EndEllipsis) | TextFormatFlags.PreserveGraphicsClipping
                | Align(list.Columns[e.ColumnIndex].TextAlign));
            e.Graphics.Clip = clip;
            return;
        }
        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", list.Font, bounds, fore,
            CellFlags | Align(list.Columns[e.ColumnIndex].TextAlign));
    }

    private void FitLastColumn() =>
        FitLastColumn(_results, _around is null ? _results.Columns.Count - 1 : AroundFillColumn);

    private static void FitLastColumn(ListView list, int fillIndex)
    {
        int used = 0;
        for (int i = 0; i < fillIndex; i++) used += list.Columns[i].Width;

        ColumnHeader last = list.Columns[fillIndex];
        int fill = list.ClientSize.Width - used;
        if (fill >= 60 && fill != last.Width) last.Width = fill;
    }

    public void Cancel() => _cancel?.Cancel();

    internal sealed class ManipDetail : Panel
    {
        private IReadOnlyList<(string Frames, string Inputs)> _rows = Array.Empty<(string, string)>();
        private IReadOnlyList<string> _settings = Array.Empty<string>();
        private string _note = "";
        private bool _table;
        private IReadOnlyList<bool> _cued = Array.Empty<bool>();
        private int _leftWidth;

        public event Action<int>? RowClicked;

        public void SetCued(IReadOnlyList<bool> cued)
        {
            _cued = cued;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!_table || e.Button != MouseButtons.Left || e.X >= _leftWidth) return;
            int index = e.Y / (Font.Height + 2) - 1;
            if (index >= 0 && index < _rows.Count) RowClicked?.Invoke(index);
        }

        public ManipDetail()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string Text
        {
            get => base.Text;
            set
            {
                base.Text = value;
                _table = false;
                Invalidate();
            }
        }

        public void ShowManip(IReadOnlyList<(string Frames, string Inputs)> rows, IReadOnlyList<string> settings, IReadOnlyList<bool> cued, string note)
        {
            _rows = rows;
            _cued = cued;
            _settings = settings;
            _note = note;
            base.Text = string.Join("; ", rows.Select(row => $"{row.Frames}: {row.Inputs}"))
                + " - " + string.Join(", ", settings) + ". " + note;
            _table = true;
            Invalidate();
        }

        private const TextFormatFlags Cell = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                                             | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            if (!_table)
            {
                TextRenderer.DrawText(g, base.Text, Font, ClientRectangle, Theme.Text,
                    TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
                return;
            }

            int row = Font.Height + 2;
            int pad = Math.Max(3, Font.Height / 4);
            int gap = Math.Max(4, Font.Height * SectionGap / 15);
            int settingsWidth = TextRenderer.MeasureText(g, "Save settings", Font).Width;
            foreach (string setting in _settings) settingsWidth = Math.Max(settingsWidth, TextRenderer.MeasureText(g, setting, Font).Width);
            settingsWidth += 2 * pad + 8;

            int framesWidth = TextRenderer.MeasureText(g, "Frames", Font).Width;
            foreach ((string frames, _) in _rows) framesWidth = Math.Max(framesWidth, TextRenderer.MeasureText(g, frames, Font).Width);
            framesWidth += 2 * pad + 8;

            int leftWidth = Width - settingsWidth - gap;
            _leftWidth = leftWidth;
            var left = new Rectangle(0, 0, leftWidth, row * (_rows.Count + 1));
            var right = new Rectangle(leftWidth + gap, 0, settingsWidth - 1, row * (_settings.Count + 1));

            using var back = new SolidBrush(Theme.ListBack);
            using var head = new SolidBrush(Theme.HeaderBack);
            using var line = new Pen(Theme.GridLine);
            using var border = new Pen(Theme.Border);

            void Frame(Rectangle table)
            {
                g.FillRectangle(back, table);
                g.FillRectangle(head, table.X, table.Y, table.Width, row);
                for (int y = table.Y + row; y < table.Bottom; y += row) g.DrawLine(line, table.X, y, table.Right, y);
                g.DrawRectangle(border, table.X, table.Y, table.Width, table.Height);
            }

            void Put(string text, int x, int y, int width, Color color, TextFormatFlags align = TextFormatFlags.Left) =>
                TextRenderer.DrawText(g, text, Font, new Rectangle(x + pad, y, width - 2 * pad, row), color, Cell | align);

            Frame(left);
            g.DrawLine(line, left.X + framesWidth, left.Y, left.X + framesWidth, left.Bottom);
            Put("Frames", left.X, 0, framesWidth, Theme.Text, TextFormatFlags.HorizontalCenter);
            Put("Inputs", left.X + framesWidth, 0, left.Width - framesWidth, Theme.Text);
            for (int i = 0; i < _rows.Count; i++)
            {
                int y = row * (i + 1);
                Put(_rows[i].Frames, left.X, y, framesWidth, i < _cued.Count && _cued[i] ? Theme.Text : Theme.DimText, TextFormatFlags.HorizontalCenter);
                Put(_rows[i].Inputs, left.X + framesWidth, y, left.Width - framesWidth, Theme.Text);
            }

            Frame(right);
            Put("Save settings", right.X, 0, right.Width, Theme.Text, TextFormatFlags.HorizontalCenter);
            for (int i = 0; i < _settings.Count; i++)
            {
                Put(_settings[i], right.X, row * (i + 1), right.Width, Theme.Text, TextFormatFlags.HorizontalCenter);
            }

            int noteTop = Math.Max(left.Bottom, right.Bottom) + gap;
            if (noteTop + Font.Height <= Height)
            {
                TextRenderer.DrawText(g, _note, Font, new Rectangle(0, noteTop, Width, Height - noteTop), Theme.DimText,
                    TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.Top);
            }
        }
    }
}
