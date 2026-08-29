using System.Reflection;
using FRLG.StarterTool.Core.Pokemon;
using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Timing;

namespace FRLG.StarterTool.App;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private static string AppVersion
    {
        get
        {
            var version = typeof(MainForm).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion.Split('+')[0];
            return string.IsNullOrEmpty(version) ? "" : "v" + version;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    public MenuStrip MenuStripMain;
    public ToolStripMenuItem MenuItemHotkeys;

    public ToolStripMenuItem MenuItemAlwaysOnTop;

    public ToolStripMenuItem MenuItemGlobalHotkeys;

    public ToolStripMenuItem MenuItemViewManip;

    public ToolStripMenuItem MenuItemViewConstraints;
    public ToolStripMenuItem MenuItemViewTraining;
    public ToolStripMenuItem MenuItemViewEncounter;
    public ToolStripMenuItem MenuItemViewSavestate;
    public ToolStripMenuItem MenuItemViewTroubleshooter;

    public ThemedTabStrip TabStrip;

    public Panel PageManip;

    public Panel PageConstraints;
    public Panel PageTraining;
    public Panel PageEncounter;
    public Panel PageSavestate;
    public Panel PageTroubleshoot;

    public ToolStripMenuItem MenuFilters;

    public ThemedGroupBox GroupBoxStarter;

    public ThemedGroupBox GroupBoxStarterConstraints;

    public ThemedGroupBox GroupBoxFilters;

    public ListBox ListBoxFilters;
    public Button ButtonFilterLoad;
    public Button ButtonFilterSaveAs;
    public Button ButtonFilterUpdate;
    public Button ButtonFilterRename;
    public Button ButtonFilterDelete;

    public ThemedGroupBox GroupBoxRanges;

    public Panel PanelRanges;
    public Button ButtonAddRange;

    public PictureBox PictureBoxSprite;
    public ThemedComboBox ComboBoxPokemon;
    public TextBox TextBoxMinFrame;
    public TextBox TextBoxMaxFrame;
    public TextBox TextBoxTrainerId;

    public Label LabelStarterPokemon;

    public Label LabelStarterMinFrame;
    public Label LabelStarterMaxFrame;
    public Label LabelStarterTrainerId;

    public Button ButtonCalculateOdds;

    public Button ButtonSearch;

    public ThemedGroupBox GroupBoxResults;
    public ThemedListView ListViewResults;

    public ThemedGroupBox GroupBoxTraining;

    public TrainingPanel TrainingPanel;

    public Label LabelTrainingLanding;

    public ThemedGroupBox GroupBoxSavestate;

    public SavestatePanel SavestatePanel;

    public ThemedGroupBox GroupBoxEncounter;

    public EncounterPanel EncounterPanel;

    public ThemedGroupBox GroupBoxRomPatch;

    public RomPatchPanel RomPatchPanel;

    public Label LabelLanding;

    public StatBoxPanel StatBoxIvs;
    public StatBoxPanel StatBoxStats;

    public ThemedGroupBox GroupBoxCapture;

    public ThemedGroupBox GroupBoxContext;

    public NpcGridPanel ContextPanel;

    public ThemedGroupBox GroupBoxTroubleshoot;

    public TroubleshootPanel TroubleshootPanel;

    public Button ButtonContextUndo;

    public Button ButtonContextClear;

    public ThemedButton ButtonContextLate;

    public ThemedButton ButtonContextAnchor;

    public ThemedButton ButtonContextFinished;

    public ThemedButton ButtonContextMiss;

    public CheckBox ButtonLevelToggle;

    public TextBox TextBoxSearchFrame;
    public Button ButtonSearchFrame;

    public ThemedGroupBox GroupBoxStatSearch;

    public ThemedComboBox ComboBoxStatNature;
    public Button ButtonStatSearch;
    public Button ButtonClearStats;

    public TextBox[] TextBoxStats = new TextBox[6];

    public ThemedGroupBox GroupBoxTimer;
    public TimerClock LabelTimer;
    public Button ButtonStart;
    public Button ButtonStop;
    public TextBox TextBoxFrame;
    public ThemedComboBox ComboBoxFps;
    public TextBox TextBoxOffset;

    public TextBox TextBoxVisualOffset;

    public TextBox TextBoxDelayOffset;

    public TextBox TextBoxInterval;
    public TextBox TextBoxBeeps;
    public Button ButtonMinus;
    public Button ButtonPlus;

    public ThemedCheckBox CheckBoxBeepEnabled;

    public ThemedCheckBox CheckBoxFlashEnabled;

    public ThemedComboBox ComboBoxEncounterRoute;

    private const int ClockFitHeight = 76;

    public Button ButtonTraining;

    private static readonly string[] StatRowNames = { "HP", "Attack", "Defense", "Sp. Atk", "Sp. Def", "Speed" };

    private static readonly string[] StatColumnNames = { "HP", "Atk", "Def", "SpA", "SpD", "Spe" };

    private const int TimeColumnIndex = 1;

    private const int LeadColumnIndex = 0;

    private const int FrameColumnWidth = 42;

    private const int TidColumnWidth = 46;

    private const int SecondsTimeColumnWidth = 48;

    private const int MinutesTimeColumnWidth = 57;

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        SuspendLayout();

        MeasureFieldHeights();

        const int LeftColumnWidth = 204;

        const int LeftInner = 8;
        const int LeftInnerRight = LeftColumnWidth - LeftInner;
        const int LeftInnerSpan = LeftInnerRight - LeftInner;

        const int RightColumnWidth = 402;
        const int RightColumnInner = RightColumnWidth - 12;
        const int RightColumnLeft = 6 + LeftColumnWidth + SectionGap;

        const int ClientWidth = RightColumnLeft + RightColumnWidth + 6;

        const int CapturePad = 4;

        const int PairButtonSpan = (LeftInnerSpan - SectionGap) / 2;

        MenuItemHotkeys = new ToolStripMenuItem("Settings…");

        MenuFilters = new ToolStripMenuItem("Filters");

        var menuExit = new ToolStripMenuItem("Exit");
        menuExit.Click += (_, _) => Close();
        var menuFile = new ToolStripMenuItem("File");
        menuFile.DropDownItems.Add(MenuFilters);
        menuFile.DropDownItems.Add(MenuItemHotkeys);
        menuFile.DropDownItems.Add(new ToolStripSeparator());
        menuFile.DropDownItems.Add(menuExit);

        MenuItemViewManip = new ToolStripMenuItem("Manip") { CheckOnClick = true, Checked = true };
        MenuItemViewConstraints = new ToolStripMenuItem("Constraints") { CheckOnClick = true, Checked = true };
        MenuItemViewTraining = new ToolStripMenuItem("Offset Trainer") { CheckOnClick = true, Checked = true };
        MenuItemViewEncounter = new ToolStripMenuItem("Encounter Route") { CheckOnClick = true, Checked = true };
        MenuItemViewSavestate = new ToolStripMenuItem("Savestate Editor") { CheckOnClick = true, Checked = false };
        MenuItemViewTroubleshooter = new ToolStripMenuItem("NPC Troubleshooter") { CheckOnClick = true, Checked = false };
        var menuView = new ToolStripMenuItem("View");
        menuView.DropDownItems.Add(MenuItemViewManip);
        menuView.DropDownItems.Add(MenuItemViewConstraints);
        menuView.DropDownItems.Add(MenuItemViewTraining);
        menuView.DropDownItems.Add(MenuItemViewEncounter);
        menuView.DropDownItems.Add(MenuItemViewSavestate);
        menuView.DropDownItems.Add(MenuItemViewTroubleshooter);

        var menuAbout = new ToolStripMenuItem("About");
        menuAbout.Click += (_, _) => StarterTool.Modal(() => MessageBox.Show(this,
            $"FRLG Starter Tool {AppVersion}\r\n\r\n"
            + "Built on the work of three tools:\r\n"
            + "  • Gen3Predictor — MKDasher, modified by JP_Xinnam\r\n"
            + "  • FlowTimer — Gunnermaniac (gunnermaniac.com/ft)\r\n"
            + "  • Starter Program — stringflow\r\n\r\n"
            + "Copyright for any code borrowed is retained by their respective owners.\r\n\r\n"
            + "Pin icon from Google Material Symbols, licensed under Apache License 2.0.\r\n\r\n"
            + $"Settings: {SettingsStore.DefaultPath}\r\n\r\n"
            + "MIT License\r\n\r\n"
            + "Copyright (c) 2026 ConstructiveCynicism\r\n\r\n"
            + "Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the “Software”), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:\r\n\r\n"
            + "The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.\r\n\r\n"
            + "THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.",
            "About", MessageBoxButtons.OK, MessageBoxIcon.Information));
        var menuClearSettings = new ToolStripMenuItem("Clear settings…");
        menuClearSettings.Click += (_, _) =>
        {
            DialogResult answer = StarterTool.Modal(() => MessageBox.Show(this,
                "Reset every setting to its default and restart the tool?\r\n\r\n"
                + "This deletes the settings file - hotkeys, offsets, colors, saved filters and saved routes. "
                + "Run logs are kept.\r\n\r\n"
                + SettingsStore.DefaultPath,
                "Clear settings", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2));
            if (answer == DialogResult.Yes) StarterTool.ClearSettings();
        };
        var menuHelp = new ToolStripMenuItem("Help");
        menuHelp.DropDownItems.Add(menuClearSettings);
        menuHelp.DropDownItems.Add(new ToolStripSeparator());
        menuHelp.DropDownItems.Add(menuAbout);

        MenuItemAlwaysOnTop = new ToolStripMenuItem
        {
            CheckOnClick = true,
            Alignment = ToolStripItemAlignment.Right,
            Image = Assets.Pin(false),
            ToolTipText = "Keep this window on top"
        };

        MenuItemGlobalHotkeys = new ToolStripMenuItem
        {
            CheckOnClick = true,
            Alignment = ToolStripItemAlignment.Right,
            Image = Assets.Globe(true),
            ToolTipText = "Global hotkeys"
        };

        MenuStripMain = new MenuStrip();
        MenuStripMain.Items.Add(menuFile);
        MenuStripMain.Items.Add(menuView);
        MenuStripMain.Items.Add(menuHelp);
        MenuStripMain.Items.Add(MenuItemAlwaysOnTop);
        MenuStripMain.Items.Add(MenuItemGlobalHotkeys);

        int starterTop = SectionTop;

        GroupBoxStarterConstraints = new ThemedGroupBox
        {
            Text = "Starter",
            Location = new Point(6, starterTop),
            Size = new Size(LeftColumnWidth, 244)
        };

        const int SpriteSize = 64;
        PictureBoxSprite = new PictureBox
        {
            Location = new Point((LeftColumnWidth - SpriteSize) / 2, 14),
            Size = new Size(SpriteSize, SpriteSize),
            BorderStyle = BorderStyle.None,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        GroupBoxStarterConstraints.Controls.Add(PictureBoxSprite);

        int starterRow = 84;

        const int StarterCaptionWidth = 68;
        const int StarterFieldX = LeftInner + StarterCaptionWidth + SectionGap;
        const int StarterFieldWidth = LeftInnerRight - StarterFieldX;

        LabelStarterPokemon = MakeLabel("Pokemon", LeftInner, starterRow, StarterCaptionWidth);
        GroupBoxStarterConstraints.Controls.Add(LabelStarterPokemon);
        ComboBoxPokemon = MakeCombo(StarterFieldX, starterRow, StarterFieldWidth);
        GroupBoxStarterConstraints.Controls.Add(ComboBoxPokemon);

        starterRow += RowPitch;
        LabelStarterMinFrame = MakeLabel("Min Frame", LeftInner, starterRow, StarterCaptionWidth);
        GroupBoxStarterConstraints.Controls.Add(LabelStarterMinFrame);
        TextBoxMinFrame = MakeTextBox(StarterFieldX, starterRow, StarterFieldWidth, "0");
        GroupBoxStarterConstraints.Controls.Add(TextBoxMinFrame);

        starterRow += RowPitch;
        LabelStarterMaxFrame = MakeLabel("Max Frame", LeftInner, starterRow, StarterCaptionWidth);
        GroupBoxStarterConstraints.Controls.Add(LabelStarterMaxFrame);
        TextBoxMaxFrame = MakeTextBox(StarterFieldX, starterRow, StarterFieldWidth, "10000");
        GroupBoxStarterConstraints.Controls.Add(TextBoxMaxFrame);

        GroupBoxStarterConstraints.Height = TextBoxMaxFrame.Bottom + BoxBottomPad;

        GroupBoxStarter = new ThemedGroupBox
        {
            Text = "Seed",
            Location = new Point(6, SectionTop),
            Size = new Size(LeftColumnWidth, 244)
        };

        starterRow = CompactStarterRow;
        LabelStarterTrainerId = MakeLabel("Trainer ID", LeftInner, starterRow, StarterCaptionWidth);
        GroupBoxStarter.Controls.Add(LabelStarterTrainerId);
        TextBoxTrainerId = MakeTextBox(StarterFieldX, starterRow, StarterFieldWidth, "0");
        TextBoxTrainerId.MaxLength = 5;
        GroupBoxStarter.Controls.Add(TextBoxTrainerId);

        int starterButtonRow = starterRow + RowPitch;
        ButtonCalculateOdds = new ThemedButton
        {
            Text = "Calculate",
            Location = new Point(LeftInner, starterButtonRow),
            Size = new Size(PairButtonSpan, _fieldHeight)
        };
        GroupBoxStarter.Controls.Add(ButtonCalculateOdds);

        ButtonSearch = new ThemedButton
        {
            Text = "Search",
            Location = new Point(LeftInner + PairButtonSpan + SectionGap, starterButtonRow),
            Size = new Size(PairButtonSpan, _fieldHeight)
        };
        GroupBoxStarter.Controls.Add(ButtonSearch);

        GroupBoxStarter.Height = ButtonSearch.Bottom + BoxBottomPad;

        int timerTop = GroupBoxStarter.Bottom + SectionGap;

        GroupBoxTimer = new ThemedGroupBox
        {
            Text = "Timer",
            Location = new Point(6, timerTop),
            Size = new Size(LeftColumnWidth, 300)
        };

        const int ClockWidth = LeftInnerSpan;
        int clockHeight = ClockFitHeight - RowPitch;
        LabelTimer = new TimerClock
        {
            Text = "0.000",
            Location = new Point(LeftInner, 18),
            Size = new Size(ClockWidth, clockHeight),
            Font = FitFont(Font.FontFamily, TimeText.Widest(TimeFormat.Seconds), ClockWidth - 4, ClockFitHeight, 44F)
        };
        GroupBoxTimer.Controls.Add(LabelTimer);

        int buttonRow = LabelTimer.Bottom + RowGap;
        ButtonStart = new ThemedButton { Text = "Start", Location = new Point(LeftInner, buttonRow), Size = new Size(PairButtonSpan, 30), Tag = Theme.StartButtonTag };
        ButtonStop = new ThemedButton { Text = "Stop", Location = new Point(LeftInner + PairButtonSpan + SectionGap, buttonRow), Size = new Size(PairButtonSpan, 30), Tag = Theme.StopButtonTag };
        GroupBoxTimer.Controls.Add(ButtonStart);
        GroupBoxTimer.Controls.Add(ButtonStop);

        const int RowCaptionX = 25;
        const int RowCaptionWidth = 48;
        const int RowFieldX = RowCaptionX + RowCaptionWidth + 3;
        const int RowFieldWidth = LeftInnerRight - RowFieldX;

        int timerRow = ButtonStart.Bottom + RowGap;
        GroupBoxTimer.Controls.Add(MakeLabel("Frame", RowCaptionX, timerRow, RowCaptionWidth));

        int nudgeSize = _fieldHeight;
        int plusX = LeftInnerRight - nudgeSize;
        int minusX = plusX - nudgeSize - SectionGap;
        TextBoxFrame = MakeTextBox(RowFieldX, timerRow, minusX - RowFieldX - SectionGap, "");
        TextBoxFrame.Enabled = false;
        GroupBoxTimer.Controls.Add(TextBoxFrame);

        ButtonMinus = MakeNudgeButton("−", minusX, timerRow, nudgeSize);
        ButtonPlus = MakeNudgeButton("+", plusX, timerRow, nudgeSize);
        GroupBoxTimer.Controls.Add(ButtonMinus);
        GroupBoxTimer.Controls.Add(ButtonPlus);

        timerRow += RowPitch;
        GroupBoxTimer.Controls.Add(MakeLabel("FPS", RowCaptionX, timerRow, RowCaptionWidth));
        ComboBoxFps = MakeCombo(RowFieldX, timerRow, RowFieldWidth);
        GroupBoxTimer.Controls.Add(ComboBoxFps);

        timerRow += RowPitch;

        CheckBoxBeepEnabled = MakeCheckCaption("Audio", 8, timerRow);
        TextBoxOffset = MakeTextBox(RowFieldX, timerRow, RowFieldWidth, "0");
        GroupBoxTimer.Controls.Add(CheckBoxBeepEnabled);
        GroupBoxTimer.Controls.Add(TextBoxOffset);

        timerRow += RowPitch;
        CheckBoxFlashEnabled = MakeCheckCaption("Visual", 8, timerRow);
        TextBoxVisualOffset = MakeTextBox(RowFieldX, timerRow, RowFieldWidth, "0");
        GroupBoxTimer.Controls.Add(CheckBoxFlashEnabled);
        GroupBoxTimer.Controls.Add(TextBoxVisualOffset);

        timerRow += RowPitch;
        GroupBoxTimer.Controls.Add(MakeLabel("Delay", RowCaptionX, timerRow, RowCaptionWidth));
        TextBoxDelayOffset = MakeTextBox(RowFieldX, timerRow, RowFieldWidth, "0");
        GroupBoxTimer.Controls.Add(TextBoxDelayOffset);

        timerRow += RowPitch;
        GroupBoxTimer.Controls.Add(MakeLabel("Interval", RowCaptionX, timerRow, RowCaptionWidth));
        TextBoxInterval = MakeTextBox(RowFieldX, timerRow, RowFieldWidth, "1000");
        GroupBoxTimer.Controls.Add(TextBoxInterval);

        timerRow += RowPitch;
        GroupBoxTimer.Controls.Add(MakeLabel("Beeps", RowCaptionX, timerRow, RowCaptionWidth));
        TextBoxBeeps = MakeTextBox(RowFieldX, timerRow, RowFieldWidth, "4");
        GroupBoxTimer.Controls.Add(TextBoxBeeps);

        timerRow += RowPitch;
        GroupBoxTimer.Controls.Add(MakeLabel("Route", RowCaptionX, timerRow, RowCaptionWidth));
        ComboBoxEncounterRoute = MakeCombo(RowFieldX, timerRow, RowFieldWidth);
        ComboBoxEncounterRoute.Items.Add("None");
        ComboBoxEncounterRoute.SelectedIndex = 0;
        GroupBoxTimer.Controls.Add(ComboBoxEncounterRoute);

        ButtonTraining = new ThemedButton
        {
            Text = "Start Offset Training",
            Location = new Point(LeftInner, timerRow + RowPitch),
            Size = new Size(LeftInnerSpan, 26)
        };
        GroupBoxTimer.Controls.Add(ButtonTraining);

        GroupBoxFilters = new ThemedGroupBox
        {
            Text = "Filters",
            Location = new Point(RightColumnLeft, SectionTop),
            Size = new Size(RightColumnWidth, GroupBoxStarterConstraints.Height)
        };

        ListBoxFilters = new ListBox
        {
            Location = new Point(6, 18),
            Size = new Size(RightColumnInner, GroupBoxFilters.Height - 18 - 22 - RowGap - BoxBottomPad),
            IntegralHeight = false,
            SelectionMode = SelectionMode.One
        };
        GroupBoxFilters.Controls.Add(ListBoxFilters);

        int filterButtonY = ListBoxFilters.Bottom + RowGap;
        int filterButtonWidth = (RightColumnInner - 4 * SectionGap) / 5;
        Button MakeFilterButton(string text, int slot) => new ThemedButton
        {
            Text = text,
            Location = new Point(6 + slot * (filterButtonWidth + SectionGap), filterButtonY),
            Size = new Size(filterButtonWidth, 22)
        };

        ButtonFilterLoad = MakeFilterButton("Load", 0);
        ButtonFilterSaveAs = MakeFilterButton("Save As", 1);
        ButtonFilterUpdate = MakeFilterButton("Update", 2);
        ButtonFilterRename = MakeFilterButton("Rename", 3);
        ButtonFilterDelete = MakeFilterButton("Delete", 4);
        GroupBoxFilters.Controls.Add(ButtonFilterLoad);
        GroupBoxFilters.Controls.Add(ButtonFilterSaveAs);
        GroupBoxFilters.Controls.Add(ButtonFilterUpdate);
        GroupBoxFilters.Controls.Add(ButtonFilterRename);
        GroupBoxFilters.Controls.Add(ButtonFilterDelete);

        GroupBoxRanges = new ThemedGroupBox
        {
            Text = "Constraint Ranges",
            Location = new Point(6, GroupBoxStarterConstraints.Bottom + SectionGap),
            Size = new Size(ClientWidth - 12, 200)
        };

        PanelRanges = new Panel
        {
            Location = new Point(6, 18),
            Size = new Size(GroupBoxRanges.Width - 12, 160),
            AutoScroll = true
        };
        GroupBoxRanges.Controls.Add(PanelRanges);

        ButtonAddRange = new ThemedButton
        {
            Text = "+ Add Range",
            Location = new Point(6, PanelRanges.Bottom + RowGap),
            Size = new Size(GroupBoxRanges.Width - 12, 22)
        };
        GroupBoxRanges.Controls.Add(ButtonAddRange);

        GroupBoxResults = new ThemedGroupBox
        {
            Text = "Found List",
            Location = new Point(RightColumnLeft, SectionTop),
            Size = new Size(RightColumnWidth, 372)
        };

        ListViewResults = new ThemedListView
        {
            Location = new Point(6, 18),
            Size = new Size(RightColumnInner, 308),
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            VirtualMode = true,
            OwnerDraw = true,
            Font = new Font("Segoe UI", 8F)
        };
        ListViewResults.Columns.Add("Frame", FrameColumnWidth, HorizontalAlignment.Center);
        ListViewResults.Columns.Add("Time", SecondsTimeColumnWidth, HorizontalAlignment.Center);
        ListViewResults.Columns.Add("Nature", 55, HorizontalAlignment.Center);
        foreach (string stat in StatColumnNames)
        {
            ListViewResults.Columns.Add(stat, 31, HorizontalAlignment.Center);
        }
        ListViewResults.Columns.Add("M/F", 31, HorizontalAlignment.Center);
        GroupBoxResults.Controls.Add(ListViewResults);

        LabelLanding = new Label
        {
            Location = new Point(6, 330),
            Size = new Size(RightColumnInner, 36),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Bold),
            Tag = Theme.KeepForeColor
        };
        GroupBoxResults.Controls.Add(LabelLanding);

        GroupBoxStatSearch = new ThemedGroupBox
        {
            Text = "Search",
            Location = new Point(RightColumnLeft, GroupBoxResults.Bottom + SectionGap),
            Size = new Size(RightColumnWidth, 100)
        };

        const int StatNatureWidth = 92;
        const int StatColumnX = 104;
        int searchRow = 16;
        GroupBoxStatSearch.Controls.Add(MakeCaption("Nature", 8, searchRow, StatNatureWidth));
        for (int stat = 0; stat < 6; stat++)
        {
            GroupBoxStatSearch.Controls.Add(MakeCaption(StatColumnNames[stat], StatColumnX + stat * 49, searchRow, 45));
        }

        searchRow += 18;
        ComboBoxStatNature = MakeCombo(8, searchRow, StatNatureWidth);
        GroupBoxStatSearch.Controls.Add(ComboBoxStatNature);

        for (int stat = 0; stat < 6; stat++)
        {
            TextBox box = MakeTextBox(StatColumnX + stat * 49, searchRow, 45, "");
            box.TextAlign = HorizontalAlignment.Center;
            TextBoxStats[stat] = box;
            GroupBoxStatSearch.Controls.Add(box);
        }

        searchRow += RowPitch;

        GroupBoxStatSearch.Controls.Add(MakeLabel("Frame", 8, searchRow, 42));
        TextBoxSearchFrame = MakeTextBox(50, searchRow, 60, "");
        GroupBoxStatSearch.Controls.Add(TextBoxSearchFrame);
        ButtonSearchFrame = new ThemedButton
        {
            Text = "Go",
            Location = new Point(114, searchRow),
            Size = new Size(44, _fieldHeight)
        };
        GroupBoxStatSearch.Controls.Add(ButtonSearchFrame);

        const int SearchButtonWidth = 80;
        int clearX = RightColumnWidth - 8 - SearchButtonWidth;
        ButtonClearStats = new ThemedButton
        {
            Text = "Clear",
            Location = new Point(clearX, searchRow),
            Size = new Size(SearchButtonWidth, _fieldHeight)
        };
        ButtonStatSearch = new ThemedButton
        {
            Text = "Search",
            Location = new Point(clearX - SearchButtonWidth - SectionGap, searchRow),
            Size = new Size(SearchButtonWidth, _fieldHeight)
        };
        GroupBoxStatSearch.Controls.Add(ButtonStatSearch);
        GroupBoxStatSearch.Controls.Add(ButtonClearStats);

        GroupBoxStatSearch.Height = searchRow + _fieldHeight + BoxBottomPad;

        ButtonLevelToggle = new ThemedCheckBox
        {
            Text = "Level 5",
            Appearance = Appearance.Button,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };
        Controls.Add(ButtonLevelToggle);

        Controls.Add(GroupBoxStatSearch);

        int captureTop = GroupBoxStatSearch.Bottom + SectionGap;
        GroupBoxCapture = new ThemedGroupBox
        {
            Text = "",
            Location = new Point(RightColumnLeft, captureTop),
            Size = new Size(RightColumnWidth, StatBoxPanel.BoxHeight + 2 * CapturePad)
        };
        StatBoxIvs = new StatBoxPanel { Location = new Point(CapturePad, CapturePad) };
        StatBoxStats = new StatBoxPanel
        {
            Location = new Point(CapturePad + StatBoxPanel.BoxWidth + 2, CapturePad),
            Cursor = Cursors.Hand
        };

        StatBoxIvs.SetTrailingCell("FRAME", "00000");
        StatBoxStats.SetTrailingCell("LEVEL", "6");
        GroupBoxCapture.Controls.Add(StatBoxIvs);
        GroupBoxCapture.Controls.Add(StatBoxStats);
        Controls.Add(GroupBoxCapture);

        int timerNeeds = ButtonTraining.Bottom + BoxBottomPad;
        int shortfall = timerTop + timerNeeds - GroupBoxCapture.Bottom;

        shortfall = Math.Max(shortfall, MinResultsHeight - ListViewResults.Height);

        GroupBoxResults.Height += shortfall;
        ListViewResults.Height += shortfall;
        LabelLanding.Top += shortfall;
        GroupBoxStatSearch.Top += shortfall;
        GroupBoxCapture.Top += shortfall;

        GroupBoxTimer.Height = Math.Max(timerNeeds, GroupBoxCapture.Bottom - timerTop);

        int contextTop = GroupBoxCapture.Bottom + SectionGap;
        GroupBoxContext = new ThemedGroupBox
        {
            Text = "Context Tracking",
            Location = new Point(6, contextTop),
            Size = new Size(ClientWidth - 12, 18 + NpcGridPanel.GridPixels + BoxBottomPad)
        };

        ContextPanel = new NpcGridPanel
        {
            Location = new Point(LeftInner, 18),
            Size = new Size(ClientWidth - 12 - 2 * LeftInner, NpcGridPanel.GridPixels)
        };
        GroupBoxContext.Controls.Add(ContextPanel);

        int contextButtonY = ContextPanel.Bottom - 24;
        int contextButtonX = ContextPanel.Left + NpcGridPanel.GridPixels + 12;

        ButtonContextUndo = new ThemedButton
        {
            Text = "Undo",
            Location = new Point(contextButtonX, contextButtonY),
            Size = new Size(66, 24)
        };
        ButtonContextClear = new ThemedButton
        {
            Text = "Clear",
            Location = new Point(ButtonContextUndo.Right + SectionGap, contextButtonY),
            Size = new Size(60, 24)
        };
        ButtonContextLate = new ThemedButton
        {
            Text = "I'm Late!",
            Location = new Point(ContextPanel.Right - 124, contextButtonY),
            Size = new Size(124, 24),
            Visible = false
        };
        ButtonContextMiss = new ThemedButton
        {
            Text = "Miss",
            Location = new Point(ButtonContextClear.Right + SectionGap, contextButtonY),
            Size = new Size(66, 24)
        };
        ButtonContextAnchor = new ThemedButton
        {
            Text = "Anchor",
            Location = new Point(ContextPanel.Right - 66, contextButtonY),
            Size = new Size(66, 24)
        };
        ButtonContextFinished = new ThemedButton
        {
            Text = "Finished!",
            Location = new Point(ContextPanel.Right - 124, contextButtonY),
            Size = new Size(124, 24),
            Visible = false
        };

        GroupBoxContext.Controls.Add(ButtonContextUndo);
        GroupBoxContext.Controls.Add(ButtonContextClear);
        GroupBoxContext.Controls.Add(ButtonContextMiss);
        GroupBoxContext.Controls.Add(ButtonContextLate);
        GroupBoxContext.Controls.Add(ButtonContextFinished);
        GroupBoxContext.Controls.Add(ButtonContextAnchor);

        ContextPanel.SendToBack();

        TabStrip = new ThemedTabStrip
        {
            Location = new Point(6, TabStripTop),
            Size = new Size(ClientWidth - 12, TabStripHeight)
        };

        Panel MakePage() => new()
        {
            Location = new Point(0, PageTop),
            Size = new Size(ClientWidth, 0),
            Visible = false
        };

        PageManip = MakePage();
        PageManip.Controls.Add(GroupBoxStarter);
        PageManip.Controls.Add(GroupBoxTimer);
        PageManip.Controls.Add(GroupBoxResults);
        PageManip.Controls.Add(GroupBoxStatSearch);
        PageManip.Controls.Add(GroupBoxCapture);
        PageManip.Controls.Add(GroupBoxContext);
        PageManip.Height = GroupBoxContext.Bottom + 6;

        const int MinRangesHeight = 240;
        int rangesHeight = Math.Max(MinRangesHeight, PageManip.Height - 6 - GroupBoxRanges.Top);
        GroupBoxRanges.Height = rangesHeight;
        PanelRanges.Height = rangesHeight - PanelRanges.Top - ButtonAddRange.Height - RowGap - BoxBottomPad;
        ButtonAddRange.Top = PanelRanges.Bottom + RowGap;

        PageConstraints = MakePage();
        PageConstraints.Controls.Add(GroupBoxStarterConstraints);
        PageConstraints.Controls.Add(GroupBoxFilters);
        PageConstraints.Controls.Add(GroupBoxRanges);
        PageConstraints.Height = GroupBoxRanges.Bottom + 6;

        const int PageBoxWidth = ClientWidth - 12;

        TrainingPanel = new TrainingPanel
        {
            Location = new Point((RightColumnWidth - TrainingPanel.PanelWidth) / 2, 18),
            Size = new Size(TrainingPanel.PanelWidth, TrainingPanel.PanelHeight)
        };
        LabelTrainingLanding = new Label
        {
            Location = new Point(6, TrainingPanel.Bottom + RowGap),
            Size = new Size(RightColumnWidth - 12, 36),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Bold),
            Tag = Theme.KeepForeColor
        };
        GroupBoxTraining = new ThemedGroupBox
        {
            Text = "Offset Training",
            Location = new Point(RightColumnLeft, SectionTop),
            Size = new Size(
                RightColumnWidth, Math.Max(LabelTrainingLanding.Bottom + BoxBottomPad, GroupBoxTimer.Height))
        };
        GroupBoxTraining.Controls.Add(TrainingPanel);
        GroupBoxTraining.Controls.Add(LabelTrainingLanding);
        PageTraining = MakePage();
        PageTraining.Controls.Add(GroupBoxTraining);
        PageTraining.Height = GroupBoxTraining.Bottom + 6;

        EncounterPanel = new EncounterPanel();
        EncounterPanel.Location = new Point((PageBoxWidth - EncounterPanel.Width) / 2, 18);
        GroupBoxEncounter = new ThemedGroupBox
        {
            Text = "Encounter Route Planner",
            Location = new Point(6, SectionTop),
            Size = new Size(PageBoxWidth, EncounterPanel.Bottom + BoxBottomPad)
        };
        GroupBoxEncounter.Controls.Add(EncounterPanel);
        RomPatchPanel = new RomPatchPanel
        {
            Location = new Point((PageBoxWidth - RomPatchPanel.PanelWidth) / 2, 18),
            Size = new Size(RomPatchPanel.PanelWidth, RomPatchPanel.PanelHeight)
        };
        GroupBoxRomPatch = new ThemedGroupBox
        {
            Text = "Rom Patch",
            Location = new Point(6, GroupBoxEncounter.Bottom + SectionGap),
            Size = new Size(PageBoxWidth, RomPatchPanel.Bottom + BoxBottomPad)
        };
        GroupBoxRomPatch.Controls.Add(RomPatchPanel);

        PageEncounter = MakePage();
        PageEncounter.Controls.Add(GroupBoxEncounter);
        PageEncounter.Controls.Add(GroupBoxRomPatch);
        PageEncounter.Height = GroupBoxRomPatch.Bottom + 6;

        SavestatePanel = new SavestatePanel
        {
            Location = new Point((PageBoxWidth - SavestatePanel.PanelWidth) / 2, 18),
            Size = new Size(SavestatePanel.PanelWidth, SavestatePanel.PanelHeight)
        };
        GroupBoxSavestate = new ThemedGroupBox
        {
            Text = "Savestate Editor",
            Location = new Point(6, SectionTop),
            Size = new Size(PageBoxWidth, SavestatePanel.Bottom + BoxBottomPad)
        };
        GroupBoxSavestate.Controls.Add(SavestatePanel);
        PageSavestate = MakePage();
        PageSavestate.Controls.Add(GroupBoxSavestate);
        PageSavestate.Height = GroupBoxSavestate.Bottom + 6;

        TroubleshootPanel = new TroubleshootPanel
        {
            Location = new Point(LeftInner, 18),
            Size = new Size(PageBoxWidth - 2 * LeftInner, NpcGridPanel.GridPixels)
        };
        GroupBoxTroubleshoot = new ThemedGroupBox
        {
            Text = "NPC Troubleshooter",
            Location = new Point(6, SectionTop),
            Size = new Size(PageBoxWidth, TroubleshootPanel.Bottom + BoxBottomPad)
        };
        GroupBoxTroubleshoot.Controls.Add(TroubleshootPanel);
        PageTroubleshoot = MakePage();
        PageTroubleshoot.Controls.Add(GroupBoxTroubleshoot);
        PageTroubleshoot.Height = GroupBoxTroubleshoot.Bottom + 6;

        AutoScaleMode = AutoScaleMode.Font;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Controls.Add(TabStrip);
        Controls.Add(PageManip);
        Controls.Add(PageConstraints);
        Controls.Add(PageTraining);
        Controls.Add(PageEncounter);
        Controls.Add(PageSavestate);
        Controls.Add(PageTroubleshoot);
        Controls.Add(MenuStripMain);
        MainMenuStrip = MenuStripMain;
        Icon = Assets.AppIcon;
        Text = "FRLG Starter Tool";

        ClientSize = new Size(ClientWidth, 0);
        ApplyClientHeight();

        ResumeLayout(false);
        PerformLayout();
    }

    private static Font FitFont(FontFamily family, string text, int width, int height, float maxSize)
    {
        for (float size = maxSize; size > 10F; size -= 1F)
        {
            var font = new Font(family, size, FontStyle.Bold);
            Size measured = TextRenderer.MeasureText(
                text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);

            if (measured.Width <= width && measured.Height <= height) return font;

            font.Dispose();
        }

        return new Font(family, 10F, FontStyle.Bold);
    }

    private int _fieldHeight = 23;

    private int _comboHeight = 23;

    private const int RowGap = 4;

    private const int SectionTop = 26;

    private const int TabStripTop = SectionTop;

    private const int TabStripHeight = 24;

    private const int PageTop = TabStripTop + TabStripHeight + SectionGap - SectionTop;

    private const int CompactStarterRow = 18;

    private const int SectionGap = 4;

    private const int BoxBottomPad = 6;

    private const int MinResultsHeight = 200;

    private int RowPitch => _fieldHeight + RowGap;

    private void MeasureFieldHeights()
    {
        int naturalBox;
        using (var probe = new ThemedTextBox()) naturalBox = probe.Height;

        using (var probe = new ThemedComboBox { DropDownStyle = ComboBoxStyle.DropDownList })
        {
            _comboHeight = probe.Height;
        }

        _fieldHeight = Math.Max(naturalBox, _comboHeight);
    }

    private ThemedButton MakeNudgeButton(string glyph, int x, int y, int size)
        => new()
        {
            Glyph = glyph,
            Font = new Font(Font.FontFamily, Font.SizeInPoints + 3F, FontStyle.Bold),
            Location = new Point(x, y),
            Size = new Size(size, size),
            Tag = Theme.NudgeButtonTag,
            Enabled = false
        };

    private Label MakeLabel(string text, int x, int rowY, int width)
        => new()
        {
            Text = text,
            Location = new Point(x, rowY),
            Size = new Size(width, _fieldHeight),
            TextAlign = ContentAlignment.MiddleLeft
        };

    private ThemedCheckBox MakeCheckCaption(string text, int x, int rowY)
    {
        var box = new ThemedCheckBox { Text = text, AutoSize = true, Checked = true };
        box.Location = new Point(x, rowY + (_fieldHeight - box.PreferredSize.Height) / 2);
        return box;
    }

    private static Label MakeCaption(string text, int x, int y, int width)
        => new() { Text = text, Location = new Point(x, y), Size = new Size(width, 16), TextAlign = ContentAlignment.MiddleCenter };

    private TextBox MakeTextBox(int x, int y, int width, string text)
        => new ThemedTextBox
        {
            Numeric = true,
            AutoSize = false,
            Location = new Point(x, y),
            Size = new Size(width, _fieldHeight),
            Text = text
        };

    private ThemedComboBox MakeCombo(int x, int rowY, int width)
    {
        var combo = new ThemedComboBox
        {
            Location = new Point(x, rowY),
            Size = new Size(width, _fieldHeight),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        combo.MatchHeight(_fieldHeight);
        combo.Top = rowY + (_fieldHeight - combo.Height) / 2;
        return combo;
    }

    #endregion
}
