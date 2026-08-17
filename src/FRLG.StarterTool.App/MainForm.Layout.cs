using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public partial class MainForm
{
    private const int CompactStarterRow = 18;

    private bool _hideConstraints;

    private float _zoom = 1F;

    private int Scaled(int designerLength) =>
        _zoom == 1F ? designerLength : ZoomLayout.Round(designerLength * _zoom);

    private bool _collapsed;

    private int _fullStarterTop;
    private int _fullStarterHeight;
    private int _fullTrainerRow;
    private int _fullTimerTop;
    private int _fullTimerHeight;
    private int _fullResultsTop;
    private int _fullResultsHeight;
    private int _fullListHeight;
    private int _fullLandingTop;
    private int _fullStatSearchTop;
    private int _fullCaptureTop;
    private int _fullContextTop;

    private void CaptureFullLayout()
    {
        _fullStarterTop = GroupBoxStarter.Top;
        _fullStarterHeight = GroupBoxStarter.Height;
        _fullTrainerRow = TextBoxTrainerId.Top;
        _fullTimerTop = GroupBoxTimer.Top;
        _fullTimerHeight = GroupBoxTimer.Height;
        _fullResultsTop = GroupBoxResults.Top;
        _fullResultsHeight = GroupBoxResults.Height;
        _fullListHeight = ListViewResults.Height;
        _fullLandingTop = LabelLanding.Top;
        _fullStatSearchTop = GroupBoxStatSearch.Top;
        _fullCaptureTop = GroupBoxCapture.Top;
        _fullContextTop = GroupBoxContext.Top;
    }

    private ZoomLayout.Baseline? _zoomBaseline;

    public void ApplyZoom(int zoomPercent)
    {
        _zoomBaseline ??= ZoomLayout.Capture(this);

        float zoom = zoomPercent / 100F;
        if (zoom == _zoom) return;

        bool collapsed = _collapsed;
        if (collapsed)
        {
            RestoreSections();
            _collapsed = false;
        }

        _zoom = zoom;
        SuspendLayout();

        if (AutoScaleMode != AutoScaleMode.None) AutoScaleMode = AutoScaleMode.None;

        ZoomLayout.Apply(this, _zoomBaseline, _zoom, ContextPanel);

        RelayoutContextSection();

        CaptureFullLayout();

        ClientSize = new Size(
            ZoomLayout.Round(_zoomBaseline.ClientWidth * _zoom), GroupBoxCapture.Bottom + Scaled(6));
        ApplyClientHeight();

        ResumeLayout(true);

        if (collapsed) RefreshConstraintLayout();

        if (StarterTool.Settings != null) ApplyTimeFormat();
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

    public void SetHideConstraints(bool hide)
    {
        _hideConstraints = hide;
        RefreshConstraintLayout();
    }

    private void RefreshConstraintLayout()
    {
        bool collapse = _hideConstraints && !TrainingPanel.Visible && !SavestatePanel.Visible;
        if (collapse == _collapsed) return;

        _collapsed = collapse;
        SuspendLayout();
        if (collapse) CollapseSections(); else RestoreSections();
        ResumeLayout(true);

        FitLastColumn();
    }

    private void CollapseSections()
    {
        GroupBoxIvConstraint.Visible = false;
        GroupBoxNatures.Visible = false;

        PictureBoxSprite.Visible = false;
        LabelStarterPokemon.Visible = false;
        ComboBoxPokemon.Visible = false;
        LabelStarterMinFrame.Visible = false;
        TextBoxMinFrame.Visible = false;
        LabelStarterMaxFrame.Visible = false;
        TextBoxMaxFrame.Visible = false;

        int lift = _fullTrainerRow - Scaled(CompactStarterRow);
        LabelStarterTrainerId.Top -= lift;
        TextBoxTrainerId.Top -= lift;
        ButtonCalculateOdds.Top -= lift;
        ButtonSearch.Top -= lift;

        GroupBoxStarter.Top = Scaled(SectionTop);
        GroupBoxStarter.Height = ButtonSearch.Bottom + Scaled(BoxBottomPad);

        int timerTop = GroupBoxStarter.Bottom + Scaled(SectionGap);
        int timerNeeds = ButtonTraining.Bottom + Scaled(BoxBottomPad);
        GroupBoxTimer.Top = timerTop;

        int belowGrid = _fullResultsHeight - _fullListHeight;
        int tail = Scaled(SectionGap) + GroupBoxStatSearch.Height + Scaled(SectionGap) + GroupBoxCapture.Height;

        int listHeight = Math.Max(
            Scaled(MinResultsHeight),
            GroupBoxTimer.Top + timerNeeds - tail - Scaled(SectionTop) - belowGrid);

        GroupBoxResults.Top = Scaled(SectionTop);
        GroupBoxResults.Height = listHeight + belowGrid;
        ListViewResults.Height = listHeight;
        TrainingPanel.Height = listHeight;
        LabelLanding.Top = ListViewResults.Bottom + (_fullLandingTop - ListViewResults.Top - _fullListHeight);

        GroupBoxStatSearch.Top = GroupBoxResults.Bottom + Scaled(SectionGap);
        GroupBoxCapture.Top = GroupBoxStatSearch.Bottom + Scaled(SectionGap);
        GroupBoxContext.Top = GroupBoxCapture.Bottom + Scaled(SectionGap);

        GroupBoxTimer.Height = Math.Max(timerNeeds, GroupBoxCapture.Bottom - timerTop);

        ApplyClientHeight();
    }

    private void RestoreSections()
    {
        GroupBoxStarter.Top = _fullStarterTop;
        GroupBoxStarter.Height = _fullStarterHeight;

        int lift = _fullTrainerRow - Scaled(CompactStarterRow);
        LabelStarterTrainerId.Top += lift;
        TextBoxTrainerId.Top += lift;
        ButtonCalculateOdds.Top += lift;
        ButtonSearch.Top += lift;

        GroupBoxTimer.Top = _fullTimerTop;
        GroupBoxTimer.Height = _fullTimerHeight;

        GroupBoxResults.Top = _fullResultsTop;
        GroupBoxResults.Height = _fullResultsHeight;
        ListViewResults.Height = _fullListHeight;
        TrainingPanel.Height = _fullListHeight;
        LabelLanding.Top = _fullLandingTop;

        GroupBoxStatSearch.Top = _fullStatSearchTop;
        GroupBoxCapture.Top = _fullCaptureTop;
        GroupBoxContext.Top = _fullContextTop;

        PictureBoxSprite.Visible = true;
        LabelStarterPokemon.Visible = true;
        ComboBoxPokemon.Visible = true;
        LabelStarterMinFrame.Visible = true;
        TextBoxMinFrame.Visible = true;
        LabelStarterMaxFrame.Visible = true;
        TextBoxMaxFrame.Visible = true;

        GroupBoxIvConstraint.Visible = true;
        GroupBoxNatures.Visible = true;

        ApplyClientHeight();
    }

    private void ApplyClientHeight()
    {
        _compactClientHeight = GroupBoxCapture.Bottom + Scaled(6);
        _trackingClientHeight = GroupBoxContext.Bottom + Scaled(6);
        ClientSize = new Size(
            ClientSize.Width, GroupBoxContext.Visible ? _trackingClientHeight : _compactClientHeight);
    }
}
