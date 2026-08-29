using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public static class HotkeyExtensions
{
    public static readonly (HotkeyAction Action, string Label)[] Actions =
    {
        (HotkeyAction.Start, "Start timer"),
        (HotkeyAction.Stop, "Stop timer"),
        (HotkeyAction.AddFrame, "+ 1 frame"),
        (HotkeyAction.SubFrame, "− 1 frame"),
        (HotkeyAction.Multiply2, "×2 frames"),
        (HotkeyAction.Multiply3, "×3 frames"),
        (HotkeyAction.ToggleLevel, "Level 5 / 6"),
        (HotkeyAction.ExportStats, "Copy IVs"),
        (HotkeyAction.ToggleGlobalHotkeys, "Global Hotkey Lock"),
        (HotkeyAction.ListUp, "List up"),
        (HotkeyAction.ListDown, "List down")
    };

    public static readonly (HotkeyAction Action, string Label)[] ContextActions =
    {
        (HotkeyAction.NpcUp, "Up"),
        (HotkeyAction.NpcDown, "Down"),
        (HotkeyAction.NpcLeft, "Left"),
        (HotkeyAction.NpcRight, "Right"),
        (HotkeyAction.NpcFocusPrev, "Select Previous"),
        (HotkeyAction.NpcFocusNext, "Select Next"),
        (HotkeyAction.NpcUndo, "Undo"),
        (HotkeyAction.NpcComplete, "Toggle"),
        (HotkeyAction.NpcMiss, "Miss")
    };

    public static IEnumerable<(HotkeyAction Action, string Label)> AllActions =>
        Actions.Concat(ContextActions);

    public static bool IsHeld(this Hotkey hotkey)
        => hotkey.IsBound
           && hotkey.IsDown(InputState.IsDown)
           && (hotkey.Global || Win32.IsForeground(StarterTool.MainFormHandle));

    public static bool IsPressed(this Hotkey hotkey, InputPress press)
    {
        if (!hotkey.Global && !press.Foreground) return false;

        int length = hotkey.MatchLength(press.Input, press.Held);
        return length > 0 && length >= press.BestMatch;
    }

    public static bool IsActivatedByEvent(this KeyMethod method, int wParam) => method switch
    {
        KeyMethod.OnPress => wParam is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN,
        KeyMethod.OnRelease => wParam is Win32.WM_KEYUP or Win32.WM_SYSKEYUP,
        _ => false
    };

    public static bool IsActivatedByEdge(this KeyMethod method, bool pressed) => method switch
    {
        KeyMethod.OnPress => pressed,
        KeyMethod.OnRelease => !pressed,
        _ => false
    };

    public static string ToFormattedString(this Keys key) => key switch
    {
        Keys.None => "Unset",

        Keys.Prior => "Page Up",
        Keys.Next => "Page Down",

        Keys.Return => "Enter",
        Keys.Back => "Backspace",
        Keys.Capital => "Caps Lock",
        Keys.Scroll => "Scroll Lock",
        Keys.Apps => "Menu",

        Keys.LShiftKey => "L Shift",
        Keys.RShiftKey => "R Shift",
        Keys.LControlKey => "L Ctrl",
        Keys.RControlKey => "R Ctrl",
        Keys.LMenu => "L Alt",
        Keys.RMenu => "R Alt",
        Keys.LWin => "L Win",
        Keys.RWin => "R Win",

        _ => key.ToString()
    };

    public static string Describe(this InputCode code) => code.IsNone
        ? "Unset"
        : code.IsKeyboard
            ? ((Keys)code.Code).ToFormattedString()
            : GamepadInput.PadName(code.Pad) + " " + GamepadInput.Name(code.Code);

    public static string Describe(this InputChord chord)
        => chord.IsEmpty ? "Unset" : string.Join("+", chord.Inputs.Select(Describe));

    public static string Describe(this Hotkey hotkey)
        => hotkey.IsBound ? string.Join(", ", hotkey.Chords.Select(Describe)) : "Unset";

    public static string ToFormattedString(this KeyMethod method) => method switch
    {
        KeyMethod.OnPress => "On Press",
        KeyMethod.OnRelease => "On Release",
        _ => method.ToString()
    };
}
