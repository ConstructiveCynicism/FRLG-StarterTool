using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using FRLG.StarterTool.Core.Updates;

namespace FRLG.StarterTool.App;

public static class Updater
{
    public const string Repository = "ConstructiveCynicism/FRLG-StarterTool";

    public const string AssetName = "FRLGStarterTool.exe";

    public const string ReleasesUrl = "https://github.com/" + Repository + "/releases";

    private const string LatestUrl = "https://api.github.com/repos/" + Repository + "/releases/latest";

    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(20);

    private static readonly Lazy<HttpClient> Http = new(() =>
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FRLGStarterTool",
            BuildInfo.Version.ToString()));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://github.com/" + Repository + ")"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    });

    private static int _checking;

    public static ReleaseInfo? Available { get; private set; }

    public static event Action? AvailableChanged;

    public static string? PendingLaunch { get; private set; }

    public static bool CanInstall => InstalledExe != null;

    private static string? InstalledExe
    {
        get
        {
            string? path = Environment.ProcessPath;
            if (path == null) return null;
            if (!string.Equals(Path.GetFileName(path), AssetName, StringComparison.OrdinalIgnoreCase)) return null;

            string? directory = Path.GetDirectoryName(path);
            if (directory == null) return null;

            string dll = Path.Combine(directory, Path.GetFileNameWithoutExtension(AssetName) + ".dll");
            return File.Exists(dll) ? null : path;
        }
    }

    public static void Start()
    {
        _ = Task.Run(async () =>
        {
            await SweepOldAsync();
            await Task.Delay(StartDelay);
            await CheckAsync(interactive: false);
        });
    }

    public static void CheckNow() => _ = CheckAsync(interactive: true);

    public static void ShowAvailable()
    {
        if (Available is { } release) ShowDialog(release);
    }

    public static void OpenPage(ReleaseInfo? release)
    {
        string url = release is { PageUrl.Length: > 0 } ? release.PageUrl : ReleasesUrl;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }

    public static void Skip(ReleaseInfo release)
    {
        StarterTool.Settings.UpdateSkipVersion = release.Tag;
        SetAvailable(null);
    }

    private static async Task CheckAsync(bool interactive)
    {
        if (Interlocked.Exchange(ref _checking, 1) != 0) return;

        ReleaseInfo? latest = null;
        string? error = null;
        try
        {
            using var timeout = new CancellationTokenSource(CheckTimeout);
            string json = await Http.Value.GetStringAsync(LatestUrl, timeout.Token);
            latest = ReleaseInfo.Parse(json);
            if (latest == null) error = "GitHub's answer named no release.";
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or IOException)
        {
            error = e is OperationCanceledException ? "No answer from GitHub in time." : e.Message;
        }
        finally
        {
            Volatile.Write(ref _checking, 0);
        }

        StarterTool.Post(() => Report(latest, error, interactive));
    }

    private static void Report(ReleaseInfo? latest, string? error, bool interactive)
    {
        if (latest == null)
        {
            if (interactive)
            {
                Say("Could not check for updates.\r\n\r\n" + error + "\r\n\r\nReleases: " + ReleasesUrl,
                    MessageBoxIcon.Warning);
            }

            return;
        }

        if (!ReleaseInfo.IsNewer(latest.Version, BuildInfo.Version))
        {
            SetAvailable(null);
            if (interactive)
            {
                Say("You have the latest version (" + BuildInfo.Display + ").", MessageBoxIcon.Information);
            }

            return;
        }

        bool skipped = string.Equals(StarterTool.Settings.UpdateSkipVersion, latest.Tag, StringComparison.OrdinalIgnoreCase);
        if (skipped && !interactive) return;

        SetAvailable(latest);
        if (interactive || !StarterTool.IsTimerRunning) ShowDialog(latest);
    }

    private static void SetAvailable(ReleaseInfo? release)
    {
        if (ReferenceEquals(Available, release)) return;

        Available = release;
        AvailableChanged?.Invoke();
    }

    private static void ShowDialog(ReleaseInfo release)
    {
        if (StarterTool.MainForm is not { IsHandleCreated: true } owner) return;

        using var dialog = new UpdateDialog(release);
        DialogResult result = StarterTool.Modal(() => dialog.ShowDialog(owner));
        if (result == DialogResult.OK && PendingLaunch != null) owner.Close();
    }

    private static void Say(string text, MessageBoxIcon icon)
    {
        if (StarterTool.MainForm is not { IsHandleCreated: true } owner) return;

        StarterTool.Modal(() => MessageBox.Show(owner, text, "Check for updates", MessageBoxButtons.OK, icon));
    }

    public static async Task<string?> InstallAsync(ReleaseInfo release, IProgress<string> progress, CancellationToken cancel)
    {
        string? exe = InstalledExe;
        if (exe == null) return "This build cannot update itself; download the release from its page.";

        ReleaseAsset? asset = release.Asset(AssetName);
        if (asset == null) return "The release has no " + AssetName + " attached.";

        string? expected = asset.Sha256;
        if (expected == null && release.Asset(AssetName + ".sha256") is { } sidecar)
        {
            try
            {
                expected = ReleaseInfo.Sha256OfSidecar(await Http.Value.GetStringAsync(sidecar.Url, cancel));
            }
            catch (Exception e) when (e is HttpRequestException or IOException)
            {
            }
        }

        if (expected == null) return "The release carries no checksum to verify the download against.";

        string fresh = exe + ".new";
        string old = exe + ".old";
        try
        {
            string actual = await DownloadAsync(asset, fresh, progress, cancel);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(fresh);
                return "The download did not match the release's checksum and was discarded.";
            }

            progress.Report("Installing…");
            TryDelete(old);
            File.Move(exe, old);
            try
            {
                File.Move(fresh, exe);
            }
            catch (Exception)
            {
                File.Move(old, exe);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            TryDelete(fresh);
            return "Cancelled.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or HttpRequestException)
        {
            TryDelete(fresh);
            return e.Message;
        }

        PendingLaunch = exe;
        return null;
    }

    private static async Task<string> DownloadAsync(ReleaseAsset asset, string path, IProgress<string> progress, CancellationToken cancel)
    {
        progress.Report("Downloading…");
        using HttpResponseMessage response = await Http.Value.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancel);
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? asset.Size;
        await using Stream source = await response.Content.ReadAsStreamAsync(cancel);
        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        byte[] buffer = new byte[1 << 16];
        long done = 0;
        int lastPercent = -1;
        int read;
        while ((read = await source.ReadAsync(buffer, cancel)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), cancel);
            hasher.AppendData(buffer, 0, read);
            done += read;

            int percent = total > 0 ? (int)(done * 100 / total) : -1;
            if (percent != lastPercent)
            {
                lastPercent = percent;
                progress.Report(percent >= 0 ? "Downloading… " + percent + "%" : "Downloading… " + (done >> 20) + " MB");
            }
        }

        return Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
    }

    private static async Task SweepOldAsync()
    {
        string? exe = InstalledExe;
        if (exe == null) return;

        string old = exe + ".old";
        for (int attempt = 0; attempt < 5 && File.Exists(old); attempt++)
        {
            if (attempt > 0) await Task.Delay(TimeSpan.FromSeconds(2));
            TryDelete(old);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
