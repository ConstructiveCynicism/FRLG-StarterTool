using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public enum TabKey
{
    Manip,
    Constraints,
    Training,
    Encounter,
    Savestate,
    Troubleshoot
}

public partial class MainForm
{
    private static readonly TabKey[] TabOrder =
    {
        TabKey.Manip, TabKey.Constraints, TabKey.Training,
        TabKey.Encounter, TabKey.Savestate, TabKey.Troubleshoot
    };

    private TabKey _selectedTab = TabKey.Manip;

    private bool _syncingView;

    public bool TrainingTabUp => _selectedTab == TabKey.Training;

    private Point _timerHome;

    private int _timerManipHeight;

    private void PlaceTimer()
    {
        bool training = _selectedTab == TabKey.Training;
        Panel page = training ? PageTraining : PageManip;
        Point at = training
            ? new Point(Scaled(6), Scaled(SectionTop))
            : new Point(Scaled(_timerHome.X), Scaled(_timerHome.Y));

        int height = training ? GroupBoxTraining.Height : Scaled(_timerManipHeight);

        if (!ReferenceEquals(GroupBoxTimer.Parent, page)) page.Controls.Add(GroupBoxTimer);
        if (GroupBoxTimer.Location != at) GroupBoxTimer.Location = at;
        if (GroupBoxTimer.Height != height) GroupBoxTimer.Height = height;
    }

    private Label ActiveLandingLabel =>
        _selectedTab == TabKey.Training ? LabelTrainingLanding : LabelLanding;

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
        _ => PageTroubleshoot
    };

    private ToolStripMenuItem ItemOf(TabKey key) => key switch
    {
        TabKey.Manip => MenuItemViewManip,
        TabKey.Constraints => MenuItemViewConstraints,
        TabKey.Training => MenuItemViewTraining,
        TabKey.Encounter => MenuItemViewEncounter,
        TabKey.Savestate => MenuItemViewSavestate,
        _ => MenuItemViewTroubleshooter
    };

    private static string CaptionOf(TabKey key) => key switch
    {
        TabKey.Manip => "Manip",
        TabKey.Constraints => "Constraints",
        TabKey.Training => "Offset Trainer",
        TabKey.Encounter => "Encounter Route",
        TabKey.Savestate => "Savestate Editor",
        _ => "NPC Troubleshooter"
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
            if (old == TabKey.Training)
            {
                TrainingPanel.Cancel();
                LabelTrainingLanding.Text = "";
            }

            if (old == TabKey.Encounter) EncounterPanel.Cancel();
        }

        if (ActiveControl != null && oldPage.Contains(ActiveControl)) ActiveControl = null;

        SuspendLayout();
        _selectedTab = key;
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
        if (!visible && _selectedTab == key) SelectTab(NearestVisible(key));

        ApplyClientHeight();
    }

    private void ApplyTabSettings(AppSettings settings)
    {
        MenuItemViewManip.Checked = settings.ViewManip;
        MenuItemViewConstraints.Checked = settings.ViewConstraints;
        MenuItemViewTraining.Checked = settings.ViewTraining;
        MenuItemViewEncounter.Checked = settings.ViewEncounter;
        MenuItemViewSavestate.Checked = settings.ViewSavestate;
        MenuItemViewTroubleshooter.Checked = settings.ViewTroubleshooter;

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
