using System.Drawing.Drawing2D;

namespace FRLG.StarterTool.App;

public sealed class TimerClock : Control
{
    private const int Inset = 2;

    private const int CornerRadius = 6;

    private const int AnimationIntervalMs = 15;

    private double _flash;

    private bool _final;

    private double[] _schedule = Array.Empty<double>();
    private double _intervalMs;
    private double _startTimeMs;

    private readonly System.Windows.Forms.Timer _animation;

    public TimerClock()
    {
        _animation = new System.Windows.Forms.Timer { Interval = AnimationIntervalMs };
        _animation.Tick += (_, _) => Sample();

        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);

        SetStyle(ControlStyles.Selectable, false);
    }

    public void SetSchedule(double[] scheduleMs, double intervalMs, double startTimeMs)
    {
        _schedule = scheduleMs;
        _intervalMs = intervalMs;
        _startTimeMs = startTimeMs;

        if (_schedule.Length == 0 || _intervalMs <= 0.0)
        {
            ClearFlash();
            return;
        }

        _animation.Start();
        Sample();
    }

    public void ClearFlash()
    {
        _animation.Stop();
        _schedule = Array.Empty<double>();
        _intervalMs = 0.0;
        SetFlash(0.0);
    }

    public void LetFlashFinish()
    {
        if (_schedule.Length > 0 && _intervalMs > 0.0) _animation.Start();
    }

    public void Sample()
    {
        if (_schedule.Length == 0 || _intervalMs <= 0.0) return;

        double elapsedMs = Win32.GetTime() - _startTimeMs;
        double intensity = Core.Timing.VariableOffsetCalculator.FlashIntensity(
            _schedule, elapsedMs, _intervalMs, out int beat);

        SetFlash(intensity, beat >= 0 && beat == _schedule.Length - 1);

        if (elapsedMs >= _schedule[^1] + _intervalMs) _animation.Stop();
    }

    private void SetFlash(double intensity, bool final = false)
    {
        double clamped = Math.Clamp(intensity, 0.0, 1.0);
        if (Alpha(clamped) == Alpha(_flash) && final == _final) return;

        _flash = clamped;
        _final = final;
        Invalidate();
    }

    private static int Alpha(double intensity) => (int)Math.Round(intensity * 255.0);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _animation.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        using (var background = new SolidBrush(BackColor))
        {
            g.FillRectangle(background, ClientRectangle);
        }

        if (_flash > 0.0)
        {
            int alpha = Alpha(_flash);
            if (alpha > 0)
            {
                Rectangle box = ClientRectangle;
                box.Inflate(-Inset, -Inset);

                using var path = RoundedRectangle(box, CornerRadius);
                using var brush = new SolidBrush(
                    Color.FromArgb(alpha, _final ? Theme.TimerFlashFinal : Theme.TimerFlash));

                SmoothingMode smoothing = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(brush, path);
                g.SmoothingMode = smoothing;
            }
        }

        TextRenderer.DrawText(
            g, Text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (d <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
