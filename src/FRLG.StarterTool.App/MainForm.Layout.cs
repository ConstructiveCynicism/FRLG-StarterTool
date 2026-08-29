using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public partial class MainForm
{
    private float _zoom = 1F;

    private int _zoomDpi;

    private int Scaled(int designerLength) =>
        _zoom == 1F ? designerLength : ZoomLayout.Round(designerLength * _zoom);

    private ZoomLayout.Baseline? _zoomBaseline;

    public void ApplyZoom(int zoomPercent)
    {
        _zoomBaseline ??= ZoomLayout.Capture(this);

        float zoom = zoomPercent / 100F;

        if (zoom == _zoom && DeviceDpi == _zoomDpi) return;
        _zoomDpi = DeviceDpi;

        _zoom = zoom;
        SuspendLayout();

        if (AutoScaleMode != AutoScaleMode.None) AutoScaleMode = AutoScaleMode.None;

        ZoomLayout.Apply(this, _zoomBaseline, _zoom, ContextPanel);

        PlaceTimer();
        RelayoutContextSection();
        RelayoutRangeCards();

        PageManip.Height = GroupBoxContext.Bottom + Scaled(6);

        ClientSize = new Size(ZoomLayout.Round(_zoomBaseline.ClientWidth * _zoom), ClientSize.Height);
        ApplyClientHeight();

        ResumeLayout(true);

        if (StarterTool.Settings != null) ApplyTimeFormat();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);

        if (StarterTool.Settings is { } settings) ApplyZoom(settings.ZoomPercent);
    }

    private void RelayoutContextSection()
    {
        GroupBoxContext.Height = ContextPanel.Bottom + Scaled(BoxBottomPad);

        TroubleshootPanel.Relayout();

        int buttonY = ContextPanel.Bottom - ButtonContextUndo.Height;
        int buttonX = ContextPanel.Left + NpcGridPanel.GridPixels + Scaled(12);
        int gap = Scaled(SectionGap);

        int corner = Math.Max(
            ButtonContextAnchor.Width, Math.Max(ButtonContextLate.Width, ButtonContextFinished.Width));
        int wanted = ButtonContextUndo.Width + ButtonContextClear.Width + ButtonContextMiss.Width
            + 3 * gap + corner;
        int available = ContextPanel.Right - buttonX;

        if (wanted > available)
        {
            float fit = (available - 3 * gap) / (float)(wanted - 3 * gap);

            foreach (ThemedButton button in new[]
            {
                ButtonContextUndo, ButtonContextClear, ButtonContextMiss,
                ButtonContextAnchor, ButtonContextLate, ButtonContextFinished
            })
            {
                button.Width = (int)(button.Width * fit);
            }
        }

        ButtonContextUndo.Location = new Point(buttonX, buttonY);
        ButtonContextClear.Location = new Point(ButtonContextUndo.Right + gap, buttonY);
        ButtonContextMiss.Location = new Point(ButtonContextClear.Right + gap, buttonY);

        ButtonContextAnchor.Location =
            new Point(ContextPanel.Right - ButtonContextAnchor.Width, buttonY);
        ButtonContextLate.Location = new Point(ContextPanel.Right - ButtonContextLate.Width, buttonY);
        ButtonContextFinished.Location =
            new Point(ContextPanel.Right - ButtonContextFinished.Width, buttonY);
    }
}
