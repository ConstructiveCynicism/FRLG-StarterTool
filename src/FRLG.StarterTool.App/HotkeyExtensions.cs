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
           && (Win32.IsKeyDown((Keys)hotkey.Primary) || Win32.IsKeyDown((Keys)hotkey.Secondary))
           && (hotkey.Global || Win32.IsForeground(StarterTool.MainFormHandle));

    public static bool IsPressed(this Hotkey hotkey, Keys key)
        => hotkey.Matches((int)key) && (hotkey.Global || Win32.IsForeground(StarterTool.MainFormHandle));

    public static bool IsActivatedByEvent(this KeyMethod method, int wParam) => method switch
    {
        KeyMethod.OnPress => wParam is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN,
        KeyMethod.OnRelease => wParam is Win32.WM_KEYUP or Win32.WM_SYSKEYUP,
        _ => false
    };

    public static string ToFormattedString(this Keys key) => key == Keys.None ? "Unset" : key.ToString();

    public static string ToFormattedString(this KeyMethod method) => method switch
    {
        KeyMethod.OnPress => "On Press",
        KeyMethod.OnRelease => "On Release",
        _ => method.ToString()
    };
}
