using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FRLG.StarterTool.Core.Encounters;

public readonly record struct RecipeStep(int Frame, int Window, string Text, bool Cued, string Label = "boot")
{
    public ManipPress Press => new("", Frame, Math.Max(Window, 1));
}

public sealed record TitleRecipe(TitleVariant Variant, string Key, string Skip, string LoopSkip,
    int Anchor0, int Anchor, int Land, string PairEntry, string LateEntry, string Alias)
{
    private string[] Parts => IsCombo ? Array.Empty<string>() : Key.Split('-');

    private bool Has(string part) => Array.IndexOf(Parts, part) >= 0;

    private string? GapButton => Key.StartsWith("gap-", StringComparison.Ordinal) ? Key[4..] : null;

    public bool IsCombo => Key.StartsWith("x-", StringComparison.Ordinal);

    private string? HeldButton => Parts.Length > 0 && Parts[0].Length == 2 && Parts[0][1] == '0' && "alr".Contains(Parts[0][0]) ? Parts[0][..1] : null;

    private int ReleaseFrame => Parts.Skip(1).FirstOrDefault(p => p.StartsWith("up", StringComparison.Ordinal)) is string up
        ? int.Parse(up[2..], CultureInfo.InvariantCulture) : 0;

    private int SkipReleaseFrame => Parts.FirstOrDefault(p => p.StartsWith("rel", StringComparison.Ordinal)) is string rel
        ? int.Parse(rel[3..], CultureInfo.InvariantCulture) : 0;

    public int SkipFrame => !Variant.IntroSkipped ? 0
        : Has("ltap") ? (Variant.Intro == TitleIntro.Skip477 ? TapSkip477Frame - (Variant.Saves == TitleSaves.Single ? 1 : 0) : TapSkip990Frame)
        : Skip is "a" or "l" ? (Variant.Intro == TitleIntro.Skip990 ? TitleSeedTable.IntroSkip990AOrLFrame : TitleSeedTable.IntroSkipFrame - (Variant.Saves == TitleSaves.Single ? 1 : 0))
        : TitleSeedTable.IntroFrameOf(Variant with { Recipe = null, Combo = null });

    public int SkipWindow => !Variant.IntroSkipped ? 0
        : Has("ltap") ? (Variant.Intro == TitleIntro.Skip477 ? TapSkip477Window : TapSkip990Window)
        : Skip is "a" or "l" ? (Variant.Intro == TitleIntro.Skip990 ? TitleSeedTable.IntroSkip990AOrLWindow - (Variant.Saves == TitleSaves.Single ? 1 : 0)
                                : TitleSeedTable.IntroSkip477AOrLWindow)
        : TitleSeedTable.IntroWindowOf(Variant with { Recipe = null, Combo = null });

    public const int TapSkip990Frame = 988;

    public const int TapSkip477Frame = 478;

    public const int TapSkip477Window = 3;

    public const int TapSkip990Window = 5;

    public const int SoftHoldFrames = 13;

    public const int GapFrame = 448;

    public const int GapPressWindow = 22;

    public const int GapReleaseWindow = 23;

    public const int TapFrame = 286;

    public const int TapWindow = GapFrame - TapFrame;

    public const int SoftTapAfterLanding = TapFrame - 270;

    public const int RepressReleaseAfterAnchor = 3567;

    public IReadOnlyList<RecipeStep> Steps
    {
        get
        {
            var steps = new List<RecipeStep>();
            string? gap = GapButton;
            bool start0 = Has("start0");

            if (start0) steps.Add(new RecipeStep(0, 0, "Hold Start from power-on", false));
            if (HeldButton is string held)
            {
                steps.Add(new RecipeStep(0, 0, Has("held") ? $"Hold {Name(held)} from power-on and never let go" : $"Hold {Name(held)} from power-on", false));
            }
            if (gap is not null)
            {
                steps.Add(new RecipeStep(0, 0, Alias.Length > 0
                    ? $"Hold {Name(gap)} (or {Name(AliasButton)}) from power-on"
                    : $"Hold {Name(gap)} from power-on", false));
            }
            if (Has("ltap")) steps.Add(new RecipeStep(TapFrame, TapWindow, "Tap L - the copyright screen", false));
            if (gap is not null) steps.Add(new RecipeStep(GapFrame, Variant.Saves == TitleSaves.Single ? GapPressWindow : GapReleaseWindow, "Release it - nothing reads the pad here", true));
            if (Variant.IntroSkipped) steps.Add(new RecipeStep(SkipFrame, SkipWindow, $"Hold {Name(Skip)} - skips the intro", true));
            if (HeldButton is string up && ReleaseFrame is int at and > 0)
            {
                bool onSkip = Variant.IntroSkipped && at >= SkipFrame && at < SkipFrame + SkipWindow;
                bool inFirstWindow = at == 478 && Variant.Intro == TitleIntro.Skip990;
                steps.Add(onSkip
                    ? new RecipeStep(SkipFrame, SkipWindow, $"Release {Name(up)} - with the skip, any frame of its window", true)
                    : inFirstWindow
                        ? new RecipeStep(TitleSeedTable.IntroSkipFrame, 4, $"Release {Name(up)} - inside the 477 window, nothing pressed there", true)
                        : new RecipeStep(at, 1, $"Release {Name(up)}" + (Variant.IntroSkipped && at > SkipFrame ? " - after the skip's poll" : ""), true));
            }
            if (SkipReleaseFrame is int rel and > 0) steps.Add(new RecipeStep(rel, 1, "Release Select - on the title screen (the sweep's frame)", true));

            if (Has("soft"))
            {
                steps.Add(new RecipeStep(GapFrame, GapPressWindow,
                    start0 ? "A+B+Select, Start still held - soft reset (when copyright fades to black)" : "A+B+Start+Select together - soft reset", false));
                steps.Add(new RecipeStep(Land + 1, SoftHoldFrames, $"Release all four (as soon as you see white)", false));
            }
            if (Has("ltap2")) steps.Add(new RecipeStep(Land + SoftTapAfterLanding, TapWindow, "Tap L - the soft boot's copyright screen", false));

            if (Variant.OnLoop)
            {
                int dead = Anchor0 + TitleSeedTable.LoopSkipAfterAnchor990 - 3;
                if (!Variant.LoopSkipped) steps.Add(new RecipeStep(0, 0, "Title screen times out, intro loops", false, "loop"));
                if (Has("uploop")) steps.Add(new RecipeStep(dead, TitleSeedTable.LoopSkip990Window, "Release Start - the loop's dead gap", true));
                if (Has("repress"))
                {
                    steps.Add(new RecipeStep(Anchor0 + RepressReleaseAfterAnchor, 1, "Release Select (the sweep's frame, before the gap)", true));
                }
                if (Variant.LoopSkipped)
                {
                    string again = Has("repress") || gap is not null ? " again" : "";
                    string button = gap is not null && Alias.Length > 0 ? "it" : Name(LoopSkip);
                    steps.Add(Variant.Loop == TitleLoop.Skip477
                        ? new RecipeStep(Anchor0 + TitleSeedTable.LoopSkipAfterAnchor477, TitleSeedTable.LoopSkip477Window, $"Hold {button}{again} - skips the loop's intro", true)
                        : new RecipeStep(dead, TitleSeedTable.LoopSkip990Window, $"Hold {button}{again} - skips the loop's intro", true));
                }
            }
            return steps;
        }
    }

    public IEnumerable<int> Windows => Steps.Where(step => step.Cued).Select(step => Math.Max(step.Window, 1));

    private string AliasButton => Alias.Length == 0 ? "" : Alias[(Alias.IndexOf('-') + 1)..Alias.IndexOf(':')];

    public IReadOnlyList<(int Offset, string Text)> EntrySteps(bool speed)
    {
        string combo = speed ? PairEntry : LateEntry;
        var rows = new List<(int, string)>();
        if (combo.Length == 0) return rows;

        string[] frames = combo.Split('-');
        bool action = false;
        int entryAt = -1;
        for (int i = 0; i < frames.Length; i++)
        {
            var down = new List<string>();
            var up = new List<string>();
            foreach (Match m in Regex.Matches(frames[i], "~?[a-z]+"))
            {
                if (m.Value[0] == '~') up.Add(m.Value[1..]); else down.Add(m.Value);
            }
            bool enters = entryAt < 0 && down.Any(b => b is "a" or "start" or "l") && (!speed || action);
            if (enters) entryAt = i;
            bool speeds = speed && !action && down.Any(b => b is "a" or "b" or "start" or "l");
            if (speeds) action = true;

            string text = down.Count > 0 ? "Hold " + string.Join("+", down.Select(Name)) : "";
            if (up.Count > 0) text += (text.Length > 0 ? ", let go of " : "Let go of ") + string.Join("+", up.Select(Name));
            if (enters) text += " - enters";
            else if (speeds) text += " - speeds the title up";
            if (i == 0 && !speed && Alias.Length > 0)
            {
                text += $" ({Name(Alias[(Alias.IndexOf(':') + 1)..])} if {Name(AliasButton)} was held)";
            }
            rows.Add((i, text));
        }
        return rows;
    }

    public bool Presses(bool speed) => speed ? PairEntry.Length > 0 : LateEntry.Length > 0;

    internal static string Name(string key) => key switch
    {
        "start" => "Start",
        "select" => "Select",
        "a" => "A",
        "l" => "L",
        "b" => "B",
        "r" => "R",
        _ => key,
    };
}

public static class TitleRecipes
{
    private static readonly Lazy<Dictionary<TitleVariant, TitleRecipe>> ByTable = new(Load);

    public static IReadOnlyCollection<TitleRecipe> All => ByTable.Value.Values;

    public static TitleRecipe? Find(TitleVariant variant) =>
        variant.HasRecipe && ByTable.Value.TryGetValue(variant.Table, out TitleRecipe? recipe) ? recipe : null;

    public static IEnumerable<TitleRecipe> For(TitleVariant pair) =>
        All.Where(recipe => recipe.Variant.Game == pair.Game && recipe.Variant.Buttons == pair.Buttons
                            && recipe.Variant.Sound == pair.Sound && recipe.Variant.Saves == pair.Saves)
            .OrderBy(recipe => recipe.Variant.Name, StringComparer.Ordinal);

    public static string Label(string key) => string.Join(", ", key.StartsWith("gap-", StringComparison.Ordinal)
        ? new[] { key[4..].ToUpperInvariant() + " held from power-on, released in the gap, pressed again on the loop" }
        : key.StartsWith("x-", StringComparison.Ordinal)
        ? new[] { "title pressed " + key[2..].ToUpperInvariant() + " (extra buttons, FireRed v1.1)" }
        : key.Split('-').Select(part => part switch
        {
            "a0" => "A held from power-on",
            "l0" => "L held from power-on",
            "r0" => "R held from power-on",
            "held" => "never let go",
            "skipa" => "the intro skipped with A",
            "skipl" => "the intro skipped with L",
            "skipstart" => "the intro skipped with START",
            string up when up.StartsWith("up", StringComparison.Ordinal) => "let go on " + up[2..],
            string rel when rel.StartsWith("rel", StringComparison.Ordinal) => "the skip's SELECT let go on " + rel[3..],
            "start0" => "START held from power-on",
            "ltap" => "L tap on the copyright screen",
            "soft" => "gap soft reset",
            "ltap2" => "L tap on the soft boot's copyright screen",
            "uploop" => "released in the loop's gap",
            "repress" => "SELECT released and pressed again on the loop",
            _ => part,
        }));

    private static Dictionary<TitleVariant, TitleRecipe> Load()
    {
        var recipes = new Dictionary<TitleVariant, TitleRecipe>();
        Assembly assembly = typeof(TitleRecipes).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream("FRLG.StarterTool.Core.Data.titleRecipes.csv");
        if (stream is null) return recipes;
        using var reader = new StreamReader(stream);

        string[] header = (reader.ReadLine() ?? "").Split(',');
        int Column(string name) => Array.IndexOf(header, name);
        int game = Column("game"), buttons = Column("buttons"), sound = Column("sound"), saves = Column("saves"),
            first = Column("first"), loop = Column("loop"), key = Column("recipe"), skip = Column("skip"),
            loopSkip = Column("loopskip"), anchor0 = Column("anchor0"), anchor = Column("anchor"), land = Column("land"),
            pair = Column("pair"), late = Column("late"), alias = Column("alias"), table = Column("table");

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;
            string[] f = line.Split(',');
            if (f.Length < header.Length) continue;

            TitleVariant variant = TitleVariant.Parse(f[buttons], f[sound],
                intro: f[loop] == "off" ? f[first] switch { "477" => "skip477", "990" => "skip990", _ => "none" } : $"loop{f[first]}-{f[loop]}",
                game: f[game], saves: f[saves]) with { Recipe = f[key] };
            if (variant.Table.Name != f[table]) continue;

            recipes[variant.Table] = new TitleRecipe(variant.Table, f[key], f[skip], f[loopSkip],
                int.Parse(f[anchor0], CultureInfo.InvariantCulture), int.Parse(f[anchor], CultureInfo.InvariantCulture),
                int.Parse(f[land], CultureInfo.InvariantCulture), f[pair], f[late], f[alias]);
        }
        return recipes;
    }
}
