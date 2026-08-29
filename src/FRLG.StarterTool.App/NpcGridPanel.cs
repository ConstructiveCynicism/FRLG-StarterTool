using System.Globalization;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using FRLG.StarterTool.Core.Npc;

namespace FRLG.StarterTool.App;

public sealed class NpcGridPanel : Control
{
    public const int GridTiles = 7;

    public const int PlayerX = 4;

    public const int PlayerY = 8;

    private const int ViewX = PlayerX;

    private const int ViewY = PlayerY - 2;

    private const int TileSize = 2 * Assets.MapTileSize;

    public const int GridPixels = GridTiles * TileSize;

    private const int Gutter = 12;

    public const int ControlStripHeight = 26;

    private int StatusHeight => Font.Height * 2 + 2;

    private const int LabRowHeight = 26;

    private const int LabHeaderHeight = Assets.NpcFrameHeight + 2;

    private const int AnimationIntervalMs = 15;

    private readonly System.Windows.Forms.Timer _animation;

    private const int BlinkIntervalMs = 700;

    private readonly System.Windows.Forms.Timer _blink;

    private IReadOnlyList<FenceCandidate> _candidates = Array.Empty<FenceCandidate>();
    private IReadOnlyList<LabOption> _boxes = Array.Empty<LabOption>();
    private IReadOnlyList<double> _likelihoods = Array.Empty<double>();
    private int _focused = -1;
    private int _mostLikely = -1;
    private double _fps = 60.0;
    private string _status = "";
    private string _report = "";

    private string _tip = "";

    private bool _tipShiny;

    private bool _blinkLit;

    private bool _labMode;

    private IReadOnlyList<HiddenMoves> _hidden = Array.Empty<HiddenMoves>();

    private int _anchors;

    private int _labCueFrame = int.MaxValue;

    private int _firstRow;

    private int _frame;

    private int _endFrame;

    public NpcGridPanel()
    {
        _animation = new System.Windows.Forms.Timer { Interval = AnimationIntervalMs };
        _animation.Tick += (_, _) => Sample();

        _blink = new System.Windows.Forms.Timer { Interval = BlinkIntervalMs };
        _blink.Tick += (_, _) =>
        {
            _blinkLit = !_blinkLit;

            Invalidate(TipStrip);
        };

        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);

        SetStyle(ControlStyles.Selectable, false);
    }

    public event EventHandler<int>? BoxClicked;

    public event EventHandler? CueChanged;

    public bool ShowingFenceCue => !_labMode && Cue == CueKind.Fence;

    private int _hovered = -1;

    private int LabRowAt(Point point)
    {
        if (!_labMode || _boxes.Count == 0 || _hidden.Count > 0) return -1;

        int top = StatusHeight + LabHeaderHeight;
        if (point.Y < top) return -1;

        int row = (point.Y - top) / LabRowHeight;
        if (row < 0 || row >= Math.Min(VisibleLabRows, _boxes.Count - _firstRow)) return -1;

        return _firstRow + row;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left) return;

        int box = LabRowAt(e.Location);
        if (box >= 0) BoxClicked?.Invoke(this, box);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHovered(LabRowAt(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHovered(-1);
    }

    private void SetHovered(int box)
    {
        if (_hovered == box) return;

        _hovered = box;
        Cursor = box >= 0 ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    private sealed class WheelFilter : IMessageFilter
    {
        private const int WmMouseWheel = 0x020A;

        private readonly NpcGridPanel _panel;

        public WheelFilter(NpcGridPanel panel) => _panel = panel;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WmMouseWheel || !_panel._labMode || !_panel.IsHandleCreated) return false;

            Point at = _panel.PointToClient(Cursor.Position);
            if (!_panel.ClientRectangle.Contains(at)) return false;

            int delta = (short)((long)m.WParam >> 16);
            _panel.ScrollLab(delta > 0 ? -1 : 1);
            return true;
        }
    }

    private WheelFilter? _wheel;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Application.AddMessageFilter(_wheel ??= new WheelFilter(this));
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (_wheel != null) Application.RemoveMessageFilter(_wheel);
        base.OnHandleDestroyed(e);
    }

    private void ScrollLab(int rows)
    {
        int first = Math.Clamp(_firstRow + rows, 0,
            Math.Max(0, _boxes.Count - VisibleLabRows));
        if (first == _firstRow) return;

        _firstRow = first;
        SetHovered(LabRowAt(PointToClient(Cursor.Position)));
        Invalidate();
    }

    public void SetHidden(IReadOnlyList<HiddenMoves> hidden)
    {
        _hidden = hidden;

        if (hidden.Count > 0)
        {
            SetHovered(-1);
            _animation.Stop();
        }

        Invalidate();
    }

    public bool ShowDelayDashes
    {
        get => _showDelayDashes;
        set
        {
            if (_showDelayDashes == value) return;
            _showDelayDashes = value;
            Invalidate();
        }
    }

    private bool _showDelayDashes = true;

    public bool ShowTips
    {
        get => _showTips;
        set
        {
            if (_showTips == value) return;
            _showTips = value;
            SyncBlink();
            Invalidate();
        }
    }

    private bool _showTips = true;

    public void SetStatus(string status, string report)
    {
        _status = status;
        _report = report;
        Invalidate();
    }

    public void SetTip(string tip, bool shiny = false)
    {
        _tip = tip;
        _tipShiny = shiny;

        _blinkLit = true;

        SyncBlink();
        Invalidate();
    }

    private void SyncBlink()
    {
        if (_tipShiny && _showTips && _tip.Length > 0) _blink.Start();
        else _blink.Stop();
    }

    public void SetField(IReadOnlyList<FenceCandidate> candidates, IReadOnlyList<double> likelihoods,
        int focused, int mostLikely, double fps, int anchors, int? oakFrame)
    {
        SetHovered(-1);

        _labMode = false;
        _candidates = candidates;
        _boxes = Array.Empty<LabOption>();
        _likelihoods = likelihoods;
        _focused = focused;
        _mostLikely = mostLikely;
        _firstRow = 0;
        _anchors = anchors;
        _labCueFrame = oakFrame is { } oak ? oak + LabTextCueFrames : int.MaxValue;

        Retime(fps, _labCueFrame == int.MaxValue ? 0 : _labCueFrame);
    }

    public void SetLabField(IReadOnlyList<LabOption> boxes, IReadOnlyList<double> likelihoods,
        int focused, int mostLikely, double fps)
    {
        _labMode = true;
        _boxes = boxes;
        _candidates = Array.Empty<FenceCandidate>();
        _likelihoods = likelihoods;
        _focused = focused;
        _mostLikely = mostLikely;
        ScrollFocusIntoView();

        Retime(fps, _boxes.Count == 0
            ? 0
            : _boxes.Max(b => b.Representative.LabPressFrame + LastEventFrame(b.Representative)));
    }

    private static int LastEventFrame(LabCandidate box)
    {
        int last = 0;
        foreach (NpcEvent e in box.Observable) last = Math.Max(last, e.Frame);
        return last;
    }

    private void ScrollFocusIntoView()
    {
        int rows = VisibleLabRows;
        if (_focused < 0 || rows <= 0) { _firstRow = 0; return; }

        _firstRow = Math.Clamp(_firstRow, Math.Max(0, _focused - rows + 1), _focused);
        _firstRow = Math.Clamp(_firstRow, 0, Math.Max(0, _boxes.Count - rows));
    }

    private int VisibleLabRows => Math.Max(1, (BodyHeight - LabHeaderHeight) / LabRowHeight);

    private int BodyHeight => Math.Max(0, Height - StatusHeight - ControlStripHeight);

    private void Retime(double fps, int endFrame)
    {
        _fps = fps > 0.0 ? fps : 60.0;

        _endFrame = endFrame;
        _frame = Math.Min(CurrentFrame(), _endFrame);

        if (_frame < _endFrame) _animation.Start();
        else _animation.Stop();

        Invalidate();
    }

    public void Sample()
    {
        int frame = Math.Min(CurrentFrame(), _endFrame);
        if (frame == _frame) return;

        CueKind before = Cue;
        _frame = frame;

        if (_labMode || before == CueKind.Fence || Cue != before) Invalidate();

        if (Cue != before) CueChanged?.Invoke(this, EventArgs.Empty);

        if (frame >= _endFrame) _animation.Stop();
    }

    private int CurrentFrame()
    {
        double elapsedMs = Win32.GetTime() - StarterTool.TimerStart;
        return (int)Math.Floor(elapsedMs * _fps / 1000.0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animation.Dispose();
            _blink.Dispose();
            _tipFont?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        using (var background = new SolidBrush(BackColor))
        {
            g.FillRectangle(background, ClientRectangle);
        }

        if (_hidden.Count > 0) PaintHidden(g);
        else if (_labMode) PaintLab(g);
        else PaintCue(g);
    }

    private const int HiddenRowHeight = Assets.NpcFrameHeight + 2;

    private const int HiddenTextX = LadyColumnX + Assets.NpcFrameWidth + 8;

    private void PaintHidden(Graphics g)
    {
        TextRenderer.DrawText(g, _status, Font, new Rectangle(0, 0, Width, StatusHeight), Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak
            | TextFormatFlags.NoPadding);

        var body = new Rectangle(0, StatusHeight, Width, BodyHeight);

        bool complete = true;
        foreach (HiddenMoves hidden in _hidden)
        {
            if (!hidden.Known || hidden.Partial) { complete = false; break; }
        }

        TextRenderer.DrawText(g, complete ? "Undetectable Rolls:" : "Predicted Rolls:", Font,
            new Rectangle(body.X + 4, body.Y, body.Width - 8, LabHeaderHeight / 2),
            Theme.SectionCaption,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);

        int top = body.Y + LabHeaderHeight / 2 + 2;

        using (var rule = new Pen(Theme.GridLine))
        {
            g.DrawLine(rule, body.X, top - 1, body.Right - 1, top - 1);
        }

        int rows = Math.Min(_hidden.Count, Math.Max(0, body.Bottom - top) / HiddenRowHeight);
        for (int row = 0; row < rows; row++)
        {
            DrawHiddenRow(g, new Rectangle(body.X, top + row * HiddenRowHeight, body.Width,
                HiddenRowHeight), _hidden[row]);
        }

        PaintTip(g);
    }

    private const string TipLabel = "Tip:";

    private void PaintTip(Graphics g)
    {
        if (!_showTips || _tip.Length == 0) return;

        Rectangle strip = TipStrip;

        bool lit = _tipShiny && _blinkLit;

        using (var band = new SolidBrush(lit ? Theme.TipShinyBack : Theme.TipBack))
        {
            g.FillRectangle(band, strip);
        }

        using (var rule = new Pen(Theme.TipRule))
        {
            g.DrawLine(rule, strip.X, strip.Y, strip.Right - 1, strip.Y);
        }

        const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

        var label = new Rectangle(strip.X + 4, strip.Y + 1, strip.Width - 8, strip.Height - 1);
        TextRenderer.DrawText(g, TipLabel, BoldFont, label, Theme.SectionCaption, flags);

        int labelWidth = TextRenderer.MeasureText(g, TipLabel, BoldFont, strip.Size, flags).Width;
        var text = new Rectangle(label.X + labelWidth + 5, label.Y,
            Math.Max(0, label.Width - labelWidth - 5), label.Height);

        TextRenderer.DrawText(g, _tip, Font, text, lit ? Theme.TipShinyText : Theme.Text, flags);
    }

    private Rectangle TipStrip => new(0, Math.Max(0, Height - ControlStripHeight), Width,
        Math.Min(Height, ControlStripHeight));

    private Font BoldFont => _tipFont ??= new Font(Font, FontStyle.Bold);

    private Font? _tipFont;

    protected override void OnFontChanged(EventArgs e)
    {
        _tipFont?.Dispose();
        _tipFont = null;
        base.OnFontChanged(e);
    }

    private void DrawHiddenRow(Graphics g, Rectangle row, HiddenMoves hidden)
    {
        Image? sprite = hidden.Npc switch
        {
            NpcId.FatMan => Assets.FatMan(Direction.South, false),
            NpcId.Aide => Assets.Aide(),
            NpcId.ScientistLeft or NpcId.ScientistRight => Assets.Scientist(),
            _ => null,
        };

        if (sprite != null)
        {
            InterpolationMode interpolation = g.InterpolationMode;
            PixelOffsetMode offset = g.PixelOffsetMode;

            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(sprite, new Rectangle(row.X + LadyColumnX, row.Y,
                Assets.NpcFrameWidth, Assets.NpcFrameHeight));
            g.InterpolationMode = interpolation;
            g.PixelOffsetMode = offset;
        }

        var text = new Rectangle(row.X + HiddenTextX, row.Y + Assets.NpcFrameHeight / 2,
            Math.Max(0, row.Width - HiddenTextX - 8), Assets.NpcFrameHeight / 2);

        TextRenderer.DrawText(g, HiddenName(hidden.Npc), Font, text, Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        TextRenderer.DrawText(g, HiddenText(hidden), Font, text,
            hidden.Known && hidden.Total > 0 ? Theme.LandingMaybeText : Theme.DimText,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private static string HiddenName(NpcId npc) => npc switch
    {
        NpcId.Aide => "Lady",
        NpcId.ScientistLeft => "Scientist (left)",
        NpcId.ScientistRight => "Scientist (right)",
        _ => npc.Name(),
    };

    private static string HiddenText(HiddenMoves hidden)
    {
        if (!hidden.Known) return "unknown";

        string window = hidden.Partial ? " (to Oak only)" : "";
        if (hidden.Total == 0) return "none predicted" + window;

        var parts = new List<string>(3);
        if (hidden.OffScreen > 0) parts.Add($"{hidden.OffScreen} off screen");
        if (hidden.Bonks > 0) parts.Add($"{hidden.Bonks} bonk" + (hidden.Bonks == 1 ? "" : "s"));
        if (hidden.SilentTurns > 0) parts.Add($"{hidden.SilentTurns} same-way spin"
            + (hidden.SilentTurns == 1 ? "" : "s"));

        return string.Join(" · ", parts) + window;
    }

    private Rectangle FenceReadout => new(
        GridPixels + Gutter, 0,
        Math.Max(0, Width - GridPixels - Gutter),
        Math.Max(0, Height - ControlStripHeight));

    private enum CueKind
    {
        House,

        OakText,

        Fence,

        LabText,
    }

    public const int LabTextCueFrames = RouteTimeline.LeadWalkFatManFreezeFrames + 400;

    private CueKind Cue => _anchors switch
    {
        0 => CueKind.House,
        1 => CueKind.OakText,
        2 => _frame >= _labCueFrame ? CueKind.LabText : CueKind.Fence,
        _ => CueKind.LabText,
    };

    private const string OakCueText = "I know!\nHere, come with me!";

    private const string LabCueText = "OAK: Be patient, RIVAL.\nYou can have one, too!";

    private void PaintCue(Graphics g)
    {
        switch (Cue)
        {
            case CueKind.House: PaintHouseCue(g); break;
            case CueKind.Fence: PaintFenceCue(g); break;
            default: PaintTextCue(g); break;
        }
    }

    private void PaintFenceCue(Graphics g)
    {
        var grid = new Rectangle(0, 0, GridPixels, GridPixels);
        DrawFence(g, grid);

        Rectangle readout = FenceReadout;
        TextRenderer.DrawText(g, _status, Font,
            new Rectangle(readout.X, readout.Y, readout.Width, StatusHeight), Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak
            | TextFormatFlags.NoPadding);

        DrawFenceReadout(g, new Rectangle(readout.X, readout.Y + StatusHeight + 4, readout.Width,
            Math.Max(0, readout.Height - StatusHeight - 4)), compact: false);
    }

    private void PaintHouseCue(Graphics g)
    {
        var grid = new Rectangle(0, 0, GridPixels, GridPixels);
        DrawHouse(g, grid);

        Rectangle readout = FenceReadout;
        TextRenderer.DrawText(g, _status, Font,
            new Rectangle(readout.X, readout.Y, readout.Width, StatusHeight), Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak
            | TextFormatFlags.NoPadding);

        DrawFenceReadout(g, new Rectangle(readout.X, readout.Y + StatusHeight + 4, readout.Width,
            Math.Max(0, readout.Height - StatusHeight - 4)), compact: false);
    }

    private void PaintTextCue(Graphics g)
    {
        TextRenderer.DrawText(g, _status, Font, new Rectangle(0, 0, Width, StatusHeight), Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak
            | TextFormatFlags.NoPadding);

        bool oak = Cue == CueKind.OakText;
        var box = new Rectangle(0, StatusHeight,
            TextBoxWidth * TextBoxScale, TextBoxHeight * TextBoxScale);

        DrawTextBox(g, box, oak ? OakCueText : LabCueText, spent: oak && _anchors >= 2);

        DrawFenceReadout(g, new Rectangle(0, box.Bottom + 4, Width,
            Math.Max(0, Height - ControlStripHeight - box.Bottom - 4)), compact: true);
    }

    private void DrawFenceReadout(Graphics g, Rectangle readout, bool compact)
    {
        if (_candidates.Count == 0)
        {
            TextRenderer.DrawText(g, "Press the Anchor button/timer hotkey to track context",
                Font, readout, Theme.DimText,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak
                | TextFormatFlags.NoPadding);
            return;
        }

        int shown = _mostLikely >= 0 && _mostLikely < _candidates.Count ? _mostLikely : 0;
        FenceCandidate candidate = _candidates[shown];

        int y = readout.Y;
        string frames = string.Format(CultureInfo.InvariantCulture, "exit {0} · Oak {1}",
            candidate.ExitFrame, candidate.OakFrame);

        if (!compact)
        {
            Line("Most likely", frames);
            Line("Advances", candidate.TotalAdvances.ToString(CultureInfo.InvariantCulture));
        }

        if (compact || shown < _likelihoods.Count)
        {
            var bar = new Rectangle(readout.X, y, Math.Min(readout.Width, 180), 18);
            if (shown < _likelihoods.Count) DrawChance(g, bar, _likelihoods[shown], leader: true);

            if (compact)
            {
                TextRenderer.DrawText(g, frames, Font,
                    new Rectangle(bar.Right + 8, y, Math.Max(0, readout.Right - bar.Right - 8), 18),
                    Theme.DimText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.NoPadding);
            }

            y = bar.Bottom + (compact ? 2 : 6);
        }

        if (_report.Length > 0)
        {
            TextRenderer.DrawText(g, _report, Font,
                new Rectangle(readout.X, y, readout.Width, readout.Bottom - y), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak
                | TextFormatFlags.NoPadding);
        }

        void Line(string caption, string value)
        {
            var row = new Rectangle(readout.X, y, readout.Width, 18);
            TextRenderer.DrawText(g, caption, Font, row, Theme.DimText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, value, Font, row, Theme.Text,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            y = row.Bottom + 2;
        }
    }

    private void PaintLab(Graphics g)
    {
        TextRenderer.DrawText(g, _status, Font, new Rectangle(0, 0, Width, StatusHeight), Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak
            | TextFormatFlags.NoPadding);

        var body = new Rectangle(0, StatusHeight, Width, BodyHeight);

        if (_boxes.Count == 0)
        {
            TextRenderer.DrawText(g, "No boxes - the lab anchor has not been taken.", Font, body,
                Theme.DimText,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);
            return;
        }

        int target = (int)(StarterTool.VariableOffset?.Info.Frame ?? 0u);
        int manual = StarterTool.Context.Lab?.ManualAdvances ?? 0;

        DrawLabHeader(g, new Rectangle(body.X, body.Y, body.Width, LabHeaderHeight));

        int rows = Math.Min(VisibleLabRows, _boxes.Count - _firstRow);
        int top = body.Y + LabHeaderHeight;

        for (int row = 0; row < rows; row++)
        {
            int ordinal = _firstRow + row;
            DrawLabRow(g,
                new Rectangle(body.X, top + row * LabRowHeight, body.Width, LabRowHeight),
                _boxes[ordinal], ordinal, target, manual);
        }

        using var divider = new Pen(Theme.GridLine);
        int split = body.X + LadyColumnX + HalfWidth(body.Width);
        g.DrawLine(divider, split, body.Y + 2, split, top + rows * LabRowHeight - 2);
    }

    private const int LadyColumnX = 26;

    private const int ScientistColumnFraction = 2;

    private static int HalfWidth(int width) =>
        (width - LadyColumnX - RightColumnWidth) / ScientistColumnFraction;

    private void DrawLabHeader(Graphics g, Rectangle header)
    {
        int half = HalfWidth(header.Width);

        Column(header.X + LadyColumnX, Assets.Aide(), "Lady");
        Column(header.X + LadyColumnX + half, Assets.Scientist(), "Scientist");

        DrawOffScreenBoxes(g, header);

        using var rule = new Pen(Theme.GridLine);
        g.DrawLine(rule, header.X, header.Bottom - 1, header.Right - 1, header.Bottom - 1);

        void Column(int x, Image? sprite, string name)
        {
            if (sprite != null)
            {
                InterpolationMode interpolation = g.InterpolationMode;
                PixelOffsetMode offset = g.PixelOffsetMode;

                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(sprite, new Rectangle(x, header.Y,
                    Assets.NpcFrameWidth, Assets.NpcFrameHeight));
                g.InterpolationMode = interpolation;
                g.PixelOffsetMode = offset;
            }

            TextRenderer.DrawText(g, name, Font,
                new Rectangle(x + Assets.NpcFrameWidth + 6, header.Y + Assets.NpcFrameHeight / 2,
                    Math.Max(0, half - Assets.NpcFrameWidth - 6), Assets.NpcFrameHeight / 2),
                Theme.SectionCaption,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    private void DrawOffScreenBoxes(Graphics g, Rectangle header)
    {
        int above = _firstRow;
        int below = Math.Max(0, _boxes.Count - _firstRow - VisibleLabRows);
        if (above == 0 && below == 0) return;

        string caption = above > 0 && below > 0
            ? string.Format(CultureInfo.InvariantCulture, "▲ {0}   ▼ {1} off screen", above, below)
            : above > 0
                ? string.Format(CultureInfo.InvariantCulture, "▲ {0} off screen", above)
                : string.Format(CultureInfo.InvariantCulture, "▼ {0} off screen", below);

        Size text = TextRenderer.MeasureText(g, caption, BoldFont);
        var badge = new Rectangle(
            Math.Max(header.X, header.Right - text.Width - 14),
            header.Y + (header.Height - 1 - text.Height - 4) / 2,
            Math.Min(header.Width, text.Width + 10),
            text.Height + 4);

        using (var fill = new SolidBrush(Theme.Accent))
        {
            g.FillRectangle(fill, badge);
        }

        TextRenderer.DrawText(g, caption, BoldFont, badge, Theme.AccentText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPadding);
    }

    private void DrawLabRow(Graphics g, Rectangle row, LabOption box, int ordinal, int target,
        int manual)
    {
        bool focused = ordinal == _focused;
        LabCandidate shown = box.Representative;

        if (focused)
        {
            using var fill = new SolidBrush(Theme.NpcRowFocus);
            g.FillRectangle(fill, row);

            using var edge = new Pen(Theme.Accent);
            g.DrawRectangle(edge, row.X, row.Y, row.Width - 1, row.Height - 1);
        }
        else if (ordinal == _hovered)
        {
            using var edge = new Pen(Theme.Border);
            g.DrawRectangle(edge, row.X, row.Y, row.Width - 1, row.Height - 1);
        }

        Color ink = focused ? Theme.Text : Theme.DimText;

        TextRenderer.DrawText(g, (ordinal + 1).ToString(CultureInfo.InvariantCulture), Font,
            new Rectangle(row.X + 4, row.Y, 18, row.Height), ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        int frame = _frame - shown.LabPressFrame;

        int half = HalfWidth(row.Width);
        DrawNpcStrip(g, new Rectangle(row.X + LadyColumnX, row.Y, half, row.Height),
            shown.Aide, frame, shown);
        DrawNpcStrip(g, new Rectangle(row.X + LadyColumnX + half, row.Y, half, row.Height),
            shown.Scientist, frame, shown);

        (int min, int max) = box.CorrectionSpan(target, manual);
        string correction = min == max
            ? string.Format(CultureInfo.InvariantCulture, "{0:+#;-#;+0}", min)
            : string.Format(CultureInfo.InvariantCulture, "{0:+#;-#;+0}..{1:+#;-#;+0}", min, max);

        if (shown.StreamShift != 0) correction += "~";

        Color correctionInk = focused
            ? min == max && shown.StreamShift == 0 ? Theme.LandingHitText : Theme.LandingMaybeText
            : ink;

        var right = new Rectangle(row.Right - RightColumnWidth, row.Y, RightColumnWidth, row.Height);
        TextRenderer.DrawText(g, correction, Font,
            new Rectangle(right.X, right.Y, CorrectionWidth, right.Height), correctionInk,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        if (ordinal < _likelihoods.Count)
        {
            DrawChance(g,
                new Rectangle(right.X + CorrectionWidth + 8, right.Y + 4,
                    right.Width - CorrectionWidth - 8, row.Height - 8),
                _likelihoods[ordinal], ordinal == _mostLikely);
        }
    }

    private const int RightColumnWidth = 132;

    private const int CorrectionWidth = 56;

    private const int GlyphWidth = 18;

    private const int StripPad = 5;

    private void DrawNpcStrip(Graphics g, Rectangle cell,
        IReadOnlyList<NpcEvent> events, int frame, LabCandidate box)
    {
        var strip = new Rectangle(cell.X + StripPad, cell.Y, cell.Width - StripPad, cell.Height);

        if (events.Count == 0)
        {
            TextRenderer.DrawText(g, "nothing", Font, strip, Theme.DimText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            return;
        }

        int room = Math.Max(1, strip.Width / GlyphWidth);
        int slot = 0;
        int shown = 0;

        Rectangle Cell(int at) =>
            new(strip.X + at * GlyphWidth, strip.Y, GlyphWidth, strip.Height);

        for (int i = 0; i < events.Count && slot < room; i++)
        {
            int at = _showDelayDashes ? Math.Max(slot, SlotOf(events[i].Frame)) : slot;

            for (; slot < at && slot < room; slot++)
            {
                DrawQuietGlyph(g, Cell(slot), IntervalEnd(slot, box) <= frame);
            }

            if (slot >= room) break;

            DrawEventGlyph(g, Cell(slot), events[i], events[i].Frame <= frame,
                box.Completes(events[i]));
            slot++;
            shown++;
        }

        if (shown < events.Count)
        {
            TextRenderer.DrawText(g, "…", Font,
                new Rectangle(strip.X + room * GlyphWidth - GlyphWidth / 2, strip.Y, GlyphWidth,
                    strip.Height),
                Theme.DimText,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    private const int QuietIntervalFrames = MovementStrip.QuietIntervalFrames;

    private static int SlotOf(int frame) => MovementStrip.SlotOf(frame);

    private static int IntervalEnd(int slot, LabCandidate box) =>
        Math.Min((slot + 1) * QuietIntervalFrames, box.ObservableFrames);

    private static void DrawQuietGlyph(Graphics g, Rectangle cell, bool passed)
    {
        Color ink = passed ? Theme.DimText : Color.FromArgb(0x60, Theme.DimText);

        float cx = cell.X + cell.Width / 2f;
        float cy = cell.Y + cell.Height / 2f;
        float arm = cell.Width * 0.26f;

        using var pen = new Pen(ink, 1.4f);
        g.DrawLine(pen, cx - arm, cy, cx + arm, cy);
    }

    private static void DrawEventGlyph(Graphics g, Rectangle cell, NpcEvent e, bool started,
        bool completed)
    {
        float cx = cell.X + cell.Width / 2f;
        float cy = cell.Y + cell.Height / 2f;

        int dx = 0, dy = 0;
        Directions.MoveCoords(e.Direction, ref dx, ref dy);

        float arm = cell.Width * 0.32f;
        var tip = new PointF(cx + dx * arm, cy + dy * arm);
        var back = new PointF(cx - dx * arm * 0.5f, cy - dy * arm * 0.5f);
        var barbs = new[]
        {
            tip,
            new PointF(back.X + dy * arm * 0.8f, back.Y + dx * arm * 0.8f),
            new PointF(back.X - dy * arm * 0.8f, back.Y - dx * arm * 0.8f),
        };

        Color ink = started ? Theme.SectionCaption : Color.FromArgb(0x60, Theme.SectionCaption);

        SmoothingMode smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (completed)
        {
            using var brush = new SolidBrush(ink);
            g.FillPolygon(brush, barbs);
        }
        else
        {
            using var pen = new Pen(ink, 1.4f);
            g.DrawPolygon(pen, barbs);
        }

        g.SmoothingMode = smoothing;
    }

    private void DrawChance(Graphics g, Rectangle row, double chance, bool leader)
    {
        Color ink = leader ? Theme.LandingHitText : Theme.LandingMaybeText;
        Color fill = leader ? Theme.LandingHitBack : Theme.LandingMaybeBack;

        var track = new Rectangle(row.X, row.Bottom - 3, row.Width, 3);
        using (var background = new SolidBrush(Theme.GridLine))
        {
            g.FillRectangle(background, track);
        }

        int filled = (int)Math.Round(chance * track.Width);
        if (filled > 0)
        {
            using var bar = new SolidBrush(fill);
            g.FillRectangle(bar, track.X, track.Y, filled, track.Height);
        }

        int line = Math.Max(row.Height - 4, Font.Height);

        TextRenderer.DrawText(g,
            (chance * 100.0).ToString("0", CultureInfo.InvariantCulture) + "%", Font,
            new Rectangle(row.X, track.Y - line, row.Width, line), ink,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private static readonly Color TileLattice = Color.FromArgb(0x38, 0x00, 0x00, 0x00);

    private static void DrawHouse(Graphics g, Rectangle grid)
    {
        int left = ViewX - GridTiles / 2;
        int top = ViewY - GridTiles / 2;

        using (var offMap = new SolidBrush(Theme.NpcTileBlocked))
        {
            g.FillRectangle(offMap, grid);
        }

        DrawMapArt(g, grid, Assets.PlayersHouseMap, left, top);

        using (var lattice = new Pen(TileLattice))
        {
            for (int row = 0; row < GridTiles; row++)
            {
                for (int column = 0; column < GridTiles; column++)
                {
                    g.DrawRectangle(lattice,
                        grid.X + column * TileSize, grid.Y + row * TileSize,
                        TileSize - 1, TileSize - 1);
                }
            }
        }

        DrawSprite(g, grid, Assets.Player(), PlayerX - left, PlayerY - top);
        DrawDoorArrow(g, grid, PlayerX - left, PlayerY + 1 - top);
    }

    private void DrawFence(Graphics g, Rectangle grid)
    {
        int left = FatManHomeX - GridTiles / 2;
        int top = FatManHomeY - GridTiles / 2;

        using (var offMap = new SolidBrush(Theme.NpcTileBlocked))
        {
            g.FillRectangle(offMap, grid);
        }

        DrawMapArt(g, grid, Assets.PalletTownMap, left, top);
        DrawTerrainVeil(g, grid, left, top);
        DrawLeash(g, grid, left, top);

        using (var lattice = new Pen(TileLattice))
        {
            for (int row = 0; row < GridTiles; row++)
            {
                for (int column = 0; column < GridTiles; column++)
                {
                    g.DrawRectangle(lattice,
                        grid.X + column * TileSize, grid.Y + row * TileSize,
                        TileSize - 1, TileSize - 1);
                }
            }
        }

        if (FocusedCandidate is not { } candidate) return;
        if (candidate.Motion.Count == 0) return;

        int index = Math.Clamp(_frame - candidate.LeadWalkStartFrame, 0, candidate.Motion.Count - 1);
        FenceFrameState state = candidate.Motion[index];

        float back = state.Walking
            ? 1f - (float)state.WalkStep / ObjectEventSim.NormalWalkFrames
            : 0f;

        int dx = 0, dy = 0;
        if (state.Walking) Directions.MoveCoords(state.Facing, ref dx, ref dy);

        bool offScreen = _frame < candidate.LeadWalkVisibleFrame;

        Region clip = g.Clip;
        g.IntersectClip(grid);

        DrawSprite(g, grid, Assets.FatMan(state.Facing, state.Walking),
            state.X - left - dx * back, state.Y - top - dy * back,
            offScreen ? OffScreenOpacity : 1f);

        if (state.Walking) DrawStepArrow(g, grid, state.X - left, state.Y - top, state.Facing);

        g.Clip = clip;

        if (offScreen) DrawOffScreenCaption(g, grid);
    }

    private const float OffScreenOpacity = 0.35f;

    private void DrawOffScreenCaption(Graphics g, Rectangle grid)
    {
        const string Caption = "off screen";

        Size text = TextRenderer.MeasureText(g, Caption, Font, grid.Size, TextFormatFlags.NoPadding);
        var pill = new Rectangle(
            grid.X + (grid.Width - text.Width) / 2 - 6,
            grid.Bottom - text.Height - 10,
            text.Width + 12,
            text.Height + 4);

        using (var back = new SolidBrush(CaptionBack))
        {
            g.FillRectangle(back, pill);
        }

        TextRenderer.DrawText(g, Caption, Font, pill, CaptionInk,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPadding);
    }

    private static readonly Color CaptionBack = Color.FromArgb(0xC8, 0x0B, 0x10, 0x16);

    private static readonly Color CaptionInk = Color.FromArgb(0xF2, 0xF4, 0xF7);

    private FenceCandidate? FocusedCandidate
    {
        get
        {
            int at = _focused >= 0 ? _focused : _mostLikely;
            return at >= 0 && at < _candidates.Count ? _candidates[at] : null;
        }
    }

    private static void DrawTerrainVeil(Graphics g, Rectangle grid, int left, int top)
    {
        GameMap map = GameMap.PalletTown;
        using var veil = new SolidBrush(TerrainVeil);

        for (int row = 0; row < GridTiles; row++)
        {
            for (int column = 0; column < GridTiles; column++)
            {
                if (!map.IsTerrainBlockedFor(MapObjects.Elevation, left + column, top + row)) continue;

                g.FillRectangle(veil,
                    grid.X + column * TileSize, grid.Y + row * TileSize, TileSize, TileSize);
            }
        }
    }

    private static void DrawLeash(Graphics g, Rectangle grid, int left, int top)
    {
        int x0 = FatManHomeX - LeashRangeX, x1 = FatManHomeX + LeashRangeX;
        int y0 = FatManHomeY - LeashRangeY, y1 = FatManHomeY + LeashRangeY;

        using var pen = new Pen(LeashOutline) { DashStyle = DashStyle.Dot };

        int px0 = grid.X + (x0 - left) * TileSize;
        int px1 = grid.X + (x1 + 1 - left) * TileSize;
        int py0 = grid.Y + (y0 - top) * TileSize;
        int py1 = grid.Y + (y1 + 1 - top) * TileSize;

        if (py0 >= grid.Top && py0 <= grid.Bottom) g.DrawLine(pen, px0, py0, px1, py0);
        if (py1 >= grid.Top && py1 <= grid.Bottom) g.DrawLine(pen, px0, py1, px1, py1);
        if (px0 >= grid.Left && px0 <= grid.Right) g.DrawLine(pen, px0, py0, px0, py1);
        if (px1 >= grid.Left && px1 <= grid.Right) g.DrawLine(pen, px1, py0, px1, py1);
    }

    private const int FatManHomeX = 13;

    private const int FatManHomeY = 17;

    private const int LeashRangeX = 6;

    private const int LeashRangeY = 2;

    private static readonly Color TerrainVeil = Color.FromArgb(0x8C, 0x0B, 0x10, 0x16);

    private static readonly Color LeashOutline = Color.FromArgb(0xB0, 0xF2, 0xF4, 0xF7);

    private static void DrawStepArrow(Graphics g, Rectangle grid, int column, int row,
        Direction direction)
    {
        float cx = grid.X + (column + 0.5f) * TileSize;
        float cy = grid.Y + (row + 0.5f) * TileSize;
        const float Arm = TileSize * 0.3f;

        int dx = 0, dy = 0;
        Directions.MoveCoords(direction, ref dx, ref dy);

        var barbs = new[]
        {
            new PointF(cx + dx * Arm, cy + dy * Arm),
            new PointF(cx - dx * Arm * 0.6f + dy * Arm * 0.8f,
                cy - dy * Arm * 0.6f + dx * Arm * 0.8f),
            new PointF(cx - dx * Arm * 0.6f - dy * Arm * 0.8f,
                cy - dy * Arm * 0.6f - dx * Arm * 0.8f),
        };

        SmoothingMode smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var brush = new SolidBrush(Theme.NpcStepArrow))
        {
            g.FillPolygon(brush, barbs);
        }
        g.SmoothingMode = smoothing;
    }

    private static void DrawMapArt(Graphics g, Rectangle grid, Bitmap? art, int left, int top)
    {
        if (art == null) return;

        int width = art.Width / Assets.MapTileSize;
        int height = art.Height / Assets.MapTileSize;

        int x0 = Math.Max(left, 0), y0 = Math.Max(top, 0);
        int x1 = Math.Min(left + GridTiles, width), y1 = Math.Min(top + GridTiles, height);
        if (x1 <= x0 || y1 <= y0) return;

        var source = new Rectangle(
            x0 * Assets.MapTileSize, y0 * Assets.MapTileSize,
            (x1 - x0) * Assets.MapTileSize, (y1 - y0) * Assets.MapTileSize);
        var destination = new Rectangle(
            grid.X + (x0 - left) * TileSize, grid.Y + (y0 - top) * TileSize,
            (x1 - x0) * TileSize, (y1 - y0) * TileSize);

        InterpolationMode interpolation = g.InterpolationMode;
        PixelOffsetMode offset = g.PixelOffsetMode;

        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(art, destination, source, GraphicsUnit.Pixel);
        g.InterpolationMode = interpolation;
        g.PixelOffsetMode = offset;
    }

    private static void DrawSprite(Graphics g, Rectangle grid, Image? sprite, float column, float row,
        float opacity = 1f)
    {
        if (sprite == null) return;

        float scale = (float)TileSize / Assets.NpcFrameWidth;
        var bounds = new RectangleF(
            grid.X + column * TileSize,
            grid.Y + row * TileSize - Assets.NpcFrameHeight * scale + TileSize,
            Assets.NpcFrameWidth * scale,
            Assets.NpcFrameHeight * scale);

        InterpolationMode interpolation = g.InterpolationMode;
        PixelOffsetMode offset = g.PixelOffsetMode;

        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        if (opacity >= 1f)
        {
            g.DrawImage(sprite, bounds);
        }
        else
        {
            var matrix = new ColorMatrix { Matrix33 = opacity };
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix);

            g.DrawImage(sprite,
                new Rectangle((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height),
                0, 0, sprite.Width, sprite.Height, GraphicsUnit.Pixel, attributes);
        }

        g.InterpolationMode = interpolation;
        g.PixelOffsetMode = offset;
    }

    private static void DrawDoorArrow(Graphics g, Rectangle grid, int column, int row)
    {
        float cx = grid.X + (column + 0.5f) * TileSize;
        float cy = grid.Y + (row + 0.5f) * TileSize;

        const float Arm = TileSize * 0.42f;

        var barbs = new[]
        {
            new PointF(cx, cy + Arm),
            new PointF(cx + Arm * 0.7f, cy - Arm * 0.4f),
            new PointF(cx - Arm * 0.7f, cy - Arm * 0.4f),
        };

        SmoothingMode smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var brush = new SolidBrush(Theme.NpcStepArrow))
        using (var edge = new Pen(ArrowOutline, 1.5f))
        {
            g.FillPolygon(brush, barbs);
            g.DrawPolygon(edge, barbs);
        }
        g.SmoothingMode = smoothing;
    }

    private static readonly Color ArrowOutline = Color.FromArgb(0xE0, 0x0B, 0x10, 0x16);

    private const int TextBoxWidth = 240;

    private const int TextBoxHeight = 48;

    private const int TextBoxScale = 2;

    private const int TextOriginX = 16;

    private const int TextOriginY = 9;

    private void DrawTextBox(Graphics g, Rectangle box, string text, bool spent)
    {
        Bitmap? art = Assets.TextBox;
        if (art == null) return;

        InterpolationMode interpolation = g.InterpolationMode;
        PixelOffsetMode offset = g.PixelOffsetMode;

        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(art, box);
        g.InterpolationMode = interpolation;
        g.PixelOffsetMode = offset;

        GameText.Draw(g, text,
            new Point(box.X + TextOriginX * TextBoxScale, box.Y + TextOriginY * TextBoxScale),
            TextBoxScale);

        if (!spent) return;

        using var veil = new SolidBrush(Color.FromArgb(0xA0, BackColor));
        g.FillRectangle(veil, box);
    }
}
