namespace FRLG.StarterTool.App;

public abstract class BaseTimer
{
    public abstract void OnInit();

    public abstract void OnTimerStart();

    public abstract void OnTimerStop();

    public abstract void OnKeyEvent(InputPress press);

    public abstract double TimerCallback(double startTimeMs);

    public virtual bool TryRecordLanding(double pressTimeMs, double pressLagMs = 0.0) => false;

    public virtual void Nudge(int direction)
    {
    }

    public virtual void OnSelected(bool selected)
    {
    }
}
