namespace FRLG.StarterTool.App;

public sealed class HotkeySelection : Form
{
    public Keys Key { get; private set; } = Keys.None;

    public HotkeySelection()
    {
        Text = "Bind key";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(200, 64);
        KeyPreview = true;

        Controls.Add(new Label
        {
            Text = "Press any key...",
            Location = new Point(16, 22),
            AutoSize = true
        });

        Theme.Apply(this);
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

        if (key == Keys.None)
        {
            return true;
        }

        Key = key;
        DialogResult = DialogResult.OK;
        return true;
    }
}
