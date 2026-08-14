using System.Globalization;

using FRLG.StarterTool.Core.Npc;
using FRLG.StarterTool.Core.Troubleshoot;

namespace FRLG.StarterTool.App;

public sealed class TroubleshootPanel : UserControl
{
    private const int RowHeight = 22;
    private const int Gap = 6;
    private const int LabelWidth = 62;
    private const int KeysTop = 52;
    private const int KeyHeight = 20;
    private const int KeyWidth = 34;
    private const int KeyGap = 4;
    private const int ClearWidth = 52;

    private const int SummaryTop = 74;

    private const int SummaryHeight = 30;

    private const int RowsTop = 106;

    private static int RowHeightFor(TroubleshootStage stage) =>
        stage == TroubleshootStage.Lab ? 26 : 18;

    private const int SlotPitch = 17;

    private const int GutterWidth = 15;

    private const int NotesHeight = 30;

    private int _scroll;

    private readonly ThemedComboBox _runs = new();
    private readonly ThemedComboBox _stage = new();
    private readonly ThemedTextBox _first = new();
    private readonly ThemedTextBox _second = new();
    private readonly Label _runLabel = new();
    private readonly Label _stageLabel = new();
    private readonly Label _firstLabel = new();
    private readonly Label _secondLabel = new();
    private readonly ThemedButton _reload = new();

    private readonly ThemedButton _sweep = new();

    private readonly ThemedButton[] _keys;

    private ThemedTextBox _target;

    private IReadOnlyList<RunRecord> _records = Array.Empty<RunRecord>();
    private TroubleshootResult? _result;
    private SweepResult? _swept;
    private string _error = "";
    private Font? _bold;

    private string _fenceReport = "";
    private string _labAide = "";
    private string _labScientist = "";

    private TroubleshootStage _reportsFor = TroubleshootStage.Fence;

    public TroubleshootPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer, true);

        _runLabel.Text = "Run";
        _stageLabel.Text = "Watching";
        _firstLabel.Text = "Fence guy";
        _secondLabel.Text = "Scientist";

        foreach (Label label in new[] { _runLabel, _stageLabel, _firstLabel, _secondLabel })
        {
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(label);
        }

        _runs.DropDownStyle = ComboBoxStyle.DropDownList;
        _stage.DropDownStyle = ComboBoxStyle.DropDownList;
        _stage.Items.AddRange(new object[] { "Fence Guy", "Lab" });
        _stage.SelectedIndex = 0;

        _reload.Text = "Reload";
        _sweep.Text = "Sweep seed";

        _keys = new[]
        {
            Key("↑", "N"), Key("↓", "S"), Key("←", "W"), Key("→", "E"),
            Key("–", "-"), Key("⌫", ""), Key("Clear", null),
        };

        _target = _first;

        Controls.Add(_runs);
        Controls.Add(_stage);
        Controls.Add(_first);
        Controls.Add(_second);
        Controls.Add(_reload);
        Controls.Add(_sweep);

        _first.TextChanged += (_, _) => Recompute();
        _second.TextChanged += (_, _) => Recompute();
        _runs.SelectedIndexChanged += (_, _) => Recompute();
        _stage.SelectedIndexChanged += (_, _) => { ApplyStage(); Recompute(); };
        _reload.Click += (_, _) => Reload();
        _sweep.Click += (_, _) => Sweep();

        _first.Enter += (_, _) => _target = _first;
        _second.Enter += (_, _) => _target = _second;

        Layout1();
        ApplyStage();
    }

    private ThemedButton Key(string caption, string? letter)
    {
        var button = new ThemedButton { Text = caption, TabStop = false };

        button.Click += (_, _) =>
        {
            ThemedTextBox box = Target;

            if (letter == null)
            {
                box.Clear();
            }
            else if (letter.Length == 0)
            {
                box.Text = Backspace(box.Text);
            }
            else
            {
                box.Text = box.Text.TrimEnd().Length == 0
                    ? letter
                    : box.Text.TrimEnd() + " " + letter;
            }

            box.Focus();
            box.SelectionStart = box.TextLength;
        };

        Controls.Add(button);
        return button;
    }

    private static string Backspace(string text)
    {
        string trimmed = text.TrimEnd();
        int last = trimmed.LastIndexOf(' ');
        return last < 0 ? "" : trimmed[..last];
    }

    private ThemedTextBox Target =>
        ReferenceEquals(_target, _second) && _second.Visible ? _second : _first;

    public void Reload()
    {
        _records = RunLogParser.ReadFolder(RunLog.Directory);

        _runs.BeginUpdate();
        _runs.Items.Clear();
        foreach (RunRecord record in _records) _runs.Items.Add(Describe(record));
        _runs.EndUpdate();

        if (_runs.Items.Count > 0) _runs.SelectedIndex = 0;
        Recompute();
    }

    private static string Describe(RunRecord record)
    {
        string when = record.Started == default
            ? record.FileName
            : record.Started.ToString("HH:mm  dd MMM", CultureInfo.CurrentCulture);

        string seed = record.Seed > 0
            ? "TID " + record.Seed.ToString(CultureInfo.InvariantCulture)
            : "no TID";

        string end = record.Outcome.Length > 0 ? record.Outcome
            : record.LandedFrame is { } landed
                ? "landed " + landed.ToString(CultureInfo.InvariantCulture)
                : "no landing";

        return $"{when}   {seed}   {end}";
    }

    private RunRecord? Selected =>
        _runs.SelectedIndex >= 0 && _runs.SelectedIndex < _records.Count
            ? _records[_runs.SelectedIndex]
            : null;

    private TroubleshootStage Stage =>
        _stage.SelectedIndex == 1 ? TroubleshootStage.Lab : TroubleshootStage.Fence;

    private void ApplyStage()
    {
        bool lab = Stage == TroubleshootStage.Lab;

        if (Stage != _reportsFor)
        {
            if (_reportsFor == TroubleshootStage.Lab)
            {
                _labAide = _first.Text;
                _labScientist = _second.Text;
            }
            else
            {
                _fenceReport = _first.Text;
            }

            _reportsFor = Stage;
            _first.Text = lab ? _labAide : _fenceReport;
            _second.Text = lab ? _labScientist : "";
        }

        _firstLabel.Text = lab ? "Lady" : "Fence guy";
        _secondLabel.Visible = lab;
        _second.Visible = lab;

        Layout1();
    }

    private void Layout1()
    {
        float factor = ScaleFactor;
        int S(int value) => ZoomLayout.Round(value * factor);

        int right = Math.Max(S(240), Width);
        int rowHeight = S(RowHeight);
        int label = S(LabelWidth);
        int gap = S(Gap);
        int reload = S(66);

        _runLabel.Bounds = new Rectangle(0, 0, label, rowHeight);
        _runs.Bounds = new Rectangle(label, 0, S(206), rowHeight);
        _stageLabel.Bounds = new Rectangle(_runs.Right + gap * 2, 0, S(70), rowHeight);
        _stage.Bounds = new Rectangle(
            _stageLabel.Right, 0,
            Math.Max(S(60), right - reload - S(84) - 2 * gap - _stageLabel.Right),
            rowHeight);
        int sweep = S(84);
        _sweep.Bounds = new Rectangle(right - reload - gap - sweep, 0, sweep, rowHeight);
        _reload.Bounds = new Rectangle(right - reload, 0, reload, rowHeight);

        _runs.MatchHeight(rowHeight);
        _stage.MatchHeight(rowHeight);

        int top = rowHeight + gap;

        _firstLabel.Bounds = new Rectangle(0, top, label, rowHeight);

        if (Stage == TroubleshootStage.Lab)
        {
            int half = (right - 2 * label - gap) / 2;

            _first.Bounds = new Rectangle(label, top, half, rowHeight);
            _secondLabel.Bounds = new Rectangle(_first.Right + gap, top, label, rowHeight);
            _second.Bounds = new Rectangle(
                _secondLabel.Right, top, right - _secondLabel.Right, rowHeight);
        }
        else
        {
            _first.Bounds = new Rectangle(label, top, right - label, rowHeight);
        }

        int keyTop = S(KeysTop);
        int keyHeight = S(KeyHeight);
        int keyGap = S(KeyGap);
        int x = label;

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

    private void Sweep()
    {
        if (Selected is not { } run || _error.Length > 0) return;

        MovementStrip.TryParse(_first.Text, out List<StripToken> first, out _);
        MovementStrip.TryParse(_second.Text, out List<StripToken> second, out _);

        UseWaitCursor = true;
        try
        {
            MovementStrip.TryParse(_fenceReport, out List<StripToken> fenceGuy, out _);

            _swept = Stage == TroubleshootStage.Lab
                ? FrameSweep.Lab(run,
                    _first.Text.Trim().Length == 0 ? null : first,
                    _second.Text.Trim().Length == 0 ? null : second,
                    fenceGuy: _fenceReport.Trim().Length == 0 ? null : fenceGuy)
                : FrameSweep.Fence(run, first);
        }
        finally
        {
            UseWaitCursor = false;
        }

        _scroll = 0;
        Invalidate();
    }

    private void Recompute()
    {
        _error = "";
        _result = null;

        _swept = null;

        _scroll = 0;

        if (Selected is not { } run)
        {
            Invalidate();
            return;
        }

        if (!MovementStrip.TryParse(_first.Text, out List<StripToken> first, out char bad))
        {
            _error = $"'{bad}' is not a direction. Type N/S/E/W (or U/D/L/R) and - for a quiet 32 frames.";
            Invalidate();
            return;
        }

        if (!MovementStrip.TryParse(_second.Text, out List<StripToken> second, out char alsoBad))
        {
            _error = $"'{alsoBad}' is not a direction. Type N/S/E/W (or U/D/L/R) and - for a quiet 32 frames.";
            Invalidate();
            return;
        }

        _result = Stage == TroubleshootStage.Lab
            ? Troubleshooter.Lab(run,
                _first.Text.Trim().Length == 0 ? null : first,
                _second.Text.Trim().Length == 0 ? null : second)
            : Troubleshooter.Fence(run, first);

        Invalidate();
    }

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

        if (_error.Length > 0)
        {
            Draw(g, _error, 0, scaled(SummaryTop), Width, scaled(SummaryHeight),
                Theme.LandingMissText, BoldFont);
            return;
        }

        if (_result is not { } result)
        {
            Draw(g, _records.Count == 0
                    ? "No runs recorded yet. Every attempt writes its own file under runs/."
                    : "Pick a run, then type what you saw.",
                0, scaled(SummaryTop), Width, scaled(SummaryHeight), Theme.DimText, Font);
            return;
        }

        if (_swept is { } swept)
        {
            Draw(g, swept.Summary, 0, scaled(SummaryTop), Width, scaled(SummaryHeight),
                swept.Found ? Theme.LandingHitText : Theme.LandingMaybeText, BoldFont);

            PaintSweep(g, swept, scaled(RowsTop), ResultRowHeight(result));
            return;
        }

        Draw(g, result.Summary, 0, scaled(SummaryTop), Width, scaled(SummaryHeight),
            SummaryInk(result), BoldFont);

        PaintRows(g, result, scaled(RowsTop), ResultRowHeight(result));
        PaintNotes(g, result, scaled(NotesHeight));
    }

    private static Color SummaryInk(TroubleshootResult result)
    {
        if (result.Rows.Count > 0 && result.Fits.Count == 0) return Theme.LandingMissText;

        return result.OutByFrames switch
        {
            0 => Theme.LandingHitText,
            not null => Theme.LandingMissText,
            _ => Theme.LandingMaybeText,
        };
    }

    private void PaintRows(Graphics g, TroubleshootResult result, int top, int height)
    {
        IReadOnlyList<RowMatch> shown = result.Rows;
        int visible = VisibleRows(top, height);
        int drawn = Math.Min(shown.Count - _scroll, visible);

        for (int i = 0; i < drawn; i++)
        {
            RowMatch row = shown[_scroll + i];
            var bounds = new Rectangle(0, top + i * height, Width, height);

            if (row.WasUsed)
            {
                using var band = new SolidBrush(Theme.LandingContextBack);
                g.FillRectangle(band, bounds);
            }

            Color ink = row.Quality switch
            {
                MatchQuality.Exact => Theme.LandingHitText,
                MatchQuality.Movements => Theme.LandingMaybeText,
                _ => Theme.DimText,
            };

            int x = 0;
            int mark = ZoomLayout.Round(52 * ScaleFactor);
            int advance = ZoomLayout.Round(58 * ScaleFactor);
            int label = ZoomLayout.Round(84 * ScaleFactor);

            Draw(g, row.Quality switch
                {
                    MatchQuality.Exact => "exact",
                    MatchQuality.Movements => "moves",
                    _ => "+" + row.Distance.ToString(CultureInfo.InvariantCulture),
                },
                x, bounds.Y, mark, height, ink, Font);
            x += mark;

            Draw(g, row.Advances.ToString(CultureInfo.InvariantCulture), x, bounds.Y, advance, height,
                Theme.Text, row.WasUsed ? BoldFont : Font);
            x += advance;

            Draw(g, row.Label, x, bounds.Y, label, height, Theme.DimText, Font);
            x += label;

            PaintStrips(g, row, new Rectangle(x, bounds.Y, Width - x, height));
        }
    }

    private void PaintSweep(Graphics g, SweepResult swept, int top, int height)
    {
        int visible = VisibleRows(top, height);
        int drawn = Math.Min(swept.Hits.Count - _scroll, visible);

        for (int i = 0; i < drawn; i++)
        {
            SweepHit hit = swept.Hits[_scroll + i];
            var bounds = new Rectangle(0, top + i * height, Width, height);

            Color ink = hit.Quality switch
            {
                MatchQuality.Exact => Theme.LandingHitText,
                MatchQuality.Movements => Theme.LandingMaybeText,
                _ => Theme.DimText,
            };

            int x = 0;
            int mark = ZoomLayout.Round(52 * ScaleFactor);
            int advance = ZoomLayout.Round(58 * ScaleFactor);
            int label = ZoomLayout.Round(104 * ScaleFactor);

            Draw(g, hit.Quality switch
                {
                    MatchQuality.Exact => "exact",
                    MatchQuality.Movements => "moves",
                    _ => "+" + hit.Distance.ToString(CultureInfo.InvariantCulture),
                },
                x, bounds.Y, mark, height, ink, Font);
            x += mark;

            Draw(g, hit.Advances.ToString(CultureInfo.InvariantCulture), x, bounds.Y, advance, height,
                Theme.Text, hit.OffsetFrames == 0 ? BoldFont : Font);
            x += advance;

            Draw(g, string.Format(CultureInfo.InvariantCulture, "{0:+#;-#;0}f  w{1}{2}",
                    hit.OffsetFrames, hit.ObservableFrames,
                    hit.StreamShift == 0
                        ? ""
                        : string.Format(CultureInfo.InvariantCulture, "  s{0:+#;-#}", hit.StreamShift)),
                x, bounds.Y, label, height,
                hit.StreamShift == 0 ? Theme.DimText : Theme.LandingMaybeText, Font);
            x += label;

            PaintStrips(g, new RowMatch(0, hit.Quality, hit.Advances, hit.Lines, "", false,
                hit.Distance), new Rectangle(x, bounds.Y, Width - x, height));
        }

        int notes = ZoomLayout.Round(NotesHeight * ScaleFactor);

        string legend = string.Format(CultureInfo.InvariantCulture,
            "swept {0} frames · offset moves the press, w is the window, s the stream · edit the "
            + "report to return to the field", swept.Scanned);

        if (swept.Note.Length == 0)
        {
            Draw(g, legend, 0, Height - notes, Width, notes, Theme.DimText, Font);
            return;
        }

        Draw(g, swept.Note, 0, Height - notes, Width, notes / 2, Theme.LandingMaybeText, BoldFont);
        Draw(g, legend, 0, Height - notes / 2, Width, notes / 2, Theme.DimText, Font);
    }

    private void PaintStrips(Graphics g, RowMatch row, Rectangle bounds)
    {
        int lines = Math.Max(1, row.Lines.Count);
        int lineHeight = bounds.Height / lines;
        int pitch = ZoomLayout.Round(SlotPitch * ScaleFactor);
        int gutter = row.Lines.Count > 1 ? ZoomLayout.Round(GutterWidth * ScaleFactor) : 0;

        for (int i = 0; i < row.Lines.Count; i++)
        {
            StripLine line = row.Lines[i];
            int y = bounds.Y + i * lineHeight;

            if (gutter > 0)
            {
                Draw(g, line.Label, bounds.X, y, gutter, lineHeight, Theme.DimText, Font);
            }

            int slots = Math.Max(0, (bounds.Width - gutter) / Math.Max(1, pitch));

            if (line.Tokens.Count == 0)
            {
                Draw(g, "nothing", bounds.X + gutter, y, bounds.Width - gutter, lineHeight,
                    Theme.DimText, Font);
                continue;
            }

            for (int slot = 0; slot < line.Tokens.Count && slot < slots; slot++)
            {
                StripToken token = line.Tokens[slot];

                Draw(g, token.ToString(), bounds.X + gutter + slot * pitch, y, pitch, lineHeight,
                    token.IsQuiet ? Theme.DimText : Theme.Text, Font);
            }

            if (line.Tokens.Count > slots)
            {
                Draw(g, "…", bounds.X + gutter + slots * pitch - pitch / 2, y, pitch, lineHeight,
                    Theme.DimText, Font);
            }
        }
    }

    private int ResultRowHeight(TroubleshootResult result) =>
        ZoomLayout.Round(RowHeightFor(result.Stage) * ScaleFactor);

    private int VisibleRows(int top, int height) =>
        Math.Max(1, (Height - ZoomLayout.Round(NotesHeight * ScaleFactor) - top) / height);

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (_result is not { } result) return;

        int visible = VisibleRows(
            ZoomLayout.Round(RowsTop * ScaleFactor), ResultRowHeight(result));
        int most = Math.Max(0, result.Rows.Count - visible);

        int moved = Math.Clamp(_scroll - Math.Sign(e.Delta), 0, most);
        if (moved == _scroll) return;

        _scroll = moved;
        Invalidate();
    }

    private void PaintNotes(Graphics g, TroubleshootResult result, int height)
    {
        var parts = new List<string>();

        if (result.Rows.Count > 0)
        {
            int visible = VisibleRows(
                ZoomLayout.Round(RowsTop * ScaleFactor), ResultRowHeight(result));

            string count = string.Format(CultureInfo.InvariantCulture, "{0} of {1} fit",
                result.Fits.Count, result.Rows.Count);

            parts.Add(result.Rows.Count > visible
                ? count + string.Format(CultureInfo.InvariantCulture,
                    " · showing {0}-{1}, scroll for the rest",
                    _scroll + 1, Math.Min(result.Rows.Count, _scroll + visible))
                : count);
        }

        parts.AddRange(result.Notes);

        if (parts.Count == 0) return;

        Draw(g, string.Join("  ·  ", parts), 0, Height - height, Width, height,
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
