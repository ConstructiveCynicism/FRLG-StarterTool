using FRLG.StarterTool.Core.RomPatch;

namespace FRLG.StarterTool.App;

public sealed class RomPatchPanel : Panel
{
    public const int PanelWidth = 594;

    private const int RowHeight = 22;

    private const int BrowseX = PanelWidth - 62;

    private const int FieldX = 50;

    private const int StatusTop = 52;
    private const int ButtonTop = 78;

    public const int PanelHeight = ButtonTop + RowHeight;

    private const int VerifyDelayMs = 400;

    private readonly TextBox _romBox;
    private readonly TextBox _outputBox;
    private readonly Label _status;
    private readonly Button _buttonPatch;
    private readonly System.Windows.Forms.Timer _verifyDelay;

    private RomKind? _kind;

    private bool _sticky;

    public RomPatchPanel()
    {
        Size = new Size(PanelWidth, PanelHeight);

        Controls.Add(MakeLabel("Load From", 0, 3, FieldX - 6, ContentAlignment.MiddleRight));
        Controls.Add(MakeLabel("Save To", 0, 29, FieldX - 6, ContentAlignment.MiddleRight));

        _romBox = MakeBox(FieldX, 0, BrowseX - FieldX - 2);
        _verifyDelay = new System.Windows.Forms.Timer { Interval = VerifyDelayMs };
        _verifyDelay.Tick += (_, _) =>
        {
            _verifyDelay.Stop();
            Verify();
        };
        _romBox.TextChanged += (_, _) =>
        {
            _sticky = false;
            _kind = null;
            _verifyDelay.Stop();
            _verifyDelay.Start();
        };
        Controls.Add(_romBox);

        _outputBox = MakeBox(FieldX, 26, BrowseX - FieldX - 2);
        _outputBox.TextChanged += (_, _) =>
        {
            _sticky = false;
            Report();
        };
        Controls.Add(_outputBox);

        Button browseRom = MakeButton("Browse…", BrowseX, 0, 62);
        browseRom.Click += (_, _) => BrowseFile(
            _romBox, "Clean FireRed/LeafGreen (U) v1.1 ROM", "GBA ROM (*.gba)|*.gba|All files (*.*)|*.*");
        Controls.Add(browseRom);

        Button browseOutput = MakeButton("Browse…", BrowseX, 26, 62);
        browseOutput.Click += (_, _) => BrowseOutput();
        Controls.Add(browseOutput);

        _status = new Label
        {
            Location = new Point(0, StatusTop),
            Size = new Size(PanelWidth, RowHeight),
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = Theme.KeepForeColor
        };
        Controls.Add(_status);

        _buttonPatch = MakeButton("Patch ROM…", 0, ButtonTop, 130);
        _buttonPatch.Click += (_, _) => ApplyPatch();
        Controls.Add(_buttonPatch);

        Report();
    }

    public string RomPath
    {
        get => _romBox.Text;
        set => _romBox.Text = value;
    }

    public string OutputPath
    {
        get => _outputBox.Text;
        set => _outputBox.Text = value;
    }

    private void Verify()
    {
        _kind = null;

        string path = _romBox.Text.Trim();
        if (path.Length == 0 || !File.Exists(path))
        {
            Report();
            return;
        }

        try
        {
            _kind = RomPatcher.Identify(File.ReadAllBytes(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Say(e.Message, hit: false);
            return;
        }

        Report();
    }

    private void Report()
    {
        _buttonPatch.Enabled = _kind != null;
        if (_sticky) return;

        string path = _romBox.Text.Trim();
        if (path.Length == 0)
        {
            _status.Text = "Works with a FireRed or LeafGreen v1.1 ROM.";
            _status.ForeColor = Theme.DimText;
            return;
        }

        if (!File.Exists(path))
        {
            _status.Text = "No such file.";
            _status.ForeColor = Theme.DimText;
            return;
        }

        if (_kind is null)
        {
            _status.Text = RomPatcher.VersionError;
            _status.ForeColor = Theme.LandingMissText;
            return;
        }

        _status.Text = _kind.Name;
        _status.ForeColor = Theme.LandingHitText;
    }

    private void ApplyPatch()
    {
        string rom = _romBox.Text.Trim();
        string output = _outputBox.Text.Trim();

        if (output.Length == 0)
        {
            Say("Choose where to save the patched ROM.", hit: false);
            return;
        }

        try
        {
            RomKind kind = RomPatcher.Patch(rom, patchPath: null, output);
            Say($"Patched {kind.Name} to {Path.GetFileName(output)}.", hit: true);
        }
        catch (Exception e) when (
            e is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            Say(e.Message, hit: false);
        }
    }

    private void BrowseOutput()
    {
        string rom = _romBox.Text.Trim();
        using var dialog = new SaveFileDialog
        {
            Title = "Write the patched ROM to",
            Filter = "GBA ROM (*.gba)|*.gba|All files (*.*)|*.*",
            FileName = _outputBox.Text.Trim().Length > 0
                ? _outputBox.Text.Trim()
                : Path.GetFileNameWithoutExtension(rom) + " (patched).gba",
            InitialDirectory = Path.GetDirectoryName(
                _outputBox.Text.Trim().Length > 0 ? _outputBox.Text.Trim() : rom) ?? "",
            OverwritePrompt = true
        };

        if (StarterTool.Modal(() => dialog.ShowDialog(this)) == DialogResult.OK)
        {
            _outputBox.Text = dialog.FileName;
        }
    }

    private void Say(string message, bool hit)
    {
        _sticky = true;
        _status.Text = message;
        _status.ForeColor = hit ? Theme.LandingHitText : Theme.LandingMissText;
    }

    private void BrowseFile(TextBox target, string title, string filter)
    {
        using var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            FileName = target.Text,
            InitialDirectory = Path.GetDirectoryName(target.Text) ?? ""
        };

        if (StarterTool.Modal(() => dialog.ShowDialog(this)) == DialogResult.OK)
        {
            target.Text = dialog.FileName;
        }
    }

    private static Label MakeLabel(string text, int x, int y, int width, ContentAlignment align) =>
        new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 16),
            TextAlign = align
        };

    private static TextBox MakeBox(int x, int y, int width) =>
        new ThemedTextBox
        {
            Numeric = false,
            AutoSize = false,
            Location = new Point(x, y),
            Size = new Size(width, RowHeight)
        };

    private static Button MakeButton(string text, int x, int y, int width) =>
        new ThemedButton { Text = text, Location = new Point(x, y), Size = new Size(width, RowHeight) };
}
