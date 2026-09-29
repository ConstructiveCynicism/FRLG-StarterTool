using System.Globalization;
using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Voice;

namespace FRLG.StarterTool.App;

public partial class MainForm
{
    private static volatile bool _numberFieldFocused;

    public static bool NumberFieldFocused => _numberFieldFocused;

    private static volatile bool _trainerIdFocused;

    public static bool TrainerIdFocused => _trainerIdFocused;

    private void WatchNumberFields(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is TextBox box) WatchNumberField(box);

            WatchNumberFields(control);
        }
    }

    internal void WatchNumberField(TextBox box)
    {
        box.Enter += (_, _) => _numberFieldFocused = true;
        box.Leave += (_, _) => _numberFieldFocused = false;

        box.KeyUp += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;

            if (box.Focused) ReleaseNumberField(box);
        };
    }

    public static bool IsNumberKey(Keys key) =>
        key is >= Keys.D0 and <= Keys.D9
            or >= Keys.NumPad0 and <= Keys.NumPad9
            or Keys.Decimal;

    private void ReleaseNumberField(TextBox box)
    {
        HandCaretToResults();
        if (box.Focused) ActiveControl = null;
    }

    public static bool IsTextEntryKey(Keys key) => key switch
    {
        >= Keys.D0 and <= Keys.D9 => true,
        >= Keys.A and <= Keys.Z => true,
        >= Keys.NumPad0 and <= Keys.NumPad9 => true,
        >= Keys.Oem1 and <= Keys.OemBackslash => true,
        Keys.Multiply or Keys.Add or Keys.Subtract or Keys.Decimal or Keys.Divide => true,
        Keys.Space => true,

        Keys.Enter or Keys.Escape or Keys.Tab => true,
        Keys.Back or Keys.Delete or Keys.Insert => true,
        Keys.Left or Keys.Right or Keys.Up or Keys.Down => true,
        Keys.Home or Keys.End => true,

        _ => false
    };

    public void ScrollResults(HotkeyAction action)
    {
        int delta = action switch
        {
            HotkeyAction.ListUp => -1,
            HotkeyAction.ListDown => 1,
            _ => 0
        };

        switch (_selectedTab)
        {
            case TabKey.Manip:
                MoveResultSelection(delta);
                break;
            case TabKey.GenericFixed:
                FixedPanel.MoveSelection(delta);
                break;
            case TabKey.GenericIgt:
                IgtPanel.MoveSelection(delta);
                break;
        }
    }

    public void HandleGlobalNumpad(Keys rawKey, bool extended)
    {
        if (!StarterTool.Settings.GlobalNumpadInput) return;
        if (ReferenceEquals(ActiveForm, this)) return;

        if (_selectedTab is TabKey.Training or TabKey.GenericTraining
            or TabKey.GenericFixed or TabKey.GenericIgt)
        {
            return;
        }

        Keys key = TranslateNumpad(rawKey, extended);
        if (key == Keys.None) return;

        if (_selectedTab == TabKey.GenericVariable)
        {
            if (StarterTool.IsTimerRunning) HandleFrameNumpad(key);
            return;
        }

        bool navigating = ReferenceEquals(ActiveControl, ListViewResults) && _selectedTab == TabKey.Manip;
        if (navigating && HandleResultsNumpad(key)) return;

        if (!StarterTool.IsTimerRunning) return;

        if (_trainerIdLocked) return;

        HandleTrainerIdNumpad(key);
    }

    public static Keys TranslateNumpad(Keys key, bool extended) => key switch
    {
        >= Keys.NumPad0 and <= Keys.NumPad9 => key,
        Keys.Decimal => key,
        Keys.Return => extended ? Keys.Return : Keys.None,

        >= Keys.D0 and <= Keys.D9 => TrainerIdFocused ? Keys.NumPad0 + (key - Keys.D0) : Keys.None,

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

        Keys.Back => TrainerIdFocused ? Keys.Back : Keys.None,

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

            case Keys.Back:
                EraseTrainerIdDigit();
                break;

            case Keys.Return:
                RunSearch();
                break;
        }
    }

    public void EnterSpokenTrainerId(string heard, SpokenId? spoken, int press, bool final, bool paused)
    {
        if (press == _voiceEnteredPress) return;
        string said = heard.Length > 0 ? $"\"{heard}\"" : "nothing";

        string? closed = _selectedTab is TabKey.Training or TabKey.GenericTraining or TabKey.GenericFixed
                or TabKey.GenericIgt or TabKey.GenericVariable
            ? "not on this tab"
            : !StarterTool.IsTimerRunning ? "no run started"
            : _trainerIdLocked ? "the ID is locked"
            : !TextBoxTrainerId.Enabled ? "the ID box is off"
            : null;
        if (closed != null)
        {
            if (final) ContextSession.Log($"voice: heard {said}, ignored - {closed}");
            return;
        }

        if (spoken is not { } id)
        {
            if (!final) return;
            ContextSession.Log($"voice: heard {said}, not a Trainer ID");
            LabelLanding.Text = heard.Length > 0 ? $"Voice: {said} is not a Trainer ID" : "Voice: heard nothing";
            return;
        }

        if (!final)
        {
            bool caretIn = ReferenceEquals(ActiveControl, TextBoxTrainerId);
            int prospective = id.Kind == SpokenKind.Number || id.Digits.Length == MaxTrainerIdDigits || !caretIn
                ? id.Digits.Length
                : TextBoxTrainerId.Text.Length - TextBoxTrainerId.SelectionLength + id.Digits.Length;
            bool open = !paused && SpokenNumber.EndsOpen(heard.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            bool complete = prospective == MaxTrainerIdDigits && !open && (id.Kind == SpokenKind.Digits || paused);
            if (!complete) return;
        }

        _voiceEnteredPress = press;
        FocusTrainerIdIfNeeded();

        if (id.Kind == SpokenKind.Number || id.Digits.Length == MaxTrainerIdDigits)
        {
            TextBoxTrainerId.Text = id.Digits;
            TextBoxTrainerId.SelectionStart = id.Digits.Length;
        }
        else
        {
            string box = TextBoxTrainerId.Text;
            int typed = box.Length - TextBoxTrainerId.SelectionLength + id.Digits.Length;
            if (typed > MaxTrainerIdDigits)
            {
                ContextSession.Log($"voice: heard {said}, refused - it would make {typed} digits");
                LabelLanding.Text = $"Voice: {said} would make {typed} digits";
                return;
            }

            foreach (char digit in id.Digits) TypeTrainerIdDigit(digit);
        }

        string entered = TextBoxTrainerId.Text;
        bool whole = entered.Length == MaxTrainerIdDigits;
        bool valid = int.TryParse(entered, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
                     && value <= MaxTrainerId;

        ContextSession.Log($"voice: heard {said}{(final ? "" : " (key still down)")}, ID box {entered}"
                           + (!whole ? ", waiting for more" : valid ? ", searching" : ", not a Trainer ID"));

        if (whole && !valid)
        {
            LabelLanding.Text = $"Voice: {entered} is over {MaxTrainerId}";
            return;
        }

        ClearSearchNote();
        if (whole) RunSearch();
    }

    private const int MaxTrainerIdDigits = 5;

    private int _voiceEnteredPress;

    private void HandleFrameNumpad(Keys key)
    {
        if (!TextBoxFrame.Enabled) return;
        if (!ReferenceEquals(ActiveControl, TextBoxFrame)) TakeCaret(TextBoxFrame);

        if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
        {
            TextBoxFrame.SelectedText = ((char)('0' + (key - Keys.NumPad0))).ToString();
            return;
        }

        switch (key)
        {
            case Keys.Decimal:
                TextBoxFrame.Clear();
                break;
            case Keys.Return:
                StarterTool.VariableOffset.Arm();
                break;
        }
    }

    private bool HandleResultsNumpad(Keys key)
    {
        VariableOffsetTimer? timer = StarterTool.VariableOffset;

        switch (key)
        {
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

        if (_trainerIdLocked) return;

        FocusTrainerIdIfNeeded();
        TextBoxTrainerId.SelectedText = digit.ToString();
    }

    private void EraseTrainerIdDigit()
    {
        if (!TextBoxTrainerId.Enabled) return;
        if (_trainerIdLocked) return;

        FocusTrainerIdIfNeeded();
        if (TextBoxTrainerId.SelectionLength > 0)
        {
            TextBoxTrainerId.SelectedText = "";
            return;
        }

        int at = TextBoxTrainerId.SelectionStart;
        if (at <= 0) return;
        TextBoxTrainerId.Text = TextBoxTrainerId.Text.Remove(at - 1, 1);
        TextBoxTrainerId.SelectionStart = at - 1;
    }

    private void ResetTrainerId()
    {
        if (_trainerIdLocked) return;

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
