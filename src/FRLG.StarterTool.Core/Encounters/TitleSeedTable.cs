using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace FRLG.StarterTool.Core.Encounters;

public readonly record struct TitleSeedEntry(int Offset, int Seed, int Boots);

public enum TitleProtocol
{
    Rta,

    Sweep,
}

public enum TitleButtonMode
{
    Help = 0,
    LEqualsA = 1,
}

public enum TitleSoundMode
{
    Mono = 0,
    Stereo = 1,
}

public enum TitleGame
{
    FireRed = 0,
    LeafGreen = 1,
}

public enum TitleSaves
{
    Multi = 0,

    Single = 1,
}

public enum TitleIntro
{
    Played = 0,

    Skip477 = 1,

    Skip990 = 2,
}

public enum TitleLoop
{
    None = 0,

    Played = 1,

    Skip477 = 2,

    Skip990 = 3,
}

public enum TitleAnimation
{
    Either = 0,

    PlayedOut = 1,

    SpedUp = 2,
}

public readonly record struct TitleVariant(TitleButtonMode Buttons, TitleSoundMode Sound, TitleIntro Intro = TitleIntro.Played,
    TitleAnimation Animation = TitleAnimation.Either, TitleCombo? Combo = null, TitleGame Game = TitleGame.FireRed,
    TitleLoop Loop = TitleLoop.None, TitleSaves Saves = TitleSaves.Multi, string? Recipe = null)
{
    public bool HasRecipe => Recipe is not null;

    public string SavesKey => Saves == TitleSaves.Single ? "single" : "multi";

    public bool Skip477IsTerm => Intro == TitleIntro.Skip477 && !OnLoop && !OwnsTable && Saves == TitleSaves.Multi && !HasRecipe;

    public bool OnLoop => Loop != TitleLoop.None;

    public bool LoopSkipped => Loop is TitleLoop.Skip477 or TitleLoop.Skip990;

    public int Skips => (IntroSkipped ? 1 : 0) + (LoopSkipped ? 1 : 0);

    public string LoopKey => Loop switch
    {
        TitleLoop.Skip477 => "477",
        TitleLoop.Skip990 => "990",
        _ => "none",
    };

    private string FirstKey => Intro switch
    {
        TitleIntro.Skip477 => "477",
        TitleIntro.Skip990 => "990",
        _ => "none",
    };

    public string ChoiceKey => OnLoop ? "loop" + FirstKey + "-" + LoopKey : IntroKey;

    public string FirstLoopKey => FirstKey + "-" + LoopKey;

    public static TitleVariant Default => new(TitleButtonMode.Help, TitleSoundMode.Mono);

    public string GameKey => Game == TitleGame.LeafGreen ? "lg" : "fr";

    public string PairName => ButtonsKey + "_" + SoundKey;

    public bool IntroSkipped => Intro != TitleIntro.Played;

    public string IntroKey => Intro switch
    {
        TitleIntro.Skip477 => "skip477",
        TitleIntro.Skip990 => "skip990",
        _ => "none",
    };

    public string AnimationKey => Animation switch
    {
        TitleAnimation.PlayedOut => "played",
        TitleAnimation.SpedUp => "spedup",
        _ => "either",
    };

    public string ComboKey => Combo?.Key ?? "any";

    public TitleVariant Table => this with
    {
        Intro = OnLoop || Intro == TitleIntro.Skip990 || (Intro == TitleIntro.Skip477 && !Skip477IsTerm) ? Intro : TitleIntro.Played,
        Animation = TitleAnimation.Either,
        Combo = OwnsTable ? Combo : null,
    };

    public bool OwnsTable =>
        !HasRecipe
        && !OnLoop
        && Combo is TitleCombo c && TitleCombos.OwnTableKey(c, IntroSkipped, Sound) is not null
        && (Intro != TitleIntro.Skip477 || TitleSeedTable.IntroSkipShiftOf(this) is null);

    public TitleVariant PairTable => (this with { Combo = null }).Table;

    public bool Allows(int offset)
    {
        bool speed = offset < TitleSeedTable.AnimationEndsOf(Game);
        bool allowed = Animation switch
        {
            TitleAnimation.PlayedOut => !speed,
            TitleAnimation.SpedUp => speed,
            _ => true,
        };
        return allowed && (Combo is not TitleCombo combo || combo.Fits(Skips, speed))
            && (TitleRecipes.Find(this) is not TitleRecipe recipe || recipe.Presses(speed));
    }

    public string ButtonsKey => Buttons == TitleButtonMode.LEqualsA ? "la" : "help";

    public string SoundKey => Sound == TitleSoundMode.Stereo ? "stereo" : "mono";

    public string Name => (OnLoop ? "loop" + FirstKey + "-" + LoopKey + "_" : Intro == TitleIntro.Skip990 ? "intro990_" : Intro == TitleIntro.Skip477 && !Skip477IsTerm ? "intro477_" : "")
        + (Saves == TitleSaves.Single ? "single_" : "")
        + (Game == TitleGame.LeafGreen ? "lg_" : "")
        + ButtonsKey + "_" + SoundKey
        + (HasRecipe ? "__r-" + Recipe
            : Combo is TitleCombo c && TitleCombos.OwnTableKey(c, IntroSkipped, Sound) is string key ? "__" + key : "");

    public static TitleVariant Parse(string? buttons, string? sound, string? intro = null, string? animation = null,
        string? combo = null, string? game = null, string? saves = null)
    {
        TitleButtonMode mode = buttons == "la" ? TitleButtonMode.LEqualsA : TitleButtonMode.Help;
        TitleCombo? presses = TitleCombo.Parse(combo);
        if (presses is TitleCombo chosen && !TitleCombos.All(mode).Contains(chosen)) presses = null;
        TitleAnimation title = animation switch
        {
            "played" => TitleAnimation.PlayedOut,
            "spedup" => TitleAnimation.SpedUp,
            _ => TitleAnimation.Either,
        };
        TitleIntro skip = intro switch
        {
            "skip477" => TitleIntro.Skip477,
            "skip990" => TitleIntro.Skip990,
            _ => TitleIntro.Played,
        };
        TitleLoop loop = TitleLoop.None;
        if (intro is not null && intro.StartsWith("loop", StringComparison.Ordinal) && intro.IndexOf('-') is int dash and > 4)
        {
            skip = intro[4..dash] switch { "477" => TitleIntro.Skip477, "990" => TitleIntro.Skip990, _ => TitleIntro.Played };
            loop = intro[(dash + 1)..] switch { "477" => TitleLoop.Skip477, "990" => TitleLoop.Skip990, _ => TitleLoop.Played };
        }
        return new TitleVariant(mode, sound == "stereo" ? TitleSoundMode.Stereo : TitleSoundMode.Mono, skip, title, presses,
            game == "lg" ? TitleGame.LeafGreen : TitleGame.FireRed, loop,
            saves == "single" ? TitleSaves.Single : TitleSaves.Multi);
    }

    public override string ToString() => (Game == TitleGame.LeafGreen ? "LeafGreen, " : "")
       + (Buttons == TitleButtonMode.LEqualsA ? "L=A" : "Help")
       + " + " + (Sound == TitleSoundMode.Stereo ? "stereo" : "mono")
       + (Saves == TitleSaves.Single ? ", single save" : "")
       + (Intro switch
       {
           TitleIntro.Skip477 => ", intro skipped at 477",
           TitleIntro.Skip990 => ", intro skipped at 990",
           _ => "",
       })
       + (Loop switch
       {
           TitleLoop.Played => ", second title screen (loop intro played)",
           TitleLoop.Skip477 => ", second title screen (loop intro skipped at its 477)",
           TitleLoop.Skip990 => ", second title screen (loop intro skipped at its 990)",
           _ => "",
       })
       + (Animation switch
       {
           TitleAnimation.PlayedOut => ", title played out",
           TitleAnimation.SpedUp => ", title sped up",
           _ => "",
       })
       + (Combo is TitleCombo combo ? ", " + combo.Short : "")
       + (HasRecipe ? ", " + TitleRecipes.Label(Recipe!) : "");
}

public readonly record struct RtaSeedEntry(int Offset, int Seed, int Band, bool Measured);

public readonly record struct PressFrame(int Offset, int Pass, int Seed, int Cycles,
    TitleProtocol Protocol = TitleProtocol.Sweep, int Band = 0, bool Measured = true,
    TitleVariant Variant = default, int Window = 1)
{
    public int LastOffset => Offset + Window - 1;

    public int IntroWindow => TitleSeedTable.IntroWindowOf(Variant);

    public int? SeedPressFrame =>
        Protocol == TitleProtocol.Rta && Offset < TitleSeedTable.AnimationEndsOf(Variant.Game) ? Offset + 1 : null;

    public int Recorded => (Seed + Cycles) & 0xFFFF;

    public int TableCounter => (Recorded - (Variant.Skip477IsTerm ? TitleSeedTable.IntroSkipShiftOf(Variant) ?? 0 : 0)) & 0xFFFF;

    public int ResetFrame => TitleRecipes.Find(Variant) is TitleRecipe recipe
        ? recipe.Anchor + Offset
        : Variant.OnLoop
        ? TitleSeedTable.LoopAnchorOf(Variant) + (Pass - 1) * TitleSeedTable.RtaPassFrames + Offset
        : TitleSeedTable.IntroAnchorOf(Variant.Intro, Variant.Saves) + WaitFrames;

    public int WaitFrames => Pass * TitleSeedTable.PassFramesOf(Protocol) + Offset;

    public double WaitSeconds => WaitFrames / TitleSeedTable.FramesPerSecond;
}

public static class TitleSeedTable
{
    public const int PassShift = 34368;

    public const int CycleOffset = 53;

    public const int PassFrames = 4325;

    public const int RtaPassShift = 34368;

    public const int RtaPassFrames = 4325;

    public const int RtaPassBand = 83;

    public const int IntroSkipShift = 6004;

    public static int? IntroSkipShiftOf(TitleVariant variant)
    {
        if (variant.Saves == TitleSaves.Single) return null;

        int? term = (variant with { Combo = null, Intro = TitleIntro.Played, Loop = TitleLoop.None }).Name switch
        {
            "help_mono" => IntroSkipShift,
            "help_stereo" => IntroSkipShift,
            "la_mono" => 6015,
            "la_stereo" => 6015,
            "lg_help_mono" => IntroSkipShift,
            "lg_help_stereo" => IntroSkipShift,
            "lg_la_mono" => 6015,
            "lg_la_stereo" => 6015,
            _ => null,
        };
        if (term is null || variant.Combo is not TitleCombo combo) return term;

        IReadOnlyList<TitleButton> order = combo.Order;
        if (order.Count < 2) return term;
        TitleButton skip = order[0];
        var title = order.Skip(1).ToList();
        bool sped = title.Count == 2;
        bool tableTitle = sped
            ? (title[0] == TitleButton.A && (title[1] == TitleButton.Start || title[1] == TitleButton.L))
              || (title[0] == TitleButton.Start && title[1] == TitleButton.A)
            : title[0] == TitleButton.Start;
        if (skip == TitleButton.Select || skip == TitleButton.Start) return tableTitle ? term : null;
        if ((variant with { Combo = null, Intro = TitleIntro.Played, Loop = TitleLoop.None }).Name is not ("la_mono" or "la_stereo")) return null;
        if (skip == TitleButton.A)
        {
            return sped && title[0] != TitleButton.A && title[1] != TitleButton.A && title.Contains(TitleButton.L) && title.Contains(TitleButton.Start)
                ? 6007 : null;
        }
        return sped && title[0] == TitleButton.A && title[1] == TitleButton.Start ? 6010 : null;
    }

    public const int IntroSkipFrame = 477;

    public const int IntroSkipWindow = 3;

    public const int IntroSkip990Frame = 987;

    public const int IntroSkip990Window = 5;

    public static bool SweptHeld(TitleVariant variant) => variant.OnLoop || variant.Saves == TitleSaves.Single || variant.HasRecipe || variant.PairTable.Name is
        "help_mono" or "intro990_help_mono"
        or "help_stereo" or "intro990_help_stereo"
        or "la_mono" or "intro990_la_mono"
        or "la_stereo" or "intro990_la_stereo"
        or "lg_help_stereo" or "lg_help_mono" or "lg_la_stereo" or "lg_la_mono"
        or "intro990_lg_help_stereo" or "intro990_lg_help_mono" or "intro990_lg_la_stereo" or "intro990_lg_la_mono";

    public const int IntroSkip990Press = 990;

    public static int IntroFrameOf(TitleIntro intro) => intro switch
    {
        TitleIntro.Skip477 => IntroSkipFrame,
        TitleIntro.Skip990 => IntroSkip990Frame,
        _ => 0,
    };

    public static int IntroFrameOf(TitleVariant variant) =>
        TitleRecipes.Find(variant) is TitleRecipe recipe ? recipe.SkipFrame
        : SkipsWithAOrL(variant)
            ? (variant.Intro == TitleIntro.Skip990 ? IntroSkip990AOrLFrame : IntroSkipFrame - (variant.Saves == TitleSaves.Single ? 1 : 0))
            : variant.Saves == TitleSaves.Single && variant.Intro == TitleIntro.Skip477 ? IntroSkipFrame - 1
            : IntroFrameOf(variant.Intro);

    public static int IntroWindowOf(TitleVariant variant) =>
        TitleRecipes.Find(variant) is TitleRecipe recipe ? recipe.SkipWindow
        : SkipsWithAOrL(variant) ? (variant.Intro == TitleIntro.Skip990 ? IntroSkip990AOrLWindow - (variant.Saves == TitleSaves.Single ? 1 : 0)
                                   : IntroSkip477AOrLWindow)
        : variant.Saves == TitleSaves.Single && variant.Intro == TitleIntro.Skip990 ? IntroSkip990Window - 1
        : IntroWindowOf(variant.Intro);

    public const int IntroSkip477AOrLWindow = 3;

    public const int IntroSkip990AOrLFrame = 988;

    public const int IntroSkip990AOrLWindow = 4;

    private static bool SkipsWithAOrL(TitleVariant variant) =>
        variant.IntroSkipped && variant.Combo is TitleCombo combo
        && (combo.First == TitleButton.A || combo.First == TitleButton.L);

    public static int IntroWindowOf(TitleIntro intro) => intro switch
    {
        TitleIntro.Skip477 => IntroSkipWindow,
        TitleIntro.Skip990 => IntroSkip990Window,
        _ => 0,
    };

    public static int IntroAnchorOf(TitleIntro intro, TitleSaves saves = TitleSaves.Multi) => (intro switch
    {
        TitleIntro.Skip477 => 482,
        TitleIntro.Skip990 => 994,
        _ => 1742,
    }) - (saves == TitleSaves.Single ? 1 : 0);

    public static int PassFramesOf(TitleProtocol protocol) =>
        protocol == TitleProtocol.Rta ? RtaPassFrames : PassFrames;

    public const int AnimationEnds = 268;

    public const int AnimationEndsLeafGreen = 267;

    public static int AnimationEndsOf(TitleGame game) => game == TitleGame.LeafGreen ? AnimationEndsLeafGreen : AnimationEnds;

    public const double FramesPerSecond = 59.7275;

    private const int SeedSpace = 1 << 16;

    private const int Stride = 64;

    private const int PassCycle = SeedSpace / Stride;

    private static readonly Lazy<TitleSeedEntry[]> Table = new(Load);

    private static readonly ConcurrentDictionary<TitleVariant, RtaSeedEntry[]?> RtaTables = new();

    private static readonly Lazy<Dictionary<int, int>> SweepWindows = new(() => Windows(Table.Value.Select(e => (e.Offset, e.Seed))));

    private static readonly ConcurrentDictionary<TitleVariant, Dictionary<int, int>> RtaWindows = new();

    private static readonly ConcurrentDictionary<TitleVariant, Dictionary<int, RtaSeedEntry[]>> RtaByCounter = new();

    private static readonly RtaSeedEntry[] NoEntries = Array.Empty<RtaSeedEntry>();

    private static RtaSeedEntry[] RowsReading(TitleVariant table, int counter) =>
        RtaByCounter.GetOrAdd(table, t => RtaEntriesFor(t)
                .GroupBy(entry => entry.Seed)
                .ToDictionary(group => group.Key, group => group.ToArray()))
            .TryGetValue(counter, out RtaSeedEntry[]? rows) ? rows : NoEntries;

    public static int WindowOf(int offset, TitleProtocol protocol = TitleProtocol.Sweep, TitleVariant variant = default)
    {
        Dictionary<int, int> windows = protocol == TitleProtocol.Rta
            ? RtaWindows.GetOrAdd(variant, v => Windows(RtaEntriesFor(v).Select(e => (e.Offset, e.Seed))))
            : SweepWindows.Value;
        return windows.TryGetValue(offset, out int width) ? width : 1;
    }

    private static Dictionary<int, int> Windows(IEnumerable<(int Offset, int Seed)> rows)
    {
        var widths = new Dictionary<int, int>();
        int start = -1, last = -1, seed = -1;
        foreach ((int offset, int counter) in rows.OrderBy(r => r.Offset))
        {
            if (start >= 0 && offset == last + 1 && counter == seed)
            {
                last = offset;
                widths[offset] = 0;
                continue;
            }
            if (start >= 0) widths[start] = last - start + 1;
            start = last = offset;
            seed = counter;
        }
        if (start >= 0) widths[start] = last - start + 1;
        return widths;
    }

    public static IReadOnlyList<TitleSeedEntry> Entries => Table.Value;

    public static IReadOnlyList<RtaSeedEntry> RtaEntriesFor(TitleVariant variant) =>
        RtaTables.GetOrAdd(variant.Table, LoadRta) ?? Array.Empty<RtaSeedEntry>();

    public static bool HasRta(TitleVariant variant) =>
        RtaTables.GetOrAdd(variant.Table, LoadRta) is not null
        && (!variant.Skip477IsTerm || IntroSkipShiftOf(variant) is not null);

    public static int LoopFrameOf(TitleVariant variant)
    {
        int anchor = IntroAnchorOf(variant.Intro, variant.Saves);
        return variant.Loop switch
        {
            TitleLoop.Skip477 => anchor + LoopSkipAfterAnchor477,
            TitleLoop.Skip990 => anchor + LoopSkipAfterAnchor990 - 3,
            _ => 0,
        };
    }

    public const int LoopSkip477Window = 3;

    public const int LoopSkip990Window = 5;

    public static int LoopWindowOf(TitleVariant variant) => variant.Loop switch
    {
        TitleLoop.Skip477 => LoopSkip477Window,
        TitleLoop.Skip990 => LoopSkip990Window,
        _ => 0,
    };

    public const int LoopSkipAfterAnchor477 = 3060;

    public const int LoopSkipAfterAnchor990 = 3573;

    public static int LoopAnchorOf(TitleVariant variant)
    {
        if (!variant.OnLoop) return IntroAnchorOf(variant.Intro, variant.Saves);
        if (variant.Loop == TitleLoop.Played) return IntroAnchorOf(variant.Intro, variant.Saves) + RtaPassFrames;
        return IntroAnchorOf(variant.Intro, variant.Saves)
            + (variant.Loop == TitleLoop.Skip477 ? LoopSkipAfterAnchor477 + 5 : LoopSkipAfterAnchor990 + 4);
    }

    public static readonly (int, int) LoopSkipFramesAfterPlayed = (477 + RtaPassFrames, 990 + RtaPassFrames);

    public static readonly (int, int) LoopSkipFramesAfter477 = (477 + RtaPassFrames - (1742 - 482), 990 + RtaPassFrames - (1742 - 482));

    public static readonly (int, int) LoopSkipFramesAfter990 = (477 + RtaPassFrames - (1742 - 994), 990 + RtaPassFrames - (1742 - 994));

    public static IReadOnlyList<RtaSeedEntry> RtaEntries => RtaEntriesFor(TitleVariant.Default);

    public static PressFrame? Find(int titleSeed, int cycles = CycleOffset,
        TitleProtocol protocol = TitleProtocol.Sweep, TitleVariant variant = default, int maxResetFrame = 0)
    {
        if (protocol == TitleProtocol.Rta) return FindRta(titleSeed, cycles, variant, maxResetFrame);

        int wanted = ((titleSeed + cycles) % SeedSpace + SeedSpace) % SeedSpace;

        int unit = PassShift / Stride;
        int inverse = Inverse(unit, PassCycle);

        PressFrame? best = null;
        foreach (TitleSeedEntry entry in Table.Value)
        {
            int difference = ((wanted - entry.Seed) % SeedSpace + SeedSpace) % SeedSpace;
            if (difference % Stride != 0) continue;

            int pass = (int)((long)(difference / Stride) * inverse % PassCycle);
            int window = WindowOf(entry.Offset);
            if (window == 0) continue;
            if (best is null || pass < best.Value.Pass
                || (pass == best.Value.Pass && (window > best.Value.Window
                    || (window == best.Value.Window && entry.Offset < best.Value.Offset))))
            {
                best = new PressFrame(entry.Offset, pass, titleSeed, cycles, Window: window);
            }
        }

        return best;
    }

    private static PressFrame? FindRta(int titleSeed, int cycles, TitleVariant variant, int maxResetFrame = 0)
    {
        int shift = 0;
        if (variant.Skip477IsTerm)
        {
            if (IntroSkipShiftOf(variant) is not int term) return null;
            shift = term;
        }
        int wanted = ((titleSeed + cycles - shift) % SeedSpace + SeedSpace) % SeedSpace;

        TitleVariant loopPlayed = variant with { Loop = TitleLoop.Played };
        bool measuredLoop = !variant.OnLoop && variant.Table.Combo is null && !variant.HasRecipe
            && RtaTables.GetOrAdd(loopPlayed.Table, LoadRta) is not null;

        PressFrame? best = null;
        void Consider(PressFrame candidate)
        {
            if (maxResetFrame > 0 && candidate.ResetFrame > maxResetFrame) return;
            if (best is null || candidate.Pass < best.Value.Pass
                || (candidate.Pass == best.Value.Pass && (candidate.Measured && !best.Value.Measured
                    || (candidate.Measured == best.Value.Measured && (candidate.Window > best.Value.Window
                        || (candidate.Window == best.Value.Window && candidate.Offset < best.Value.Offset))))))
            {
                best = candidate;
            }
        }

        foreach (RtaSeedEntry entry in RowsReading(variant.Table, wanted))
        {
            if (!variant.Allows(entry.Offset)) continue;

            int window = WindowOf(entry.Offset, TitleProtocol.Rta, variant.Table);
            if (window == 0) continue;
            Consider(new PressFrame(entry.Offset, variant.OnLoop ? 1 : 0, titleSeed, cycles, TitleProtocol.Rta,
                Band: 0, Measured: true, Variant: variant, Window: window));
        }

        if (measuredLoop)
        {
            int wantedLoop = ((titleSeed + cycles) % SeedSpace + SeedSpace) % SeedSpace;
            foreach (RtaSeedEntry entry in RowsReading(loopPlayed.Table, wantedLoop))
            {
                if (!loopPlayed.Allows(entry.Offset)) continue;
                int window = WindowOf(entry.Offset, TitleProtocol.Rta, loopPlayed.Table);
                if (window == 0) continue;
                Consider(new PressFrame(entry.Offset, 1, titleSeed, cycles, TitleProtocol.Rta,
                    Band: 0, Measured: true, Variant: loopPlayed, Window: window));
            }
        }

        return best;
    }

    public static IReadOnlyCollection<int> ReachableSeeds(TitleVariant variant, int cycles = CycleOffset)
    {
        var seeds = new HashSet<int>();
        if (!HasRta(variant)) return seeds;

        int shift = variant.Skip477IsTerm ? IntroSkipShiftOf(variant) ?? 0 : 0;
        foreach (RtaSeedEntry entry in RtaEntriesFor(variant.Table))
        {
            seeds.Add(((entry.Seed + shift - cycles) % SeedSpace + SeedSpace) % SeedSpace);
        }

        TitleVariant loopPlayed = variant with { Loop = TitleLoop.Played };
        if (!variant.OnLoop && variant.Table.Combo is null && !variant.HasRecipe && RtaTables.GetOrAdd(loopPlayed.Table, LoadRta) is not null)
        {
            foreach (RtaSeedEntry entry in RtaEntriesFor(loopPlayed.Table))
            {
                seeds.Add(((entry.Seed - cycles) % SeedSpace + SeedSpace) % SeedSpace);
            }
        }
        return seeds;
    }

    private static readonly (TitleIntro Intro, TitleLoop Loop)[] IntroChoices =
    {
        (TitleIntro.Played, TitleLoop.None), (TitleIntro.Skip477, TitleLoop.None), (TitleIntro.Skip990, TitleLoop.None),
        (TitleIntro.Played, TitleLoop.Skip477), (TitleIntro.Played, TitleLoop.Skip990),
        (TitleIntro.Skip477, TitleLoop.Skip477), (TitleIntro.Skip477, TitleLoop.Skip990),
        (TitleIntro.Skip990, TitleLoop.Skip477), (TitleIntro.Skip990, TitleLoop.Skip990),
    };

    public static List<TitleVariant> Spanning(TitleGame game, TitleButtonMode? buttons, TitleSaves? saves,
        List<TitleVariant>? unswept = null)
    {
        var variants = new List<TitleVariant>();
        var seen = new HashSet<(string Table, int Term, int Count)>();
        TitleButtonMode[] modes = buttons is TitleButtonMode one
            ? new[] { one }
            : new[] { TitleButtonMode.Help, TitleButtonMode.LEqualsA };
        TitleSaves[] kinds = saves is TitleSaves kind
            ? new[] { kind }
            : new[] { TitleSaves.Multi, TitleSaves.Single };

        foreach (TitleSaves saveKind in kinds)
        foreach (TitleButtonMode mode in modes)
        foreach (TitleSoundMode sound in new[] { TitleSoundMode.Mono, TitleSoundMode.Stereo })
        {
            var pair = new TitleVariant(mode, sound, Game: game, Saves: saveKind);
            if (!HasRta(pair))
            {
                unswept?.Add(pair);
                continue;
            }

            foreach ((TitleIntro intro, TitleLoop loop) in IntroChoices)
            {
                TitleVariant plain = pair with { Intro = intro, Loop = loop };
                if (HasRta(plain)) variants.Add(plain);

                foreach (TitleCombo combo in TitleCombos.All(mode))
                {
                    TitleVariant own = plain with { Combo = combo };
                    if (!combo.Fits(own.Skips, speed: true) && !combo.Fits(own.Skips, speed: false)) continue;

                    int term = own.Skip477IsTerm ? IntroSkipShiftOf(own) ?? -1 : 0;
                    bool ownTerm = own.Skip477IsTerm && term >= 0 && term != (IntroSkipShiftOf(plain) ?? -1);
                    if (!own.OwnsTable && !ownTerm) continue;
                    if (!HasRta(own)) continue;
                    if (seen.Add((own.Table.Name, term, combo.Count))) variants.Add(own);
                }
            }

            foreach (TitleRecipe recipe in TitleRecipes.For(pair))
            {
                if (HasRta(recipe.Variant)) variants.Add(recipe.Variant);
            }
        }
        return variants;
    }

    public static int? SeedAt(int offset, int pass, int cycles, TitleVariant variant)
    {
        int shift = 0;
        if (variant.Skip477IsTerm)
        {
            if (IntroSkipShiftOf(variant) is not int term) return null;
            shift = term;
        }
        TitleVariant table = variant;
        if (!variant.OnLoop && pass == 1)
        {
            if (variant.Table.Combo is not null || variant.HasRecipe) return null;
            table = variant with { Loop = TitleLoop.Played };
            shift = 0;
            if (RtaTables.GetOrAdd(table.Table, LoadRta) is null) return null;
        }
        else if (pass != (variant.OnLoop ? 1 : 0))
        {
            return null;
        }
        foreach (RtaSeedEntry entry in RtaEntriesFor(table.Table))
        {
            if (entry.Offset != offset) continue;
            long counter = entry.Seed + shift - cycles;
            return (int)((counter % SeedSpace + SeedSpace) % SeedSpace);
        }
        return null;
    }

    private static int Inverse(int value, int modulus)
    {
        for (int candidate = 1; candidate < modulus; candidate += 2)
        {
            if ((long)candidate * value % modulus == 1) return candidate;
        }
        throw new InvalidOperationException("the pass shift does not invert");
    }

    private static TitleSeedEntry[] Load()
    {
        Assembly assembly = typeof(TitleSeedTable).Assembly;
        using Stream stream = assembly.GetManifestResourceStream("FRLG.StarterTool.Core.Data.titleSeeds.csv")
            ?? throw new InvalidOperationException("titleSeeds.csv is missing from the assembly");
        using var reader = new StreamReader(stream);

        var entries = new List<TitleSeedEntry>();
        reader.ReadLine();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;
            string[] fields = line.Split(',');
            if (fields.Length < 3) continue;

            entries.Add(new TitleSeedEntry(
                int.Parse(fields[0], CultureInfo.InvariantCulture),
                int.Parse(fields[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                int.Parse(fields[2], CultureInfo.InvariantCulture)));
        }

        return entries.ToArray();
    }

    private static RtaSeedEntry[]? LoadRta(TitleVariant variant)
    {
        Assembly assembly = typeof(TitleSeedTable).Assembly;
        string name = variant == TitleVariant.Default
            ? "FRLG.StarterTool.Core.Data.titleSeedsRta.csv"
            : "FRLG.StarterTool.Core.Data.titleSeedsRta_" + variant.Name + ".csv";
        using Stream? stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            if (variant == TitleVariant.Default) throw new InvalidOperationException("titleSeedsRta.csv is missing from the assembly");
            return null;
        }
        using var reader = new StreamReader(stream);

        var entries = new List<RtaSeedEntry>();
        reader.ReadLine();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;
            string[] fields = line.Split(',');
            if (fields.Length < 4) continue;

            entries.Add(new RtaSeedEntry(
                int.Parse(fields[0], CultureInfo.InvariantCulture),
                int.Parse(fields[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                int.Parse(fields[2], CultureInfo.InvariantCulture),
                int.Parse(fields[3], CultureInfo.InvariantCulture) > 0));
        }

        return entries.ToArray();
    }
}
