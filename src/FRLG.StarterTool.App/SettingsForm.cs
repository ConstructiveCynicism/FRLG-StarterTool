using System.Globalization;
using FRLG.StarterTool.Core.Npc;
using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Timing;

namespace FRLG.StarterTool.App;

public sealed class SettingsForm : Form
{
    private const int SectionGap = 18;

    private const int RowGap = 8;

    private const int LeftMargin = 15;

    private const int DesktopMargin = 64;

    private static readonly int[] ZoomPercentages = { 75, 100, 125 };

    private const int VolumeBarWidth = 150;

    private static readonly string[] Captions =
    {
        "Trigger on",
        "Context window (ms)",
        "Cued press (frames)",
        "Cue window (ms)",
        "Beep sound",
        "Volume",
        "Clipboard format",
        "Time format",
        "Stat box labels",
        "Stat box text",
        "Stat box background",
        "Stat box outline",
        "Stat box frame"
    };

    private readonly AppSettings _settings;

    private readonly Dictionary<HotkeyAction, (Button Primary, Button Secondary)> _keyButtons = new();

    private readonly float _zoom;

    private readonly Font? _zoomFont;

    private int Scaled(int length) => _zoom == 1F ? length : ZoomLayout.Round(length * _zoom);

    public bool ReopenForZoom { get; private set; }

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;

        _zoom = Math.Clamp(settings.ZoomPercent, 75, 125) / 100F;
        if (_zoom != 1F)
        {
            _zoomFont = new Font(Font.FontFamily, Font.Size * _zoom, Font.Style, Font.Unit);
            Font = _zoomFont;
        }

        Text = "Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;

        Label hotkeyHeader = AddSectionHeader("Hotkeys", Scaled(12));
        TableLayoutPanel table = AddHotkeyTable(
            HotkeyExtensions.Actions, hotkeyHeader.Bottom + Scaled(6), out Button? firstKeyButton);

        Label contextHeader = AddSectionHeader("Context Tracking", table.Bottom + Scaled(SectionGap));
        TableLayoutPanel contextTable = AddHotkeyTable(
            HotkeyExtensions.ContextActions, contextHeader.Bottom + Scaled(6), out _);
        AlignColumns(table, contextTable);

        int y = contextTable.Bottom + Scaled(SectionGap);
        Label timingHeader = AddSectionHeader("Timing", y);
        y = timingHeader.Bottom + Scaled(RowGap);

        var methodLabel = new Label
        {
            Text = "Trigger on", Location = new Point(Scaled(LeftMargin), y + Scaled(4)), AutoSize = true
        };

        int keyColumnX = firstKeyButton == null ? Scaled(90) : table.Left + firstKeyButton.Left;
        int widestCaption = Captions.Max(text => TextRenderer.MeasureText(text, Font).Width);
        int comboX = Math.Max(keyColumnX, Scaled(LeftMargin) + widestCaption + Scaled(12));

        int comboWidth = Math.Max(firstKeyButton?.Width ?? Scaled(110), Scaled(VolumeBarWidth));

        var methodBox = new ThemedComboBox
        {
            Location = new Point(comboX, y),
            Size = new Size(comboWidth, Scaled(23)),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        foreach (KeyMethod method in Enum.GetValues<KeyMethod>())
        {
            methodBox.Items.Add(method.ToFormattedString());
        }
        methodBox.SelectedIndex = (int)_settings.KeyMethod;
        methodBox.SelectedIndexChanged += (_, _) => _settings.KeyMethod = (KeyMethod)methodBox.SelectedIndex;
        Controls.Add(methodLabel);
        Controls.Add(methodBox);

        int contextY = methodBox.Bottom + Scaled(RowGap + 4);
        Controls.Add(new Label
        {
            Text = "Context window (ms)",
            Location = new Point(Scaled(LeftMargin), contextY + Scaled(4)),
            AutoSize = true
        });
        var contextBox = new ThemedTextBox
        {
            Location = new Point(comboX, contextY),
            Width = comboWidth,
            Text = _settings.NpcContextWindowMs.ToString("0.###", CultureInfo.InvariantCulture)
        };
        contextBox.Leave += (_, _) =>
        {
            if (double.TryParse(contextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out double ms) && ms >= 0.0)
            {
                _settings.NpcContextWindowMs = Math.Min(ms, 1000.0);
            }

            contextBox.Text = _settings.NpcContextWindowMs.ToString("0.###", CultureInfo.InvariantCulture);
        };
        Controls.Add(contextBox);

        var cuedPress = new ThemedCheckBox
        {
            Text = "Cue the lab press",
            Location = new Point(Scaled(LeftMargin), contextBox.Bottom + Scaled(RowGap + 2)),
            AutoSize = true,
            Checked = _settings.NpcCuedLabPress
        };
        Controls.Add(cuedPress);

        int cueFramesY = cuedPress.Bottom + Scaled(RowGap + 4);
        Controls.Add(new Label
        {
            Text = "Cued press (+frames)",
            Location = new Point(Scaled(LeftMargin), cueFramesY + Scaled(4)),
            AutoSize = true
        });
        var cueFramesBox = new ThemedTextBox
        {
            Location = new Point(comboX, cueFramesY),
            Width = comboWidth,
            Text = _settings.NpcCuedLabPressOffsetFrames.ToString(CultureInfo.InvariantCulture)
        };
        cueFramesBox.Leave += (_, _) =>
        {
            if (int.TryParse(cueFramesBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int frames) && frames >= 0)
            {
                _settings.NpcCuedLabPressOffsetFrames = Math.Clamp(frames, 0, 6000);
            }

            cueFramesBox.Text =
                _settings.NpcCuedLabPressOffsetFrames.ToString(CultureInfo.InvariantCulture);
        };
        Controls.Add(cueFramesBox);

        int cueWindowY = cueFramesBox.Bottom + Scaled(RowGap + 4);
        Controls.Add(new Label
        {
            Text = "Cue window (ms)",
            Location = new Point(Scaled(LeftMargin), cueWindowY + Scaled(4)),
            AutoSize = true
        });
        var cueWindowBox = new ThemedTextBox
        {
            Location = new Point(comboX, cueWindowY),
            Width = comboWidth,
            Text = _settings.NpcCuedPressWindowMs.ToString("0.###", CultureInfo.InvariantCulture)
        };
        cueWindowBox.Leave += (_, _) =>
        {
            if (double.TryParse(cueWindowBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out double ms) && ms >= 0.0)
            {
                _settings.NpcCuedPressWindowMs = Math.Min(ms, 1000.0);
            }

            cueWindowBox.Text = _settings.NpcCuedPressWindowMs.ToString("0.###", CultureInfo.InvariantCulture);
        };
        Controls.Add(cueWindowBox);

        cueFramesBox.Enabled = cuedPress.Checked;
        cueWindowBox.Enabled = cuedPress.Checked;
        cuedPress.CheckedChanged += (_, _) =>
        {
            _settings.NpcCuedLabPress = cuedPress.Checked;
            cueFramesBox.Enabled = cuedPress.Checked;
            cueWindowBox.Enabled = cuedPress.Checked;
        };

        y = cueWindowBox.Bottom + Scaled(SectionGap);
        Label audioHeader = AddSectionHeader("Audio", y);
        y = audioHeader.Bottom + Scaled(RowGap);

        var soundLabel = new Label
        {
            Text = "Beep sound", Location = new Point(Scaled(LeftMargin), y + Scaled(4)), AutoSize = true
        };
        var soundBox = new ThemedComboBox
        {
            Location = new Point(comboX, y),
            Size = new Size(comboWidth, Scaled(23)),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        foreach (string name in BeepSounds.Names)
        {
            soundBox.Items.Add(BeepSounds.DisplayName(name));
        }
        int soundIndex = Array.FindIndex(
            BeepSounds.Names,
            n => string.Equals(n, StarterTool.Beeps.Sound, StringComparison.OrdinalIgnoreCase));
        soundBox.SelectedIndex = soundIndex >= 0 ? soundIndex : 0;
        soundBox.SelectedIndexChanged += (_, _) =>
        {
            string name = BeepSounds.Names[soundBox.SelectedIndex];
            _settings.BeepSound = name;
            StarterTool.VariableOffset.ChangeBeepSound(name);
        };
        Controls.Add(soundLabel);
        Controls.Add(soundBox);

        var volumeLabel = new Label
        {
            Text = "Volume",
            Location = new Point(Scaled(LeftMargin), soundBox.Bottom + Scaled(14)),
            AutoSize = true
        };
        var volumeBar = new TrackBar
        {
            Location = new Point(comboX, soundBox.Bottom + Scaled(8)),
            AutoSize = false,
            Size = new Size(comboWidth, Scaled(40)),
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Value = Math.Clamp(_settings.Volume, 0, 100)
        };
        volumeBar.ValueChanged += (_, _) =>
        {
            _settings.Volume = volumeBar.Value;
            StarterTool.VariableOffset.ChangeVolume(volumeBar.Value);
        };
        Controls.Add(volumeLabel);
        Controls.Add(volumeBar);

        y = volumeBar.Bottom + Scaled(SectionGap);
        Label inputHeader = AddSectionHeader("Input", y);
        y = inputHeader.Bottom + Scaled(RowGap);

        var globalEntry = new ThemedCheckBox
        {
            Text = "Global entry input",
            Location = new Point(Scaled(LeftMargin), y),
            AutoSize = true,
            Checked = _settings.GlobalNumpadInput
        };
        globalEntry.CheckedChanged += (_, _) => _settings.GlobalNumpadInput = globalEntry.Checked;
        Controls.Add(globalEntry);

        int clipboardY = globalEntry.Bottom + Scaled(RowGap + 4);
        Controls.Add(new Label
        {
            Text = "Clipboard format",
            Location = new Point(Scaled(LeftMargin), clipboardY + Scaled(4)),
            AutoSize = true
        });
        var clipboardBox = new ThemedComboBox
        {
            Location = new Point(comboX, clipboardY),
            Size = new Size(comboWidth, Scaled(23)),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        clipboardBox.Items.Add("Column (one per line)");
        clipboardBox.Items.Add("Row (tab separated)");
        clipboardBox.SelectedIndex = (int)_settings.ClipboardFormat;
        clipboardBox.SelectedIndexChanged += (_, _) =>
            _settings.ClipboardFormat = (ClipboardFormat)clipboardBox.SelectedIndex;
        Controls.Add(clipboardBox);

        y = clipboardBox.Bottom + Scaled(SectionGap);
        Label appearanceHeader = AddSectionHeader("Appearance", y);
        y = appearanceHeader.Bottom + Scaled(RowGap);

        var darkMode = new ThemedCheckBox
        {
            Text = "Dark mode",
            Location = new Point(Scaled(LeftMargin), y),
            AutoSize = true,
            Checked = _settings.DarkMode
        };
        darkMode.CheckedChanged += (_, _) =>
        {
            _settings.DarkMode = darkMode.Checked;
            StarterTool.ApplyTheme();

            foreach ((HotkeyAction action, _) in HotkeyExtensions.AllActions)
            {
                RefreshKeyButtons(action);
            }
        };
        Controls.Add(darkMode);

        var hideConstraints = new ThemedCheckBox
        {
            Text = "Hide constraints",
            Location = new Point(Scaled(LeftMargin), darkMode.Bottom + Scaled(RowGap)),
            AutoSize = true,
            Checked = _settings.HideConstraints
        };
        hideConstraints.CheckedChanged += (_, _) =>
        {
            _settings.HideConstraints = hideConstraints.Checked;
            StarterTool.MainForm.SetHideConstraints(hideConstraints.Checked);
        };
        Controls.Add(hideConstraints);

        int zoomY = hideConstraints.Bottom + Scaled(RowGap + 4);
        Controls.Add(new Label
        {
            Text = "Window zoom",
            Location = new Point(Scaled(LeftMargin), zoomY + Scaled(4)),
            AutoSize = true
        });
        var zoomBox = new ThemedComboBox
        {
            Location = new Point(comboX, zoomY),
            Size = new Size(comboWidth, Scaled(23)),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        foreach (int percent in ZoomPercentages) zoomBox.Items.Add(percent + "%");
        zoomBox.SelectedIndex = NearestZoomIndex(_settings.ZoomPercent);
        zoomBox.SelectedIndexChanged += (_, _) =>
        {
            _settings.ZoomPercent = ZoomPercentages[zoomBox.SelectedIndex];
            StarterTool.MainForm.ApplyZoom(_settings.ZoomPercent);

            ReopenForZoom = true;
            BeginInvoke(Close);
        };
        Controls.Add(zoomBox);

        int timeFormatY = zoomBox.Bottom + Scaled(RowGap + 4);
        Controls.Add(new Label
        {
            Text = "Time format",
            Location = new Point(Scaled(LeftMargin), timeFormatY + Scaled(4)),
            AutoSize = true
        });
        var timeFormatBox = new ThemedComboBox
        {
            Location = new Point(comboX, timeFormatY),
            Size = new Size(comboWidth, Scaled(23)),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        timeFormatBox.Items.Add("SSS.mmm");
        timeFormatBox.Items.Add("M:SS.mmm");
        timeFormatBox.SelectedIndex = (int)_settings.TimeFormat;
        timeFormatBox.SelectedIndexChanged += (_, _) =>
        {
            _settings.TimeFormat = (TimeFormat)timeFormatBox.SelectedIndex;
            StarterTool.MainForm.ApplyTimeFormat();
        };
        Controls.Add(timeFormatBox);

        Panel labelSwatch = AddColorRow(
            "Stat box labels", timeFormatBox.Bottom + Scaled(10), comboX, comboWidth,
            () => StatBoxPanel.LabelColor,
            colour =>
            {
                StatBoxPanel.LabelColor = colour;
                _settings.StatBoxLabelColor = StatBoxPanel.ToHex(colour);
            });

        Panel valueSwatch = AddColorRow(
            "Stat box text", labelSwatch.Bottom + Scaled(8), comboX, comboWidth,
            () => StatBoxPanel.ValueColor,
            colour =>
            {
                StatBoxPanel.ValueColor = colour;
                _settings.StatBoxValueColor = StatBoxPanel.ToHex(colour);
            });

        Panel fillSwatch = AddColorRow(
            "Stat box background", valueSwatch.Bottom + Scaled(8), comboX, comboWidth,
            () => StatBoxPanel.FillColor,
            colour =>
            {
                StatBoxPanel.FillColor = colour;
                _settings.StatBoxFillColor = StatBoxPanel.ToHex(colour);
            });

        Panel outlineSwatch = AddColorRow(
            "Stat box outline", fillSwatch.Bottom + Scaled(8), comboX, comboWidth,
            () => StatBoxPanel.OutlineColor,
            colour =>
            {
                StatBoxPanel.OutlineColor = colour;
                _settings.StatBoxOutlineColor = StatBoxPanel.ToHex(colour);
            });

        Panel frameSwatch = AddColorRow(
            "Stat box frame", outlineSwatch.Bottom + Scaled(8), comboX, comboWidth,
            () => StatBoxPanel.FrameColor,
            colour =>
            {
                StatBoxPanel.FrameColor = colour;
                _settings.StatBoxFrameColor = StatBoxPanel.ToHex(colour);
            });

        var labDashes = new ThemedCheckBox
        {
            Text = "Lab delay timings",
            Location = new Point(Scaled(LeftMargin), frameSwatch.Bottom + Scaled(RowGap + 4)),
            AutoSize = true,
            Checked = _settings.ShowLabDelayDashes
        };
        labDashes.CheckedChanged += (_, _) =>
        {
            _settings.ShowLabDelayDashes = labDashes.Checked;
            StarterTool.MainForm.ContextPanel.ShowDelayDashes = labDashes.Checked;
        };
        Controls.Add(labDashes);

        var runTips = new ThemedCheckBox
        {
            Text = "Run tips",
            Location = new Point(Scaled(LeftMargin), labDashes.Bottom + Scaled(RowGap)),
            AutoSize = true,
            Checked = _settings.ShowRunTips
        };
        runTips.CheckedChanged += (_, _) =>
        {
            _settings.ShowRunTips = runTips.Checked;
            StarterTool.MainForm.ContextPanel.ShowTips = runTips.Checked;
        };
        Controls.Add(runTips);

        y = runTips.Bottom + Scaled(SectionGap);
        Label experimentalHeader = AddSectionHeader("Experimental", y);
        y = experimentalHeader.Bottom + Scaled(RowGap);

        Controls.Add(new Label
        {
            Text = "Fence guy parity",
            Location = new Point(Scaled(LeftMargin), y + Scaled(4)),
            AutoSize = true
        });
        var parityBox = new ThemedComboBox
        {
            Location = new Point(comboX, y),
            Size = new Size(comboWidth, Scaled(23)),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        parityBox.Items.Add("Post (original model)");
        parityBox.Items.Add("Pre");
        parityBox.Items.Add("Both (two hypotheses)");
        parityBox.SelectedIndex = (int)_settings.FenceGuyParity;
        parityBox.SelectedIndexChanged += (_, _) =>
            _settings.FenceGuyParity = (FenceGuyParity)parityBox.SelectedIndex;
        Controls.Add(parityBox);

        int contentRight = Math.Max(
            Math.Max(Math.Max(table.Right, contextTable.Right), methodBox.Right),
            volumeBar.Right);

        var close = new ThemedButton
        {
            Text = "Close", Size = new Size(Scaled(80), Scaled(28)), DialogResult = DialogResult.OK
        };
        close.Location = new Point(contentRight - close.Width, parityBox.Bottom + Scaled(SectionGap));
        Controls.Add(close);
        AcceptButton = close;

        FormClosing += (_, _) => ActiveControl = null;

        ClientSize = new Size(contentRight + Scaled(12), close.Bottom + Scaled(12));
        FitToDesktop();

        Theme.Apply(this);
    }

    private void FitToDesktop()
    {
        Form? main = StarterTool.MainForm;
        Screen screen = main != null
            ? Screen.FromControl(main)
            : Screen.PrimaryScreen ?? Screen.AllScreens[0];

        int chrome = Height - ClientSize.Height;
        int roof = screen.WorkingArea.Height - chrome - DesktopMargin;
        int target = main != null ? Math.Min(main.Height - chrome, roof) : roof;
        if (target <= 0 || ClientSize.Height == target) return;

        bool scrolls = ClientSize.Height > target;
        if (scrolls) AutoScroll = true;
        ClientSize = new Size(
            ClientSize.Width + (scrolls ? SystemInformation.VerticalScrollBarWidth : 0), target);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        Rectangle desktop = Screen.FromControl(this).WorkingArea;
        Location = new Point(
            Math.Clamp(Left, desktop.Left, Math.Max(desktop.Left, desktop.Right - Width)),
            Math.Clamp(Top, desktop.Top, Math.Max(desktop.Top, desktop.Bottom - Height)));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _zoomFont?.Dispose();
    }

    private static int NearestZoomIndex(int percent)
    {
        int best = 0;
        for (int i = 1; i < ZoomPercentages.Length; i++)
        {
            if (Math.Abs(ZoomPercentages[i] - percent) < Math.Abs(ZoomPercentages[best] - percent))
            {
                best = i;
            }
        }

        return best;
    }

    private TableLayoutPanel AddHotkeyTable((HotkeyAction Action, string Label)[] actions, int y,
        out Button? firstKeyButton)
    {
        var table = new TableLayoutPanel
        {
            Location = new Point(Scaled(12), y),
            AutoSize = true,
            ColumnCount = 5,
            RowCount = actions.Length + 1
        };

        foreach (string header in new[] { "Action", "Key", "Alt. key", "", "Global" })
        {
            table.Controls.Add(new Label
            {
                Text = header,
                AutoSize = true,
                Anchor = header == "Global" ? AnchorStyles.None : AnchorStyles.Left,
                Margin = new Padding(Scaled(3), Scaled(6), Scaled(3), Scaled(3))
            });
        }

        firstKeyButton = null;

        foreach ((HotkeyAction action, string label) in actions)
        {
            Hotkey hotkey = _settings.GetHotkey(action);

            table.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Margin = new Padding(Scaled(3), Scaled(8), Scaled(12), Scaled(3))
            });

            Button primary = MakeKeyButton(action, secondary: false);
            Button secondary = MakeKeyButton(action, secondary: true);
            firstKeyButton ??= primary;
            _keyButtons[action] = (primary, secondary);
            table.Controls.Add(primary);
            table.Controls.Add(secondary);

            var clear = new ThemedButton
            {
                Text = "Clear",
                Size = new Size(Scaled(56), Scaled(25)),
                Margin = new Padding(Scaled(3), Scaled(3), Scaled(12), Scaled(3))
            };
            clear.Click += (_, _) =>
            {
                hotkey.ClearOne();
                RefreshKeyButtons(action);
            };
            table.Controls.Add(clear);

            var global = new ThemedCheckBox
            {
                Checked = hotkey.Global,
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Margin = new Padding(Scaled(3), Scaled(7), Scaled(3), Scaled(3))
            };
            global.CheckedChanged += (_, _) => hotkey.Global = global.Checked;
            table.Controls.Add(global);

            RefreshKeyButtons(action);
        }

        Controls.Add(table);
        table.PerformLayout();
        return table;
    }

    private static void AlignColumns(TableLayoutPanel first, TableLayoutPanel second)
    {
        int[] firstWidths = first.GetColumnWidths();
        int[] secondWidths = second.GetColumnWidths();

        foreach (TableLayoutPanel table in new[] { first, second })
        {
            table.ColumnStyles.Clear();
            for (int column = 0; column < firstWidths.Length; column++)
            {
                table.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Absolute, Math.Max(firstWidths[column], secondWidths[column])));
            }
            table.PerformLayout();
        }
    }

    private Label AddSectionHeader(string text, int y)
    {
        var header = new Label
        {
            Text = text,
            Location = new Point(Scaled(12), y),
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Tag = Theme.SectionHeader
        };
        Controls.Add(header);
        return header;
    }

    private Panel AddColorRow(string caption, int y, int x, int width, Func<Color> read, Action<Color> write)
    {
        Controls.Add(new Label
        {
            Text = caption, Location = new Point(Scaled(LeftMargin), y + Scaled(4)), AutoSize = true
        });

        var swatch = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(width, Scaled(23)),
            BackColor = read(),
            Cursor = Cursors.Hand,
            Tag = Theme.KeepBackColor
        };
        swatch.Paint += (_, paint) =>
        {
            using var pen = new Pen(Theme.Border);
            paint.Graphics.DrawRectangle(pen, 0, 0, swatch.Width - 1, swatch.Height - 1);
        };
        swatch.Click += (_, _) =>
        {
            using var picker = new ColorDialog
            {
                Color = read(),
                FullOpen = true,
                CustomColors = new[] { ColorTranslator.ToOle(read()) }
            };
            if (picker.ShowDialog(this) != DialogResult.OK) return;

            write(picker.Color);
            swatch.BackColor = picker.Color;
        };
        Controls.Add(swatch);
        return swatch;
    }

    private Button MakeKeyButton(HotkeyAction action, bool secondary)
    {
        var button = new ThemedButton
        {
            Size = new Size(Scaled(110), Scaled(25)),
            Margin = new Padding(Scaled(3)),
            Tag = Theme.KeepForeColor
        };
        button.Click += (_, _) =>
        {
            using var selection = new HotkeySelection();
            if (selection.ShowDialog(this) != DialogResult.OK) return;

            Hotkey hotkey = _settings.GetHotkey(action);
            if (secondary)
            {
                hotkey.Secondary = (int)selection.Key;
            }
            else
            {
                hotkey.Primary = (int)selection.Key;
            }
            RefreshKeyButtons(action);
        };
        return button;
    }

    private void RefreshKeyButtons(HotkeyAction action)
    {
        Hotkey hotkey = _settings.GetHotkey(action);
        (Button primary, Button secondary) = _keyButtons[action];
        Apply(primary, (Keys)hotkey.Primary);
        Apply(secondary, (Keys)hotkey.Secondary);

        static void Apply(Button button, Keys key)
        {
            button.Text = key.ToFormattedString();
            button.ForeColor = key == Keys.None ? Theme.DimText : Theme.Text;
        }
    }
}
