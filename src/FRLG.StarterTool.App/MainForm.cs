using System.Globalization;
using FRLG.StarterTool.Core.Npc;
using FRLG.StarterTool.Core.Pokemon;
using FRLG.StarterTool.Core.Rng;
using FRLG.StarterTool.Core.Search;
using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Timing;

namespace FRLG.StarterTool.App;

public partial class MainForm : Form
{
    private const int MaxTrainerId = 65535;

    private List<PokemonRng> _results = new();

    private int? _landingFrame;

    private int? _landingTarget;

    public bool HasLanding => _landingFrame != null || _landingTarget != null;

    private double _landingChance;

    private int? _landingAlternate;

    private readonly HashSet<int> _landingContext = new();

    private bool _reportingLanding;

    private PokemonSpecies _resultSpecies = PokemonSpecies.Get(1);
    private double _resultFps = 60.0;

    private int _timeShiftFrames;

    private bool _levelStatRows;

    private bool _allSeedRows;

    private bool _encounterGrid;

    private List<EncounterGridRow> _encounterRows = new();

    private readonly record struct EncounterGridRow(string[] Cells, double? Chance);

    private static readonly (string Text, int Width)[] EncounterLandingColumns =
        { ("Press", 52), ("Target", 76), ("Landed", 60), ("Off", 62), ("Hit", 60) };

    private (string Text, int Width)[] _designedColumns = Array.Empty<(string, int)>();

    private int _encounterFill = 4;

    private bool _allSeedSearchRunning;

    public MainForm()
    {
        InitializeComponent();

        foreach (PokemonSpecies species in PokemonSpecies.GetList())
        {
            ComboBoxPokemon.Items.Add(species);
        }
        ComboBoxPokemon.SelectedIndexChanged += (_, _) => UpdateSprite();
        SelectSpecies(1);
        InitializeRanges();
        ButtonSearch.Click += (_, _) => RunSearch();
        ButtonCalculateOdds.Click += (_, _) => CalculateOdds();
        ButtonSearchFrame.Click += (_, _) => SearchTypedFrame();
        ButtonLevelToggle.CheckedChanged += (_, _) =>
        {
            ButtonLevelToggle.Text = ButtonLevelToggle.Checked ? "Level 6" : "Level 5";
            RefreshStatBoxLevel();
            RefreshStatColumns();
        };
        StatBoxStats.Click += (_, _) => ToggleLevel();
        StatBoxIvs.Click += (_, _) => ExportStats();
        StatBoxIvs.Cursor = Cursors.Hand;
        StatBoxPanel.ColorsChanged += (_, _) => RefreshStatBoxColors();
        InitializeStatSearch();
        MenuItemHotkeys.Click += (_, _) => StarterTool.ShowSettings();
        MenuItemAlwaysOnTop.CheckedChanged += (_, _) => RefreshAlwaysOnTop();
        MenuItemGlobalHotkeys.CheckedChanged += (_, _) => RefreshGlobalHotkeys();
        InitializeTabs();
        EncounterPanel.RoutesChanged += (_, _) => RefreshEncounterRoutes(SelectedEncounterRoute);
        SavestatePanel.FilterSource = () => CaptureFilter();
        SavestatePanel.CloseRequested += (_, _) => SelectTab(TabKey.Manip);
        StarterTool.Context.Changed += (_, _) => ShowContextSession();
        ButtonContextUndo.Click += (_, _) => StarterTool.Context.Undo();
        ButtonContextClear.Click += (_, _) => StarterTool.Context.Clear();
        ButtonContextLate.Click += (_, _) => StarterTool.Context.Next();
        ButtonContextFinished.Click += (_, _) => StarterTool.Context.Next();
        ButtonContextAnchor.Click += (_, _) => StarterTool.Context.MarkNextAnchor(Win32.GetTime());
        ButtonContextMiss.Click += (_, _) => StarterTool.Context.Miss();
        ContextPanel.BoxClicked += (_, box) => StarterTool.Context.FocusBox(box);
        ContextPanel.CueChanged += (_, _) => ShowContextSession();
        ButtonTraining.Click += (_, _) => ToggleTraining();
        TrainingPanel.StateChanged += (_, _) => RefreshTrainingButton();
        TrainingPanel.CloseRequested += (_, _) => SelectTab(TabKey.Manip);
        InitializeFilters();

        TextBoxTrainerId.KeyPress += TextBoxTrainerId_KeyPress;
        TextBoxTrainerId.KeyDown += TextBoxTrainerId_KeyDown;
        TextBoxTrainerId.Enter += (_, _) => BounceTrainerIdCaret();

        TextBoxTrainerId.Enter += (_, _) => _trainerIdFocused = true;
        TextBoxTrainerId.Leave += (_, _) => _trainerIdFocused = false;

        TextBoxTrainerId.TextChanged += (_, _) =>
        {
            if (int.TryParse(TextBoxTrainerId.Text.Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int typed)
                && typed > 0 && typed <= MaxTrainerId)
            {
                RunLog.SetTrainerId(typed);
            }
        };

        TextBoxSearchFrame.KeyPress += TextBoxTrainerId_KeyPress;
        TextBoxSearchFrame.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;

            SearchTypedFrame();
            e.SuppressKeyPress = true;
        };

        var searchInputs = new List<Control> { TextBoxMinFrame, TextBoxMaxFrame, ComboBoxPokemon };

        foreach (Control input in searchInputs)
        {
            input.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;

                RunSearch();
                e.SuppressKeyPress = true;
            };
        }

        _designedColumns = ListViewResults.Columns.Cast<ColumnHeader>()
            .Select(column => (column.Text, column.Width)).ToArray();
        ListViewResults.RetrieveVirtualItem += ListViewResults_RetrieveVirtualItem;
        ListViewResults.SelectedIndexChanged += ListViewResults_SelectedIndexChanged;
        ListViewResults.KeyDown += ListViewResults_KeyDown;
        ListViewResults.DoubleClick += (_, _) => SearchAroundSelectedFrame();
        ListViewResults.DrawColumnHeader += ListViewResults_DrawColumnHeader;
        ListViewResults.DrawItem += (_, _) => { };
        ListViewResults.DrawSubItem += ListViewResults_DrawSubItem;

        WatchNumberFields(this);

        Load += (_, _) =>
        {
            StarterTool.Init(this);
            FitLastColumn();
        };
        FormClosed += (_, _) => StarterTool.Destroy();
    }

    private int SelectedLevel => ButtonLevelToggle.Checked ? 6 : 5;

    public void ToggleLevel()
    {
        if (!ButtonLevelToggle.Enabled) return;

        ButtonLevelToggle.Checked = !ButtonLevelToggle.Checked;
    }

    private PokemonSpecies SelectedSpecies => ComboBoxPokemon.SelectedItem as PokemonSpecies ?? PokemonSpecies.Get(1);

    public void ToggleGlobalHotkeys() => MenuItemGlobalHotkeys.Checked = !MenuItemGlobalHotkeys.Checked;

    private void RefreshGlobalHotkeys()
    {
        bool enabled = MenuItemGlobalHotkeys.Checked;

        if (StarterTool.Settings != null) StarterTool.Settings.GlobalHotkeysEnabled = enabled;

        MenuItemGlobalHotkeys.Image = Assets.Globe(enabled);
        MenuItemGlobalHotkeys.ToolTipText = enabled ? "Global hotkeys on" : "Global hotkeys off";
    }

    private void RefreshAlwaysOnTop()
    {
        bool pinned = MenuItemAlwaysOnTop.Checked;

        TopMost = pinned;
        MenuItemAlwaysOnTop.Image = Assets.Pin(pinned);
        MenuItemAlwaysOnTop.ToolTipText = pinned ? "Window pinned on top" : "Keep this window on top";
    }

    public void ApplySettings(AppSettings settings)
    {
        ApplyZoom(settings.ZoomPercent);

        ButtonLevelToggle.Checked = settings.Level == 6;
        RefreshStatBoxLevel();
        StatBoxPanel.LabelColor =
            StatBoxPanel.ParseColor(settings.StatBoxLabelColor, StatBoxPanel.DefaultLabelColor);
        StatBoxPanel.FillColor =
            StatBoxPanel.ParseColor(settings.StatBoxFillColor, StatBoxPanel.DefaultFillColor);
        StatBoxPanel.ValueColor =
            StatBoxPanel.ParseColor(settings.StatBoxValueColor, StatBoxPanel.DefaultValueColor);
        StatBoxPanel.OutlineColor =
            StatBoxPanel.ParseColor(settings.StatBoxOutlineColor, StatBoxPanel.DefaultOutlineColor);
        StatBoxPanel.FrameColor =
            StatBoxPanel.ParseColor(settings.StatBoxFrameColor, StatBoxPanel.DefaultFrameColor);
        TrainingPanel.LoadRounds(settings.TrainingRounds);

        SavestatePanel.LoadFolder = settings.SavestateLoadPath;
        SavestatePanel.SaveFolder = settings.SavestateSavePath;

        RomPatchPanel.RomPath = settings.RomPatchRomPath;
        RomPatchPanel.OutputPath = settings.RomPatchOutputPath;

        EncounterPanel.LoadRoute(settings.EncounterRoute);
        EncounterPanel.Cycles = settings.EncounterCycles;
        EncounterPanel.Variant = Core.Encounters.TitleVariant.Parse(settings.EncounterButtons, settings.EncounterSound,
            settings.EncounterIntro, settings.EncounterTitle, settings.EncounterCombo, settings.EncounterGame);
        EncounterPanel.SoundAny = settings.EncounterSound == "any";
        EncounterPanel.IntroAny = settings.EncounterIntro == "any";
        EncounterPanel.DelayMs = settings.EncounterDelayMs;
        EncounterPanel.IntroFrame = settings.EncounterIntroFrame;
        EncounterPanel.IntroWindow = settings.EncounterIntroWindow;
        EncounterPanel.TitleFrame = settings.EncounterTitleFrame;
        EncounterPanel.TitleWindow = settings.EncounterTitleWindow;
        EncounterPanel.SetRoutes(settings.EncounterRoutes, settings.EncounterActiveRoute);
        RefreshEncounterRoutes(settings.TimerEncounterRoute);

        ContextPanel.ShowDelayDashes = settings.ShowLabDelayDashes;
        ContextPanel.ShowTips = settings.ShowRunTips;

        ApplyTimeFormat();

        ApplyTabSettings(settings);

        MenuItemAlwaysOnTop.Checked = settings.AlwaysOnTop;
        MenuItemGlobalHotkeys.Checked = settings.GlobalHotkeysEnabled;
        RefreshContextTracking();
        RefreshGlobalHotkeys();
        RefreshAlwaysOnTop();

        ApplyFilter(settings.GetCurrentFilter());
    }

    public void ApplyTimeFormat()
    {
        TimeFormat format = StarterTool.TimeFormat;

        Font previous = LabelTimer.Font;
        LabelTimer.Font = FitFont(
            Font.FontFamily, TimeText.Widest(format), LabelTimer.Width - 4, Scaled(ClockFitHeight), 44F);
        previous.Dispose();

        if (!StarterTool.IsTimerRunning) LabelTimer.Text = TimeText.Format(0.0, format);

        if (!_allSeedRows && !_encounterGrid)
        {
            ListViewResults.Columns[TimeColumnIndex].Width = Scaled(format == TimeFormat.Minutes
                ? MinutesTimeColumnWidth
                : SecondsTimeColumnWidth);
        }
        FitLastColumn();

        if (_results.Count > 0) ListViewResults.RedrawItems(0, _results.Count - 1, true);
    }

    public void CaptureSettings(AppSettings settings)
    {
        settings.Level = SelectedLevel;
        settings.StatBoxLabelColor = StatBoxPanel.ToHex(StatBoxPanel.LabelColor);
        settings.StatBoxFillColor = StatBoxPanel.ToHex(StatBoxPanel.FillColor);
        settings.StatBoxValueColor = StatBoxPanel.ToHex(StatBoxPanel.ValueColor);
        settings.StatBoxOutlineColor = StatBoxPanel.ToHex(StatBoxPanel.OutlineColor);
        settings.StatBoxFrameColor = StatBoxPanel.ToHex(StatBoxPanel.FrameColor);
        settings.TrainingRounds = TrainingPanel.SaveRounds();
        settings.SavestateLoadPath = SavestatePanel.LoadFolder;
        settings.SavestateSavePath = SavestatePanel.SaveFolder;
        settings.RomPatchRomPath = RomPatchPanel.RomPath;
        settings.RomPatchOutputPath = RomPatchPanel.OutputPath;
        settings.EncounterRoute = EncounterPanel.SaveRoute();
        settings.EncounterCycles = EncounterPanel.Cycles;
        settings.EncounterGame = EncounterPanel.Variant.GameKey;
        settings.EncounterButtons = EncounterPanel.Variant.ButtonsKey;
        settings.EncounterSound = EncounterPanel.SoundAny ? "any" : EncounterPanel.Variant.SoundKey;
        settings.EncounterIntro = EncounterPanel.IntroAny ? "any" : EncounterPanel.Variant.IntroKey;
        settings.EncounterTitle = EncounterPanel.Variant.AnimationKey;
        settings.EncounterCombo = EncounterPanel.Variant.ComboKey;
        settings.EncounterDelayMs = EncounterPanel.DelayMs;
        settings.EncounterIntroFrame = EncounterPanel.IntroFrame;
        settings.EncounterIntroWindow = EncounterPanel.IntroWindow;
        settings.EncounterTitleFrame = EncounterPanel.TitleFrame;
        settings.EncounterTitleWindow = EncounterPanel.TitleWindow;
        settings.EncounterRoutes = EncounterPanel.Routes;
        settings.EncounterActiveRoute = EncounterPanel.ActiveRoute;
        settings.TimerEncounterRoute = SelectedEncounterRoute;
        settings.AlwaysOnTop = MenuItemAlwaysOnTop.Checked;
        settings.GlobalHotkeysEnabled = MenuItemGlobalHotkeys.Checked;
        CaptureTabSettings(settings);
        settings.SetCurrentFilter(CaptureFilter());
    }

    private void ToggleTraining()
    {
        if (TrainingPanel.IsRunning)
        {
            TrainingPanel.Cancel();
            SelectTab(TabKey.Manip);
            return;
        }

        if (!MenuItemViewTraining.Checked) MenuItemViewTraining.Checked = true;
        SelectTab(TabKey.Training);
        TrainingPanel.StartSession();
    }

    private void RefreshTrainingButton()
        => ButtonTraining.Text = TrainingPanel.IsRunning ? "Stop Offset Training" : "Start Offset Training";

    public bool ReportMovement(Direction direction) => TroubleshootPanel.Append(direction);

    public bool ReportUndo() => TroubleshootPanel.Backspace();

    public bool ReportFocus(int delta) => TroubleshootPanel.MoveNpc(delta);

    private void ShowContextSession()
    {
        ContextSession session = StarterTool.Context;
        ContextPanel.SetStatus(session.Summary, session.Report);

        if (session.Stage == ContextStage.Lab)
        {
            LabTracker? lab = session.Lab;

            ButtonContextClear.Visible = false;
            ButtonContextUndo.Visible = false;
            ButtonContextAnchor.Visible = false;
            ButtonContextFinished.Visible = false;

            ButtonContextLate.Visible = session.Hidden.Count == 0;
            ButtonContextLate.Text = lab?.Lateness switch
            {
                LabLateness.Late => "Very Late!",
                LabLateness.VeryLate => "I'm Fast!",
                _ => "I'm Late!",
            };
            ButtonContextLate.Enabled = lab is { All.Count: > 0 };

            ContextPanel.SetLabField(
                lab?.All ?? (IReadOnlyList<LabOption>)Array.Empty<LabOption>(),
                lab?.Likelihoods ?? (IReadOnlyList<double>)Array.Empty<double>(),
                lab?.FocusedIndex ?? -1,
                lab?.MostLikelyIndex ?? -1,
                StarterTool.VariableOffset?.SelectedFps ?? 60.0);
        }
        else
        {
            FenceTracker? tracker = session.Tracker;

            bool open = session.Hidden.Count == 0;

            ButtonContextClear.Visible = open;
            ButtonContextUndo.Visible = open;
            ButtonContextLate.Visible = false;
            ButtonContextUndo.Enabled = tracker is { Inputs.Count: > 0 };
            ButtonContextClear.Enabled = tracker is { Inputs.Count: > 0 } or { Complete: true };

            ContextPanel.SetField(
                tracker?.Alive ?? (IReadOnlyList<FenceCandidate>)Array.Empty<FenceCandidate>(),
                tracker?.Likelihoods ?? (IReadOnlyList<double>)Array.Empty<double>(),
                tracker?.FocusedIndex ?? -1,
                tracker?.MostLikelyIndex ?? -1,
                tracker?.Fps ?? 60.0,
                session.AnchorCount,
                session.OakAnchorFrame);

            bool fenceGuy = open && ContextPanel.ShowingFenceCue;

            ButtonContextAnchor.Visible = open && !fenceGuy;
            ButtonContextFinished.Visible = fenceGuy;
            ButtonContextFinished.Text = tracker is { Complete: true } ? "Not Finished" : "Finished!";
            ButtonContextFinished.Enabled = tracker != null;
        }

        ButtonContextAnchor.Enabled = session.NextAnchor != null;

        ButtonContextMiss.Visible = session.Hidden.Count == 0;
        ButtonContextMiss.Enabled = session.CanMiss;

        ContextPanel.SetTip(session.Tip, session.TipIsShiny);

        ContextPanel.SetHidden(session.Hidden);
    }

    public void SampleContextPanel() => ContextPanel.Sample();

    private void SelectSpecies(int dexNumber)
    {
        for (int i = 0; i < ComboBoxPokemon.Items.Count; i++)
        {
            if (ComboBoxPokemon.Items[i] is PokemonSpecies species && species.Id == dexNumber)
            {
                ComboBoxPokemon.SelectedIndex = i;
                return;
            }
        }
    }

    private void UpdateSprite() => PictureBoxSprite.Image = Assets.Sprite(SelectedSpecies.Id);

    public void LockTrainerId()
    {
        _trainerIdLocked = true;
        _trainerIdCaretClosed = true;
        TextBoxTrainerId.ReadOnly = true;

        if (ReferenceEquals(ActiveControl, TextBoxTrainerId)) HandCaretToResults();
    }

    public void UnlockTrainerId()
    {
        _trainerIdLocked = false;
        _trainerIdCaretClosed = false;
        TextBoxTrainerId.ReadOnly = false;
    }

    private bool _trainerIdLocked;

    private bool _trainerIdCaretClosed;

    private void ReopenTrainerIdCaret() => _trainerIdCaretClosed = false;

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        if (!_trainerIdCaretClosed || ActiveControl != null) return;

        HandCaretToResults();
    }

    private void BounceTrainerIdCaret()
    {
        if (!_trainerIdCaretClosed) return;

        if (!_trainerIdLocked && MouseButtons != MouseButtons.None) return;
        if (!ReferenceEquals(ActiveForm, this)) return;

        BeginInvoke(() =>
        {
            if (ReferenceEquals(ActiveControl, TextBoxTrainerId)) HandCaretToResults();
        });
    }

    public void FocusTrainerId()
    {
        SelectTab(TabKey.Manip);

        if (!TextBoxTrainerId.CanFocus) return;

        TakeCaret(TextBoxTrainerId);
        TextBoxTrainerId.SelectAll();
    }

    private void TextBoxTrainerId_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
        {
            e.Handled = true;
        }
    }

    private void TextBoxTrainerId_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Decimal:
            case Keys.Escape:
                if (StarterTool.IsBoundKey(e.KeyCode)) break;

                if (_trainerIdLocked) break;
                TextBoxTrainerId.Clear();
                e.SuppressKeyPress = true;
                break;

            case Keys.Enter:
                if (StarterTool.TakeIdleStart(e.KeyCode)) break;
                RunSearch();
                e.SuppressKeyPress = true;
                break;
        }
    }

    private void RunSearch()
    {
        if (TrainerIdBoxEmpty && !StarterTool.IsTimerRunning)
        {
            RunAllSeedSearch();
            return;
        }

        ClearLanding();
        ClearSearchNote();

        List<RangeSearchCriteria> criteria = ReadRangeCriteria();

        UseWaitCursor = true;
        try
        {
            List<PokemonRng> results = RangeSearch.Search(criteria);

            ReadFrameRange(out int loggedMin, out int loggedMax);
            ContextSession.Log(string.Format(CultureInfo.InvariantCulture,
                "search: TID {0}, frames {1}-{2}, {3} range{4}, {5} results",
                ReadTrainerId(), loggedMin, loggedMax, criteria.Count, criteria.Count == 1 ? "" : "s",
                results.Count));

            ShowResults(results, 0);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ClearSearchNote()
    {
        if (LabelLanding.Text.Length > 0) LabelLanding.Text = "";
    }

    private bool TrainerIdBoxEmpty => TextBoxTrainerId.Text.Trim().Length == 0;

    private async void RunAllSeedSearch()
    {
        if (_allSeedSearchRunning) return;

        ClearLanding();
        ClearSearchNote();

        List<RangeSearchCriteria> criteria = ReadRangeCriteria(seedless: true);

        _allSeedSearchRunning = true;
        ButtonSearch.Enabled = false;
        UseWaitCursor = true;
        try
        {
            AllSeedSearchResult found = await Task.Run(() => RangeSearch.AllSeeds(criteria));

            ReadFrameRange(out int loggedMin, out int loggedMax);
            ContextSession.Log(string.Format(CultureInfo.InvariantCulture,
                "search: all TIDs, frames {0}-{1}, {2} range{3}, {4} results{5}",
                loggedMin, loggedMax, criteria.Count, criteria.Count == 1 ? "" : "s", found.TotalMatches,
                found.Truncated ? " (showing " + found.Matches.Count + ")" : ""));

            ShowResults(found.Matches, -1, takeFocus: false, allSeed: true);

            if (found.Truncated)
            {
                LabelLanding.Text = string.Format(CultureInfo.InvariantCulture,
                    "{0} matches across all TIDs - showing the first {1}. Tighten the filter.",
                    found.TotalMatches, found.Matches.Count);
            }
        }
        catch (Exception)
        {
            LabelLanding.Text = "All-TID search failed.";
        }
        finally
        {
            UseWaitCursor = false;
            ButtonSearch.Enabled = true;
            _allSeedSearchRunning = false;
        }
    }

    private async void CalculateOdds()
    {
        List<RangeSearchCriteria> criteria = ReadRangeCriteria();

        if (StarterTool.Settings != null) StarterTool.Settings.TipOddsCalculated = true;

        ButtonCalculateOdds.Enabled = false;
        ButtonCalculateOdds.Text = "Calculating...";
        try
        {
            double odds = await Task.Run(() => RangeSearch.Odds(criteria));
            ButtonCalculateOdds.Text = (odds * 100.0).ToString("0.00", CultureInfo.InvariantCulture) + "%";
        }
        catch (Exception)
        {
            ButtonCalculateOdds.Text = "Calculate";
        }
        finally
        {
            ButtonCalculateOdds.Enabled = true;
        }
    }

    private void SearchAroundSelectedFrame()
    {
        if (ListViewResults.SelectedIndices.Count == 0) return;

        int index = ListViewResults.SelectedIndices[0];
        if (index < 0 || index >= _results.Count) return;

        if (_allSeedRows)
        {
            TextBoxTrainerId.Text = _results[index].Seed.ToString(CultureInfo.InvariantCulture);
        }

        SearchAroundFrame(_results[index].Frame);
    }

    private void SearchTypedFrame()
    {
        if (!int.TryParse(TextBoxSearchFrame.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int frame)
            || frame < 0)
        {
            TextBoxSearchFrame.Focus();
            TextBoxSearchFrame.SelectAll();
            return;
        }

        SearchAroundFrame(frame);
    }

    private const int SearchRadius = 20;

    private void SearchAroundFrame(int centre)
    {
        ClearLanding();
        SearchAroundFrame(centre, centre, takeFocus: true);
    }

    private void SearchAroundFrame(int centre, int selectFrame, bool takeFocus, int radius = SearchRadius)
    {
        var seed = new Seed(ReadTrainerId());

        var around = new List<PokemonRng>(radius * 2);
        int selected = -1;
        for (int frame = centre - radius; frame < centre + radius; frame++)
        {
            if (frame < 0) continue;

            if (frame == selectFrame) selected = around.Count;
            around.Add(new PokemonMethod1(seed, frame));
        }

        ShowResults(around, selected, takeFocus,
            levelStats: StarterTool.Settings?.AutoShowLevelStats ?? true);
    }

    private void InitializeStatSearch()
    {
        ComboBoxStatNature.Items.Add("Any");
        foreach (Nature nature in Nature.GetList())
        {
            ComboBoxStatNature.Items.Add(nature);
        }
        ComboBoxStatNature.SelectedIndex = 0;

        ButtonStatSearch.Click += (_, _) => RunStatSearch();
        ButtonClearStats.Click += (_, _) => ClearStatSearch();

        foreach (TextBox box in TextBoxStats)
        {
            box.MaxLength = 3;
            box.KeyPress += TextBoxTrainerId_KeyPress;
        }

        var inputs = new List<Control>(TextBoxStats) { ComboBoxStatNature };
        foreach (Control input in inputs)
        {
            input.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;

                RunStatSearch();
                e.SuppressKeyPress = true;
            };
        }
    }

    private void ClearStatSearch()
    {
        foreach (TextBox box in TextBoxStats)
        {
            box.Clear();
        }
        ComboBoxStatNature.SelectedIndex = 0;
        TextBoxStats[0].Focus();
    }

    private void RunStatSearch()
    {
        var stats = new int[6];
        bool any = false;
        for (int stat = 0; stat < 6; stat++)
        {
            if (int.TryParse(TextBoxStats[stat].Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int value)
                && value > 0)
            {
                stats[stat] = value;
                any = true;
            }
        }

        int natureId = ComboBoxStatNature.SelectedItem is Nature nature ? nature.Id : -1;
        if (!any && natureId < 0)
        {
            TextBoxStats[0].Focus();
            return;
        }

        ClearLanding();
        ReadFrameRange(out int minFrame, out int maxFrame);
        var criteria = new StatSearchCriteria
        {
            Seed = ReadTrainerId(),
            MinFrame = minFrame,
            MaxFrame = maxFrame,
            BaseStats = SelectedSpecies.BaseStats,
            Level = SelectedLevel,
            Stats = stats,
            NatureId = natureId
        };

        UseWaitCursor = true;
        try
        {
            ShowResults(StatSearch.Search(criteria), 0);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ShowResults(List<PokemonRng> results, int selectedIndex, bool takeFocus = true,
        bool levelStats = false, bool allSeed = false)
    {
        SelectTab(TabKey.Manip);

        _resultSpecies = SelectedSpecies;
        _resultFps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;
        _timeShiftFrames = StarterTool.VariableOffset?.PressShiftFrames ?? 0;
        _levelStatRows = levelStats;

        ListViewResults.VirtualListSize = 0;
        _results = results;

        ApplyResultColumns(allSeed);

        ListViewResults.VirtualListSize = _results.Count;
        FitLastColumn();
        ListViewResults.Refresh();

        if (takeFocus)
        {
            _trainerIdCaretClosed = true;
            HandCaretToResults();
        }

        if (_results.Count == 0)
        {
            StatBoxIvs.Clear();
            StatBoxIvs.TrailingValue = "";
            StatBoxStats.Clear();
            PublishStatBoxes();
            return;
        }

        ListViewResults.SelectedIndices.Clear();
        if (selectedIndex < 0) return;

        selectedIndex = Math.Clamp(selectedIndex, 0, _results.Count - 1);
        ListViewResults.SelectedIndices.Add(selectedIndex);
        ListViewResults.EnsureVisible(selectedIndex);
        UpdateStatBoxes();
    }

    private void ApplyResultColumns(bool allSeed, (string Text, int Width)[]? encounter = null)
    {
        _allSeedRows = allSeed;

        bool wasEncounter = _encounterGrid;
        _encounterGrid = encounter is not null;
        if (encounter is not null)
        {
            _encounterFill = encounter.Length - 1;
            for (int i = 0; i < ListViewResults.Columns.Count; i++)
            {
                ColumnHeader column = ListViewResults.Columns[i];
                if (i < encounter.Length)
                {
                    column.Text = encounter[i].Text;
                    column.Width = Scaled(encounter[i].Width);
                }
                else
                {
                    column.Width = 0;
                }
            }

            FitLastColumn();
            return;
        }

        if (wasEncounter)
        {
            for (int i = TimeColumnIndex + 1; i < ListViewResults.Columns.Count && i < _designedColumns.Length; i++)
            {
                ListViewResults.Columns[i].Text = _designedColumns[i].Text;
                ListViewResults.Columns[i].Width = Scaled(_designedColumns[i].Width);
            }
        }

        ColumnHeader lead = ListViewResults.Columns[LeadColumnIndex];
        ColumnHeader second = ListViewResults.Columns[TimeColumnIndex];

        lead.Text = allSeed ? "TID" : "Frame";
        lead.Width = Scaled(allSeed ? TidColumnWidth : FrameColumnWidth);

        second.Text = allSeed ? "Frame" : "Time";
        second.Width = allSeed
            ? Scaled(FrameColumnWidth)
            : Scaled(StarterTool.TimeFormat == TimeFormat.Minutes
                ? MinutesTimeColumnWidth
                : SecondsTimeColumnWidth);

        FitLastColumn();
    }

    public void RefreshTimeColumn()
    {
        int shift = StarterTool.VariableOffset?.PressShiftFrames ?? 0;
        if (shift == _timeShiftFrames) return;

        _timeShiftFrames = shift;
        if (_results.Count > 0) ListViewResults.RedrawItems(0, _results.Count - 1, true);
    }

    private void FitLastColumn()
    {
        const int MinLastColumnWidth = 33;

        if (_encounterGrid)
        {
            int taken = 0;
            for (int i = 0; i < _encounterFill; i++) taken += ListViewResults.Columns[i].Width;

            ColumnHeader shown = ListViewResults.Columns[_encounterFill];
            int room = ListViewResults.ClientSize.Width - taken;
            if (room >= Scaled(MinLastColumnWidth) && room != shown.Width) shown.Width = room;
            return;
        }

        ColumnHeader last = ListViewResults.Columns[ListViewResults.Columns.Count - 1];
        int minLast = Scaled(MinLastColumnWidth);
        int used = 0;
        foreach (ColumnHeader column in ListViewResults.Columns)
        {
            if (column != last) used += column.Width;
        }

        int fill = ListViewResults.ClientSize.Width - used;
        if (fill >= minLast && fill != last.Width) last.Width = fill;
    }

    public void ShowLanding(
        int? landedFrame,
        int targetFrame,
        double deltaMs,
        double hitChance,
        int adjustmentFrames = 0,
        double compensationMs = 0.0,
        double fps = 0.0,
        double rawChance = double.NaN)
    {
        double gradedChance = double.IsNaN(rawChance) ? hitChance : rawChance;

        Label readout = ActiveLandingLabel;
        readout.ForeColor = gradedChance > 0.5 ? Theme.LandingHitText
            : gradedChance > 0.0 ? Theme.LandingMaybeText
            : Theme.LandingMissText;

        string likely = landedFrame is { } named
            ? $"Likely Frame {named}, Target "
            : "Frame not anchored (no lab box), Target ";

        readout.Text =
            likely
            + VariableOffsetCalculator.FormatFrameWithAdjustment((uint)Math.Max(targetFrame, 0), adjustmentFrames)
            + $"  ({deltaMs:+0;-0;0}ms)  Hit Chance {FormatChance(hitChance)}"
            + CompensationSuffix(compensationMs, compact: true);

        if (TrainingPanel.RecordLanding(landedFrame ?? targetFrame, targetFrame, deltaMs, gradedChance)) return;

        _landingTarget = targetFrame;

        if (landedFrame is not { } landed)
        {
            _landingFrame = null;
            _landingAlternate = null;
            _landingContext.Clear();

            _reportingLanding = true;
            try
            {
                SearchAroundFrame(targetFrame, targetFrame, takeFocus: false);
            }
            finally
            {
                _reportingLanding = false;
            }

            int target = _results.FindIndex(pkm => pkm.Frame == targetFrame);
            if (target >= 0) ListViewResults.EnsureVisible(target);
            ListViewResults.Invalidate();
            return;
        }

        _landingFrame = landed;
        _landingChance = gradedChance;

        _landingAlternate = null;
        if (fps > 0.0 && VariableOffsetCalculator.AlternateChance(deltaMs, fps) > 0.0)
        {
            int alternate = VariableOffsetCalculator.AlternateFrame(landed, deltaMs, fps);
            if (alternate != targetFrame) _landingAlternate = alternate;
        }

        _landingContext.Clear();
        if (fps > 0.0)
        {
            double frameMs = 1000.0 / fps;
            double frames = deltaMs / frameMs;
            double pressMs = (landed + (frames - Math.Floor(frames + 0.5))) * frameMs;

            foreach (int frame in FrameWindow.Candidates(pressMs, fps, StarterTool.Settings?.NpcContextWindowMs ?? 0.0))
            {
                if (frame == landed || frame == targetFrame || frame == _landingAlternate) continue;
                _landingContext.Add(frame);
            }
        }

        int index = _allSeedRows ? -1 : _results.FindIndex(pkm => pkm.Frame == landed);

        bool candidateMissing =
            (_landingAlternate.HasValue && !_results.Exists(pkm => pkm.Frame == _landingAlternate.Value))
            || _landingContext.Any(frame => !_results.Exists(pkm => pkm.Frame == frame));

        if (index < 0 || candidateMissing)
        {
            int radius = SearchRadius;
            foreach (int frame in _landingContext) radius = Math.Max(radius, Math.Abs(frame - landed) + 1);

            _reportingLanding = true;
            try
            {
                SearchAroundFrame(landed, targetFrame, takeFocus: false, radius);
            }
            finally
            {
                _reportingLanding = false;
            }

            index = _results.FindIndex(pkm => pkm.Frame == landed);
        }

        if (index >= 0) ListViewResults.EnsureVisible(index);
        ListViewResults.Invalidate();
    }

    private static string CompensationSuffix(double compensationMs, bool compact = false) =>
        Math.Abs(compensationMs) < 0.5 ? ""
            : compact ? $"  (comp {compensationMs:+0;-0;0} ms)"
            : $"   Timing compensation: {compensationMs:+0;-0;0} ms";

    internal static string FormatChance(double chance) =>
        chance > 0.0 && chance < 0.005 ? "<1%" : $"{chance * 100.0:0}%";

    public void ShowTimingStatus(string what, double compensationMs)
    {
        ClearLanding();

        string suffix = CompensationSuffix(compensationMs);
        Label readout = ActiveLandingLabel;
        readout.ForeColor = Theme.DimText;
        readout.Text = suffix.Length == 0 ? "" : what + suffix;
    }

    public void ShowEncounterLanding(IReadOnlyList<EncounterLandingRow> rows, string status, double? worstChance)
    {
        ClearLanding();

        ListViewResults.VirtualListSize = 0;
        _results = new List<PokemonRng>();
        _encounterRows = rows
            .Select(r => new EncounterGridRow(new[] { r.Press, r.Target, r.Landed, r.Off, r.Hit }, r.Chance))
            .ToList();
        ApplyResultColumns(allSeed: false, EncounterLandingColumns);
        ListViewResults.VirtualListSize = _encounterRows.Count;
        FitLastColumn();
        ListViewResults.SelectedIndices.Clear();
        ListViewResults.Refresh();

        LabelLanding.ForeColor = worstChance is not { } chance ? Theme.DimText
            : chance > 0.5 ? Theme.LandingHitText
            : chance > 0.0 ? Theme.LandingMaybeText
            : Theme.LandingMissText;
        LabelLanding.Text = status;
    }

    private void DrawEncounterSubItem(DrawListViewSubItemEventArgs e)
    {
        double? chance = e.ItemIndex >= 0 && e.ItemIndex < _encounterRows.Count
            ? _encounterRows[e.ItemIndex].Chance
            : null;

        Color back = chance is not { } scored
                ? e.ItemIndex % 2 == 1 ? Theme.RowAlternate : Theme.RowPrimary
            : scored > 0.5 ? Theme.LandingHitBack
            : scored > 0.0 ? Theme.LandingMaybeBack
            : Theme.LandingMissBack;
        Color fore = chance != null ? Theme.LandingRowText : ListViewResults.ForeColor;

        using (var brush = new SolidBrush(back))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }

        using (var pen = new Pen(Theme.GridLine))
        {
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            if (e.ColumnIndex < _encounterFill)
            {
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom - 1);
            }
        }

        TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                                | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter;
        Rectangle bounds = e.Bounds;
        bounds.Inflate(-1, 0);
        bounds.Height -= ThemedListView.RuleClearance;
        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", ListViewResults.Font, bounds, fore, flags);
    }

    public string SelectedEncounterRoute =>
        ComboBoxEncounterRoute.SelectedIndex > 0 ? ComboBoxEncounterRoute.SelectedItem as string ?? "" : "";

    public void RefreshEncounterRoutes(string keep)
    {
        ComboBoxEncounterRoute.BeginUpdate();
        try
        {
            ComboBoxEncounterRoute.Items.Clear();
            ComboBoxEncounterRoute.Items.Add("None");
            int index = 0;
            foreach (Core.Settings.EncounterRoutePreset route in EncounterPanel.Routes)
            {
                ComboBoxEncounterRoute.Items.Add(route.Name);
                if (Core.Settings.EncounterRoutePreset.NameEquals(route.Name, keep))
                {
                    index = ComboBoxEncounterRoute.Items.Count - 1;
                }
            }
            ComboBoxEncounterRoute.SelectedIndex = index;
        }
        finally
        {
            ComboBoxEncounterRoute.EndUpdate();
        }
    }

    public void ClearLanding()
    {
        if (_landingFrame == null && _landingTarget == null) return;

        _landingFrame = null;
        _landingTarget = null;
        _landingAlternate = null;
        _landingContext.Clear();
        ActiveLandingLabel.Text = "";
        ListViewResults.Invalidate();
    }

    public int TrackerSeed => ReadTrainerId();

    private int ReadTrainerId()
    {
        string text = TextBoxTrainerId.Text.Trim();
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            || value < 0 || value > MaxTrainerId)
        {
            value = 0;
            TextBoxTrainerId.Text = "0";
        }
        return value;
    }

    private void ReadFrameRange(out int minFrame, out int maxFrame)
    {
        if (!int.TryParse(TextBoxMinFrame.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out minFrame)
            || minFrame < 0)
        {
            minFrame = 0;
            TextBoxMinFrame.Text = "0";
        }

        if (!int.TryParse(TextBoxMaxFrame.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxFrame)
            || maxFrame < minFrame)
        {
            maxFrame = minFrame;
            TextBoxMaxFrame.Text = maxFrame.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void ListViewResults_RetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        if (_encounterGrid)
        {
            var row = new ListViewItem(string.Empty);
            if (e.ItemIndex >= 0 && e.ItemIndex < _encounterRows.Count)
            {
                string[] cells = _encounterRows[e.ItemIndex].Cells;
                row.Text = cells[0];
                for (int cell = 1; cell < cells.Length; cell++) row.SubItems.Add(cells[cell]);
            }
            while (row.SubItems.Count < ListViewResults.Columns.Count) row.SubItems.Add(string.Empty);
            e.Item = row;
            return;
        }

        if (e.ItemIndex < 0 || e.ItemIndex >= _results.Count)
        {
            var blank = new ListViewItem(string.Empty);
            for (int column = 1; column < ListViewResults.Columns.Count; column++)
            {
                blank.SubItems.Add(string.Empty);
            }
            e.Item = blank;
            return;
        }

        PokemonRng pkm = _results[e.ItemIndex];

        GenderRate rate = _resultSpecies.GenderRate;
        string gender = pkm.IsFemale(rate) ? "F" : rate == GenderRate.Genderless ? "-" : "M";

        ListViewItem item;
        if (_allSeedRows)
        {
            item = new ListViewItem(pkm.Seed.ToString(CultureInfo.InvariantCulture));
            item.SubItems.Add(pkm.Frame.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            item = new ListViewItem(pkm.Frame.ToString(CultureInfo.InvariantCulture));
            item.SubItems.Add(FrameTime.Format(pkm.Frame + _timeShiftFrames, _resultFps, StarterTool.TimeFormat));
        }
        var nature = pkm.Nature ?? new Nature(0);
        item.SubItems.Add(pkm.Nature?.Name ?? "");
        int[] values = new[] { pkm.Hp, pkm.Atk, pkm.Def, pkm.Spa, pkm.Spd, pkm.Spe };
        if (_levelStatRows)
        {
            values = StatCalculator.Calculate(_resultSpecies.BaseStats, values, SelectedLevel, nature);
        }
        foreach (int value in values)
        {
            item.SubItems.Add(value.ToString(CultureInfo.InvariantCulture));
        }
        item.SubItems.Add(gender);
        e.Item = item;
    }

    private void ListViewResults_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using (var background = new SolidBrush(Theme.HeaderBack))
        {
            e.Graphics.FillRectangle(background, e.Bounds);
        }
        using (var pen = new Pen(Theme.Border))
        {
            if (e.ColumnIndex < ListViewResults.Columns.Count - 1)
            {
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 2, e.Bounds.Right - 1, e.Bounds.Bottom - 3);
            }
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                                | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding
                                | TextFormatFlags.HorizontalCenter;
        Rectangle bounds = e.Bounds;
        bounds.Inflate(-2, 0);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", ListViewResults.Font, bounds, Theme.Text, flags);
    }

    private void ListViewResults_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (_encounterGrid)
        {
            DrawEncounterSubItem(e);
            return;
        }

        bool inRange = e.ItemIndex >= 0 && e.ItemIndex < _results.Count;
        bool marked = inRange && _landingFrame != null && _results[e.ItemIndex].Frame == _landingFrame.Value;

        bool alternate = !marked && inRange
                         && _landingAlternate != null
                         && _results[e.ItemIndex].Frame == _landingAlternate.Value;

        bool contextOnly = !marked && !alternate && inRange
                           && _landingContext.Contains(_results[e.ItemIndex].Frame);

        bool target = !marked && !alternate && !contextOnly && inRange
                      && _landingTarget != null
                      && _results[e.ItemIndex].Frame == _landingTarget.Value;

        bool selected = ListViewResults.SelectedIndices.Contains(e.ItemIndex);

        Color? range = !marked && !alternate && !contextOnly && !target && inRange
            ? RangeRowColor(_results[e.ItemIndex].RangeIndex)
            : null;

        Color back = marked
                ? _landingChance > 0.5 ? Theme.LandingHitBack
                : _landingChance > 0.0 ? Theme.LandingMaybeBack
                : Theme.LandingMissBack
            : alternate ? Theme.LandingAlternateBack
            : contextOnly ? Theme.LandingContextBack
            : target ? Theme.LandingTargetBack
            : range ?? (e.ItemIndex % 2 == 1 ? Theme.RowAlternate : Theme.RowPrimary);

        if (selected) back = Theme.Selected(back);

        Color fore = marked || alternate || contextOnly || target ? Theme.LandingRowText
            : range != null ? Theme.RangeRowText(back)
            : ListViewResults.ForeColor;

        using (var brush = new SolidBrush(back))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }

        using (var pen = new Pen(Theme.GridLine))
        {
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            if (e.ColumnIndex < ListViewResults.Columns.Count - 1)
            {
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom - 1);
            }
        }

        TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                                | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter;

        Rectangle bounds = e.Bounds;
        bounds.Inflate(-1, 0);
        bounds.Height -= ThemedListView.RuleClearance;
        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", ListViewResults.Font, bounds, fore, flags);

        DrawSelectionOutline(e, selected);
    }

    private void DrawSelectionOutline(DrawListViewSubItemEventArgs e, bool selected)
    {
        if (!selected) return;

        const int Thickness = 2;
        Rectangle cell = e.Bounds;
        using var brush = new SolidBrush(Theme.Accent);

        e.Graphics.FillRectangle(brush, cell.Left, cell.Top, cell.Width, Thickness);
        e.Graphics.FillRectangle(brush, cell.Left, cell.Bottom - Thickness, cell.Width, Thickness);

        if (e.ColumnIndex == 0)
        {
            e.Graphics.FillRectangle(brush, cell.Left, cell.Top, Thickness, cell.Height);
        }
        if (e.ColumnIndex == ListViewResults.Columns.Count - 1)
        {
            e.Graphics.FillRectangle(brush, cell.Right - Thickness, cell.Top, Thickness, cell.Height);
        }
    }

    private void ListViewResults_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ListViewResults.SelectedIndices.Count == 0) return;

        int index = ListViewResults.SelectedIndices[0];
        if (index < 0 || index >= _results.Count) return;

        if (!_reportingLanding) StarterTool.VariableOffset?.SetFrame(_results[index].Frame);
        UpdateStatBoxes();
    }

    private void RefreshStatBoxLevel()
    {
        StatBoxStats.TrailingValue = SelectedLevel.ToString(CultureInfo.InvariantCulture);
        UpdateStatBoxes();

        PublishStatBoxes();
    }

    private void RefreshStatColumns()
    {
        if (!_levelStatRows || _results.Count == 0) return;

        ListViewResults.RedrawItems(0, _results.Count - 1, true);
    }

    private void RefreshStatBoxColors()
    {
        StatBoxIvs.RefreshColors();
        StatBoxStats.RefreshColors();
        PublishStatBoxes();
    }

    private void PublishStatBoxes() =>
        StarterTool.StatServer?.Publish(StatBoxIvs.Content, StatBoxStats.Content);

    public void ExportStats()
    {
        if (ListViewResults.SelectedIndices.Count == 0) return;

        int index = ListViewResults.SelectedIndices[0];
        if (index < 0 || index >= _results.Count) return;

        PokemonRng pkm = _results[index];
        var lines = new List<string> { pkm.Nature?.Name ?? "" };
        foreach (int iv in new[] { pkm.Hp, pkm.Atk, pkm.Def, pkm.Spa, pkm.Spd, pkm.Spe })
        {
            lines.Add(iv.ToString(CultureInfo.InvariantCulture));
        }

        bool asRow = StarterTool.Settings?.ClipboardFormat == ClipboardFormat.Row;

        try
        {
            Clipboard.SetText(string.Join(asRow ? "\t" : "\r\n", lines));
        }
        catch (Exception)
        {
            return;
        }

        if (HasLanding) return;

        Label readout = ActiveLandingLabel;
        readout.ForeColor = Theme.DimText;
        readout.Text = $"Copied frame {pkm.Frame}: nature and IVs, "
                       + (asRow ? "one row" : "one per line");
    }

    private void UpdateStatBoxes()
    {
        if (ListViewResults.SelectedIndices.Count == 0) return;

        int index = ListViewResults.SelectedIndices[0];
        if (index < 0 || index >= _results.Count) return;

        PokemonRng pkm = _results[index];
        var nature = pkm.Nature ?? new Nature(0);
        var ivs = new[] { pkm.Hp, pkm.Atk, pkm.Def, pkm.Spa, pkm.Spd, pkm.Spe };

        StatBoxIvs.SetValues(ivs, nature.Name);
        StatBoxIvs.TrailingValue = pkm.Frame.ToString(CultureInfo.InvariantCulture);
        StatBoxStats.SetValues(
            StatCalculator.Calculate(_resultSpecies.BaseStats, ivs, SelectedLevel, nature),
            nature.Name);
        PublishStatBoxes();
    }

    private void ListViewResults_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Up or Keys.Down or Keys.Left or Keys.Right:
            case Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End:
            case Keys.Add or Keys.Subtract:
                e.SuppressKeyPress = true;
                return;
        }

        VariableOffsetTimer? timer = StarterTool.VariableOffset;
        if (timer == null) return;

        switch (e.KeyCode)
        {
            case Keys.Enter:
                if (StarterTool.TakeIdleStart(e.KeyCode)) break;
                timer.Arm();
                e.SuppressKeyPress = true;
                break;

            case Keys.Escape:
                if (StarterTool.IsBoundKey(e.KeyCode)) break;
                ResetTrainerId();
                e.SuppressKeyPress = true;
                break;

            case Keys.Decimal:
                if (!StarterTool.IsTimerRunning || StarterTool.IsBoundKey(e.KeyCode)) break;
                ResetTrainerId();
                e.SuppressKeyPress = true;
                break;

            case >= Keys.NumPad0 and <= Keys.NumPad9:
                if (!StarterTool.IsTimerRunning) break;
                TypeTrainerIdDigit((char)('0' + (e.KeyCode - Keys.NumPad0)));
                e.SuppressKeyPress = true;
                break;
        }
    }
}
