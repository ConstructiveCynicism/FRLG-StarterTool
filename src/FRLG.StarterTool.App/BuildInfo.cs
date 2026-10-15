using System.Reflection;
using FRLG.StarterTool.Core.Updates;

namespace FRLG.StarterTool.App;

public static class BuildInfo
{
    public static string Informational { get; } =
        typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "";

    public static Version Version { get; } =
        ReleaseInfo.TryParseVersion(Informational) ?? new Version(0, 0, 0);

    public static string Suffix { get; } = SuffixOf(Informational);

    public static string Commit { get; } = CommitOf(Informational);

    public static bool IsTestBuild => Suffix.Length != 0;

    public static string Display
    {
        get
        {
            if (Informational.Length == 0) return "";

            string shown = Informational.Split('+')[0];
            return "v" + shown + (IsTestBuild && Commit.Length != 0 ? " (" + Commit + ")" : "");
        }
    }

    private static string SuffixOf(string informational)
    {
        string before = informational.Split('+')[0];
        int dash = before.IndexOf('-');
        return dash < 0 ? "" : before[(dash + 1)..];
    }

    private static string CommitOf(string informational)
    {
        int plus = informational.IndexOf('+');
        if (plus < 0) return "";

        string sha = informational[(plus + 1)..];
        return sha.Length > 7 ? sha[..7] : sha;
    }
}
