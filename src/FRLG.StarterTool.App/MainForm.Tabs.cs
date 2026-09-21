using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public enum TabKey
{
    Manip,
    Constraints,
    Training,
    Encounter,
    Savestate,
    Troubleshoot,
    GenericFixed,
    GenericVariable,
    GenericIgt,
    GenericTraining
}

public enum TimerMode
{
    Frlg,

    GenericVariable,

    GenericTraining,

    Fixed,

    Igt
}

public partial class MainForm
{
    private static readonly TabKey[] TabOrder =
    {
        TabKey.Manip, TabKey.Constraints, TabKey.Training,
        TabKey.Encounter, TabKey.Savestate, TabKey.Troubleshoot,
        TabKey.GenericFixed, TabKey.GenericVariable, TabKey.GenericIgt, TabKey.GenericTraining
    };

    public static TimerMode ModeOf(TabKey key) => key switch
    {
        TabKey.GenericFixed => TimerMode.Fixed,
        TabKey.GenericVariable => TimerMode.GenericVariable,
        TabKey.GenericIgt => TimerMode.Igt,
        TabKey.GenericTraining => TimerMode.GenericTraining,
        _ => TimerMode.Frlg
    };

    public TimerMode TimerMode => ModeOf(_selectedTab);

    public TabKey SelectedTab => _selectedTab;

    private TabKey _selectedTab = TabKey.Manip;

    private bool _syncingView;

    public bool TrainingTabUp => _selectedTab is TabKey.Training or TabKey.GenericTraining;

    private TabKey _trainingReturnTab = TabKey.Manip;

    private Point _timerHome;

    private int _timerManipHeight;

    private void PlaceTimer()
    {
        TabKey tab = _selectedTab;
        bool manip = tab == TabKey.Manip || TimerPageOf(tab) == null;
        bool training = tab is TabKey.Training or TabKey.GenericTraining;
        Panel page = TimerPageOf(tab) ?? PageManip;
        Point at = manip
            ? new Point(Scaled(_timerHome.X), Scaled(_timerHome.Y))
            : new Point(Scaled(6), Scaled(SectionTop));

        TimerMode mode = ModeOf(tab);
        int contentBottom = LayoutTimerRows(mode);
        GroupBoxTimer.Text = mode == TimerMode.Frlg ? "FRLG Timer" : "Timer";

        if (training && !ReferenceEquals(GroupBoxTraining.Parent, page)) page.Controls.Add(GroupBoxTraining);

        int height = tab switch
        {
            TabKey.Training or TabKey.GenericTraining => GroupBoxTraining.Height,
            TabKey.GenericFixed => Math.Max(FixedPanel.Height, contentBottom),
            TabKey.GenericVariable => Math.Max(GroupBoxLandingLog.Height, contentBottom),
            TabKey.GenericIgt => Math.Max(IgtPanel.Height, contentBottom),
            _ => Scaled(_timerManipHeight)
        };

        if (!ReferenceEquals(GroupBoxTimer.Parent, page)) page.Controls.Add(GroupBoxTimer);
        if (GroupBoxTimer.Location != at) GroupBoxTimer.Location = at;
        if (GroupBoxTimer.Height != height) GroupBoxTimer.Height = height;
    }

    private Panel? TimerPageOf(TabKey key) => key switch
    {
        TabKey.Manip => PageManip,
        TabKey.Training => PageTraining,
        TabKey.GenericFixed => PageGenericFixed,
        TabKey.GenericVariable => PageGenericVariable,
        TabKey.GenericIgt => PageGenericIgt,
        TabKey.GenericTraining => PageGenericTraining,
        _ => null
    };

    private sealed record TimerRow(Control[] Controls, int DesignerY);

    private TimerRow[] _timerRows = Array.Empty<TimerRow>();

    private readonly Dictionary<Control, int> _timerRowTops = new();

    private const int RowFrame = 0, RowFps = 1, RowAudio = 2, RowVisual = 3, RowDelay = 4,
        RowInterval = 5, RowBeeps = 6, RowRoute = 7, RowTraining = 8;

    private void CaptureTimerRowTops()
    {
        foreach (TimerRow row in _timerRows)
        {
            foreach (Control control in row.Controls) _timerRowTops[control] = control.Top;
        }
    }

    private static bool ShowsTimerRow(TimerMode mode, int row) => mode switch
    {
        TimerMode.Frlg => true,
        TimerMode.GenericVariable or TimerMode.GenericTraining => row != RowRoute,
        TimerMode.Fixed => row is RowFrame or RowFps or RowAudio or RowVisual or RowDelay or RowTraining,
        _ => false
    };

    private int LayoutTimerRows(TimerMode mode)
    {
        LabelTimerFrame.Text = mode == TimerMode.Fixed ? "Adjust" : "Frame";

        int slot = 0;
        int bottom = ButtonStart.Bottom;
        foreach ((TimerRow row, int index) in _timerRows.Select((row, index) => (row, index)))
        {
            bool shown = ShowsTimerRow(mode, index);
            int shift = shown ? Scaled(_timerRows[slot].DesignerY) - Scaled(row.DesignerY) : 0;

            foreach (Control control in row.Controls)
            {
                control.Visible = shown;
                if (!shown) continue;

                int top = _timerRowTops[control] + shift;
                if (control.Top != top) control.Top = top;
                bottom = Math.Max(bottom, control.Bottom);
            }

            if (shown) slot++;
        }

        return bottom + Scaled(BoxBottomPad);
    }

    private Label ActiveLandingLabel => _selectedTab switch
    {
        TabKey.Training or TabKey.GenericTraining => LabelTrainingLanding,
        TabKey.GenericVariable => LandingLog.Readout,
        TabKey.GenericFixed => FixedPanel.Readout,
        _ => LabelLanding
    };

    public void ShowManipTab() => SelectTab(TabKey.Manip);

    private void HandCaretToResults()
    {
        if (_selectedTab == TabKey.Manip) TakeCaret(ListViewResults);
        else ActiveControl = null;
    }

    private Panel PageOf(TabKey key) => key switch
    {
        TabKey.Manip => PageManip,
        TabKey.Constraints => PageConstraints,
        TabKey.Training => PageTraining,
        TabKey.Encounter => PageEncounter,
        TabKey.Savestate => PageSavestate,
        TabKey.Troubleshoot => PageTroubleshoot,
        TabKey.GenericFixed => PageGenericFixed,
        TabKey.GenericVariable => PageGenericVariable,
        TabKey.GenericIgt => PageGenericIgt,
        _ => PageGenericTraining
    };

    private ToolStripMenuItem ItemOf(TabKey key) => key switch
    {
        TabKey.Manip => MenuItemViewManip,
        TabKey.Constraints => MenuItemViewConstraints,
        TabKey.Training => MenuItemViewTraining,
        TabKey.Encounter => MenuItemViewEncounter,
        TabKey.Savestate => MenuItemViewSavestate,
        TabKey.Troubleshoot => MenuItemViewTroubleshooter,
        TabKey.GenericFixed => MenuItemViewGenericFixed,
        TabKey.GenericVariable => MenuItemViewGenericVariable,
        TabKey.GenericIgt => MenuItemViewGenericIgt,
        _ => MenuItemViewGenericTraining
    };

    private string CaptionOf(TabKey key) => key switch
    {
        TabKey.Manip => "Manip",
        TabKey.Constraints => "Constraints",
        TabKey.Training => MenuItemViewGenericTraining.Checked ? "FRLG Trainer" : "Offset Trainer",
        TabKey.Encounter => "Encounter Route",
        TabKey.Savestate => "Savestate Editor",
        TabKey.Troubleshoot => "NPC Troubleshooter",
        TabKey.GenericFixed => "Fixed Offset",
        TabKey.GenericVariable => "Variable Offset",
        TabKey.GenericIgt => "IGT Tracking",
        _ => "Offset Trainer"
    };

    private static string KeyOf(TabKey key) => key.ToString().ToLowerInvariant();

    private static TabKey ParseTab(string? key)
    {
        foreach (TabKey tab in TabOrder)
        {
            if (string.Equals(KeyOf(tab), key, StringComparison.OrdinalIgnoreCase)) return tab;
        }

        return TabKey.Manip;
    }

    private bool TabVisible(TabKey key) => ItemOf(key).Checked;

    private void InitializeTabs()
    {
        foreach (TabKey key in TabOrder)
        {
            TabStrip.Add(KeyOf(key), CaptionOf(key));

            TabKey captured = key;
            ToolStripMenuItem item = ItemOf(key);
            TabStrip.SetVisible(KeyOf(key), item.Checked);
            item.CheckedChanged += (_, _) => SetTabVisible(captured, item.Checked);
        }

        TabStrip.TabClicked += (_, key) => SelectTab(ParseTab(key));

        _timerHome = GroupBoxTimer.Location;
        _timerManipHeight = GroupBoxTimer.Height;

        _timerRows = new[]
        {
            new TimerRow(new Control[] { LabelTimerFrame, TextBoxFrame, ButtonMinus, ButtonPlus }, TextBoxFrame.Top),
            new TimerRow(new Control[] { LabelTimerFps, ComboBoxFps }, LabelTimerFps.Top),
            new TimerRow(new Control[] { CheckBoxBeepEnabled, TextBoxOffset }, TextBoxOffset.Top),
            new TimerRow(new Control[] { CheckBoxFlashEnabled, TextBoxVisualOffset }, TextBoxVisualOffset.Top),
            new TimerRow(new Control[] { LabelTimerDelay, TextBoxDelayOffset }, TextBoxDelayOffset.Top),
            new TimerRow(new Control[] { LabelTimerInterval, TextBoxInterval }, TextBoxInterval.Top),
            new TimerRow(new Control[] { LabelTimerBeeps, TextBoxBeeps }, TextBoxBeeps.Top),
            new TimerRow(new Control[] { LabelTimerRoute, ComboBoxEncounterRoute }, LabelTimerRoute.Top),
            new TimerRow(new Control[] { ButtonTraining }, ButtonTraining.Top)
        };
        CaptureTimerRowTops();
        PageManip.Visible = true;
        TabStrip.SelectedKey = KeyOf(TabKey.Manip);
    }

    private void SelectTab(TabKey key)
    {
        if (!TabVisible(key)) key = NearestVisible(key);
        if (key == _selectedTab && PageOf(key).Visible) return;

        TabKey old = _selectedTab;
        Panel oldPage = PageOf(old);
        Panel page = PageOf(key);

        if (old != key)
        {
            if (old is TabKey.Training or TabKey.GenericTraining)
            {
                TrainingPanel.Cancel();
                if (ModeOf(old) != ModeOf(key) && key is TabKey.Training or TabKey.GenericTraining)
                {
                    TrainingPanel.Clear();
                }
                LabelTrainingLanding.Text = "";
            }

            if (old == TabKey.Encounter) EncounterPanel.Cancel();

            if (ModeOf(old) != ModeOf(key) && StarterTool.IsTimerRunning) StarterTool.StopTimer(false);
        }

        if (ActiveControl != null && oldPage.Contains(ActiveControl)) ActiveControl = null;

        SuspendLayout();
        _selectedTab = key;
        if (ModeOf(old) != ModeOf(key)) StarterTool.SetTimerMode(ModeOf(key));
        PlaceTimer();
        page.Visible = true;
        if (!ReferenceEquals(oldPage, page)) oldPage.Visible = false;
        ResumeLayout();

        TabStrip.SelectedKey = KeyOf(key);

        switch (key)
        {
            case TabKey.Savestate:
                SavestatePanel.Rescan();
                break;
            case TabKey.Troubleshoot:
                TroubleshootPanel.Reload();
                break;
            case TabKey.Manip when _trainerIdCaretClosed && ActiveControl == null:
                TakeCaret(ListViewResults);
                break;
        }

        RefreshTrainingButton();
        RefreshContextTracking();
    }

    private TabKey NearestVisible(TabKey key)
    {
        int at = Array.IndexOf(TabOrder, key);

        for (int i = at + 1; i < TabOrder.Length; i++)
        {
            if (TabVisible(TabOrder[i])) return TabOrder[i];
        }

        for (int i = at - 1; i >= 0; i--)
        {
            if (TabVisible(TabOrder[i])) return TabOrder[i];
        }

        return key;
    }

    private void SetTabVisible(TabKey key, bool visible)
    {
        if (_syncingView) return;

        if (!visible && !TabOrder.Any(other => other != key && TabVisible(other)))
        {
            _syncingView = true;
            ItemOf(key).Checked = true;
            _syncingView = false;
            return;
        }

        TabStrip.SetVisible(KeyOf(key), visible);
        TabStrip.SetCaption(KeyOf(TabKey.Training), CaptionOf(TabKey.Training));
        if (!visible && _selectedTab == key) SelectTab(NearestVisible(key));

        ApplyClientHeight();
    }

    private void ApplyTabSettings(AppSettings settings)
    {
        var wanted = new (ToolStripMenuItem Item, bool Visible)[]
        {
            (MenuItemViewManip, settings.ViewManip),
            (MenuItemViewConstraints, settings.ViewConstraints),
            (MenuItemViewTraining, settings.ViewTraining),
            (MenuItemViewEncounter, settings.ViewEncounter),
            (MenuItemViewSavestate, settings.ViewSavestate),
            (MenuItemViewTroubleshooter, settings.ViewTroubleshooter),
            (MenuItemViewGenericFixed, settings.ViewGenericFixed),
            (MenuItemViewGenericVariable, settings.ViewGenericVariable),
            (MenuItemViewGenericIgt, settings.ViewGenericIgt),
            (MenuItemViewGenericTraining, settings.ViewGenericTraining)
        };

        foreach ((ToolStripMenuItem item, bool visible) in wanted)
        {
            if (visible) item.Checked = true;
        }

        foreach ((ToolStripMenuItem item, bool visible) in wanted)
        {
            if (!visible) item.Checked = false;
        }

        SelectTab(ParseTab(settings.SelectedTab));
    }

    private void CaptureTabSettings(AppSettings settings)
    {
        settings.ViewManip = MenuItemViewManip.Checked;
        settings.ViewConstraints = MenuItemViewConstraints.Checked;
        settings.ViewTraining = MenuItemViewTraining.Checked;
        settings.ViewEncounter = MenuItemViewEncounter.Checked;
        settings.ViewSavestate = MenuItemViewSavestate.Checked;
        settings.ViewTroubleshooter = MenuItemViewTroubleshooter.Checked;
        settings.ViewGenericFixed = MenuItemViewGenericFixed.Checked;
        settings.ViewGenericVariable = MenuItemViewGenericVariable.Checked;
        settings.ViewGenericIgt = MenuItemViewGenericIgt.Checked;
        settings.ViewGenericTraining = MenuItemViewGenericTraining.Checked;
        settings.SelectedTab = KeyOf(_selectedTab);
    }

    private void RefreshContextTracking() =>
        StarterTool.Context.Tracking = _selectedTab == TabKey.Manip;

    private void ApplyClientHeight()
    {
        int tallest = 0;
        foreach (TabKey key in TabOrder)
        {
            if (TabVisible(key)) tallest = Math.Max(tallest, PageOf(key).Height);
        }

        ClientSize = new Size(ClientSize.Width, Scaled(PageTop) + tallest + Scaled(6));
    }
}
