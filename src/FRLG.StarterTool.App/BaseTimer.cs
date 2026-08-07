namespace FRLG.StarterTool.App;

public abstract class BaseTimer
{
    public abstract void OnInit();

    public abstract void OnTimerStart();

    public abstract void OnTimerStop();

    public abstract void OnKeyEvent(Keys key);

    public abstract double TimerCallback(double startTimeMs);
}
