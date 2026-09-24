using FRLG.StarterTool.Core.Encounters;

namespace FRLG.StarterTool.Core.Npc;

public enum RouteAnchor
{
    ExitHouse,

    CloseOakText,

    CloseLabText,

    PressBall,
}

public static class RouteTimeline
{
    public const int MapLoadRolls = 3;

    public const int MapLoadFrames = 1;

    public const int AnchorCorrectionFrames = 14;

    public const int PressToReseedFrames = 24;

    public const int PreExitLoadRolls = 10;

    public const int AdapterWarpLagFrames = 9;

    public const int AdapterWarpLagFramesLong = 10;

    public const int AdapterPreExitLagFrames = 54;

    public static int AdapterTrainerCardAdvances(TitleButtonMode buttons) =>
        buttons == TitleButtonMode.Help ? -12 : -8;

    public static (int Advances, double Prior)[] AdapterTrainerCardForks(TitleButtonMode buttons) =>
        buttons == TitleButtonMode.Help
            ? new[] { (-12, 1.0), (-13, 27.0 / 59.0), (-11, 9.0 / 59.0) }
            : new[] { (-8, 1.0), (-7, 19.0 / 124.0), (-9, 7.0 / 124.0) };

    public static int PcVisitAdvances(bool adapter) => adapter ? -40 : 3;

    public const int RivalNameLagFrames = 9;

    public static int RivalNameAdvances(bool adapter, bool named) =>
        adapter && !named ? RivalNameLagFrames : 0;

    public static int AnchorCorrection(bool adapter) =>
        adapter ? PressToReseedFrames : AnchorCorrectionFrames;

    public static int AdvancesPerFrame(bool adapter) => adapter ? 2 : 1;

    public static int AdapterPlainFrame(int advances) =>
        PressToReseedFrames
        + (int)Math.Ceiling((advances - PreExitLoadRolls + AdapterPreExitLagFrames) / 2.0);

    public static int AdapterPlainAdvances(int frame) =>
        2 * (frame - PressToReseedFrames) - AdapterPreExitLagFrames + PreExitLoadRolls;

    public const int BallGenerationAdvances = 2;

    public const int AdapterBallGenerationAdvances = 3;

    public static int BallGeneration(bool adapter) =>
        adapter ? AdapterBallGenerationAdvances : BallGenerationAdvances;

    public const int ExitHouseToPalletControlFrames =
        ExitHouseToFatManSpawnFrames + FatManSpawnToControlFrames;

    public const int ExitHouseToFatManSpawnFrames = 82;

    public const int FatManSpawnToControlFrames = 34;

    public const int FatManActiveFrames = 109;

    public const int TriggerToOakTextFrames = 349;

    public const int OakTextToLabLoadFrames = 426;

    public const int LeadWalkFatManRespawnFrames = 145;

    public const int LeadWalkFatManFreezeFrames = 399;

    public const int LeadWalkFatManVisibleFrames = 201;

    public const int LeadWalkFatManFullyVisibleFrames = 233;

    public const int LabLoadToReleaseFrames = 32;

    public const int LabTextFloorFrames = 832;

    public const int LabObservableFrames = 65;

    public const int LabObservableLateFrames = 95;

    public const int LabObservableVeryLateFrames = 111;

    public const int LabEntryX = 6;

    public const int LabEntryY = 12;

    public const int PalletExitX = 6;

    public const int PalletExitY = 8;

    public static readonly IReadOnlyList<NpcId> LabObservable = new[] { NpcId.Aide, NpcId.ScientistRight };

    public static NpcId LabObservableScientist => LabObservable[1];

    public static readonly IReadOnlyList<NpcId> PalletObservable = new[] { NpcId.FatMan };

    public static void RunPlayersHouse(GameRng rng, int framesInHouse = 0,
        int warpLagFrames = AdapterWarpLagFrames)
    {
        int lag = rng.Adapter ? warpLagFrames : 0;
        for (int i = 0; i < framesInHouse + ExitHouseToFatManSpawnFrames - lag; i++)
        {
            rng.QuietFrame();
        }

        for (int i = 0; i < lag; i++)
        {
            rng.VBlank();
        }

        for (int i = 0; i < MapLoadRolls + AmbientCrySim.LoadRolls; i++)
        {
            rng.Random();
        }
    }

    public static OverworldSim RunPalletTown(GameRng rng, int framesToTrigger,
        List<NpcEvent>? events = null, SpawnRead spawnRead = SpawnRead.PostVBlank)
    {
        OverworldSim sim = MapObjects.NewPalletTown(rng);
        sim.UpdateSpawns(PalletExitX, PalletExitY);

        sim.StepFrame(events,
            spawnRead == SpawnRead.PreVBlank ? MapObjects.PalletFatMan : -1);
        for (int i = 1; i < FatManSpawnToControlFrames; i++)
        {
            sim.StepFrame(events);
        }

        sim.ControlsLocked = false;

        int fatManFrames = Math.Min(FatManActiveFrames - FatManSpawnToControlFrames, framesToTrigger);
        for (int i = 0; i < fatManFrames; i++)
        {
            sim.StepFrame(events);
        }

        sim.SetActive(MapObjects.PalletFatMan, false);
        for (int i = fatManFrames; i < framesToTrigger; i++)
        {
            sim.StepFrame(events);
        }

        sim.FreezeAll(true);
        return sim;
    }

    public static void RunFrozenCutscene(OverworldSim pallet, int frames, List<NpcEvent>? events = null,
        Action<int, OverworldSim>? onFrame = null, SpawnRead respawnRead = SpawnRead.PostVBlank)
    {
        pallet.FreezeAll(true);

        int respawn = CutsceneRespawnFrame(frames);
        int refreeze = CutsceneFreezeFrame(frames);

        for (int i = 0; i < frames; i++)
        {
            if (i == respawn) pallet.SetActive(MapObjects.PalletFatMan, true);

            if (i == refreeze) pallet.FreezeAll(true);

            pallet.StepFrame(events,
                i == respawn && respawnRead == SpawnRead.PreVBlank ? MapObjects.PalletFatMan : -1);
            onFrame?.Invoke(i, pallet);
        }
    }

    public static int CutsceneRespawnFrame(int frames) =>
        frames - OakTextToLabLoadFrames + LeadWalkFatManRespawnFrames;

    public static int CutsceneFreezeFrame(int frames) =>
        frames - OakTextToLabLoadFrames + LeadWalkFatManFreezeFrames;

    public static OverworldSim EnterLab(GameRng rng, int framesFrozen, List<NpcEvent>? events = null)
    {
        OverworldSim sim = MapObjects.NewOaksLab(rng);
        sim.UpdateSpawns(LabEntryX, LabEntryY);

        sim.FreezeAll(true);

        rng.VBlank();
        for (int i = 0; i < MapLoadRolls; i++)
        {
            rng.Random();
        }

        int lag = rng.Adapter ? AdapterWarpLagFrames - 1 : 0;
        int frozenBefore = Math.Min(LabLoadToReleaseFrames - 1, framesFrozen);
        for (int i = 0; i < frozenBefore; i++)
        {
            if (i < lag) sim.StepLagFrame();
            else sim.StepFrame(events);
        }

        if (framesFrozen > frozenBefore)
        {
            sim.FreezeAll(false);
            sim.StepFrame(events);
            sim.FreezeAll(true);
        }

        for (int i = frozenBefore + 1; i < framesFrozen; i++)
        {
            sim.StepFrame(events);
        }

        return sim;
    }
}
