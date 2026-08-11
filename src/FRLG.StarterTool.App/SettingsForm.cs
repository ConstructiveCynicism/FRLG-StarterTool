using System.Globalization;
using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Timing;

namespace FRLG.StarterTool.App;

public sealed class SettingsForm : Form
{
    private const int SectionGap = 18;

    private const int RowGap = 8;

    private const int LeftMargin = 15;

    private const int VolumeBarWidth = 150;

    private static readonly string[] Captions =
    {
        "Trigger on",
        "Context window (ms)",
        "Beep sound",
        "Volume",
        "Clipboard format",
        "Stat box labels",
        "Stat box background"
    };

    private readonly AppSettings _settings;

    private readonly Dictionary<HotkeyAction, (Button Primary, Button Secondary)> _keyButtons = new();

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;

        Text = "Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;

        Label hotkeyHeader = AddSectionHeader("Hotkeys", 12);
        TableLayoutPanel table = AddHotkeyTable(
            HotkeyExtensions.Actions, hotkeyHeader.Bottom + 6, out Button? firstKeyButton);

        Label contextHeader = AddSectionHeader("Context Tracking", table.Bottom + SectionGap);
        TableLayoutPanel contextTable = AddHotkeyTable(
            HotkeyExtensions.ContextActions, contextHeader.Bottom + 6, out _);

        int y = contextTable.Bottom + SectionGap;
        Label timingHeader = AddSectionHeader("Timing", y);
        y = timingHeader.Bottom + RowGap;

        var methodLabel = new Label { Text = "Trigger on", Location = new Point(LeftMargin, y + 4), AutoSize = true };

        int keyColumnX = firstKeyButton == null ? 90 : table.Left + firstKeyButton.Left;
        int widestCaption = Captions.Max(text => TextRenderer.MeasureText(text, Font).Width);
        int comboX = Math.Max(keyColumnX, LeftMargin + widestCaption + 12);

        int comboWidth = Math.Max(firstKeyButton?.Width ?? 110, VolumeBarWidth);

        var methodBox = new ThemedComboBox
        {
            Location = new Point(comboX, y),
            Size = new Size(comboWidth, 23),
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

        int contextY = methodBox.Bottom + RowGap + 4;
        Controls.Add(new Label
        {
            Text = "Context window (ms)",
            Location = new Point(LeftMargin, contextY + 4),
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

        y = contextBox.Bottom + SectionGap;
        Label audioHeader = AddSectionHeader("Audio", y);
        y = audioHeader.Bottom + RowGap;

        var soundLabel = new Label { Text = "Beep sound", Location = new Point(LeftMargin, y + 4), AutoSize = true };
        var soundBox = new ThemedComboBox
        {
            Location = new Point(comboX, y),
            Size = new Size(comboWidth, 23),
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

        var volumeLabel = new Label { Text = "Volume", Location = new Point(LeftMargin, soundBox.Bottom + 14), AutoSize = true };
        var volumeBar = new TrackBar
        {
            Location = new Point(comboX, soundBox.Bottom + 8),
            AutoSize = false,
            Size = new Size(comboWidth, 40),
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

        y = volumeBar.Bottom + SectionGap;
        Label inputHeader = AddSectionHeader("Input", y);
        y = inputHeader.Bottom + RowGap;

        var globalEntry = new ThemedCheckBox
        {
            Text = "Global entry input",
            Location = new Point(LeftMargin, y),
            AutoSize = true,
            Checked = _settings.GlobalNumpadInput
        };
        globalEntry.CheckedChanged += (_, _) => _settings.GlobalNumpadInput = globalEntry.Checked;
        Controls.Add(globalEntry);

        int clipboardY = globalEntry.Bottom + RowGap + 4;
        Controls.Add(new Label
        {
            Text = "Clipboard format",
            Location = new Point(LeftMargin, clipboardY + 4),
            AutoSize = true
        });
        var clipboardBox = new ThemedComboBox
        {
            Location = new Point(comboX, clipboardY),
            Size = new Size(comboWidth, 23),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        clipboardBox.Items.Add("Column (one per line)");
        clipboardBox.Items.Add("Row (tab separated)");
        clipboardBox.SelectedIndex = (int)_settings.ClipboardFormat;
        clipboardBox.SelectedIndexChanged += (_, _) =>
            _settings.ClipboardFormat = (ClipboardFormat)clipboardBox.SelectedIndex;
        Controls.Add(clipboardBox);

        y = clipboardBox.Bottom + SectionGap;
        Label appearanceHeader = AddSectionHeader("Appearance", y);
        y = appearanceHeader.Bottom + RowGap;

        var darkMode = new ThemedCheckBox
        {
            Text = "Dark mode",
            Location = new Point(LeftMargin, y),
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

        Panel labelSwatch = AddColorRow(
            "Stat box labels", darkMode.Bottom + 10, comboX, comboWidth,
            () => StatBoxPanel.LabelColor,
            colour =>
            {
                StatBoxPanel.LabelColor = colour;
                _settings.StatBoxLabelColor = StatBoxPanel.ToHex(colour);
            });

        Panel fillSwatch = AddColorRow(
            "Stat box background", labelSwatch.Bottom + 8, comboX, comboWidth,
            () => StatBoxPanel.FillColor,
            colour =>
            {
                StatBoxPanel.FillColor = colour;
                _settings.StatBoxFillColor = StatBoxPanel.ToHex(colour);
            });

        int contentRight = Math.Max(
            Math.Max(Math.Max(table.Right, contextTable.Right), methodBox.Right),
            volumeBar.Right);

        var close = new ThemedButton { Text = "Close", Size = new Size(80, 28), DialogResult = DialogResult.OK };
        close.Location = new Point(contentRight - close.Width, fillSwatch.Bottom + SectionGap);
        Controls.Add(close);
        AcceptButton = close;

        ClientSize = new Size(contentRight + 12, close.Bottom + 12);

        Theme.Apply(this);
    }

    private TableLayoutPanel AddHotkeyTable((HotkeyAction Action, string Label)[] actions, int y,
        out Button? firstKeyButton)
    {
        var table = new TableLayoutPanel
        {
            Location = new Point(12, y),
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
                Margin = new Padding(3, 6, 3, 3)
            });
        }

        firstKeyButton = null;

        foreach ((HotkeyAction action, string label) in actions)
        {
            Hotkey hotkey = _settings.GetHotkey(action);

            table.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 8, 12, 3) });

            Button primary = MakeKeyButton(action, secondary: false);
            Button secondary = MakeKeyButton(action, secondary: true);
            firstKeyButton ??= primary;
            _keyButtons[action] = (primary, secondary);
            table.Controls.Add(primary);
            table.Controls.Add(secondary);

            var clear = new ThemedButton { Text = "Clear", Size = new Size(56, 25), Margin = new Padding(3, 3, 12, 3) };
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
                Margin = new Padding(3, 7, 3, 3)
            };
            global.CheckedChanged += (_, _) => hotkey.Global = global.Checked;
            table.Controls.Add(global);

            RefreshKeyButtons(action);
        }

        Controls.Add(table);
        table.PerformLayout();
        return table;
    }

    private Label AddSectionHeader(string text, int y)
    {
        var header = new Label
        {
            Text = text,
            Location = new Point(12, y),
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Tag = Theme.SectionHeader
        };
        Controls.Add(header);
        return header;
    }

    private Panel AddColorRow(string caption, int y, int x, int width, Func<Color> read, Action<Color> write)
    {
        Controls.Add(new Label { Text = caption, Location = new Point(LeftMargin, y + 4), AutoSize = true });

        var swatch = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(width, 23),
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
        var button = new ThemedButton { Size = new Size(110, 25), Margin = new Padding(3), Tag = Theme.KeepForeColor };
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
