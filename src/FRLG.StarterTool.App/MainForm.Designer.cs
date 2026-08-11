using System.Reflection;
using FRLG.StarterTool.Core.Pokemon;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private static string AppVersion =>
        typeof(MainForm).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0]
        ?? "";

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

    public ToolStripMenuItem MenuItemTraining;

    public ToolStripMenuItem MenuItemContextTracking;

    public ToolStripMenuItem MenuFilters;

    public ThemedGroupBox GroupBoxIvConstraint;
    public Button ButtonClearIvs;

    public ThemedGroupBox GroupBoxStarter;
    public PictureBox PictureBoxSprite;
    public ThemedComboBox ComboBoxPokemon;
    public TextBox TextBoxMinFrame;
    public TextBox TextBoxMaxFrame;
    public TextBox TextBoxTrainerId;

    public Button ButtonCalculateOdds;

    public Button ButtonSearch;

    public ThemedGroupBox GroupBoxNatures;
    public Button ButtonNaturesAll;
    public Button ButtonNaturesNone;

    public ThemedGroupBox GroupBoxResults;
    public ThemedListView ListViewResults;

    public TrainingPanel TrainingPanel;

    public Label LabelLanding;

    public StatBoxPanel StatBoxIvs;
    public StatBoxPanel StatBoxStats;

    public ThemedGroupBox GroupBoxCapture;

    public ThemedGroupBox GroupBoxContext;

    public NpcGridPanel ContextPanel;

    public Button ButtonContextUndo;

    public Button ButtonContextClear;

    public ThemedButton ButtonContextLate;

    public ThemedButton ButtonContextAnchor;

    public ThemedButton ButtonContextFinished;

    public ThemedButton ButtonContextMiss;

    private int _compactClientHeight;

    private int _trackingClientHeight;

    public CheckBox ButtonLevelToggle;

    public TextBox TextBoxSearchFrame;
    public Button ButtonSearchFrame;

    public ThemedGroupBox GroupBoxStatSearch;

    public ThemedComboBox ComboBoxStatNature;
    public Button ButtonStatSearch;
    public Button ButtonClearStats;

    public TextBox[] TextBoxStats = new TextBox[6];

    public TextBox[,] TextBoxIvThresholds = new TextBox[3, 6];

    public CheckBox[] CheckBoxNatures = new CheckBox[Nature.NatureCount];

    public ThemedGroupBox GroupBoxTimer;
    public TimerClock LabelTimer;
    public Button ButtonStart;
    public Button ButtonStop;
    public TextBox TextBoxFrame;
    public ThemedComboBox ComboBoxFps;
    public TextBox TextBoxOffset;

    public TextBox TextBoxVisualOffset;

    public TextBox TextBoxInterval;
    public TextBox TextBoxBeeps;
    public Button ButtonMinus;
    public Button ButtonPlus;

    public ThemedCheckBox CheckBoxBeepEnabled;

    public ThemedCheckBox CheckBoxFlashEnabled;

    public Button ButtonTraining;

    private static readonly string[] StatRowNames = { "HP", "Attack", "Defense", "Sp. Atk", "Sp. Def", "Speed" };

    private static readonly string[] StatColumnNames = { "HP", "Atk", "Def", "SpA", "SpD", "Spe" };

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        SuspendLayout();

        MeasureFieldHeights();

        const int Top = 26;

        const int RightColumnWidth = 402;
        const int RightColumnInner = RightColumnWidth - 12;

        const int CapturePad = 4;

        const int SectionGap = 4;

        const int PairButtonSpan = (202 - SectionGap) / 2;

        const int BoxBottomPad = 6;

        MenuItemHotkeys = new ToolStripMenuItem("Settings…");

        MenuFilters = new ToolStripMenuItem("Filters");

        MenuItemTraining = new ToolStripMenuItem("Training Mode") { CheckOnClick = true };

        MenuItemContextTracking = new ToolStripMenuItem("Context Tracking") { CheckOnClick = true };

        var menuExit = new ToolStripMenuItem("Exit");
        menuExit.Click += (_, _) => Close();
        var menuFile = new ToolStripMenuItem("File");
        menuFile.DropDownItems.Add(MenuFilters);
        menuFile.DropDownItems.Add(MenuItemContextTracking);
        menuFile.DropDownItems.Add(MenuItemHotkeys);
        menuFile.DropDownItems.Add(new ToolStripSeparator());
        menuFile.DropDownItems.Add(menuExit);

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
        var menuHelp = new ToolStripMenuItem("Help");
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
        MenuStripMain.Items.Add(menuHelp);
        MenuStripMain.Items.Add(MenuItemAlwaysOnTop);
        MenuStripMain.Items.Add(MenuItemGlobalHotkeys);

        int ivBoxHeight = _fieldHeight;
        int ivRowPitch = ivBoxHeight - 1;

        const int IvRowsTop = 28;
        int ivClearY = IvRowsTop + 6 * ivRowPitch + 4;
        int ivHeight = ivClearY + 30;

        GroupBoxIvConstraint = new ThemedGroupBox
        {
            Text = "IV Constraints",
            Location = new Point(6, Top),
            Size = new Size(218, ivHeight)
        };

        int[] packX = { 66, 114, 162 };
        string[] packHeaders = { "-", "Neutral", "+" };
        for (int pack = 0; pack < 3; pack++)
        {
            GroupBoxIvConstraint.Controls.Add(new Label
            {
                Text = packHeaders[pack],
                Location = new Point(packX[pack], IvRowsTop - 14),
                Size = new Size(49, 14),
                TextAlign = ContentAlignment.MiddleCenter
            });
        }

        for (int stat = 0; stat < 6; stat++)
        {
            int y = IvRowsTop + stat * ivRowPitch;
            GroupBoxIvConstraint.Controls.Add(MakeLabel(StatRowNames[stat], 8, y, 56));

            if (stat == 0)
            {
                var hp = MakeTextBox(66, y, 145, "0");
                hp.TextAlign = HorizontalAlignment.Center;
                for (int pack = 0; pack < 3; pack++)
                {
                    TextBoxIvThresholds[pack, 0] = hp;
                }
                GroupBoxIvConstraint.Controls.Add(hp);
                continue;
            }

            for (int pack = 0; pack < 3; pack++)
            {
                TextBox box = MakeTextBox(packX[pack], y, 49, "0");
                box.TextAlign = HorizontalAlignment.Center;
                TextBoxIvThresholds[pack, stat] = box;
                GroupBoxIvConstraint.Controls.Add(box);
            }
        }

        ButtonClearIvs = new ThemedButton { Text = "Clear", Location = new Point(8, ivClearY), Size = new Size(203, 22) };
        GroupBoxIvConstraint.Controls.Add(ButtonClearIvs);

        int starterTop = Top + ivHeight + 4;

        GroupBoxStarter = new ThemedGroupBox
        {
            Text = "Starter",
            Location = new Point(6, starterTop),
            Size = new Size(218, 244)
        };

        PictureBoxSprite = new PictureBox
        {
            Location = new Point(77, 14),
            Size = new Size(64, 64),
            BorderStyle = BorderStyle.None,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        GroupBoxStarter.Controls.Add(PictureBoxSprite);

        int starterRow = 84;

        GroupBoxStarter.Controls.Add(MakeLabel("Pokemon", 8, starterRow, 70));
        ComboBoxPokemon = MakeCombo(82, starterRow, 128);
        GroupBoxStarter.Controls.Add(ComboBoxPokemon);

        starterRow += RowPitch;
        GroupBoxStarter.Controls.Add(MakeLabel("Min Frame", 8, starterRow, 70));
        TextBoxMinFrame = MakeTextBox(82, starterRow, 128, "0");
        GroupBoxStarter.Controls.Add(TextBoxMinFrame);

        starterRow += RowPitch;
        GroupBoxStarter.Controls.Add(MakeLabel("Max Frame", 8, starterRow, 70));
        TextBoxMaxFrame = MakeTextBox(82, starterRow, 128, "10000");
        GroupBoxStarter.Controls.Add(TextBoxMaxFrame);

        starterRow += RowPitch;
        GroupBoxStarter.Controls.Add(MakeLabel("Trainer ID", 8, starterRow, 70));
        TextBoxTrainerId = MakeTextBox(82, starterRow, 128, "0");
        TextBoxTrainerId.MaxLength = 5;
        GroupBoxStarter.Controls.Add(TextBoxTrainerId);

        int starterButtonRow = starterRow + RowPitch;
        ButtonCalculateOdds = new ThemedButton
        {
            Text = "Calculate",
            Location = new Point(8, starterButtonRow),
            Size = new Size(PairButtonSpan, _fieldHeight)
        };
        GroupBoxStarter.Controls.Add(ButtonCalculateOdds);

        ButtonSearch = new ThemedButton
        {
            Text = "Search",
            Location = new Point(8 + PairButtonSpan + SectionGap, starterButtonRow),
            Size = new Size(PairButtonSpan, _fieldHeight)
        };
        GroupBoxStarter.Controls.Add(ButtonSearch);

        GroupBoxStarter.Height = ButtonSearch.Bottom + BoxBottomPad;

        int timerTop = GroupBoxStarter.Bottom + SectionGap;

        GroupBoxTimer = new ThemedGroupBox
        {
            Text = "Timer",
            Location = new Point(6, timerTop),
            Size = new Size(218, 300)
        };

        const int ClockWidth = 202;
        const int ClockHeight = 76;
        LabelTimer = new TimerClock
        {
            Text = "0.000",
            Location = new Point(8, 18),
            Size = new Size(ClockWidth, ClockHeight),
            Font = FitFont(Font.FontFamily, "999.999", ClockWidth - 4, ClockHeight, 44F)
        };
        GroupBoxTimer.Controls.Add(LabelTimer);

        int buttonRow = LabelTimer.Bottom + RowGap;
        ButtonStart = new ThemedButton { Text = "Start", Location = new Point(8, buttonRow), Size = new Size(PairButtonSpan, 30), Tag = Theme.StartButtonTag };
        ButtonStop = new ThemedButton { Text = "Stop", Location = new Point(8 + PairButtonSpan + SectionGap, buttonRow), Size = new Size(PairButtonSpan, 30), Tag = Theme.StopButtonTag };
        GroupBoxTimer.Controls.Add(ButtonStart);
        GroupBoxTimer.Controls.Add(ButtonStop);

        const int RowCaptionX = 25;
        const int RowCaptionWidth = 52;
        const int RowFieldX = 80;
        const int RowFieldWidth = 210 - RowFieldX;

        int timerRow = ButtonStart.Bottom + RowGap;
        GroupBoxTimer.Controls.Add(MakeLabel("Frame", RowCaptionX, timerRow, RowCaptionWidth));

        int nudgeSize = _fieldHeight;
        int plusX = 210 - nudgeSize;
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
        GroupBoxTimer.Controls.Add(MakeLabel("Interval", RowCaptionX, timerRow, RowCaptionWidth));
        TextBoxInterval = MakeTextBox(RowFieldX, timerRow, RowFieldWidth, "1000");
        GroupBoxTimer.Controls.Add(TextBoxInterval);

        timerRow += RowPitch;
        GroupBoxTimer.Controls.Add(MakeLabel("Beeps", RowCaptionX, timerRow, RowCaptionWidth));
        TextBoxBeeps = MakeTextBox(RowFieldX, timerRow, RowFieldWidth, "4");
        GroupBoxTimer.Controls.Add(TextBoxBeeps);

        ButtonTraining = new ThemedButton
        {
            Text = "Start Offset Training",
            Location = new Point(8, timerRow + RowPitch),
            Size = new Size(202, 26)
        };
        GroupBoxTimer.Controls.Add(ButtonTraining);

        GroupBoxNatures = new ThemedGroupBox
        {
            Text = "Natures",
            Location = new Point(228, Top),
            Size = new Size(RightColumnWidth, 158)
        };

        List<Nature> natures = Nature.GetList();
        for (int i = 0; i < Nature.NatureCount; i++)
        {
            var box = new ThemedCheckBox
            {
                Text = natures[i].Name,
                Location = new Point(6 + (i % 5) * 78, 18 + (i / 5) * 22),
                Size = new Size(76, 20),
                Checked = true,
                BoldWhenChecked = true
            };
            CheckBoxNatures[i] = box;
            GroupBoxNatures.Controls.Add(box);
        }

        ButtonNaturesAll = new ThemedButton { Text = "Check All", Location = new Point(6, 130), Size = new Size(193, 22) };
        ButtonNaturesNone = new ThemedButton { Text = "Uncheck All", Location = new Point(203, 130), Size = new Size(193, 22) };
        GroupBoxNatures.Controls.Add(ButtonNaturesAll);
        GroupBoxNatures.Controls.Add(ButtonNaturesNone);

        GroupBoxResults = new ThemedGroupBox
        {
            Text = "Found List",
            Location = new Point(228, Top + 162),
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
        ListViewResults.Columns.Add("Frame", 42, HorizontalAlignment.Center);
        ListViewResults.Columns.Add("Time", 52, HorizontalAlignment.Center);
        ListViewResults.Columns.Add("Nature", 56, HorizontalAlignment.Center);
        foreach (string stat in StatColumnNames)
        {
            ListViewResults.Columns.Add(stat, 31, HorizontalAlignment.Center);
        }
        ListViewResults.Columns.Add("M/F", 33, HorizontalAlignment.Center);
        GroupBoxResults.Controls.Add(ListViewResults);

        TrainingPanel = new TrainingPanel
        {
            Location = ListViewResults.Location,
            Size = ListViewResults.Size,
            Visible = false
        };
        GroupBoxResults.Controls.Add(TrainingPanel);

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
            Location = new Point(228, GroupBoxResults.Bottom + SectionGap),
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
            Location = new Point(228, captureTop),
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

        const int MinResultsHeight = 200;
        shortfall = Math.Max(shortfall, MinResultsHeight - ListViewResults.Height);

        GroupBoxResults.Height += shortfall;
        ListViewResults.Height += shortfall;
        TrainingPanel.Height += shortfall;
        LabelLanding.Top += shortfall;
        GroupBoxStatSearch.Top += shortfall;
        GroupBoxCapture.Top += shortfall;

        GroupBoxTimer.Height = Math.Max(timerNeeds, GroupBoxCapture.Bottom - timerTop);

        int contextTop = GroupBoxCapture.Bottom + SectionGap;
        GroupBoxContext = new ThemedGroupBox
        {
            Text = "Context Tracking",
            Location = new Point(6, contextTop),
            Size = new Size(624, 18 + NpcGridPanel.GridPixels + BoxBottomPad),
            Visible = false
        };

        ContextPanel = new NpcGridPanel
        {
            Location = new Point(8, 18),
            Size = new Size(608, NpcGridPanel.GridPixels)
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
            Text = "I'm Slow!",
            Location = new Point(ContextPanel.Right - 124, contextButtonY),
            Size = new Size(124, 24),
            Visible = false
        };
        ButtonContextMiss = new ThemedButton
        {
            Text = "Miss",
            Location = new Point(ContextPanel.Right - 124 - SectionGap - 66, contextButtonY),
            Size = new Size(66, 24)
        };
        ButtonContextAnchor = new ThemedButton
        {
            Text = "Anchor",
            Location = new Point(ContextPanel.Right - 124, contextButtonY),
            Size = new Size(124, 24)
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
        Controls.Add(GroupBoxContext);

        AutoScaleMode = AutoScaleMode.Font;
        _compactClientHeight = GroupBoxCapture.Bottom + 6;
        _trackingClientHeight = GroupBoxContext.Bottom + 6;
        ClientSize = new Size(636, _compactClientHeight);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Controls.Add(GroupBoxIvConstraint);
        Controls.Add(GroupBoxStarter);
        Controls.Add(GroupBoxTimer);
        Controls.Add(GroupBoxNatures);
        Controls.Add(GroupBoxResults);
        Controls.Add(MenuStripMain);
        MainMenuStrip = MenuStripMain;
        Icon = Assets.AppIcon;
        Text = "FRLG Starter Tool";
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
