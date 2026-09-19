using System.Drawing.Drawing2D;
using System.Globalization;
using FRLG.StarterTool.App.Capture;
using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Video;

namespace FRLG.StarterTool.App;

internal sealed class CaptureViewPanel : Control
{
    private const int Gap = 4;

    private enum StripAction { RefBack, RefForward, Back, Forward, SaveView, SaveReference, ClearAll, EditDelay, Close }

    private readonly Dictionary<int, Bitmap> _bitmaps = new();
    private readonly List<(Rectangle Bounds, StripAction Action)> _buttons = new();
    private readonly List<(Bitmap Image, int Frames)> _references = new();
    private CaptureResult? _result;
    private int _step;
    private int _reference = -1;
    private ViewState _view = ViewState.Fit;
    private StripAction? _hot;
    private Point _dragFrom;
    private bool _dragging;
    private WheelFilter? _wheel;
    private string? _notice;
    private int _rightEdge;

    public CaptureViewPanel()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
        SetStyle(ControlStyles.Selectable, false);
    }

    public event Action<CapturedFrame, ViewState, int>? SaveReferenceRequested;

    public event Action<ViewState>? SaveViewRequested;

    public event EventHandler? ClearRequested;

    public event Action<int>? EditDelayRequested;

    public event EventHandler? CloseRequested;

    public bool HasResult => _result != null;

    public string? RouteName => _result?.RouteName;

    public int PressFramesOff()
    {
        if (_result is not { Pressed: true, FrameMs: > 0 } result) return 0;
        return (int)Math.Round((result.PressMs - result.DueMs) / result.FrameMs);
    }

    public bool HasReferences => _references.Count > 0;

    public void Show(CaptureResult result, IEnumerable<(Bitmap Image, int Frames)> references, ViewState view)
    {
        Clear();
        _result = result;
        _step = 0;
        SetReferences(references, null);
        _view = view.Normalize();
        ClampView();
        Invalidate();
    }

    public void SetReferences(IEnumerable<(Bitmap Image, int Frames)> references, int? select)
    {
        foreach ((Bitmap image, _) in _references) image.Dispose();
        _references.Clear();
        _references.AddRange(references.OrderBy(reference => reference.Frames));

        _reference = -1;
        if (select is int frames) _reference = _references.FindIndex(reference => reference.Frames == frames);
        if (_reference < 0 && _references.Count > 0)
        {
            _reference = _references
                .Select((reference, i) => (Distance: Math.Abs(reference.Frames), Index: i))
                .OrderBy(pair => pair.Distance)
                .First().Index;
        }
        Invalidate();
    }

    public void Notice(string text)
    {
        _notice = text;
        Invalidate();
    }

    public void Clear()
    {
        _notice = null;
        foreach (Bitmap bitmap in _bitmaps.Values) bitmap.Dispose();
        _bitmaps.Clear();
        foreach ((Bitmap image, _) in _references) image.Dispose();
        _references.Clear();
        _reference = -1;
        _result?.ReleaseFrames?.Invoke();
        _result = null;
        _step = 0;
        Invalidate();
    }

    private int StripHeight => Font.Height + 10;

    private int HeaderHeight => Font.Height + 8;

    private Rectangle PaneBounds(int position)
    {
        int height = Math.Max(1, Height - StripHeight - Gap);
        int width = (Width - Gap) / 2;
        return new Rectangle(position * (width + Gap), 0, width, height);
    }

    private Rectangle PictureBounds(int position)
    {
        Rectangle pane = PaneBounds(position);
        int header = HeaderHeight;
        return new Rectangle(pane.X, pane.Y + header, pane.Width, Math.Max(1, pane.Height - header));
    }

    private Bitmap? ReferenceImage => _reference >= 0 && _reference < _references.Count ? _references[_reference].Image : null;

    private double SlotMs(int step) =>
        _result == null ? 0 : CaptureSession.TargetSlotMs(_result) + step * _result.FrameMs;

    private int IndexOf(int step) => _result == null ? -1 : FrameRing.IndexAt(_result.Frames, SlotMs(step));

    private int Index => IndexOf(_step);

    private int DelayAt(int step) => _result == null ? 0 : (int)Math.Round(SlotMs(step) - _result.PressMs);

    private bool Repeated => Index >= 0 && (IndexOf(_step - 1) == Index || IndexOf(_step + 1) == Index);

    private Bitmap? FrameImage
    {
        get
        {
            int index = Index;
            if (_result == null || index < 0) return null;
            if (!_bitmaps.TryGetValue(index, out Bitmap? bitmap))
            {
                foreach (Bitmap old in _bitmaps.Values) old.Dispose();
                _bitmaps.Clear();
                bitmap = CaptureSession.ToBitmap(_result.Frames[index]);
                _bitmaps[index] = bitmap;
            }
            return bitmap;
        }
    }

    private Bitmap? ImageAt(int position) => position == 0 ? ReferenceImage : FrameImage;

    private RectangleF SourceOf(Rectangle pane, Size image, out float scale)
    {
        float fit = Math.Min(pane.Width / (float)image.Width, pane.Height / (float)image.Height);
        scale = fit * (float)_view.Zoom;
        float width = pane.Width / scale;
        float height = pane.Height / scale;
        return new RectangleF(
            (float)(_view.CenterX * image.Width) - width / 2f,
            (float)(_view.CenterY * image.Height) - height / 2f,
            width, height);
    }

    private void ClampView()
    {
        Bitmap? image = FrameImage ?? ReferenceImage;
        if (image == null) return;
        RectangleF source = SourceOf(PictureBounds(1), image.Size, out _);
        _view = _view.Clamp(source.Width / image.Width, source.Height / image.Height);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ClampView();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Window);
        _buttons.Clear();

        PaintPane(g, 0);
        PaintPane(g, 1);
        PaintStrip(g);
    }

    private void PaintPane(Graphics g, int position)
    {
        Rectangle picture = PictureBounds(position);
        using (var back = new SolidBrush(Theme.ListBack)) g.FillRectangle(back, picture);

        Bitmap? image = ImageAt(position);
        if (image != null)
        {
            RectangleF source = SourceOf(picture, image.Size, out float scale);

            RectangleF clipped = RectangleF.Intersect(source, new RectangleF(0, 0, image.Width, image.Height));
            if (!clipped.IsEmpty)
            {
                var dest = new RectangleF(
                    picture.X + (clipped.X - source.X) * scale,
                    picture.Y + (clipped.Y - source.Y) * scale,
                    clipped.Width * scale,
                    clipped.Height * scale);

                GraphicsState state = g.Save();
                g.SetClip(picture);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(image, dest, clipped, GraphicsUnit.Pixel);
                g.Restore(state);
            }
        }

        bool target = position == 1 && _result is { TargetIndex: >= 0 } && _step == 0;
        using (var pen = new Pen(target ? Theme.Accent : Theme.Border, target ? 2f : 1f))
        {
            g.DrawRectangle(pen, picture.X, picture.Y, picture.Width - 1, picture.Height - 1);
        }

        Rectangle pane = PaneBounds(position);
        var header = new Rectangle(pane.X, pane.Y, pane.Width, HeaderHeight - 2);
        bool canStep = position == 0 ? _references.Count > 1 : _result is { Frames.Count: > 1 };
        Rectangle backButton = DrawButton(g, "◀", position == 0 ? StripAction.RefBack : StripAction.Back,
            new Rectangle(header.X, header.Y, header.Height * 2, header.Height), enabled: canStep);
        Rectangle forward = DrawButton(g, "▶", position == 0 ? StripAction.RefForward : StripAction.Forward,
            new Rectangle(header.Right - header.Height * 2, header.Y, header.Height * 2, header.Height), enabled: canStep);
        var caption = Rectangle.FromLTRB(backButton.Right + Gap, header.Y, forward.Left - Gap, header.Bottom);
        TextRenderer.DrawText(g, position == 0 ? ReferenceCaption() : FrameCaption(), Font, caption,
            target ? Theme.SectionCaption : Theme.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private string ReferenceCaption()
    {
        if (_reference < 0) return "No reference";
        string text = "Reference: " + CaptureReference.Describe(_references[_reference].Frames);
        return _references.Count > 1 ? $"{text}  ({_reference + 1}/{_references.Count})" : text;
    }

    private string FrameCaption()
    {
        if (_result == null) return "";
        if (Index < 0) return "No frames were captured";

        string name = _step == 0 ? "Target" : "Target " + _step.ToString("+0;-0", CultureInfo.InvariantCulture);
        double ms = SlotMs(_step) - _result.PressMs;
        return string.Format(CultureInfo.InvariantCulture, "{0}  ·  {1:+0;-0} ms{2}", name, ms, Repeated ? "  ·  repeated" : "");
    }

    private Rectangle DrawButton(Graphics g, string text, StripAction action, Rectangle bounds, bool lit = false, bool enabled = true)
    {
        Color back = lit ? Theme.Accent : enabled && _hot == action ? Theme.Hover : Theme.Surface;
        using (var brush = new SolidBrush(back)) g.FillRectangle(brush, bounds);
        using (var pen = new Pen(Theme.Border)) g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        TextRenderer.DrawText(g, text, Font, bounds,
            lit ? Theme.AccentText : enabled ? Theme.Text : Theme.DimText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (enabled) _buttons.Add((bounds, action));
        return bounds;
    }

    private void PaintStrip(Graphics g)
    {
        int top = Height - StripHeight;
        int height = StripHeight;

        void Button(string text, StripAction action, bool lit = false)
        {
            int width = TextRenderer.MeasureText(text, Font).Width + 12;
            _rightEdge -= width;
            DrawButton(g, text, action, new Rectangle(_rightEdge, top, width, height), lit);
            _rightEdge -= Gap;
        }

        _rightEdge = Width;
        Button("✕", StripAction.Close);
        if (HasReferences) Button("Clear", StripAction.ClearAll);
        Button("Delay", StripAction.EditDelay);
        Button("Save Ref", StripAction.SaveReference);
        Button("Save View", StripAction.SaveView);

        var statusBounds = new Rectangle(2, top, Math.Max(0, _rightEdge - 2), height);
        TextRenderer.DrawText(g, StatusText(), Font, statusBounds, Theme.DimText,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private string StatusText()
    {
        if (_notice != null) return _notice;
        if (_result == null) return "";
        if (_result.Frames.Count == 0) return "No frames were captured";

        string delay = _result.Calibration
            ? $"delay {DelayAt(_step)} ms (proposed)"
            : $"delay {_result.DelayMs} ms";
        string note = _result.Note.Length > 0 ? _result.Note + " · " : "";
        return $"{note}{delay} · capture {Index + 1}/{_result.Frames.Count}";
    }

    private StripAction? ButtonAt(Point at)
    {
        foreach ((Rectangle bounds, StripAction action) in _buttons)
        {
            if (bounds.Contains(at)) return action;
        }
        return null;
    }

    private int PictureAt(Point at, out Rectangle picture)
    {
        for (int position = 0; position < 2; position++)
        {
            picture = PictureBounds(position);
            if (picture.Contains(at)) return position;
        }
        picture = Rectangle.Empty;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_dragging)
        {
            Bitmap? image = FrameImage ?? ReferenceImage;
            if (image != null)
            {
                SourceOf(PictureBounds(1), image.Size, out float scale);
                _view = _view.Pan(
                    -(e.X - _dragFrom.X) / (scale * image.Width),
                    -(e.Y - _dragFrom.Y) / (scale * image.Height));
                ClampView();
                _dragFrom = e.Location;
                Invalidate();
            }
            return;
        }

        StripAction? hot = ButtonAt(e.Location);
        if (hot != _hot)
        {
            _hot = hot;
            Invalidate();
        }
        Cursor = PictureAt(e.Location, out _) >= 0 ? Cursors.SizeAll : Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hot != null)
        {
            _hot = null;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        _notice = null;
        if (ButtonAt(e.Location) is { } action)
        {
            Perform(action);
            Invalidate();
            return;
        }
        if (PictureAt(e.Location, out _) >= 0)
        {
            _dragging = true;
            _dragFrom = e.Location;
            Capture = true;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
    }

    private void Wheel(Point at, int delta)
    {
        int position = PictureAt(at, out Rectangle picture);
        if (position < 0) return;

        Bitmap? image = ImageAt(position) ?? FrameImage ?? ReferenceImage;
        if (image == null) return;

        RectangleF source = SourceOf(picture, image.Size, out float scale);
        double atX = (source.X + (at.X - picture.X) / scale) / image.Width;
        double atY = (source.Y + (at.Y - picture.Y) / scale) / image.Height;
        double factor = Math.Pow(ViewState.Step, delta / 120.0);
        _view = _view.ZoomAbout(factor, atX, atY);
        ClampView();
        Invalidate();
    }

    private void Perform(StripAction action)
    {
        switch (action)
        {
            case StripAction.Back:
                Step(-1);
                break;
            case StripAction.Forward:
                Step(1);
                break;
            case StripAction.RefBack:
                StepReference(-1);
                break;
            case StripAction.RefForward:
                StepReference(1);
                break;
            case StripAction.SaveView:
                SaveViewRequested?.Invoke(_view);
                break;
            case StripAction.SaveReference:
                if (_result != null && Index >= 0)
                {
                    SaveReferenceRequested?.Invoke(_result.Frames[Index], _view, DelayAt(_step));
                }
                break;
            case StripAction.EditDelay:
                if (_result != null && Index >= 0) EditDelayRequested?.Invoke(DelayAt(_step));
                break;
            case StripAction.ClearAll:
                ClearRequested?.Invoke(this, EventArgs.Empty);
                break;
            case StripAction.Close:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void Step(int by)
    {
        if (_result == null || _result.Frames.Count == 0) return;

        double first = _result.Frames[0].StampMs;
        double last = _result.Frames[^1].StampMs + _result.FrameMs;
        int next = _step + by;
        double slot = SlotMs(next);
        if (slot < first || slot >= last) return;
        _step = next;
        Invalidate();
    }

    private void StepReference(int by)
    {
        if (_references.Count == 0) return;
        _reference = Math.Clamp(_reference + by, 0, _references.Count - 1);
        Invalidate();
    }

    private sealed class WheelFilter : IMessageFilter
    {
        private const int WmMouseWheel = 0x020A;

        private readonly CaptureViewPanel _panel;

        public WheelFilter(CaptureViewPanel panel) => _panel = panel;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WmMouseWheel || !_panel.Visible || !_panel.IsHandleCreated) return false;

            Point at = _panel.PointToClient(Cursor.Position);
            if (!_panel.ClientRectangle.Contains(at)) return false;

            _panel.Wheel(at, (short)((long)m.WParam >> 16));
            return true;
        }
    }

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

    protected override void Dispose(bool disposing)
    {
        if (disposing) Clear();
        base.Dispose(disposing);
    }
}
