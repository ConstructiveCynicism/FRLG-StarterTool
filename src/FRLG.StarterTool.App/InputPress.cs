using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public static class InputState
{
    public static bool IsDown(InputCode code) => code.Device == InputDevice.Keyboard
        ? Win32.IsKeyDown((Keys)code.Code)
        : Gamepads.IsDown(code.Pad, code.Code);
}

public sealed class InputPress
{
    private readonly HashSet<InputCode> _held;

    private InputPress(InputCode input, HashSet<InputCode> held, bool foreground)
    {
        Input = input;
        _held = held;
        Foreground = foreground;
        Held = WasHeld;
        BestMatch = 0;
    }

    public InputCode Input { get; }

    public bool Foreground { get; }

    public Func<InputCode, bool> Held { get; }

    public int BestMatch { get; private set; }

    public bool IsKeyboard => Input.IsKeyboard;

    public Keys Key => Input.IsKeyboard ? (Keys)Input.Code : Keys.None;

    public bool WasHeld(InputCode code) => _held.Contains(code);

    public static InputPress Capture(InputCode input, AppSettings settings)
    {
        var held = new HashSet<InputCode>();
        foreach (Hotkey hotkey in settings.AllHotkeys())
        {
            foreach (InputChord chord in hotkey.Chords)
            {
                foreach (InputCode code in chord.Inputs)
                {
                    if (code != input && !held.Contains(code) && InputState.IsDown(code)) held.Add(code);
                }
            }
        }

        var press = new InputPress(input, held, Win32.IsForeground(StarterTool.MainFormHandle));
        press.Score(settings);
        return press;
    }

    public InputPress As(InputCode other)
    {
        var press = new InputPress(other, _held, Foreground);
        press.Score(StarterTool.Settings);
        return press;
    }

    private void Score(AppSettings settings)
    {
        int best = 0;
        foreach (Hotkey hotkey in settings.AllHotkeys())
        {
            if (!hotkey.Global && !Foreground) continue;
            best = Math.Max(best, hotkey.MatchLength(Input, Held));
        }
        BestMatch = best;
    }
}
