using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Timing;

namespace FRLG.StarterTool.App;

public sealed class IgtTimer : BaseTimer
{
    private readonly MainForm _form;

    private bool _playing;

    private double _currentOffset = double.MaxValue;

    private double _adjusted;

    public IgtTimer(MainForm form)
    {
        _form = form;
    }

    private IgtPanel Panel => _form.IgtPanel;

    private bool Active => ReferenceEquals(StarterTool.CurrentTab, this);

    public override void OnInit()
    {
        Panel.ButtonPlay.Click += (_, _) => Play();
        Panel.ButtonUndo.Click += (_, _) => Undo();
        Panel.Delayed += (_, frames) => ChangeAudio(frames);
    }

    public override void OnSelected(bool selected)
    {
        if (!selected) return;

        _form.LabelTimer.Text = TimeText.Format(0.0, StarterTool.TimeFormat);
        EnableControls(play: false, undo: false);
    }

    public override void OnTimerStart()
    {
        _currentOffset = double.MaxValue;
        _playing = false;
        _adjusted = 0.0;
        _form.LabelTimer.ClearFlash();
        Panel.ResetCounts();
        EnableControls(play: true, undo: false);
    }

    public override void OnTimerStop()
    {
        _playing = false;
        _adjusted = 0.0;
        Panel.ResetCounts();
        EnableControls(play: false, undo: false);
        _form.LabelTimer.Text = TimeText.Format(0.0, StarterTool.TimeFormat);
    }

    public override double TimerCallback(double startTimeMs)
    {
        double elapsed = Math.Max((Win32.GetTime() - startTimeMs) / 1000.0, 0.001);

        if (_playing && _currentOffset < elapsed)
        {
            _playing = false;
            EnableControls(play: true, undo: false);
        }

        return elapsed;
    }

    public override void OnKeyEvent(InputPress press)
    {
        AppSettings settings = StarterTool.Settings;

        if (settings.AddFrame.IsPressed(press)) Panel.PressDelayer(0, +1);
        else if (settings.SubFrame.IsPressed(press)) Panel.PressDelayer(0, -1);
        else if (settings.IgtAdd2.IsPressed(press)) Panel.PressDelayer(1, +1);
        else if (settings.IgtSub2.IsPressed(press)) Panel.PressDelayer(1, -1);
        else if (settings.IgtAdd3.IsPressed(press)) Panel.PressDelayer(2, +1);
        else if (settings.IgtSub3.IsPressed(press)) Panel.PressDelayer(2, -1);
        else if (settings.IgtAdd4.IsPressed(press)) Panel.PressDelayer(3, +1);
        else if (settings.IgtSub4.IsPressed(press)) Panel.PressDelayer(3, -1);
        else if (settings.IgtAdd5.IsPressed(press)) Panel.PressDelayer(4, +1);
        else if (settings.IgtSub5.IsPressed(press)) Panel.PressDelayer(4, -1);
        else if (settings.IgtAdd6.IsPressed(press)) Panel.PressDelayer(5, +1);
        else if (settings.IgtSub6.IsPressed(press)) Panel.PressDelayer(5, -1);
        else if (settings.IgtUndo.IsPressed(press) && Panel.ButtonUndo.Enabled) Undo();
        else if (settings.IgtPlay.IsPressed(press) && Panel.ButtonPlay.Enabled) Play();
    }

    private TimerError ParseSelected(out IgtTimerInfo info)
    {
        IgtTimerEntry entry = Panel.SelectedEntry;
        return IgtCalculator.Parse(entry.Frame, entry.Offsets, entry.Interval, entry.NumBeeps, Panel.Fps, out info);
    }

    public void Play()
    {
        if (!Active || !StarterTool.IsTimerRunning) return;
        if (ParseSelected(out IgtTimerInfo info) != TimerError.NoError) return;

        double elapsedMs = Win32.GetTime() - StarterTool.TimerStart;
        double[] offsets = IgtCalculator.PlayOffsets(info, elapsedMs, _adjusted);

        StarterTool.Beeps.QueueBeeps(IgtCalculator.BeepSchedule(info, offsets));
        _currentOffset = (offsets[^1] + elapsedMs) / 1000.0;
        _playing = true;
        EnableControls(play: true, undo: true);
    }

    public void Undo()
    {
        if (!_playing) return;

        _playing = false;
        _currentOffset = double.MaxValue;
        StarterTool.Beeps.ClearPending();
        EnableControls(play: true, undo: false);
    }

    private void ChangeAudio(double frames)
    {
        if (!Active || ParseSelected(out IgtTimerInfo info) != TimerError.NoError) return;

        _adjusted += frames * 1000.0 / info.Fps;
        if (_playing) Play();
    }

    private void EnableControls(bool play, bool undo)
    {
        Panel.ButtonPlay.Enabled = play;
        Panel.ButtonUndo.Enabled = undo;
    }
}
