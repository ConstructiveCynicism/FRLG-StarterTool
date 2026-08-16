using System.Globalization;

using FRLG.StarterTool.Core.Npc;
using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Troubleshoot;

namespace FRLG.StarterTool.App;

public sealed class TroubleshootPanel : UserControl
{
    private const int RowHeight = 20;
    private const int Gap = 6;
    private const int LabelWidth = 44;

    private const int ReportWidth = 220;

    private const int NpcTop = 26;

    private const int NpcRowHeight = 22;

    private const int NpcCount = 3;

    private const int KeysTop = NpcTop + NpcCount * NpcRowHeight + 4;
    private const int KeyHeight = 20;

    private const int KeyWidth = 24;
    private const int KeyGap = 3;
    private const int ClearWidth = 44;

    private const int NotesTop = KeysTop + KeyHeight + 8;

    private const int SummaryTop = NpcTop;

    private const int SummaryHeight = 32;

    private const int OptionsTop = SummaryTop + SummaryHeight + 4;

    private const int OptionHeight = 17;

    private const int DetailTop = OptionsTop + TroubleshootSearch.MostOptions * OptionHeight + 6;

    private const int ReportPitch = 14;

    private const int GutterWidth = 11;

    private const int NameWidth = 38;

    private static readonly string[] NpcNames = { "Fence", "Lady", "Sci" };

    private readonly List<StripToken>[] _report =
    {
        new(), new(), new(),
    };

    private int _npc;

    private readonly ThemedComboBox _runs = new();
    private readonly ThemedTextBox _frameHit = new();
    private readonly Label _runLabel = new();
    private readonly Label _hitLabel = new();
    private readonly ThemedButton _reload = new();

    private readonly ThemedButton _window = new();

    private int[] _windows = Array.Empty<int>();

    private int _windowIndex;

    private readonly ThemedButton _search = new();

    private readonly ThemedButton[] _keys;

    private IReadOnlyList<RunRecord> _records = Array.Empty<RunRecord>();
    private SearchOutcome? _found;

    private int _selected;

    private Font? _bold;

    public TroubleshootPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer, true);

        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;

        _runLabel.Text = "Run";
        _hitLabel.Text = "Hit";

        foreach (Label label in new[] { _runLabel, _hitLabel })
        {
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(label);
        }

        _runs.DropDownStyle = ComboBoxStyle.DropDownList;

        _reload.Text = "Reload";
        _search.Text = "Search";

        _keys = new[]
        {
            Key("↑", Direction.North), Key("↓", Direction.South),
            Key("←", Direction.West), Key("→", Direction.East),
            Key("–", Direction.None), Key("⌫", null), Key("Clear", null, clear: true),
        };

        Controls.Add(_runs);
        Controls.Add(_frameHit);
        Controls.Add(_window);
        Controls.Add(_reload);
        Controls.Add(_search);

        _window.TabStop = false;
        _window.Click += (_, _) =>
        {
            if (_windows.Length > 0) _windowIndex = (_windowIndex + 1) % _windows.Length;

            ShowWindow();
            Focus();
            Clear();
        };

        _runs.SelectedIndexChanged += (_, _) => LoadRun();
        _reload.Click += (_, _) => Reload();
        _search.Click += (_, _) => Search();

        _frameHit.TextChanged += (_, _) => Clear();

        ShowWindow();
        Layout1();
    }

    private ThemedButton Key(string caption, Direction? direction, bool clear = false)
    {
        var button = new ThemedButton { Text = caption, TabStop = false };

        button.Click += (_, _) =>
        {
            if (clear) _report[_npc].Clear();
            else if (direction is not { } appended) Backspace();
            else Append(appended);

            Focus();
            Clear();
        };

        Controls.Add(button);
        return button;
    }

    public bool Append(Direction direction)
    {
        if (!Accepting) return false;

        _report[_npc].Add(direction == Direction.None
            ? StripToken.Quiet
            : new StripToken(direction));

        Clear();
        return true;
    }

    public bool Backspace()
    {
        if (!Accepting) return false;

        List<StripToken> line = _report[_npc];
        if (line.Count > 0) line.RemoveAt(line.Count - 1);

        Clear();
        return true;
    }

    public bool MoveNpc(int delta)
    {
        if (!Accepting) return false;

        _npc = ((_npc + delta) % NpcCount + NpcCount) % NpcCount;
        Invalidate();
        return true;
    }

    private bool Accepting =>
        Visible && ContainsFocus && !_frameHit.Focused && !_runs.Focused;

    private void Clear()
    {
        _found = null;
        _selected = 0;
        Invalidate();
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (Accepting && Bound(keyData) == null)
        {
            switch (keyData)
            {
                case Keys.Up or Keys.W: return Append(Direction.North);
                case Keys.Down or Keys.S: return Append(Direction.South);
                case Keys.Left or Keys.A: return Append(Direction.West);
                case Keys.Right or Keys.D: return Append(Direction.East);

                case Keys.Space or Keys.OemMinus or Keys.Subtract:
                    return Append(Direction.None);

                case Keys.Back: return Backspace();
                case Keys.Tab: return MoveNpc(1);
                case Keys.Tab | Keys.Shift: return MoveNpc(-1);
            }
        }

        return base.ProcessCmdKey(ref message, keyData);
    }

    private static Direction? Bound(Keys key)
    {
        AppSettings? settings = StarterTool.Settings;
        if (settings == null) return null;

        if (settings.NpcUp.IsPressed(key)) return Direction.North;
        if (settings.NpcDown.IsPressed(key)) return Direction.South;
        if (settings.NpcLeft.IsPressed(key)) return Direction.West;
        if (settings.NpcRight.IsPressed(key)) return Direction.East;

        return null;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        int scaled(int value) => ZoomLayout.Round(value * ScaleFactor);

        if (e.X < scaled(ReportWidth))
        {
            int row = (e.Y - scaled(NpcTop)) / Math.Max(1, scaled(NpcRowHeight));
            if (row >= 0 && row < NpcCount)
            {
                _npc = row;
                Invalidate();
            }

            return;
        }

        if (_found is not { } found || found.Options.Count == 0) return;

        int option = (e.Y - scaled(OptionsTop)) / Math.Max(1, scaled(OptionHeight));
        if (option < 0 || option >= found.Options.Count) return;

        _selected = option;
        Invalidate();
    }

    public void Reload()
    {
        _records = RunLogParser.ReadFolder(RunLog.Directory);

        _runs.BeginUpdate();
        _runs.Items.Clear();
        foreach (RunRecord record in _records) _runs.Items.Add(Describe(record));
        _runs.EndUpdate();

        if (_runs.Items.Count > 0) _runs.SelectedIndex = 0;

        LoadRun();
        Focus();
    }

    private void LoadRun()
    {
        foreach (List<StripToken> line in _report) line.Clear();

        _windows = Selected is { } picked
            ? TroubleshootSearch.WindowChoices(picked)
            : Array.Empty<int>();

        _windowIndex = 0;
        ShowWindow();

        if (Selected is { } run)
        {
            _report[0].AddRange(run.Taps.Select(d => new StripToken(d)));

            LabRow? box = run.Lab.FirstOrDefault(r => r.Focused)
                ?? (run.Lab.Count == 1 ? run.Lab[0] : null);

            if (box != null)
            {
                _report[1].AddRange(box.AideStrip);
                _report[2].AddRange(box.ScientistStrip);
            }
        }

        Clear();
    }

    private static string Describe(RunRecord record)
    {
        string when = record.Started == default
            ? record.FileName
            : record.Started.ToString("dd MMM HH:mm", CultureInfo.CurrentCulture);

        string seed = record.Seed > 0
            ? record.Seed.ToString(CultureInfo.InvariantCulture)
            : "no TID";

        string end = record.Outcome.Length > 0 ? Short(record.Outcome)
            : record.LandedFrame is { } landed
                ? "hit " + landed.ToString(CultureInfo.InvariantCulture)
                : "no landing";

        return $"{when} · {seed} · {end}";
    }

    private static string Short(string outcome)
    {
        string text = outcome.Trim().Trim(',').Trim();

        const string open = "anchor chain left open at ";
        if (text.StartsWith(open, StringComparison.Ordinal)) return "open " + text[open.Length..];

        int cut = text.IndexOf(',');
        return cut > 0 ? text[..cut] : text;
    }

    private int? ChosenWindow =>
        _windows.Length == 0 ? null : _windows[Math.Clamp(_windowIndex, 0, _windows.Length - 1)];

    private void ShowWindow() =>
        _window.Text = ChosenWindow is { } frames
            ? string.Format(CultureInfo.InvariantCulture, "Win {0}", frames)
            : "Win —";

    private RunRecord? Selected =>
        _runs.SelectedIndex >= 0 && _runs.SelectedIndex < _records.Count
            ? _records[_runs.SelectedIndex]
            : null;

    private int? FrameHit =>
        int.TryParse(_frameHit.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
            out int frame) && frame >= 0
            ? frame
            : null;

    private void Search()
    {
        if (Selected is not { } run) return;

        UseWaitCursor = true;
        try
        {
            _found = TroubleshootSearch.Run(run,
                Line(0), Line(1), Line(2),
                FrameHit,
                StarterTool.Settings?.NpcContextWindowMs ?? 0.0,
                TroubleshootSearch.DefaultRadius,
                ChosenWindow);
        }
        finally
        {
            UseWaitCursor = false;
        }

        _selected = 0;
        Invalidate();
        Focus();
    }

    private IReadOnlyList<StripToken>? Line(int npc) =>
        _report[npc].Count == 0 ? null : _report[npc];

    private void Layout1()
    {
        float factor = ScaleFactor;
        int S(int value) => ZoomLayout.Round(value * factor);

        int right = Math.Max(S(240), Width);
        int rowHeight = Math.Max(S(RowHeight), Font.Height + 5);
        int label = S(LabelWidth);
        int gap = S(Gap);
        int button = S(70);

        _runLabel.Bounds = new Rectangle(0, 0, label, rowHeight);
        _runs.Bounds = new Rectangle(label, 0, S(206), rowHeight);
        _runs.MatchHeight(rowHeight);

        _hitLabel.Bounds = new Rectangle(_runs.Right + gap * 2, 0, S(24), rowHeight);
        _frameHit.Bounds = new Rectangle(_hitLabel.Right, 0, S(60), rowHeight);

        _window.Bounds = new Rectangle(_frameHit.Right + gap * 2, 0, button, rowHeight);

        _search.Bounds = new Rectangle(right - 2 * button - gap, 0, button, rowHeight);
        _reload.Bounds = new Rectangle(right - button, 0, button, rowHeight);

        int keyTop = S(KeysTop);
        int keyHeight = S(KeyHeight);
        int keyGap = S(KeyGap);
        int x = S(GutterWidth);

        for (int i = 0; i < _keys.Length; i++)
        {
            int width = S(i == _keys.Length - 1 ? ClearWidth : KeyWidth);
            _keys[i].Bounds = new Rectangle(x, keyTop, width, keyHeight);
            x += width + keyGap;
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        Layout1();
    }

    public void Relayout() => Layout1();

    private Font BoldFont => _bold ??= new Font(Font, FontStyle.Bold);

    protected override void OnFontChanged(EventArgs e)
    {
        _bold?.Dispose();
        _bold = null;
        base.OnFontChanged(e);
        Layout1();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _bold?.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Window);

        int scaled(int value) => ZoomLayout.Round(value * ScaleFactor);

        PaintReport(g, scaled);

        int left = scaled(ReportWidth) + scaled(Gap);
        int width = Math.Max(0, Width - left);

        if (Selected is null)
        {
            Draw(g, _records.Count == 0 ? "No runs in runs/." : "Pick a run.",
                left, scaled(SummaryTop), width, scaled(SummaryHeight), Theme.DimText, Font);
            return;
        }

        if (_found is not { } found)
        {
            Draw(g, "Report the NPCs, type the frame, Search.\r\nLines start on the box this run used.",
                left, scaled(SummaryTop), width, scaled(SummaryHeight), Theme.DimText, Font);
            return;
        }

        Draw(g, found.Summary, left, scaled(SummaryTop), width, scaled(SummaryHeight),
            found.Found ? Theme.LandingHitText : Theme.LandingMaybeText, BoldFont);

        PaintOptions(g, found, left, width, scaled);
        PaintDetail(g, found, left, width, scaled);
        PaintNotes(g, found, scaled);
    }

    private void PaintReport(Graphics g, Func<int, int> scaled)
    {
        int rowHeight = scaled(NpcRowHeight);
        int pitch = scaled(ReportPitch);
        int gutter = scaled(GutterWidth);
        int name = scaled(NameWidth);
        int width = scaled(ReportWidth);

        for (int npc = 0; npc < NpcCount; npc++)
        {
            var bounds = new Rectangle(0, scaled(NpcTop) + npc * rowHeight, width, rowHeight);

            if (npc == _npc)
            {
                using var band = new SolidBrush(Theme.LandingContextBack);
                g.FillRectangle(band, bounds);
            }

            Image? sprite = npc switch
            {
                0 => Assets.FatMan(Direction.South, false),
                1 => Assets.Aide(),
                _ => Assets.Scientist(),
            };

            if (sprite != null)
            {
                int height = bounds.Height - 2;
                int spriteWidth = Math.Max(1, height / 2);

                System.Drawing.Drawing2D.InterpolationMode was = g.InterpolationMode;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(sprite, new Rectangle(bounds.X, bounds.Y + 1, spriteWidth, height));
                g.InterpolationMode = was;
            }

            Draw(g, NpcNames[npc], bounds.X + gutter, bounds.Y, name, rowHeight,
                npc == _npc ? Theme.Text : Theme.DimText, npc == _npc ? BoldFont : Font);

            int x = bounds.X + gutter + name;
            int slots = Math.Max(0, (width - gutter - name) / Math.Max(1, pitch));

            if (_report[npc].Count == 0)
            {
                Draw(g, "—", x, bounds.Y, width - gutter - name, rowHeight, Theme.DimText, Font);
                continue;
            }

            for (int slot = 0; slot < _report[npc].Count && slot < slots; slot++)
            {
                Arrow(g, new Rectangle(x + slot * pitch, bounds.Y, pitch, rowHeight),
                    _report[npc][slot], Theme.Text);
            }

            if (_report[npc].Count > slots)
            {
                Draw(g, "…", x + slots * pitch - pitch, bounds.Y, pitch, rowHeight,
                    Theme.DimText, Font);
            }
        }
    }

    private void PaintOptions(Graphics g, SearchOutcome found, int left, int width,
        Func<int, int> scaled)
    {
        int height = scaled(OptionHeight);
        int top = scaled(OptionsTop);

        for (int i = 0; i < found.Options.Count; i++)
        {
            RouteOption option = found.Options[i];
            var bounds = new Rectangle(left, top + i * height, width, height);

            if (i == _selected)
            {
                using var band = new SolidBrush(Theme.LandingContextBack);
                g.FillRectangle(band, bounds);
            }

            Color ink = option.Quality switch
            {
                MatchQuality.Exact => Theme.LandingHitText,
                MatchQuality.Movements => Theme.LandingMaybeText,
                _ => Theme.DimText,
            };

            Color context = option.InContext ? Theme.LandingHitText : Theme.LandingMissText;

            int x = bounds.X;
            int mark = scaled(40);
            int advances = scaled(48);
            int cost = scaled(52);

            Draw(g, option.Quality switch
                {
                    MatchQuality.Exact => "exact",
                    MatchQuality.Movements => "moves",
                    _ => "+" + option.Distance.ToString(CultureInfo.InvariantCulture),
                },
                x, bounds.Y, mark, height, ink, Font);
            x += mark;

            Draw(g, option.Advances.ToString(CultureInfo.InvariantCulture), x, bounds.Y, advances,
                height, Theme.Text, i == _selected ? BoldFont : Font);
            x += advances;

            Draw(g, option.InContext
                    ? "in ctx"
                    : string.Format(CultureInfo.InvariantCulture, "+{0}f", option.OutOfContextFrames),
                x, bounds.Y, cost, height, context, Font);
            x += cost;

            Draw(g, option.Offsets, x, bounds.Y, bounds.Right - x, height, context, Font);
        }
    }

    private static void Arrow(Graphics g, Rectangle slot, StripToken token, Color ink)
    {
        if (token.IsQuiet)
        {
            int bar = Math.Max(1, slot.Height / 12);
            using var dim = new SolidBrush(Theme.DimText);
            g.FillRectangle(dim, slot.X + slot.Width / 4, slot.Y + slot.Height / 2 - bar / 2,
                Math.Max(2, slot.Width / 2), Math.Max(1, bar));
            return;
        }

        int size = Math.Max(4, Math.Min(slot.Width, slot.Height) - 6);
        int cx = slot.X + slot.Width / 2;
        int cy = slot.Y + slot.Height / 2;
        int half = size / 2;

        PointF[] points = token.Direction switch
        {
            Direction.North => new PointF[]
                { new(cx, cy - half), new(cx - half, cy + half), new(cx + half, cy + half) },
            Direction.South => new PointF[]
                { new(cx, cy + half), new(cx - half, cy - half), new(cx + half, cy - half) },
            Direction.West => new PointF[]
                { new(cx - half, cy), new(cx + half, cy - half), new(cx + half, cy + half) },
            _ => new PointF[]
                { new(cx + half, cy), new(cx - half, cy - half), new(cx - half, cy + half) },
        };

        System.Drawing.Drawing2D.SmoothingMode was = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        if (token.Complete)
        {
            using var brush = new SolidBrush(ink);
            g.FillPolygon(brush, points);
        }
        else
        {
            using var pen = new Pen(ink);
            g.DrawPolygon(pen, points);
        }

        g.SmoothingMode = was;
    }

    private void PaintDetail(Graphics g, SearchOutcome found, int left, int width,
        Func<int, int> scaled)
    {
        if (found.Options.Count == 0) return;

        int index = Math.Clamp(_selected, 0, found.Options.Count - 1);
        RouteOption option = found.Options[index];

        int top = scaled(DetailTop);
        int line = scaled(14);

        var parts = new List<string>
        {
            string.Format(CultureInfo.InvariantCulture, "advances {0}", option.Advances),
            "parity " + option.Parity,
        };

        if (option.ObservableFrames > 0)
        {
            parts.Add(string.Format(CultureInfo.InvariantCulture, "window {0}",
                option.ObservableFrames));
        }

        if (option.Landing is { } landing)
        {
            parts.Add(string.Format(CultureInfo.InvariantCulture, "lands on {0}", landing));
        }

        if (option.StreamShift != 0)
        {
            parts.Add(string.Format(CultureInfo.InvariantCulture, "stream {0:+#;-#}",
                option.StreamShift));
        }

        Draw(g, string.Join(" · ", parts), left, top, width, line, Theme.DimText, Font);
        top += line;

        if (option.Corrections.Count > 0)
        {
            Draw(g, string.Join("  ", option.Corrections), left, top, width, Height - top,
                Theme.LandingMaybeText, BoldFont);
            return;
        }

        Draw(g, option.InContext
                ? "The run's own box — tracker was right, frame went elsewhere."
                : string.Format(CultureInfo.InvariantCulture,
                    "Never on screen: needs +{0}f of context on one anchor.",
                    option.OutOfContextFrames),
            left, top, width, Height - top, Theme.DimText, Font);
    }

    private void PaintNotes(Graphics g, SearchOutcome found, Func<int, int> scaled)
    {
        var parts = new List<string>(found.Notes)
        {
            string.Format(CultureInfo.InvariantCulture, "{0} readings · green = in context",
                found.Scanned),
        };

        int top = scaled(NotesTop);
        Draw(g, string.Join("  ", parts), 0, top, scaled(ReportWidth), Height - top,
            Theme.DimText, Font);
    }

    private static void Draw(Graphics g, string text, int x, int y, int width, int height,
        Color ink, Font font) =>
        TextRenderer.DrawText(g, text, font, new Rectangle(x, y, width, height), ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
            | TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak);

    private float ScaleFactor => Font.SizeInPoints / DesignerFontPoints;

    private const float DesignerFontPoints = 9F;
}
