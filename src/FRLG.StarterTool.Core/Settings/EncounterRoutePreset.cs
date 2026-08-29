using FRLG.StarterTool.Core.Encounters;

namespace FRLG.StarterTool.Core.Settings;

public sealed class EncounterRoutePreset
{
    public string Name { get; set; } = "";

    public string Route { get; set; } = "";

    public string Game { get; set; } = "fr";

    public string Buttons { get; set; } = "help";
    public string Sound { get; set; } = "mono";
    public string Intro { get; set; } = "none";
    public string Title { get; set; } = "either";

    public string Combo { get; set; } = "any";

    public int DelayMs { get; set; }

    public int IntroFrame { get; set; }

    public int IntroWindow { get; set; } = 1;

    public int TitleFrame { get; set; }

    public int TitleWindow { get; set; } = 1;

    public int Seed { get; set; } = -1;

    public int Offset { get; set; }

    public int Pass { get; set; }

    public bool HasIntroPress => IntroFrame > 0;

    public bool HasTitlePress => TitleFrame > 0;

    public List<ManipPress> Presses()
    {
        var presses = new List<ManipPress>(2);
        if (HasIntroPress) presses.Add(new ManipPress("Intro", IntroFrame, IntroWindow));
        if (HasTitlePress) presses.Add(new ManipPress("Title", TitleFrame, TitleWindow));
        return presses;
    }

    public static bool NameEquals(string? left, string? right) =>
        string.Equals((left ?? "").Trim(), (right ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    public EncounterRoutePreset Normalize()
    {
        Name = (Name ?? "").Trim();
        Route ??= "";
        Game = Game is "lg" ? Game : "fr";
        Buttons = Buttons is "la" ? Buttons : "help";
        Sound = Sound is "stereo" or "any" ? Sound : "mono";
        Intro = Intro is "skip477" or "skip990" or "any" ? Intro : "none";
        Title = Title is "played" or "spedup" ? Title : "either";
        Combo = TitleCombo.Parse(Combo)?.Key ?? "any";
        DelayMs = Math.Clamp(DelayMs, -10000, 10000);
        IntroFrame = Math.Clamp(IntroFrame, 0, 100000);
        TitleFrame = Math.Clamp(TitleFrame, 0, 100000);
        IntroWindow = Math.Clamp(IntroWindow, 1, 60);
        TitleWindow = Math.Clamp(TitleWindow, 1, 60);
        if (Seed < -1 || Seed > 0xFFFF) Seed = -1;
        Offset = Math.Max(Offset, 0);
        Pass = Math.Max(Pass, 0);
        return this;
    }

    public EncounterRoutePreset Clone(string? name = null) => new()
    {
        Name = name ?? Name,
        Route = Route,
        Game = Game,
        Buttons = Buttons,
        Sound = Sound,
        Intro = Intro,
        Title = Title,
        Combo = Combo,
        DelayMs = DelayMs,
        IntroFrame = IntroFrame,
        IntroWindow = IntroWindow,
        TitleFrame = TitleFrame,
        TitleWindow = TitleWindow,
        Seed = Seed,
        Offset = Offset,
        Pass = Pass
    };
    public static IReadOnlyList<EncounterRoutePreset> Examples => new[]
    {
        new EncounterRoutePreset
        {
            Name = "Example FR Round 2",
            Route = "R1a,21,22,1,6,0,1\nR1b,21,5,1,6,0,0\nR1c,21,22,1,6,0,1\n" +
                    "R2,21,5,1,6,0,0\nF1,14,43,1,7,0,2\nF2,14,9,1,7,0,0",
            Game = "fr",
            Buttons = "help",
            Sound = "any",
            Intro = "any",
            Title = "either",
            Combo = "any",
            IntroFrame = 987,
            IntroWindow = 5,
            TitleFrame = 1393,
            TitleWindow = 2,
            Seed = 26870,
            Offset = 399,
        },
        new EncounterRoutePreset
        {
            Name = "Example FR Glitchless",
            Route = "R1a,21,22,1,6,0,1\nR1b,21,10,1,6,0,0\nR1c,21,22,1,6,0,1\n" +
                    "R2,21,5,1,6,0,0\nF1,14,41,1,7,0,0\nF2,14,10,1,7,0,0",
            Game = "fr",
            Buttons = "la",
            Sound = "any",
            Intro = "any",
            Title = "either",
            Combo = "any",
            IntroFrame = 477,
            IntroWindow = 3,
            TitleFrame = 632,
            TitleWindow = 1,
            Seed = 15509,
            Offset = 150,
        },
    };
}
