using FRLG.StarterTool.Core.Updates;

namespace FRLG.StarterTool.App;

public sealed class UpdateDialog : Form
{
    private readonly ReleaseInfo _release;
    private readonly Label _status;
    private readonly ThemedButton _update;
    private readonly ThemedButton _skip;
    private readonly ThemedButton _snooze;
    private CancellationTokenSource? _download;

    public UpdateDialog(ReleaseInfo release)
    {
        _release = release;

        Text = "New Version Available";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(464, 284);

        Controls.Add(new Label
        {
            Text = "v" + release.Version.ToString(3) + (release.Prerelease ? " (pre-release)" : "")
                + " - you have " + BuildInfo.Display,
            Location = new Point(12, 12),
            AutoSize = true
        });

        var notes = new ThemedTextBox
        {
            Location = new Point(12, 40),
            Size = new Size(440, 180),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Text = NotesOf(release),
            TabStop = false
        };
        Controls.Add(notes);

        _status = new Label
        {
            Location = new Point(12, 232),
            Size = new Size(440, 16),
            AutoEllipsis = true,
            Text = Updater.CanInstall ? "" : "Run from a build folder - Update opens the release page."
        };
        Controls.Add(_status);

        const int ButtonWidth = 100;
        const int ButtonGap = 8;
        int x = 452;
        _snooze = Button("Snooze", ref x, ButtonWidth, ButtonGap);
        _snooze.DialogResult = DialogResult.Cancel;
        _skip = Button("Skip", ref x, ButtonWidth, ButtonGap);
        _update = Button("Update", ref x, ButtonWidth, ButtonGap);

        _skip.Click += (_, _) =>
        {
            Updater.Skip(_release);
            DialogResult = DialogResult.Cancel;
        };
        _update.Click += async (_, _) =>
        {
            if (Updater.CanInstall) await InstallAsync();
            else Updater.OpenPage(_release);
        };

        AcceptButton = _update;
        CancelButton = _snooze;

        FormClosing += (_, _) => _download?.Cancel();

        Theme.Apply(this);
    }

    private ThemedButton Button(string text, ref int right, int width, int gap)
    {
        right -= width;
        var button = new ThemedButton
        {
            Text = text,
            Location = new Point(right, 252),
            Size = new Size(width, 26)
        };
        Controls.Add(button);
        right -= gap;
        return button;
    }

    private async Task InstallAsync()
    {
        _update.Enabled = false;
        _skip.Enabled = false;
        _snooze.Text = "Cancel";
        _download = new CancellationTokenSource();
        _snooze.Click += CancelDownload;

        var progress = new Progress<string>(line => _status.Text = line);
        string? error = await Updater.InstallAsync(_release, progress, _download.Token);

        _snooze.Click -= CancelDownload;
        _download.Dispose();
        _download = null;

        if (error == null)
        {
            _status.Text = "Installed. Restarting…";
            DialogResult = DialogResult.OK;
            return;
        }

        _status.Text = error;
        _snooze.Text = "Snooze";
        _update.Enabled = true;
        _skip.Enabled = true;
    }

    private void CancelDownload(object? sender, EventArgs e)
    {
        _download?.Cancel();
        DialogResult = DialogResult.None;
    }

    private static string NotesOf(ReleaseInfo release)
    {
        string notes = release.Notes.Replace("\r\n", "\n").Replace("\n", "\r\n").Trim();
        if (notes.Length == 0) notes = "No release notes.";
        string url = release.PageUrl.Length != 0 ? release.PageUrl : Updater.ReleasesUrl;
        return notes + "\r\n\r\n" + url;
    }
}
