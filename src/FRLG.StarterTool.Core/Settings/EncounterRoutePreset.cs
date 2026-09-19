using FRLG.StarterTool.Core.Encounters;

namespace FRLG.StarterTool.Core.Settings;

public sealed class EncounterRoutePreset
{
    public string Name { get; set; } = "";

    public string Route { get; set; } = "";

    public string Game { get; set; } = "fr";

    public string Buttons { get; set; } = "help";

    public string Saves { get; set; } = "multi";

    public int MaxSeconds { get; set; }

    public string Sound { get; set; } = "mono";
    public string Intro { get; set; } = "none";
    public string Title { get; set; } = "either";

    public string Combo { get; set; } = "any";

    public int DelayMs { get; set; }

    public int? OffsetMs { get; set; }

    public int IntroFrame { get; set; }

    public int IntroWindow { get; set; } = 1;

    public int LoopFrame { get; set; }

    public int LoopWindow { get; set; } = 1;

    public int TitleFrame { get; set; }

    public int TitleWindow { get; set; } = 1;

    public string IntroExtra { get; set; } = "";

    public string TitleExtra { get; set; } = "";

    public int Seed { get; set; } = -1;

    public int Offset { get; set; }

    public int Pass { get; set; }

    public List<CaptureReference> References { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ReferenceImage { get; set; }

    public int VideoDelayMs { get; set; }

    public const int MinVideoDelayMs = -2000;

    public const int MaxVideoDelayMs = 20000;

    public double ReferenceZoom { get; set; }

    public double ReferenceCenterX { get; set; } = 0.5;

    public double ReferenceCenterY { get; set; } = 0.5;

    public List<string> ManipRows { get; set; } = new();

    public List<string> ManipSettings { get; set; } = new();

    public (List<(string Frames, string Inputs)> Rows, List<string> Settings) ManipTable()
    {
        if (ManipRows.Count > 0)
        {
            var kept = ManipRows
                .Select(row => row.Split('\t', 2))
                .Select(parts => (parts[0], parts.Length > 1 ? parts[1] : ""))
                .ToList();
            return (kept, new List<string>(ManipSettings));
        }

        var rows = Presses().Select(press => (press.Frames, press.Name + " press")).ToList();
        var settings = new List<string>
        {
            Buttons switch { "la" => "L=A", "either" => "Help or L=A", _ => "Help" },
            Sound switch { "stereo" => "Stereo", "any" => "Mono/Stereo", _ => "Mono" },
            Saves switch { "single" => "Single Save", "either" => "Either Save", _ => "Multi Save" },
        };
        if (Seed >= 0) settings.Add($"Seed {Seed:X4}");
        return (rows, settings);
    }

    public bool HasIntroPress => IntroFrame > 0;

    public bool HasLoopPress => LoopFrame > 0;

    public bool HasTitlePress => TitleFrame > 0;

    public List<ManipPress> Presses()
    {
        var presses = new List<ManipPress>(3);
        if (HasIntroPress) presses.Add(new ManipPress("Intro", IntroFrame, IntroWindow));
        if (HasLoopPress) presses.Add(new ManipPress("Loop", LoopFrame, LoopWindow));
        presses.AddRange(ManipPress.ParseList("Intro", IntroExtra, firstNumber: 3));
        if (HasTitlePress) presses.Add(new ManipPress("Title", TitleFrame, TitleWindow));
        presses.AddRange(ManipPress.ParseList("Title", TitleExtra));
        return presses.OrderBy(press => press.Frame).ToList();
    }

    public static bool NameEquals(string? left, string? right) =>
        string.Equals((left ?? "").Trim(), (right ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    public EncounterRoutePreset Normalize()
    {
        Name = (Name ?? "").Trim();
        Route ??= "";
        Game = Game is "lg" ? Game : "fr";
        Buttons = Buttons is "la" or "either" ? Buttons : "help";
        Saves = Saves is "single" or "either" ? Saves : "multi";
        MaxSeconds = Math.Clamp(MaxSeconds, 0, 100000);
        Sound = Sound is "stereo" or "any" ? Sound : "mono";
        Intro = Intro is "any" ? Intro : TitleVariant.Parse(null, null, Intro).ChoiceKey;
        Title = Title is "played" or "spedup" ? Title : "either";
        Combo = TitleCombo.Parse(Combo)?.Key ?? "any";
        DelayMs = Math.Clamp(DelayMs, -10000, 10000);
        if (OffsetMs is int offsetMs) OffsetMs = Math.Clamp(offsetMs, -10000, 10000);
        IntroFrame = Math.Clamp(IntroFrame, 0, 100000);
        TitleFrame = Math.Clamp(TitleFrame, 0, 100000);
        IntroWindow = Math.Clamp(IntroWindow, 1, 60);
        LoopFrame = Math.Clamp(LoopFrame, 0, 100000);
        LoopWindow = Math.Clamp(LoopWindow, 1, 60);
        TitleWindow = Math.Clamp(TitleWindow, 1, 60);
        IntroExtra = ManipPress.FormatList(ManipPress.ParseList("Intro", IntroExtra));
        TitleExtra = ManipPress.FormatList(ManipPress.ParseList("Title", TitleExtra));
        if (Seed < -1 || Seed > 0xFFFF) Seed = -1;
        Offset = Math.Max(Offset, 0);
        Pass = Math.Max(Pass, 0);
        References ??= new();
        string legacy = Path.GetFileName(ReferenceImage ?? "");
        if (legacy.Length > 0) References.Insert(0, new CaptureReference { Image = legacy });
        ReferenceImage = null;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        References = References
            .Where(reference => reference != null)
            .Select(reference => new CaptureReference
            {
                Image = Path.GetFileName(reference.Image ?? ""),
                Frames = Math.Clamp(reference.Frames, -CaptureReference.MaxFrames, CaptureReference.MaxFrames),
            })
            .Where(reference => reference.Image.Length > 0 && seen.Add(reference.Image))
            .ToList();
        VideoDelayMs = Math.Clamp(VideoDelayMs, MinVideoDelayMs, MaxVideoDelayMs);
        ManipRows = (ManipRows ?? new()).Where(row => row != null).ToList();
        ManipSettings = (ManipSettings ?? new()).Where(setting => setting != null).ToList();
        Video.ViewState view = new Video.ViewState(ReferenceZoom, ReferenceCenterX, ReferenceCenterY).Normalize();
        ReferenceZoom = ReferenceZoom <= 0 ? 0 : view.Zoom;
        ReferenceCenterX = view.CenterX;
        ReferenceCenterY = view.CenterY;
        return this;
    }

    public EncounterRoutePreset Clone(string? name = null) => new()
    {
        Name = name ?? Name,
        Route = Route,
        Game = Game,
        Buttons = Buttons,
        Saves = Saves,
        MaxSeconds = MaxSeconds,
        Sound = Sound,
        Intro = Intro,
        Title = Title,
        Combo = Combo,
        DelayMs = DelayMs,
        OffsetMs = OffsetMs,
        IntroFrame = IntroFrame,
        IntroWindow = IntroWindow,
        LoopFrame = LoopFrame,
        LoopWindow = LoopWindow,
        TitleFrame = TitleFrame,
        TitleWindow = TitleWindow,
        IntroExtra = IntroExtra,
        TitleExtra = TitleExtra,
        Seed = Seed,
        Offset = Offset,
        Pass = Pass,
        References = (References ?? new()).Select(reference => reference.Clone()).ToList(),
        VideoDelayMs = VideoDelayMs,
        ManipRows = new List<string>(ManipRows ?? new()),
        ManipSettings = new List<string>(ManipSettings ?? new()),
        ReferenceZoom = ReferenceZoom,
        ReferenceCenterX = ReferenceCenterX,
        ReferenceCenterY = ReferenceCenterY
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
        new EncounterRoutePreset
        {
            Name = "FR Help 1-0 0A94",
            Route = EncounterPath.PlannerRoute,
            Game = "fr",
            Buttons = "help",
            Saves = "single",
            Sound = "any",
            Intro = "any",
            Title = "either",
            Combo = "any",
            TitleFrame = 2851,
            TitleWindow = 1,
            Seed = 0x0A94,
            Offset = 901,
        },
        new EncounterRoutePreset
        {
            Name = "FR L=A 1-0 0A94",
            Route = EncounterPath.PlannerRoute,
            Game = "fr",
            Buttons = "la",
            Saves = "single",
            Sound = "any",
            Intro = "any",
            Title = "either",
            Combo = "any",
            TitleFrame = 2851,
            TitleWindow = 1,
            Seed = 0x0A94,
            Offset = 901,
            ManipRows = new List<string>
            {
                "boot\tHold Start from power-on",
                "286-447\tTap L - the copyright screen",
                "448-469\tA+B+Select, Start still held - soft reset (when copyright fades to black)",
                "481-493\tRelease all four (as soon as you see white)",
                "2851\tHold Start - title clears",
            },
            ManipSettings = new List<string> { "L=A", "Mono", "Single Save", "Seed 0A94" },
        },
    };
}
