using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public partial class MainForm
{
    private const int CompactStarterRow = 18;

    private bool _hideConstraints;

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

    public void SetHideConstraints(bool hide)
    {
        _hideConstraints = hide;
        RefreshConstraintLayout();
    }

    private void RefreshConstraintLayout()
    {
        bool collapse = _hideConstraints && !TrainingPanel.Visible;
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

        int lift = _fullTrainerRow - CompactStarterRow;
        LabelStarterTrainerId.Top -= lift;
        TextBoxTrainerId.Top -= lift;
        ButtonCalculateOdds.Top -= lift;
        ButtonSearch.Top -= lift;

        GroupBoxStarter.Top = SectionTop;
        GroupBoxStarter.Height = ButtonSearch.Bottom + BoxBottomPad;

        int timerTop = GroupBoxStarter.Bottom + SectionGap;
        int timerNeeds = ButtonTraining.Bottom + BoxBottomPad;
        GroupBoxTimer.Top = timerTop;

        int belowGrid = _fullResultsHeight - _fullListHeight;
        int tail = SectionGap + GroupBoxStatSearch.Height + SectionGap + GroupBoxCapture.Height;

        int listHeight = Math.Max(MinResultsHeight, GroupBoxTimer.Top + timerNeeds - tail - SectionTop - belowGrid);

        GroupBoxResults.Top = SectionTop;
        GroupBoxResults.Height = listHeight + belowGrid;
        ListViewResults.Height = listHeight;
        TrainingPanel.Height = listHeight;
        LabelLanding.Top = ListViewResults.Bottom + (_fullLandingTop - ListViewResults.Top - _fullListHeight);

        GroupBoxStatSearch.Top = GroupBoxResults.Bottom + SectionGap;
        GroupBoxCapture.Top = GroupBoxStatSearch.Bottom + SectionGap;
        GroupBoxContext.Top = GroupBoxCapture.Bottom + SectionGap;

        GroupBoxTimer.Height = Math.Max(timerNeeds, GroupBoxCapture.Bottom - timerTop);

        ApplyClientHeight();
    }

    private void RestoreSections()
    {
        GroupBoxStarter.Top = _fullStarterTop;
        GroupBoxStarter.Height = _fullStarterHeight;

        int lift = _fullTrainerRow - CompactStarterRow;
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
        _compactClientHeight = GroupBoxCapture.Bottom + 6;
        _trackingClientHeight = GroupBoxContext.Bottom + 6;
        ClientSize = new Size(
            ClientSize.Width, GroupBoxContext.Visible ? _trackingClientHeight : _compactClientHeight);
    }
}
