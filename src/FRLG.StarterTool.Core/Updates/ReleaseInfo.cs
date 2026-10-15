using System.Globalization;
using System.Text.Json;

namespace FRLG.StarterTool.Core.Updates;

public sealed record ReleaseAsset(string Name, string Url, long Size, string? Sha256);

public sealed record ReleaseInfo(
    Version Version,
    string Tag,
    string PageUrl,
    string Notes,
    bool Prerelease,
    IReadOnlyList<ReleaseAsset> Assets)
{
    public static ReleaseInfo? Parse(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            string? tag = StringOf(root, "tag_name");
            if (tag == null || TryParseVersion(tag) is not { } version) return null;

            var assets = new List<ReleaseAsset>();
            if (root.TryGetProperty("assets", out JsonElement list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement asset in list.EnumerateArray())
                {
                    string? name = StringOf(asset, "name");
                    string? url = StringOf(asset, "browser_download_url");
                    if (name == null || url == null) continue;

                    long size = asset.TryGetProperty("size", out JsonElement s) && s.ValueKind == JsonValueKind.Number
                        ? s.GetInt64() : 0;
                    assets.Add(new ReleaseAsset(name, url, size, Sha256Of(StringOf(asset, "digest"))));
                }
            }

            bool prerelease = root.TryGetProperty("prerelease", out JsonElement pre)
                && pre.ValueKind == JsonValueKind.True;

            return new ReleaseInfo(
                version,
                tag,
                StringOf(root, "html_url") ?? "",
                StringOf(root, "body") ?? "",
                prerelease,
                assets);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public ReleaseAsset? Asset(string name) =>
        Assets.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

    public static Version? TryParseVersion(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        string core = text.Trim();
        if (core.StartsWith('v') || core.StartsWith('V')) core = core[1..];
        int cut = core.IndexOfAny(['-', '+']);
        if (cut >= 0) core = core[..cut];

        string[] parts = core.Split('.');
        if (parts.Length is < 2 or > 4) return null;

        var numbers = new int[3];
        for (int i = 0; i < Math.Min(parts.Length, 3); i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return null;
            }
        }

        return new Version(numbers[0], numbers[1], numbers[2]);
    }

    public static bool IsNewer(Version latest, Version current) =>
        Normalize(latest).CompareTo(Normalize(current)) > 0;

    private static Version Normalize(Version v) =>
        new(Math.Max(v.Major, 0), Math.Max(v.Minor, 0), Math.Max(v.Build, 0));

    private static string? StringOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    public static string? Sha256Of(string? digest)
    {
        const string Prefix = "sha256:";
        if (digest == null || !digest.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return null;

        string hex = digest[Prefix.Length..].Trim().ToLowerInvariant();
        return hex.Length == 64 && hex.All(Uri.IsHexDigit) ? hex : null;
    }

    public static string? Sha256OfSidecar(string text)
    {
        foreach (string token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = token.Trim().TrimStart('*').ToLowerInvariant();
            if (candidate.Length == 64 && candidate.All(Uri.IsHexDigit)) return candidate;
        }

        return null;
    }
}
