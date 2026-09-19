namespace FRLG.StarterTool.Core.Video;

public readonly record struct ViewState(double Zoom, double CenterX, double CenterY)
{
    public const double MinZoom = 1.0;

    public const double MaxZoom = 1024.0;

    public const double Step = 1.4142135623730951;

    public static ViewState Fit => new(1.0, 0.5, 0.5);

    public ViewState Normalize()
    {
        double zoom = double.IsFinite(Zoom) ? Math.Clamp(Zoom, MinZoom, MaxZoom) : MinZoom;
        double x = double.IsFinite(CenterX) ? Math.Clamp(CenterX, 0.0, 1.0) : 0.5;
        double y = double.IsFinite(CenterY) ? Math.Clamp(CenterY, 0.0, 1.0) : 0.5;
        return new ViewState(zoom, x, y);
    }

    public ViewState Clamp(double visibleX, double visibleY)
    {
        ViewState view = Normalize();
        return view with
        {
            CenterX = ClampAxis(view.CenterX, visibleX),
            CenterY = ClampAxis(view.CenterY, visibleY),
        };
    }

    public ViewState ZoomAbout(double factor, double atX, double atY)
    {
        ViewState view = Normalize();
        double zoom = Math.Clamp(view.Zoom * factor, MinZoom, MaxZoom);
        double scale = view.Zoom / zoom;
        return new ViewState(
            zoom,
            atX + (view.CenterX - atX) * scale,
            atY + (view.CenterY - atY) * scale);
    }

    public ViewState Pan(double dx, double dy) => this with { CenterX = CenterX + dx, CenterY = CenterY + dy };

    private static double ClampAxis(double center, double visible)
    {
        if (!(visible < 1.0)) return 0.5;
        double half = visible / 2.0;
        return Math.Clamp(center, half, 1.0 - half);
    }
}
