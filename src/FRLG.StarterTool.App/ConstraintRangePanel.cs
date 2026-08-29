using System.Globalization;
using FRLG.StarterTool.Core.Pokemon;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public sealed class ConstraintRangePanel : Panel
{
    private const float DesignerFontPoints = 9F;

    private readonly Label _grip;
    private readonly ThemedTextBox _name;
    private readonly Panel _colour;
    private readonly ThemedCheckBox _backup;
    private readonly Label _framesCaption;
    private readonly ThemedTextBox _minFrame;
    private readonly Label _frameDash;
    private readonly ThemedTextBox _maxFrame;
    private readonly ThemedButton _odds;
    private readonly ThemedButton _close;

    private Rectangle _headerRule;

    private Rectangle _columnRule;
    private Rectangle _buttonRule;

    private readonly Label[] _packHeaders = new Label[3];
    private readonly Label[] _statLabels = new Label[6];
    private readonly TextBox[,] _thresholds = new TextBox[3, 6];
    private readonly ThemedButton _clearIvs;

    private readonly ThemedCheckBox[] _natures = new ThemedCheckBox[Nature.NatureCount];
    private readonly ThemedButton _naturesAll;
    private readonly ThemedButton _naturesNone;

    private bool _writing;

    public ConstraintRangePanel(ConstraintRange range)
    {
        Range = range;

        SetStyle(ControlStyles.ResizeRedraw, true);

        _grip = new Label
        {
            Text = "⠿",
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.SizeNS,
            Tag = Theme.KeepForeColor,
            ForeColor = Theme.DimText
        };

        _name = MakeBox(numeric: false);
        _name.MaxLength = 24;
        _name.TextChanged += (_, _) => Raise(NameChanged);

        _colour = new Panel
        {
            Cursor = Cursors.Hand,
            BorderStyle = BorderStyle.FixedSingle,
            Tag = Theme.KeepBackColor
        };
        _colour.Click += (_, _) => PickColour();

        _backup = new ThemedCheckBox { Text = "Backup", DimWhenUnchecked = true };
        _backup.CheckedChanged += (_, _) => Raise(BackupChanged);

        _framesCaption = MakeCaption("Frames");
        _minFrame = MakeBox();
        _minFrame.MaxLength = 6;
        _minFrame.TextAlign = HorizontalAlignment.Center;
        _frameDash = MakeCaption("");
        _frameDash.TextAlign = ContentAlignment.MiddleCenter;
        _frameDash.Paint += DrawDash;
        _maxFrame = MakeBox();
        _maxFrame.MaxLength = 6;
        _maxFrame.TextAlign = HorizontalAlignment.Center;

        _odds = new ThemedButton { Text = OddsCaption };
        _odds.Click += (_, _) => CalculateRequested?.Invoke(this, EventArgs.Empty);

        _close = new ThemedButton
        {
            Glyph = ThemedButton.CrossGlyph,
            GlyphColor = Color.White,
            Tag = Theme.StopButtonTag,
        };
        _close.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);

        Controls.AddRange(new Control[]
        {
            _grip, _name, _colour, _backup, _framesCaption, _minFrame, _frameDash, _maxFrame,
            _odds, _close
        });

        string[] packHeaders = { "-", "Neutral", "+" };
        for (int pack = 0; pack < 3; pack++)
        {
            _packHeaders[pack] = new Label
            {
                Text = packHeaders[pack],
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(_packHeaders[pack]);
        }

        for (int stat = 0; stat < 6; stat++)
        {
            _statLabels[stat] = MakeCaption(StatRowNames[stat]);
            Controls.Add(_statLabels[stat]);

            if (stat == 0)
            {
                ThemedTextBox hp = MakeBox();
                hp.TextAlign = HorizontalAlignment.Center;
                for (int pack = 0; pack < 3; pack++) _thresholds[pack, 0] = hp;
                Controls.Add(hp);
                continue;
            }

            for (int pack = 0; pack < 3; pack++)
            {
                ThemedTextBox box = MakeBox();
                box.TextAlign = HorizontalAlignment.Center;
                box.MaxLength = 2;
                _thresholds[pack, stat] = box;
                Controls.Add(box);
            }
        }

        _clearIvs = new ThemedButton { Text = "Clear" };
        _clearIvs.Click += (_, _) => ClearThresholds();
        Controls.Add(_clearIvs);

        List<Nature> natures = Nature.GetList();
        for (int i = 0; i < Nature.NatureCount; i++)
        {
            var box = new ThemedCheckBox
            {
                Text = natures[i].Name,
                Checked = true,
                BoldWhenChecked = true
            };
            _natures[i] = box;
            Controls.Add(box);
        }

        _naturesAll = new ThemedButton { Text = "Check All" };
        _naturesAll.Click += (_, _) => SetAllNatures(true);
        _naturesNone = new ThemedButton { Text = "Uncheck All" };
        _naturesNone.Click += (_, _) => SetAllNatures(false);
        Controls.Add(_naturesAll);
        Controls.Add(_naturesNone);

        foreach (Control input in SearchInputs())
        {
            input.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;

                SearchRequested?.Invoke(this, EventArgs.Empty);
                e.SuppressKeyPress = true;
            };
        }

        Write(range);
        Layout1();
    }

    private static readonly string[] StatRowNames = { "HP", "Attack", "Defense", "Sp. Atk", "Sp. Def", "Speed" };

    public const string OddsCaption = "Calculate";

    public ConstraintRange Range { get; }

    public Control Grip => _grip;

    public event EventHandler? RemoveRequested;
    public event EventHandler? SearchRequested;
    public event EventHandler? CalculateRequested;

    public event EventHandler? NameChanged;

    public event EventHandler? ColorChanged;

    public event EventHandler? BackupChanged;

    public Color? PaletteColor { get; set; }

    public Color? RowColor =>
        Range.Color == ConstraintRange.Screen ? null
        : Range.Color == ConstraintRange.Unset ? PaletteColor
        : Color.FromArgb(Range.Color | unchecked((int)0xFF000000));

    public Color SwatchColor => RowColor ?? Theme.RowPrimary;

    public void ShowOdds(string? text)
    {
        _odds.Enabled = true;
        _odds.Text = string.IsNullOrEmpty(text) ? OddsCaption : text;
    }

    public void ShowOddsBusy()
    {
        _odds.Enabled = false;
        _odds.Text = "Calculating...";
    }

    public void Write(ConstraintRange range)
    {
        _writing = true;
        try
        {
            _name.Text = range.Name;
            _minFrame.Text = range.MinFrame;
            _maxFrame.Text = range.MaxFrame;
            _backup.Checked = range.Backup;

            for (int i = 0; i < _natures.Length && i < range.Natures.Length; i++)
            {
                _natures[i].Checked = range.Natures[i];
            }

            WritePack(0, range.IvMinus);
            WritePack(1, range.IvNeutral);
            WritePack(2, range.IvPlus);
        }
        finally
        {
            _writing = false;
        }

        RefreshSwatch();
    }

    public ConstraintRange Read()
    {
        Range.Name = _name.Text.Trim();
        Range.MinFrame = _minFrame.Text.Trim();
        Range.MaxFrame = _maxFrame.Text.Trim();
        Range.Backup = _backup.Checked;

        var natures = new bool[Nature.NatureCount];
        for (int i = 0; i < _natures.Length && i < natures.Length; i++) natures[i] = _natures[i].Checked;
        Range.Natures = natures;

        Range.IvMinus = ReadPack(0);
        Range.IvNeutral = ReadPack(1);
        Range.IvPlus = ReadPack(2);

        return Range;
    }

    public void Relayout() => Layout1();

    public void SetRemovable(bool removable) => _close.Enabled = removable;

    public void RefreshSwatch() => _colour.BackColor = SwatchColor;

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Layout1();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var pen = new Pen(Theme.Border);
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        e.Graphics.DrawRectangle(pen, bounds);

        using (var rule = new SolidBrush(Theme.GridLine))
        {
            e.Graphics.FillRectangle(rule, _headerRule);
            e.Graphics.FillRectangle(rule, _columnRule);
            e.Graphics.FillRectangle(rule, _buttonRule);
        }

        if (RowColor is not { } colour) return;

        using var bar = new SolidBrush(colour);
        e.Graphics.FillRectangle(bar, 1, 1, Scaled(3), Height - 2);
    }

    private float ScaleFactor => ZoomLayout.FontPixelFactor(this, DesignerFontPoints);

    private int Scaled(int value) => ZoomLayout.Round(value * ScaleFactor);

    private void Layout1()
    {
        SuspendLayout();

        int pad = Scaled(6);
        int gap = Scaled(4);
        int fh = _name.PreferredHeight;
        int right = Width - pad;

        int y = gap;
        int x = pad;
        _grip.Bounds = new Rectangle(x, y, Scaled(14), fh);
        x = _grip.Right + gap;
        _name.Bounds = new Rectangle(x, y, Scaled(112), fh);
        x = _name.Right + gap;
        _colour.Bounds = new Rectangle(x, y, Scaled(26), fh);
        x = _colour.Right + gap;
        _backup.Bounds = new Rectangle(x, y, Scaled(66), fh);
        x = _backup.Right + Scaled(10);
        _framesCaption.Bounds = new Rectangle(x, y, Scaled(48), fh);
        x = _framesCaption.Right;
        _minFrame.Bounds = new Rectangle(x, y, Scaled(44), fh);
        x = _minFrame.Right;
        _frameDash.Bounds = new Rectangle(x, y, Scaled(12), fh);
        x = _frameDash.Right;
        _maxFrame.Bounds = new Rectangle(x, y, Scaled(44), fh);

        _close.Bounds = new Rectangle(right - fh, y, fh, fh);
        int oddsWidth = Scaled(78);
        _odds.Bounds = new Rectangle(_close.Left - gap - oddsWidth, y, oddsWidth, fh);

        int ruleY = _name.Bottom + gap;
        _headerRule = new Rectangle(pad, ruleY, right - pad, 1);

        int bodyTop = ruleY + gap + Scaled(14);
        int rowPitch = fh - 1;
        int statWidth = Scaled(52);
        int packWidth = Scaled(36);
        int packFirstX = pad + statWidth + gap;
        int[] packX = { packFirstX, packFirstX + packWidth - 1, packFirstX + 2 * (packWidth - 1) };
        int ivRight = packX[2] + packWidth;

        for (int pack = 0; pack < 3; pack++)
        {
            Label header = _packHeaders[pack];
            header.Location = new Point(
                packX[pack] + (packWidth - header.Width) / 2, bodyTop - header.Height - Scaled(1));
        }

        for (int stat = 0; stat < 6; stat++)
        {
            int rowY = bodyTop + stat * rowPitch;
            _statLabels[stat].Bounds = new Rectangle(pad, rowY, statWidth, fh);

            if (stat == 0)
            {
                _thresholds[0, 0].Bounds = new Rectangle(packFirstX, rowY, ivRight - packFirstX, fh);
                continue;
            }

            for (int pack = 0; pack < 3; pack++)
            {
                _thresholds[pack, stat].Bounds = new Rectangle(packX[pack], rowY, packWidth, fh);
            }
        }

        int ivBottom = bodyTop + 6 * rowPitch;

        int columnRuleX = ivRight + Scaled(9);
        int naturesLeft = columnRuleX + Scaled(9);
        int naturesWidth = right - naturesLeft;
        int cellPitch = naturesWidth / 5;
        int naturePitch = Scaled(22);

        int buttonHeight = Scaled(22);
        int buttonRow = ivBottom - buttonHeight;
        int sectionTop = _headerRule.Bottom + gap;
        int naturesBlockHeight = 5 * naturePitch;
        int naturesTop = sectionTop + (buttonRow - sectionTop - naturesBlockHeight) / 2;
        for (int i = 0; i < _natures.Length; i++)
        {
            _natures[i].Bounds = new Rectangle(
                naturesLeft + i % 5 * cellPitch,
                naturesTop + i / 5 * naturePitch,
                cellPitch - Scaled(2),
                Scaled(20));
        }

        int naturesBottom = naturesTop + 5 * naturePitch;

        int buttonWidth = Scaled(104);
        int buttonsWidth = 3 * buttonWidth + 2 * gap;
        int buttonsLeft = naturesLeft + (naturesWidth - buttonsWidth) / 2;

        _clearIvs.Bounds = new Rectangle(buttonsLeft, buttonRow, buttonWidth, buttonHeight);
        _naturesAll.Bounds = new Rectangle(buttonsLeft + buttonWidth + gap, buttonRow, buttonWidth, buttonHeight);
        _naturesNone.Bounds =
            new Rectangle(buttonsLeft + 2 * (buttonWidth + gap), buttonRow, buttonWidth, buttonHeight);

        _buttonRule = new Rectangle(naturesLeft, (naturesBottom + buttonRow) / 2, naturesWidth, 1);

        Height = ivBottom + pad;

        _columnRule = new Rectangle(columnRuleX, _headerRule.Bottom + gap, 1, Height - pad - _headerRule.Bottom - gap);

        ResumeLayout(true);
    }

    private IEnumerable<Control> SearchInputs()
    {
        yield return _name;
        yield return _minFrame;
        yield return _maxFrame;

        var seen = new HashSet<TextBox>();
        for (int pack = 0; pack < 3; pack++)
        {
            for (int stat = 0; stat < 6; stat++)
            {
                if (seen.Add(_thresholds[pack, stat])) yield return _thresholds[pack, stat];
            }
        }
    }

    private static void DrawDash(object? sender, PaintEventArgs e)
    {
        if (sender is not Control control) return;

        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddString("–", control.Font.FontFamily, (int)control.Font.Style,
            e.Graphics.DpiY * control.Font.SizeInPoints / 72f, PointF.Empty, StringFormat.GenericTypographic);

        RectangleF ink = path.GetBounds();
        if (ink.Width <= 0f || ink.Height <= 0f) return;

        using (var move = new System.Drawing.Drawing2D.Matrix())
        {
            move.Translate(
                control.ClientRectangle.Width / 2f - (ink.Left + ink.Width / 2f),
                control.ClientRectangle.Height / 2f - (ink.Top + ink.Height / 2f));
            path.Transform(move);
        }

        var previous = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var brush = new SolidBrush(control.ForeColor))
        {
            e.Graphics.FillPath(brush, path);
        }
        e.Graphics.SmoothingMode = previous;
    }

    private ThemedTextBox MakeBox(bool numeric = true) => new()
    {
        Numeric = numeric,
        AutoSize = false
    };

    private static Label MakeCaption(string text) => new()
    {
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
        AutoSize = false
    };

    private void WritePack(int pack, int[] values)
    {
        for (int stat = 0; stat < 6; stat++)
        {
            _thresholds[pack, stat].Text = values[stat].ToString(CultureInfo.InvariantCulture);
        }
    }

    private int[] ReadPack(int pack)
    {
        var values = new int[6];
        for (int stat = 0; stat < 6; stat++)
        {
            _ = int.TryParse(
                _thresholds[pack, stat].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out values[stat]);
            values[stat] = Math.Clamp(values[stat], 0, 31);
        }
        return values;
    }

    private void ClearThresholds()
    {
        for (int pack = 0; pack < 3; pack++)
        {
            for (int stat = 0; stat < 6; stat++) _thresholds[pack, stat].Text = "0";
        }
    }

    private void SetAllNatures(bool @checked)
    {
        foreach (ThemedCheckBox box in _natures) box.Checked = @checked;
    }

    private void PickColour()
    {
        Color chosen = StarterTool.Modal(() =>
        {
            using var dialog = new ColorDialog
            {
                Color = SwatchColor,
                FullOpen = true,
                AnyColor = true,
                CustomColors = Theme.RangeColorValues()
            };
            return dialog.ShowDialog(this) == DialogResult.OK ? dialog.Color : Color.Empty;
        });

        if (chosen.IsEmpty) return;

        Range.Color = chosen.ToArgb() & 0xFFFFFF;
        RefreshSwatch();
        Invalidate();
        Raise(ColorChanged);
    }

    private void Raise(EventHandler? handler)
    {
        if (_writing) return;

        handler?.Invoke(this, EventArgs.Empty);
    }
}
