using FRLG.StarterTool.Core.Pokemon;
using FRLG.StarterTool.Core.Timing;
using FRLG.StarterTool.Core.Tips;

namespace FRLG.StarterTool.Core.Settings;

public enum HotkeyAction
{
    Start,
    Stop,
    AddFrame,
    SubFrame,
    ToggleLevel,

    Multiply2,

    Multiply3,

    ListUp,

    ListDown,

    ExportStats,

    ToggleGlobalHotkeys,

    NpcUp,

    NpcDown,

    NpcLeft,

    NpcRight,

    NpcFocusPrev,

    NpcFocusNext,

    NpcUndo,

    NpcComplete,

    NpcMiss
}

public enum ClipboardFormat
{
    Column,

    Row
}

public enum StatStripSide
{
    Bottom,

    Left,

    Right
}

public sealed class AppSettings
{
    public int Version { get; set; } = SettingsMigrations.CurrentVersion;

    public Hotkey Start { get; set; } = new();
    public Hotkey Stop { get; set; } = new();
    public Hotkey AddFrame { get; set; } = new();
    public Hotkey SubFrame { get; set; } = new();
    public Hotkey ToggleLevel { get; set; } = new();

    public Hotkey Multiply2 { get; set; } = new();

    public Hotkey Multiply3 { get; set; } = new();

    public Hotkey ListUp { get; set; } = new();

    public Hotkey ListDown { get; set; } = new();

    public Hotkey ExportStats { get; set; } = new();

    public Hotkey ToggleGlobalHotkeys { get; set; } = new();

    public Hotkey NpcUp { get; set; } = new();

    public Hotkey NpcDown { get; set; } = new();

    public Hotkey NpcLeft { get; set; } = new();

    public Hotkey NpcRight { get; set; } = new();

    public Hotkey NpcFocusPrev { get; set; } = new();

    public Hotkey NpcFocusNext { get; set; } = new();

    public Hotkey NpcUndo { get; set; } = new();

    public Hotkey NpcComplete { get; set; } = new();

    public Hotkey NpcMiss { get; set; } = new();

    public double NpcContextWindowMs { get; set; }

    public bool NpcCuedLabPress { get; set; }

    public int NpcCuedLabPressOffsetFrames { get; set; } = DefaultCuedLabPressOffsetFrames;

    public double NpcCuedPressWindowMs { get; set; } = DefaultCuedPressWindowMs;

    public string SavestateLoadPath { get; set; } = "";

    public string SavestateSavePath { get; set; } = "";

    public string RomPatchRomPath { get; set; } = "";

    public string RomPatchOutputPath { get; set; } = "";

    public string RomPatchPatchPath { get; set; } = "";

    public string EncounterRoute { get; set; } = "";

    public int EncounterCycles { get; set; } = 53;

    public string EncounterProtocol { get; set; } = "rta";

    public string EncounterButtons { get; set; } = "help";

    public string EncounterGame { get; set; } = "fr";

    public string EncounterCombo { get; set; } = "any";

    public string EncounterSound { get; set; } = "mono";

    public string EncounterIntro { get; set; } = "none";

    public string EncounterTitle { get; set; } = "either";

    public int EncounterDelayMs { get; set; }

    public int? EncounterOffsetMs { get; set; }

    public int EncounterIntroFrame { get; set; }

    public int EncounterIntroWindow { get; set; } = 1;

    public int EncounterTitleFrame { get; set; }

    public int EncounterTitleWindow { get; set; } = 1;

    public List<EncounterRoutePreset> EncounterRoutes { get; set; } = new();

    public string EncounterActiveRoute { get; set; } = "";
    public List<string> EncounterExamplesSeeded { get; set; } = new();

    public string TimerEncounterRoute { get; set; } = "";

    public EncounterRoutePreset? FindEncounterRoute(string name) =>
        EncounterRoutes.FirstOrDefault(route => EncounterRoutePreset.NameEquals(route.Name, name));

    public const int DefaultCuedLabPressOffsetFrames = 110;

    public const double DefaultCuedPressWindowMs = 30.0;

    public KeyMethod KeyMethod { get; set; } = KeyMethod.OnPress;

    public bool GlobalNumpadInput { get; set; } = true;

    public bool GlobalHotkeysEnabled { get; set; } = true;

    public ClipboardFormat ClipboardFormat { get; set; } = ClipboardFormat.Column;

    public bool AlwaysOnTop { get; set; }

    public bool DarkMode { get; set; } = true;

    public bool ViewManip { get; set; } = true;

    public bool ViewConstraints { get; set; } = true;

    public bool ViewTraining { get; set; } = true;

    public bool ViewEncounter { get; set; } = true;

    public bool ViewSavestate { get; set; } = false;

    public bool ViewTroubleshooter { get; set; } = false;

    public string SelectedTab { get; set; } = "manip";

    public bool ShowLabDelayDashes { get; set; }

    public bool AutoShowLevelStats { get; set; } = true;

    public double ClockDrift { get; set; } = 1.0;

    public bool AtomicClockSync { get; set; } = true;

    public int ZoomPercent { get; set; } = 100;

    public TimeFormat TimeFormat { get; set; } = TimeFormat.Seconds;

    public string StatBoxLabelColor { get; set; } = DefaultStatBoxLabelColor;

    public string StatBoxFillColor { get; set; } = DefaultStatBoxFillColor;

    public string StatBoxValueColor { get; set; } = DefaultStatBoxValueColor;

    public string StatBoxOutlineColor { get; set; } = DefaultStatBoxOutlineColor;

    public string StatBoxFrameColor { get; set; } = DefaultStatBoxFrameColor;

    public const string DefaultStatBoxLabelColor = "#4DC6D6";

    public const string DefaultStatBoxFillColor = "#3C3C3C";

    public const string DefaultStatBoxValueColor = "#FFFFFF";

    public const string DefaultStatBoxOutlineColor = "#000000";

    public const string DefaultStatBoxFrameColor = "#000000";

    public bool StatBoxColorsAreDefault =>
        string.Equals(StatBoxLabelColor, DefaultStatBoxLabelColor, StringComparison.OrdinalIgnoreCase)
        && string.Equals(StatBoxFillColor, DefaultStatBoxFillColor, StringComparison.OrdinalIgnoreCase)
        && string.Equals(StatBoxValueColor, DefaultStatBoxValueColor, StringComparison.OrdinalIgnoreCase)
        && string.Equals(StatBoxOutlineColor, DefaultStatBoxOutlineColor, StringComparison.OrdinalIgnoreCase)
        && string.Equals(StatBoxFrameColor, DefaultStatBoxFrameColor, StringComparison.OrdinalIgnoreCase);

    public bool StatServerEnabled { get; set; }

    public int StatServerPort { get; set; } = DefaultStatServerPort;

    public const int DefaultStatServerPort = 8722;

    public bool StatServerAllowNetwork { get; set; }

    public bool StatServerRequireToken { get; set; }

    public string StatServerToken { get; set; } = "";

    public bool StatServerTransparent { get; set; }

    public StatStripSide StatServerStripSide { get; set; } = StatStripSide.Bottom;

    public bool StatServerPostRun { get; set; } = true;

    public int StatServerPostRunSeconds { get; set; } = DefaultStatServerPostRunSeconds;

    public const int DefaultStatServerPostRunSeconds = 7;

    public const int MinStatServerPostRunSeconds = 1;

    public const int MaxStatServerPostRunSeconds = 60;

    public string Fps { get; set; } = "59.7275";
    public string Offset { get; set; } = "0";

    public string VisualOffset { get; set; } = "0";

    public string DelayOffset { get; set; } = "0";

    public string Interval { get; set; } = "1000";
    public string NumBeeps { get; set; } = "4";
    public int Volume { get; set; } = 70;

    public bool BeepEnabled { get; set; } = true;

    public bool FlashEnabled { get; set; } = true;

    public string BeepSound { get; set; } = "ping1";

    public int TrainingRounds { get; set; } = 10;

    public bool ShowRunTips { get; set; } = true;

    public bool TipTrainerUsed { get; set; }

    public bool TipOddsCalculated { get; set; }

    public long TipHiddenRolls { get; set; }

    public int TipAttempts { get; set; }

    public int TipLikelyHits { get; set; }

    public string MinFrame { get; set; } = "0";
    public string MaxFrame { get; set; } = "10000";
    public int SpeciesId { get; set; } = SettingsArrays.DefaultSpeciesId;

    public int Level { get; set; } = 5;

    public bool[] Natures { get; set; } = SettingsArrays.NewNatureFilter();

    public int[] IvMinus { get; set; } = new int[6];
    public int[] IvNeutral { get; set; } = new int[6];
    public int[] IvPlus { get; set; } = new int[6];

    public List<ConstraintRange> Ranges { get; set; } = new();

    public List<FilterPreset> Presets { get; set; } = new();

    public string ActivePreset { get; set; } = "";

    public FilterPreset? FindPreset(string name) =>
        Presets.FirstOrDefault(preset => FilterPreset.NameEquals(preset.Name, name));

    public ConstraintRange PrimaryRange =>
        Ranges.FirstOrDefault(range => !range.Backup) ?? Ranges.FirstOrDefault() ?? new ConstraintRange();

    public FilterPreset GetCurrentFilter(string name = "") => new FilterPreset
    {
        Name = name,
        SpeciesId = SpeciesId,
        MinFrame = MinFrame,
        MaxFrame = MaxFrame,
        Natures = Natures,
        IvMinus = IvMinus,
        IvNeutral = IvNeutral,
        IvPlus = IvPlus,
        Ranges = Ranges
    }.Clone(name);

    public void SetCurrentFilter(FilterPreset filter)
    {
        FilterPreset copy = filter.Clone();
        SpeciesId = copy.SpeciesId;
        MinFrame = copy.MinFrame;
        MaxFrame = copy.MaxFrame;
        Natures = copy.Natures;
        IvMinus = copy.IvMinus;
        IvNeutral = copy.IvNeutral;
        IvPlus = copy.IvPlus;
        Ranges = copy.Ranges;
    }

    public Hotkey GetHotkey(HotkeyAction action) => action switch
    {
        HotkeyAction.Start => Start,
        HotkeyAction.Stop => Stop,
        HotkeyAction.AddFrame => AddFrame,
        HotkeyAction.SubFrame => SubFrame,
        HotkeyAction.ToggleLevel => ToggleLevel,
        HotkeyAction.Multiply2 => Multiply2,
        HotkeyAction.Multiply3 => Multiply3,
        HotkeyAction.ListUp => ListUp,
        HotkeyAction.ListDown => ListDown,
        HotkeyAction.ExportStats => ExportStats,
        HotkeyAction.ToggleGlobalHotkeys => ToggleGlobalHotkeys,
        HotkeyAction.NpcUp => NpcUp,
        HotkeyAction.NpcDown => NpcDown,
        HotkeyAction.NpcLeft => NpcLeft,
        HotkeyAction.NpcRight => NpcRight,
        HotkeyAction.NpcFocusPrev => NpcFocusPrev,
        HotkeyAction.NpcFocusNext => NpcFocusNext,
        HotkeyAction.NpcUndo => NpcUndo,
        HotkeyAction.NpcComplete => NpcComplete,
        HotkeyAction.NpcMiss => NpcMiss,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown hotkey action")
    };

    public IEnumerable<Hotkey> AllHotkeys()
    {
        foreach (HotkeyAction action in Enum.GetValues<HotkeyAction>())
        {
            yield return GetHotkey(action);
        }
    }

    private static string StripWhitespace(string? text, string fallback)
    {
        if (text == null) return fallback;
        string stripped = string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
        return stripped.Length == 0 ? fallback : stripped;
    }

    public AppSettings Normalize()
    {
        (Start ??= new Hotkey()).Normalize();
        (Stop ??= new Hotkey()).Normalize();
        (AddFrame ??= new Hotkey()).Normalize();
        (SubFrame ??= new Hotkey()).Normalize();
        (ToggleLevel ??= new Hotkey()).Normalize();
        (Multiply2 ??= new Hotkey()).Normalize();
        (Multiply3 ??= new Hotkey()).Normalize();
        (ListUp ??= new Hotkey()).Normalize();
        (ListDown ??= new Hotkey()).Normalize();
        (ExportStats ??= new Hotkey()).Normalize();
        (ToggleGlobalHotkeys ??= new Hotkey()).Normalize();
        (NpcUp ??= new Hotkey()).Normalize();
        (NpcDown ??= new Hotkey()).Normalize();
        (NpcLeft ??= new Hotkey()).Normalize();
        (NpcRight ??= new Hotkey()).Normalize();
        (NpcFocusPrev ??= new Hotkey()).Normalize();
        (NpcFocusNext ??= new Hotkey()).Normalize();
        (NpcUndo ??= new Hotkey()).Normalize();
        (NpcComplete ??= new Hotkey()).Normalize();
        (NpcMiss ??= new Hotkey()).Normalize();

        if (!Enum.IsDefined(KeyMethod)) KeyMethod = KeyMethod.OnPress;
        if (!Enum.IsDefined(ClipboardFormat)) ClipboardFormat = ClipboardFormat.Column;
        if (!Enum.IsDefined(StatServerStripSide)) StatServerStripSide = StatStripSide.Bottom;
        if (!Enum.IsDefined(TimeFormat)) TimeFormat = TimeFormat.Seconds;

        EncounterCycles = Math.Clamp(EncounterCycles, 0, 65535);
        EncounterProtocol = EncounterProtocol == "sweep" ? "sweep" : "rta";
        EncounterButtons = EncounterButtons is "la" ? EncounterButtons : "help";
        EncounterGame = EncounterGame is "lg" ? EncounterGame : "fr";
        EncounterCombo = Encounters.TitleCombo.Parse(EncounterCombo)?.Key ?? "any";
        EncounterSound = EncounterSound is "stereo" or "any" ? EncounterSound : "mono";
        EncounterIntro = EncounterIntro is "skip477" or "skip990" or "any" ? EncounterIntro : "none";
        EncounterTitle = EncounterTitle is "played" or "spedup" ? EncounterTitle : "either";
        EncounterDelayMs = Math.Clamp(EncounterDelayMs, -10000, 10000);
        if (EncounterOffsetMs is int encounterOffsetMs)
        {
            EncounterOffsetMs = Math.Clamp(encounterOffsetMs, -10000, 10000);
        }
        EncounterIntroFrame = Math.Clamp(EncounterIntroFrame, 0, 100000);
        EncounterTitleFrame = Math.Clamp(EncounterTitleFrame, 0, 100000);
        EncounterIntroWindow = Math.Clamp(EncounterIntroWindow, 1, 60);
        EncounterTitleWindow = Math.Clamp(EncounterTitleWindow, 1, 60);
        NormalizeEncounterRoutes();

        Fps ??= "59.7275";
        Offset = StripWhitespace(Offset, "0");
        VisualOffset = StripWhitespace(VisualOffset, "0");
        DelayOffset = StripWhitespace(DelayOffset, "0");
        Interval = StripWhitespace(Interval, "1000");
        NumBeeps = StripWhitespace(NumBeeps, "4");
        Volume = Math.Clamp(Volume, 0, 100);
        if (string.IsNullOrWhiteSpace(BeepSound)) BeepSound = "ping1";
        if (string.IsNullOrWhiteSpace(StatBoxLabelColor)) StatBoxLabelColor = DefaultStatBoxLabelColor;
        if (string.IsNullOrWhiteSpace(StatBoxFillColor)) StatBoxFillColor = DefaultStatBoxFillColor;
        if (string.IsNullOrWhiteSpace(StatBoxValueColor)) StatBoxValueColor = DefaultStatBoxValueColor;
        if (string.IsNullOrWhiteSpace(StatBoxOutlineColor)) StatBoxOutlineColor = DefaultStatBoxOutlineColor;
        if (string.IsNullOrWhiteSpace(StatBoxFrameColor)) StatBoxFrameColor = DefaultStatBoxFrameColor;
        TrainingRounds = Math.Clamp(TrainingRounds, 1, 999);

        ZoomPercent = Math.Clamp(ZoomPercent == 0 ? 100 : ZoomPercent, 75, 125);

        if (double.IsNaN(NpcContextWindowMs)) NpcContextWindowMs = 0.0;
        NpcContextWindowMs = Math.Clamp(NpcContextWindowMs, 0.0, 1000.0);

        if (double.IsNaN(NpcCuedPressWindowMs)) NpcCuedPressWindowMs = DefaultCuedPressWindowMs;
        NpcCuedPressWindowMs = Math.Clamp(NpcCuedPressWindowMs, 0.0, 1000.0);
        NpcCuedLabPressOffsetFrames = Math.Clamp(NpcCuedLabPressOffsetFrames, 0, 6000);

        if (StatServerPort is < 1 or > 65535) StatServerPort = DefaultStatServerPort;
        StatServerToken ??= "";

        if (StatServerPostRunSeconds <= 0) StatServerPostRunSeconds = DefaultStatServerPostRunSeconds;
        StatServerPostRunSeconds = Math.Clamp(
            StatServerPostRunSeconds, MinStatServerPostRunSeconds, MaxStatServerPostRunSeconds);

        if (!Timing.DriftMonitor.IsPlausible(ClockDrift)) ClockDrift = 1.0;

        NormalizeTips();

        MinFrame ??= "0";
        MaxFrame ??= "10000";
        if (SpeciesId < 1 || SpeciesId > PokemonSpecies.Gen3DexSize) SpeciesId = SettingsArrays.DefaultSpeciesId;
        if (Level != 5 && Level != 6) Level = 5;

        Natures = SettingsArrays.Resize(Natures, Nature.NatureCount);
        IvMinus = SettingsArrays.Resize(IvMinus, 6);
        IvNeutral = SettingsArrays.Resize(IvNeutral, 6);
        IvPlus = SettingsArrays.Resize(IvPlus, 6);

        Ranges = SettingsArrays.Repair(Ranges, Natures, IvMinus, IvNeutral, IvPlus);
        (Natures, IvMinus, IvNeutral, IvPlus) = SettingsArrays.MirrorPrimary(PrimaryRange);

        NormalizePresets();

        return this;
    }

    private void NormalizeTips()
    {
        TipHiddenRolls = Math.Max(0L, TipHiddenRolls);
        TipAttempts = Math.Max(0, TipAttempts);
        TipLikelyHits = Math.Clamp(TipLikelyHits, 0, TipAttempts);
    }

    private void NormalizePresets()
    {
        Presets ??= new List<FilterPreset>();

        var kept = new List<FilterPreset>(Presets.Count);
        foreach (FilterPreset? preset in Presets)
        {
            if (preset == null) continue;

            preset.Normalize();
            if (preset.Name.Length == 0) continue;
            if (kept.Any(other => FilterPreset.NameEquals(other.Name, preset.Name))) continue;

            kept.Add(preset);
        }
        Presets = kept;

        ActivePreset = (ActivePreset ?? "").Trim();
        if (FindPreset(ActivePreset) == null) ActivePreset = "";
    }

    private void NormalizeEncounterRoutes()
    {
        EncounterRoutes ??= new List<EncounterRoutePreset>();

        var kept = new List<EncounterRoutePreset>(EncounterRoutes.Count);
        foreach (EncounterRoutePreset? route in EncounterRoutes)
        {
            if (route == null) continue;

            route.Normalize();
            if (route.Name.Length == 0) continue;
            if (kept.Any(other => EncounterRoutePreset.NameEquals(other.Name, route.Name))) continue;

            kept.Add(route);
        }
        SeedExampleRoutes(kept);
        EncounterRoutes = kept;

        EncounterActiveRoute = (EncounterActiveRoute ?? "").Trim();
        if (FindEncounterRoute(EncounterActiveRoute) == null) EncounterActiveRoute = "";
        TimerEncounterRoute = (TimerEncounterRoute ?? "").Trim();
        if (FindEncounterRoute(TimerEncounterRoute) == null) TimerEncounterRoute = "";
    }

    private void SeedExampleRoutes(List<EncounterRoutePreset> routes)
    {
        EncounterExamplesSeeded ??= new List<string>();

        foreach (EncounterRoutePreset example in EncounterRoutePreset.Examples)
        {
            bool seeded = EncounterExamplesSeeded.Any(
                name => EncounterRoutePreset.NameEquals(name, example.Name));
            if (seeded) continue;

            EncounterExamplesSeeded.Add(example.Name);
            if (routes.Any(route => EncounterRoutePreset.NameEquals(route.Name, example.Name))) continue;

            routes.Add(example.Clone().Normalize());
        }
    }
}
