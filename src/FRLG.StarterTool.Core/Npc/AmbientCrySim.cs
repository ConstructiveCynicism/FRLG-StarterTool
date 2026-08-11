namespace FRLG.StarterTool.Core.Npc;

public sealed class AmbientCrySim
{
    public const int LoadRolls = 1;

    private const int StateInit = 0;
    private const int StateFirstDelay = 1;
    private const int StateNextDelay = 2;
    private const int StateCountingDown = 3;

    private int _state = StateInit;
    private int _delay;

    public bool ControlsLocked { get; set; } = true;

    public bool WaterMon { get; init; } = true;

    private bool CryRolls => !WaterMon;

    public int Rolls { get; private set; }

    public void Step(GameRng rng)
    {
        if (ControlsLocked) return;

        switch (_state)
        {
            case StateInit:
                _state = StateFirstDelay;
                break;

            case StateFirstDelay:
                _delay = (rng.Random() % 2400) + 1200;
                Rolls++;
                _state = StateCountingDown;
                break;

            case StateNextDelay:
                _delay = (rng.Random() % 1200) + 1200;
                Rolls++;
                _state = StateCountingDown;
                break;

            case StateCountingDown:
                _delay--;
                if (_delay == 0)
                {
                    if (CryRolls)
                    {
                        rng.Random();
                        rng.Random();
                        Rolls += 2;
                    }
                    _state = StateNextDelay;
                }
                break;
        }
    }

    public AmbientCrySim Clone()
    {
        var copy = new AmbientCrySim { WaterMon = WaterMon, ControlsLocked = ControlsLocked };
        copy._state = _state;
        copy._delay = _delay;
        copy.Rolls = Rolls;
        return copy;
    }
}
