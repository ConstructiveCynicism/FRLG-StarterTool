using System.Drawing.Drawing2D;

namespace FRLG.StarterTool.App;

internal sealed class EncounterManipView : Control
{
    private const int Gap = 6;

    private readonly EncounterPanel.ManipDetail _table = new();
    private Bitmap? _picture;
    private string _message = "";
    private string _caption = "";
    private bool _alert;

    public EncounterManipView()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
        SetStyle(ControlStyles.Selectable, false);
        Controls.Add(_table);
    }

    public void SetTable(IReadOnlyList<(string Frames, string Inputs)> rows, IReadOnlyList<string> settings)
    {
        var combos = rows.Select(row => (row.Frames, Combo(row.Inputs))).ToList();
        _table.ShowManip(combos, settings, rows.Select(_ => true).ToList(), "");
    }

    internal static string Combo(string inputs)
    {
        int dash = inputs.IndexOf(" - ", StringComparison.Ordinal);
        string combo = dash >= 0 ? inputs[..dash] : inputs;
        if (combo.StartsWith("Hold ", StringComparison.Ordinal)) combo = combo["Hold ".Length..];
        int aside = combo.IndexOf(", the other one", StringComparison.Ordinal);
        if (aside >= 0) combo = combo[..aside];
        return combo.Trim();
    }

    public void SetPicture(Bitmap? picture)
    {
        _picture?.Dispose();
        _picture = picture;
        Invalidate(PictureBounds());
    }

    public void SetMessage(string message)
    {
        if (_message == message) return;
        _message = message;
        Invalidate();
    }

    public void SetCaption(string caption, bool alert = false)
    {
        if (_caption == caption && _alert == alert) return;
        _caption = caption;
        _alert = alert;
        Invalidate();
    }

    private int LineHeight => Font.Height + 4;

    private Rectangle PictureBounds()
    {
        int line = LineHeight;
        int height = Math.Max(1, Height - 2 * line);
        int width = Math.Min(height * 3 / 2, Math.Max(1, (Width - Gap) * 2 / 5));
        height = Math.Min(height, width * 2 / 3);
        return new Rectangle(0, line, width, height);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        Rectangle picture = PictureBounds();
        _table.Bounds = new Rectangle(picture.Right + Gap, picture.Top, Math.Max(1, Width - picture.Right - Gap), Math.Max(1, Height - picture.Top));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        PerformLayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _table.Font = Font;
        PerformLayout();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Window);
        Rectangle picture = PictureBounds();

        TextRenderer.DrawText(g, _caption, Font, new Rectangle(0, 0, picture.Width, LineHeight), _alert ? Theme.LandingMissText : Theme.SectionCaption,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        using (var back = new SolidBrush(Theme.ListBack)) g.FillRectangle(back, picture);
        if (_picture != null)
        {
            float scale = Math.Min(picture.Width / (float)_picture.Width, picture.Height / (float)_picture.Height);
            var dest = new RectangleF(
                picture.X + (picture.Width - _picture.Width * scale) / 2f,
                picture.Y + (picture.Height - _picture.Height * scale) / 2f,
                _picture.Width * scale, _picture.Height * scale);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(_picture, dest);
            g.PixelOffsetMode = PixelOffsetMode.Default;
        }
        else if (_message.Length > 0)
        {
            TextRenderer.DrawText(g, _message, Font, picture, _alert ? Theme.LandingMissText : Theme.DimText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
        using (var pen = new Pen(Theme.Border)) g.DrawRectangle(pen, picture.X, picture.Y, picture.Width - 1, picture.Height - 1);

        if (_picture != null && _message.Length > 0)
        {
            TextRenderer.DrawText(g, _message, Font, new Rectangle(0, picture.Bottom, picture.Width, LineHeight), _alert ? Theme.LandingMissText : Theme.DimText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _picture?.Dispose();
            _picture = null;
        }
        base.Dispose(disposing);
    }
}
