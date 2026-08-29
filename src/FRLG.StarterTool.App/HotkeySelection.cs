using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public sealed class HotkeySelection : Form
{
    private readonly List<InputCode> _collected = new();

    private readonly Label _chordLabel;

    private readonly System.Windows.Forms.Timer _poll;

    private readonly Action<InputCode, bool, double> _onGamepad;

    public InputChord Chord { get; private set; } = new();

    public HotkeySelection()
    {
        Text = "Bind input";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(300, 118);
        KeyPreview = true;

        Controls.Add(new Label
        {
            Text = "Press a key or controller button.\r\nHold inputs together to bind a combo;\r\nrelease to finish.",
            Location = new Point(16, 12),
            AutoSize = true
        });

        _chordLabel = new Label
        {
            Text = Gamepads.ConnectedCount > 0 ? "" : "(no controller detected)",
            Location = new Point(16, 66),
            AutoSize = true,
            Tag = Theme.KeepForeColor,
            ForeColor = Theme.DimText
        };
        Controls.Add(_chordLabel);

        var cancel = new ThemedButton
        {
            Text = "Cancel",
            Size = new Size(72, 25),
            Location = new Point(300 - 72 - 12, 118 - 25 - 10)
        };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        Controls.Add(cancel);

        _poll = new System.Windows.Forms.Timer { Interval = 8 };
        _poll.Tick += (_, _) => CheckForRelease();
        _poll.Start();

        _onGamepad = (code, pressed, _) =>
        {
            if (!pressed) return;
            try
            {
                BeginInvoke(() => Collect(code));
            }
            catch (Exception e) when (e is ObjectDisposedException or InvalidOperationException)
            {
            }
        };
        Gamepads.Changed += _onGamepad;

        Theme.Apply(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Gamepads.Changed -= _onGamepad;
            _poll.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;

        key = key switch
        {
            Keys.ShiftKey => Win32.GetAsyncKey(Keys.LShiftKey, Keys.RShiftKey),
            Keys.ControlKey => Win32.GetAsyncKey(Keys.LControlKey, Keys.RControlKey),
            Keys.Menu => Win32.GetAsyncKey(Keys.LMenu, Keys.RMenu),
            _ => key
        };

        if (key != Keys.None) Collect(InputCode.Key((int)key));
        return true;
    }

    private void Collect(InputCode code)
    {
        if (IsDisposed || DialogResult != DialogResult.None || _collected.Contains(code)) return;

        _collected.Add(code);
        _chordLabel.ForeColor = Theme.Text;
        _chordLabel.Text = new InputChord(_collected).Describe();
    }

    private void CheckForRelease()
    {
        if (_collected.Count == 0 || DialogResult != DialogResult.None) return;

        foreach (InputCode code in _collected)
        {
            if (InputState.IsDown(code)) continue;

            Chord = new InputChord(_collected);
            DialogResult = DialogResult.OK;
            return;
        }
    }
}
