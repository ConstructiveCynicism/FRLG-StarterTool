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

public sealed class AppSettings
{
    public int Version { get; set; } = 1;

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

    public Hotkey ToggleGlobalHotkeys { get; set; } = new() { Global = true };

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

    public bool NpcGridVisible { get; set; }

    public const int DefaultCuedLabPressOffsetFrames = 30;

    public const double DefaultCuedPressWindowMs = 30.0;

    public KeyMethod KeyMethod { get; set; } = KeyMethod.OnPress;

    public bool GlobalNumpadInput { get; set; } = true;

    public bool GlobalHotkeysEnabled { get; set; } = true;

    public ClipboardFormat ClipboardFormat { get; set; } = ClipboardFormat.Column;

    public bool AlwaysOnTop { get; set; }

    public bool DarkMode { get; set; } = true;

    public bool HideConstraints { get; set; }

    public bool ShowLabDelayDashes { get; set; }

    public int ZoomPercent { get; set; } = 100;

    public TimeFormat TimeFormat { get; set; } = TimeFormat.Seconds;

    public string StatBoxLabelColor { get; set; } = DefaultStatBoxLabelColor;

    public string StatBoxFillColor { get; set; } = DefaultStatBoxFillColor;

    public const string DefaultStatBoxLabelColor = "#4DC6D6";

    public const string DefaultStatBoxFillColor = "#3C3C3C";

    public bool StatBoxColorsAreDefault =>
        string.Equals(StatBoxLabelColor, DefaultStatBoxLabelColor, StringComparison.OrdinalIgnoreCase)
        && string.Equals(StatBoxFillColor, DefaultStatBoxFillColor, StringComparison.OrdinalIgnoreCase);

    public string Fps { get; set; } = "59.7275";
    public string Offset { get; set; } = "0";

    public string VisualOffset { get; set; } = "0";

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

    public List<FilterPreset> Presets { get; set; } = new();

    public string ActivePreset { get; set; } = "";

    public FilterPreset? FindPreset(string name) =>
        Presets.FirstOrDefault(preset => FilterPreset.NameEquals(preset.Name, name));

    public FilterPreset GetCurrentFilter(string name = "") => new FilterPreset
    {
        Name = name,
        SpeciesId = SpeciesId,
        MinFrame = MinFrame,
        MaxFrame = MaxFrame,
        Natures = Natures,
        IvMinus = IvMinus,
        IvNeutral = IvNeutral,
        IvPlus = IvPlus
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

    public AppSettings Normalize()
    {
        Start ??= new Hotkey();
        Stop ??= new Hotkey();
        AddFrame ??= new Hotkey();
        SubFrame ??= new Hotkey();
        ToggleLevel ??= new Hotkey();
        Multiply2 ??= new Hotkey();
        Multiply3 ??= new Hotkey();
        ListUp ??= new Hotkey();
        ListDown ??= new Hotkey();
        ExportStats ??= new Hotkey();
        ToggleGlobalHotkeys ??= new Hotkey { Global = true };
        NpcUp ??= new Hotkey();
        NpcDown ??= new Hotkey();
        NpcLeft ??= new Hotkey();
        NpcRight ??= new Hotkey();
        NpcFocusPrev ??= new Hotkey();
        NpcFocusNext ??= new Hotkey();
        NpcUndo ??= new Hotkey();
        NpcComplete ??= new Hotkey();
        NpcMiss ??= new Hotkey();

        if (!Enum.IsDefined(KeyMethod)) KeyMethod = KeyMethod.OnPress;
        if (!Enum.IsDefined(ClipboardFormat)) ClipboardFormat = ClipboardFormat.Column;
        if (!Enum.IsDefined(TimeFormat)) TimeFormat = TimeFormat.Seconds;

        Fps ??= "59.7275";
        Offset ??= "0";
        VisualOffset ??= "0";
        Interval ??= "1000";
        NumBeeps ??= "4";
        Volume = Math.Clamp(Volume, 0, 100);
        if (string.IsNullOrWhiteSpace(BeepSound)) BeepSound = "ping1";
        if (string.IsNullOrWhiteSpace(StatBoxLabelColor)) StatBoxLabelColor = DefaultStatBoxLabelColor;
        if (string.IsNullOrWhiteSpace(StatBoxFillColor)) StatBoxFillColor = DefaultStatBoxFillColor;
        TrainingRounds = Math.Clamp(TrainingRounds, 1, 999);

        ZoomPercent = Math.Clamp(ZoomPercent == 0 ? 100 : ZoomPercent, 75, 125);

        if (double.IsNaN(NpcContextWindowMs)) NpcContextWindowMs = 0.0;
        NpcContextWindowMs = Math.Clamp(NpcContextWindowMs, 0.0, 1000.0);

        if (double.IsNaN(NpcCuedPressWindowMs)) NpcCuedPressWindowMs = DefaultCuedPressWindowMs;
        NpcCuedPressWindowMs = Math.Clamp(NpcCuedPressWindowMs, 0.0, 1000.0);
        NpcCuedLabPressOffsetFrames = Math.Clamp(NpcCuedLabPressOffsetFrames, 0, 6000);

        NormalizeTips();

        MinFrame ??= "0";
        MaxFrame ??= "10000";
        if (SpeciesId < 1 || SpeciesId > PokemonSpecies.Gen3DexSize) SpeciesId = SettingsArrays.DefaultSpeciesId;
        if (Level != 5 && Level != 6) Level = 5;

        Natures = SettingsArrays.Resize(Natures, Nature.NatureCount);
        IvMinus = SettingsArrays.Resize(IvMinus, 6);
        IvNeutral = SettingsArrays.Resize(IvNeutral, 6);
        IvPlus = SettingsArrays.Resize(IvPlus, 6);

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
}
