using FRLG.StarterTool.App.Capture;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public partial class MainForm
{
    private readonly System.Windows.Forms.Timer _manipViewTimer = new() { Interval = 250 };
    private string _contextCaption = "";
    private string _manipTableKey = "";
    private bool _manipViewLive;
    private double _liveSinceMs;
    private double _lastLivePictureMs;
    private Action<Bitmap>? _livePicture;

    private const double DisconnectedAfterMs = 5000.0;

    private const double OpeningGraceMs = 1500.0;

    private const string EncounterManipCaption = "Encounter Manip";

    private void InitializeManipView()
    {
        _contextCaption = GroupBoxContext.Text;
        _livePicture = picture =>
        {
            _lastLivePictureMs = Win32.GetTime();
            ManipView.SetPicture(picture);
        };
        _manipViewTimer.Tick += (_, _) => RefreshManipView();
        _manipViewTimer.Start();
    }

    private void RefreshManipView()
    {
        if (StarterTool.VariableOffset == null || StarterTool.Settings == null) return;

        string route = SelectedEncounterRoute;
        bool countdown = StarterTool.VariableOffset.EncounterRunLive;
        bool next = route.Length > 0 && !StarterTool.IsTimerRunning && StarterTool.VariableOffset.StartsEncounterRun;
        bool loading = !countdown && !StarterTool.IsTimerRunning && StarterTool.Capture.Pending;
        bool show = route.Length > 0 && (next || countdown || loading) && !CaptureView.Visible;

        if (show != ManipView.Visible)
        {
            ManipView.Visible = show;
            if (show) ManipView.BringToFront();
            else
            {
                ManipView.SetPicture(null);
                _manipTableKey = "";
            }
        }
        string caption = show || CaptureView.Visible ? EncounterManipCaption : _contextCaption;
        if (GroupBoxContext.Text != caption) GroupBoxContext.Text = caption;

        bool live = show && next && StarterTool.Settings.VideoEnabled && StarterTool.Settings.VideoSourceKind != VideoSourceKind.None;
        if (live != _manipViewLive)
        {
            _manipViewLive = live;
            _liveSinceMs = _lastLivePictureMs = Win32.GetTime();
            StarterTool.Capture.SetLive(live ? _livePicture : null);
        }
        if (!show) return;

        if (EncounterPanel.ManipTableFor(route) is { } table)
        {
            string key = route + "\n" + string.Join("\n", table.Rows.Select(row => row.Frames + "\t" + row.Inputs))
                + "\n" + string.Join("\n", table.Settings);
            if (key != _manipTableKey)
            {
                _manipTableKey = key;
                ManipView.SetTable(table.Rows, table.Settings);
            }
        }

        double now = Win32.GetTime();
        bool recording = StarterTool.Settings.VideoSourceKind == VideoSourceKind.Recording;
        bool disconnected = live
            && ((StarterTool.Capture.SourceError != null && now - _liveSinceMs > OpeningGraceMs)
                || (!recording && now - _lastLivePictureMs > DisconnectedAfterMs));
        bool inactive = recording && StarterTool.Settings.VideoEnabled
            && StarterTool.Capture.SourceError == RecordingFrameSource.InactiveError;
        if (inactive && !loading)
        {
            ManipView.SetCaption(route + " - " + RecordingFrameSource.InactiveError, true);
        }
        else ManipView.SetCaption(
            route + (countdown ? " - frozen during manip" : loading ? " - loading capture…" : disconnected ? " - disconnected" : !live ? "" : recording ? " - recording" : " - live"),
            disconnected);
        ManipView.SetMessage(
            !StarterTool.Settings.VideoEnabled ? "Title-Press Capture is off (Settings, Video)"
            : StarterTool.Settings.VideoSourceKind == VideoSourceKind.None ? "No capture source (Settings, Video)"
            : StarterTool.Capture.Status.Split('\n')[0]);
    }
}
