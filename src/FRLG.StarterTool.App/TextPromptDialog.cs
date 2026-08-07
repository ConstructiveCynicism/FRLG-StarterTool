namespace FRLG.StarterTool.App;

public sealed class TextPromptDialog : Form
{
    private readonly TextBox _input;

    public TextPromptDialog(string title, string prompt, string initialValue = "")
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(320, 108);

        Controls.Add(new Label { Text = prompt, Location = new Point(12, 12), AutoSize = true });

        _input = new ThemedTextBox
        {
            Location = new Point(12, 34),
            Size = new Size(296, 23),
            Text = initialValue,
            MaxLength = 40
        };
        _input.SelectAll();
        Controls.Add(_input);

        var ok = new ThemedButton
        {
            Text = "OK",
            Location = new Point(148, 68),
            Size = new Size(76, 28),
            DialogResult = DialogResult.OK
        };
        var cancel = new ThemedButton
        {
            Text = "Cancel",
            Location = new Point(232, 68),
            Size = new Size(76, 28),
            DialogResult = DialogResult.Cancel
        };
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        ok.Click += (_, _) =>
        {
            if (Value.Length != 0) return;

            DialogResult = DialogResult.None;
        };

        Theme.Apply(this);
    }

    public string Value => _input.Text.Trim();
}
