using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public partial class MainForm
{
    private static volatile bool _numberFieldFocused;

    public static bool NumberFieldFocused => _numberFieldFocused;

    private static void WatchNumberFields(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is TextBox box)
            {
                box.Enter += (_, _) => _numberFieldFocused = true;
                box.Leave += (_, _) => _numberFieldFocused = false;
            }

            WatchNumberFields(control);
        }
    }

    public static bool IsNumberKey(Keys key) =>
        key is >= Keys.D0 and <= Keys.D9
            or >= Keys.NumPad0 and <= Keys.NumPad9
            or Keys.Decimal;

    public void ScrollResults(HotkeyAction action)
    {
        if (!ListViewResults.Visible) return;

        MoveResultSelection(action switch
        {
            HotkeyAction.ListUp => -1,
            HotkeyAction.ListDown => 1,
            _ => 0
        });
    }

    public void HandleGlobalNumpad(Keys rawKey, bool extended)
    {
        if (!StarterTool.Settings.GlobalNumpadInput) return;
        if (ReferenceEquals(ActiveForm, this)) return;

        if (TrainingPanel.Visible) return;

        Keys key = TranslateNumpad(rawKey, extended);
        if (key == Keys.None) return;

        bool navigating = ReferenceEquals(ActiveControl, ListViewResults) && ListViewResults.Visible;
        if (navigating && HandleResultsNumpad(key)) return;

        HandleTrainerIdNumpad(key);
    }

    private static Keys TranslateNumpad(Keys key, bool extended) => key switch
    {
        >= Keys.NumPad0 and <= Keys.NumPad9 => key,
        Keys.Decimal or Keys.Add or Keys.Subtract => key,
        Keys.Return => extended ? Keys.Return : Keys.None,

        Keys.Insert => extended ? Keys.None : Keys.NumPad0,
        Keys.End => extended ? Keys.None : Keys.NumPad1,
        Keys.Down => extended ? Keys.None : Keys.NumPad2,
        Keys.PageDown => extended ? Keys.None : Keys.NumPad3,
        Keys.Left => extended ? Keys.None : Keys.NumPad4,
        Keys.Clear => extended ? Keys.None : Keys.NumPad5,
        Keys.Right => extended ? Keys.None : Keys.NumPad6,
        Keys.Home => extended ? Keys.None : Keys.NumPad7,
        Keys.Up => extended ? Keys.None : Keys.NumPad8,
        Keys.PageUp => extended ? Keys.None : Keys.NumPad9,
        Keys.Delete => extended ? Keys.None : Keys.Decimal,

        _ => Keys.None
    };

    private void HandleTrainerIdNumpad(Keys key)
    {
        if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
        {
            TypeTrainerIdDigit((char)('0' + (key - Keys.NumPad0)));
            return;
        }

        switch (key)
        {
            case Keys.Decimal:
                ResetTrainerId();
                break;

            case Keys.Return:
                RunSearch();
                break;
        }
    }

    private bool HandleResultsNumpad(Keys key)
    {
        VariableOffsetTimer? timer = StarterTool.VariableOffset;

        switch (key)
        {
            case Keys.Add:
                if (ButtonPlus.Enabled) timer?.Nudge(1);
                return true;

            case Keys.Subtract:
                if (ButtonMinus.Enabled) timer?.Nudge(-1);
                return true;

            case Keys.Return:
                timer?.Arm();
                return true;

            default:
                return false;
        }
    }

    private void TypeTrainerIdDigit(char digit)
    {
        if (!TextBoxTrainerId.Enabled) return;

        FocusTrainerIdIfNeeded();
        TextBoxTrainerId.SelectedText = digit.ToString();
    }

    private void ResetTrainerId()
    {
        FocusTrainerIdIfNeeded();
        TextBoxTrainerId.Clear();
    }

    private void FocusTrainerIdIfNeeded()
    {
        if (ReferenceEquals(ActiveControl, TextBoxTrainerId)) return;

        FocusTrainerId();
    }

    internal void TakeCaret(Control control)
    {
        if (!control.CanSelect) return;

        control.Focus();
        if (!ReferenceEquals(ActiveControl, control)) ActiveControl = control;
    }

    private void MoveResultSelection(int delta)
    {
        if (delta == 0 || _results.Count == 0) return;

        int next;
        if (ListViewResults.SelectedIndices.Count == 0)
        {
            next = delta > 0 ? 0 : _results.Count - 1;
        }
        else
        {
            int current = ListViewResults.SelectedIndices[0];
            next = Math.Clamp(current + delta, 0, _results.Count - 1);
            if (next == current) return;
        }

        ListViewResults.SelectedIndices.Clear();
        ListViewResults.SelectedIndices.Add(next);
        ListViewResults.EnsureVisible(next);
    }
}
