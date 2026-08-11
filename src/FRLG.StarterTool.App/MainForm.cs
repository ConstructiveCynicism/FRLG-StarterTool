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

    public bool HasLanding => _landingFrame != null;

    private double _landingChance;

    private int? _landingAlternate;

    private bool _reportingLanding;

    private PokemonSpecies _resultSpecies = PokemonSpecies.Get(1);
    private double _resultFps = 60.0;

    private int _timeShiftFrames;

    public MainForm()
    {
        InitializeComponent();

        foreach (PokemonSpecies species in PokemonSpecies.GetList())
        {
            ComboBoxPokemon.Items.Add(species);
        }
        ComboBoxPokemon.SelectedIndexChanged += (_, _) => UpdateSprite();
        SelectSpecies(1);
        ButtonNaturesAll.Click += (_, _) => SetAllNatures(true);
        ButtonNaturesNone.Click += (_, _) => SetAllNatures(false);
        ButtonSearch.Click += (_, _) => RunSearch();
        ButtonClearIvs.Click += (_, _) => ClearIvThresholds();
        ButtonCalculateOdds.Click += (_, _) => CalculateOdds();
        ButtonSearchFrame.Click += (_, _) => SearchTypedFrame();
        ButtonLevelToggle.CheckedChanged += (_, _) =>
        {
            ButtonLevelToggle.Text = ButtonLevelToggle.Checked ? "Level 6" : "Level 5";
            RefreshStatBoxLevel();
        };
        StatBoxStats.Click += (_, _) => ToggleLevel();
        StatBoxIvs.Click += (_, _) => ExportStats();
        StatBoxIvs.Cursor = Cursors.Hand;
        StatBoxPanel.LabelColorChanged += (_, _) => RefreshStatBoxColors();
        StatBoxPanel.FillColorChanged += (_, _) => RefreshStatBoxColors();
        InitializeStatSearch();
        MenuItemHotkeys.Click += (_, _) => StarterTool.ShowSettings();
        MenuItemAlwaysOnTop.CheckedChanged += (_, _) => RefreshAlwaysOnTop();
        MenuItemGlobalHotkeys.CheckedChanged += (_, _) => RefreshGlobalHotkeys();
        MenuItemTraining.CheckedChanged += (_, _) => ShowTraining(MenuItemTraining.Checked);
        MenuItemContextTracking.CheckedChanged += (_, _) => ShowContextTracking(MenuItemContextTracking.Checked);
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
        TrainingPanel.CloseRequested += (_, _) => MenuItemTraining.Checked = false;
        InitializeFilters();

        TextBoxTrainerId.KeyPress += TextBoxTrainerId_KeyPress;
        TextBoxTrainerId.KeyDown += TextBoxTrainerId_KeyDown;

        TextBoxSearchFrame.KeyPress += TextBoxTrainerId_KeyPress;
        TextBoxSearchFrame.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;

            SearchTypedFrame();
            e.SuppressKeyPress = true;
        };

        foreach (Control input in new Control[] { TextBoxMinFrame, TextBoxMaxFrame, ComboBoxPokemon })
        {
            input.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;

                RunSearch();
                e.SuppressKeyPress = true;
            };
        }

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
        ButtonLevelToggle.Checked = settings.Level == 6;
        RefreshStatBoxLevel();
        StatBoxPanel.LabelColor = StatBoxPanel.ParseColor(settings.StatBoxLabelColor);
        StatBoxPanel.FillColor = StatBoxPanel.ParseFillColor(settings.StatBoxFillColor);
        TrainingPanel.LoadRounds(settings.TrainingRounds);

        MenuItemAlwaysOnTop.Checked = settings.AlwaysOnTop;
        MenuItemGlobalHotkeys.Checked = settings.GlobalHotkeysEnabled;
        MenuItemContextTracking.Checked = settings.NpcGridVisible;
        ShowContextTracking(MenuItemContextTracking.Checked);
        RefreshGlobalHotkeys();
        RefreshAlwaysOnTop();

        ApplyFilter(settings.GetCurrentFilter());
    }

    public void CaptureSettings(AppSettings settings)
    {
        settings.Level = SelectedLevel;
        settings.StatBoxLabelColor = StatBoxPanel.ToHex(StatBoxPanel.LabelColor);
        settings.StatBoxFillColor = StatBoxPanel.ToHex(StatBoxPanel.FillColor);
        settings.TrainingRounds = TrainingPanel.SaveRounds();
        settings.AlwaysOnTop = MenuItemAlwaysOnTop.Checked;
        settings.GlobalHotkeysEnabled = MenuItemGlobalHotkeys.Checked;
        settings.NpcGridVisible = MenuItemContextTracking.Checked;
        settings.SetCurrentFilter(CaptureFilter());
    }

    private void ToggleTraining()
    {
        if (TrainingPanel.IsRunning)
        {
            MenuItemTraining.Checked = false;
            return;
        }

        MenuItemTraining.Checked = true;
        TrainingPanel.StartSession();
    }

    private void RefreshTrainingButton()
        => ButtonTraining.Text = TrainingPanel.IsRunning ? "Stop Offset Training" : "Start Offset Training";

    private void ShowTraining(bool training)
    {
        if (!training) TrainingPanel.Cancel();

        TrainingPanel.Visible = training;
        ListViewResults.Visible = !training;
        GroupBoxResults.Text = training ? "Offset Training" : "Found List";

        ClearLanding();
        LabelLanding.Text = "";
    }

    private void ShowContextTracking(bool tracking)
    {
        StarterTool.Context.Tracking = tracking;

        GroupBoxContext.Visible = tracking;
        ClientSize = new Size(
            ClientSize.Width, tracking ? _trackingClientHeight : _compactClientHeight);

        if (tracking) ShowContextSession();
    }

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
            ButtonContextLate.Text = lab is { Late: true } ? "I'm Fast!" : "I'm Slow!";
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

        ButtonContextAnchor.Text = AnchorCaption(session.NextAnchor);
        ButtonContextAnchor.Enabled = session.NextAnchor != null;

        ButtonContextMiss.Visible = session.Hidden.Count == 0;
        ButtonContextMiss.Enabled = session.CanMiss;

        ContextPanel.SetHidden(session.Hidden);
    }

    private static string AnchorCaption(RouteAnchor? next) => next switch
    {
        RouteAnchor.ExitHouse => "Anchor: house exit",
        RouteAnchor.CloseOakText => "Anchor: Oak text",
        RouteAnchor.CloseLabText => "Anchor: lab text",
        _ => "Anchor",
    };

    public void SampleContextPanel() => ContextPanel.Sample();

    private void ApplyStatPack(int pack, int[] values)
    {
        for (int stat = pack == 1 ? 0 : 1; stat < 6 && stat < values.Length; stat++)
        {
            TextBoxIvThresholds[pack, stat].Text = values[stat].ToString(CultureInfo.InvariantCulture);
        }
    }

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

        if (ReferenceEquals(ActiveControl, TextBoxTrainerId)) TakeCaret(ListViewResults);
    }

    public void UnlockTrainerId() => _trainerIdLocked = false;

    private bool _trainerIdLocked;

    public void FocusTrainerId()
    {
        if (!TextBoxTrainerId.CanFocus) return;

        TakeCaret(TextBoxTrainerId);
        TextBoxTrainerId.SelectAll();
    }

    private void SetAllNatures(bool @checked)
    {
        foreach (CheckBox box in CheckBoxNatures)
        {
            box.Checked = @checked;
        }
    }

    private void ClearIvThresholds()
    {
        for (int pack = 0; pack < 3; pack++)
        {
            for (int stat = 0; stat < 6; stat++)
            {
                TextBoxIvThresholds[pack, stat].Text = "0";
            }
        }
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
                TextBoxTrainerId.Clear();
                e.SuppressKeyPress = true;
                break;

            case Keys.Enter:
                RunSearch();
                e.SuppressKeyPress = true;
                break;
        }
    }

    private void RunSearch()
    {
        ClearLanding();

        PredictorSearchCriteria criteria = ReadCriteria();

        UseWaitCursor = true;
        try
        {
            ShowResults(PredictorSearch.Search(criteria), 0);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private PredictorSearchCriteria ReadCriteria()
    {
        int seedValue = ReadTrainerId();
        ReadFrameRange(out int minFrame, out int maxFrame);

        var natures = new bool[Nature.NatureCount];
        for (int i = 0; i < Nature.NatureCount; i++)
        {
            natures[i] = CheckBoxNatures[i].Checked;
        }

        return new PredictorSearchCriteria
        {
            Seed = seedValue,
            MinFrame = minFrame,
            MaxFrame = maxFrame,
            Natures = natures,
            Minus = ReadStatPack(0),
            Neutral = ReadStatPack(1),
            Plus = ReadStatPack(2)
        };
    }

    private async void CalculateOdds()
    {
        PredictorSearchCriteria criteria = ReadCriteria();

        ButtonCalculateOdds.Enabled = false;
        ButtonCalculateOdds.Text = "Calculating...";
        try
        {
            double odds = await Task.Run(() => SeedOdds.Calculate(criteria));
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

    private void SearchAroundFrame(int centre)
    {
        ClearLanding();
        SearchAroundFrame(centre, centre, takeFocus: true);
    }

    private void SearchAroundFrame(int centre, int selectFrame, bool takeFocus)
    {
        var seed = new Seed(ReadTrainerId());

        var around = new List<PokemonRng>(40);
        int selected = -1;
        for (int frame = centre - 20; frame < centre + 20; frame++)
        {
            if (frame < 0) continue;

            if (frame == selectFrame) selected = around.Count;
            around.Add(new PokemonMethod1(seed, frame));
        }

        ShowResults(around, selected, takeFocus);
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

    private void ShowResults(List<PokemonRng> results, int selectedIndex, bool takeFocus = true)
    {
        if (MenuItemTraining.Checked) MenuItemTraining.Checked = false;

        _resultSpecies = SelectedSpecies;
        _resultFps = StarterTool.VariableOffset?.SelectedFps ?? 60.0;
        _timeShiftFrames = StarterTool.VariableOffset?.PressShiftFrames ?? 0;
        _results = results;

        ListViewResults.VirtualListSize = 0;
        ListViewResults.VirtualListSize = _results.Count;
        FitLastColumn();
        ListViewResults.Refresh();

        if (takeFocus) TakeCaret(ListViewResults);

        if (_results.Count == 0)
        {
            StatBoxIvs.Clear();
            StatBoxIvs.TrailingValue = "";
            StatBoxStats.Clear();
            return;
        }

        ListViewResults.SelectedIndices.Clear();
        if (selectedIndex < 0) return;

        selectedIndex = Math.Clamp(selectedIndex, 0, _results.Count - 1);
        ListViewResults.SelectedIndices.Add(selectedIndex);
        ListViewResults.EnsureVisible(selectedIndex);
        UpdateStatBoxes();
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

        ColumnHeader last = ListViewResults.Columns[ListViewResults.Columns.Count - 1];
        int used = 0;
        foreach (ColumnHeader column in ListViewResults.Columns)
        {
            if (column != last) used += column.Width;
        }

        int fill = ListViewResults.ClientSize.Width - used;
        if (fill >= MinLastColumnWidth && fill != last.Width) last.Width = fill;
    }

    public void ShowLanding(
        int? landedFrame,
        int targetFrame,
        double deltaMs,
        double hitChance,
        int adjustmentFrames = 0,
        double compensationMs = 0.0,
        double fps = 0.0)
    {
        LabelLanding.ForeColor = hitChance > 0.5 ? Theme.LandingHitText
            : hitChance > 0.0 ? Theme.LandingMaybeText
            : Theme.LandingMissText;

        string likely = landedFrame is { } named
            ? $"Likely Frame {named}, Target "
            : "Frame not anchored (no lab box), Target ";

        LabelLanding.Text =
            likely
            + VariableOffsetCalculator.FormatFrameWithAdjustment((uint)Math.Max(targetFrame, 0), adjustmentFrames)
            + $"  ({deltaMs:+0;-0;0}ms)  Hit Chance {FormatChance(hitChance)}"
            + CompensationSuffix(compensationMs, compact: true);

        if (TrainingPanel.RecordLanding(landedFrame ?? targetFrame, targetFrame, deltaMs, hitChance)) return;

        if (landedFrame is not { } landed)
        {
            _landingFrame = null;
            _landingAlternate = null;
            ListViewResults.Invalidate();
            return;
        }

        _landingFrame = landed;
        _landingChance = hitChance;

        _landingAlternate = null;
        if (fps > 0.0 && VariableOffsetCalculator.AlternateChance(deltaMs, fps) > 0.0)
        {
            int alternate = VariableOffsetCalculator.AlternateFrame(landed, deltaMs, fps);
            if (alternate != targetFrame) _landingAlternate = alternate;
        }

        int index = _results.FindIndex(pkm => pkm.Frame == landed);

        bool alternateMissing =
            _landingAlternate.HasValue
            && !_results.Exists(pkm => pkm.Frame == _landingAlternate.Value);

        if (index < 0 || alternateMissing)
        {
            _reportingLanding = true;
            try
            {
                SearchAroundFrame(landed, targetFrame, takeFocus: false);
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
        LabelLanding.ForeColor = Theme.DimText;
        LabelLanding.Text = suffix.Length == 0 ? "" : what + suffix;
    }

    public void ClearLanding()
    {
        if (_landingFrame == null) return;

        _landingFrame = null;
        _landingAlternate = null;
        LabelLanding.Text = "";
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

    private StatPack ReadStatPack(int pack)
    {
        var stats = new int[6];
        for (int stat = 0; stat < 6; stat++)
        {
            _ = int.TryParse(TextBoxIvThresholds[pack, stat].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out stats[stat]);
            stats[stat] = Math.Clamp(stats[stat], 0, 31);
        }
        return new StatPack(stats[0], stats[1], stats[2], stats[3], stats[4], stats[5]);
    }

    private void ListViewResults_RetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        if (e.ItemIndex < 0 || e.ItemIndex >= _results.Count)
        {
            e.Item = new ListViewItem(string.Empty);
            return;
        }

        PokemonRng pkm = _results[e.ItemIndex];

        GenderRate rate = _resultSpecies.GenderRate;
        string gender = pkm.IsFemale(rate) ? "F" : rate == GenderRate.Genderless ? "-" : "M";

        var item = new ListViewItem(pkm.Frame.ToString(CultureInfo.InvariantCulture));
        item.SubItems.Add(FrameTime.Format(pkm.Frame + _timeShiftFrames, _resultFps));
        item.SubItems.Add(pkm.Nature?.Name ?? "");
        foreach (int iv in new[] { pkm.Hp, pkm.Atk, pkm.Def, pkm.Spa, pkm.Spd, pkm.Spe })
        {
            item.SubItems.Add(iv.ToString(CultureInfo.InvariantCulture));
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
        bool inRange = e.ItemIndex >= 0 && e.ItemIndex < _results.Count;
        bool marked = inRange && _landingFrame != null && _results[e.ItemIndex].Frame == _landingFrame.Value;

        bool alternate = !marked && inRange
                         && _landingAlternate != null
                         && _results[e.ItemIndex].Frame == _landingAlternate.Value;
        bool selected = ListViewResults.SelectedIndices.Contains(e.ItemIndex);

        Color back = marked
                ? _landingChance > 0.5 ? Theme.LandingHitBack
                : _landingChance > 0.0 ? Theme.LandingMaybeBack
                : Theme.LandingMissBack
            : alternate ? Theme.LandingAlternateBack
            : selected ? Theme.Accent
            : e.ItemIndex % 2 == 1 ? Theme.RowAlternate
            : Theme.RowPrimary;
        Color fore = marked || alternate ? Theme.LandingRowText
            : selected ? Theme.AccentText
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
    }

    private void RefreshStatBoxColors()
    {
        StatBoxIvs.RefreshColors();
        StatBoxStats.RefreshColors();
    }

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

        LabelLanding.ForeColor = Theme.DimText;
        LabelLanding.Text = $"Copied frame {pkm.Frame}: nature and IVs, "
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
    }

    private void ListViewResults_KeyDown(object? sender, KeyEventArgs e)
    {
        VariableOffsetTimer? timer = StarterTool.VariableOffset;
        if (timer == null) return;

        switch (e.KeyCode)
        {
            case Keys.Enter:
                timer.Arm();
                e.SuppressKeyPress = true;
                break;

            case Keys.Escape:
                ResetTrainerId();
                e.SuppressKeyPress = true;
                break;

            case Keys.Decimal:
                if (!StarterTool.IsTimerRunning) break;
                ResetTrainerId();
                e.SuppressKeyPress = true;
                break;

            case >= Keys.NumPad0 and <= Keys.NumPad9:
                if (!StarterTool.IsTimerRunning) break;
                TypeTrainerIdDigit((char)('0' + (e.KeyCode - Keys.NumPad0)));
                e.SuppressKeyPress = true;
                break;

            case Keys.Add:
                if (ButtonPlus.Enabled) timer.Nudge(1);
                e.SuppressKeyPress = true;
                break;

            case Keys.Subtract:
                if (ButtonMinus.Enabled) timer.Nudge(-1);
                e.SuppressKeyPress = true;
                break;
        }
    }
}
