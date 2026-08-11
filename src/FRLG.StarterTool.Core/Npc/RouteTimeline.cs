namespace FRLG.StarterTool.Core.Npc;

public enum RouteAnchor
{
    ExitHouse,

    CloseOakText,

    CloseLabText,
}

public static class RouteTimeline
{
    public const int MapLoadRolls = 3;

    public const int MapLoadFrames = 1;

    public const int AnchorCorrectionFrames = 14;

    public const int BallGenerationAdvances = 2;

    public const int ExitHouseToPalletControlFrames =
        ExitHouseToFatManSpawnFrames + FatManSpawnToControlFrames;

    public const int ExitHouseToFatManSpawnFrames = 82;

    public const int FatManSpawnToControlFrames = 34;

    public const int FatManActiveFrames = 109;

    public const int TriggerToOakTextFrames = 349;

    public const int OakTextToLabLoadFrames = 426;

    public const int LeadWalkFatManRespawnFrames = 145;

    public const int LeadWalkFatManFreezeFrames = 400;

    public const int LeadWalkFatManVisibleFrames = 201;

    public const int LeadWalkFatManFullyVisibleFrames = 233;

    public const int LabLoadToReleaseFrames = 32;

    public const int LabObservableFrames = 65;

    public const int LabObservableLateFrames = 95;

    public const int LabEntryX = 6;

    public const int LabEntryY = 12;

    public const int PalletExitX = 6;

    public const int PalletExitY = 8;

    public static readonly IReadOnlyList<NpcId> LabObservable = new[] { NpcId.Aide, NpcId.ScientistRight };

    public static NpcId LabObservableScientist => LabObservable[1];

    public static readonly IReadOnlyList<NpcId> PalletObservable = new[] { NpcId.FatMan };

    public static void RunPlayersHouse(GameRng rng, int framesInHouse = 0)
    {
        for (int i = 0; i < framesInHouse + ExitHouseToFatManSpawnFrames; i++)
        {
            rng.VBlank();
        }

        for (int i = 0; i < MapLoadRolls + AmbientCrySim.LoadRolls; i++)
        {
            rng.Random();
        }
    }

    public static OverworldSim RunPalletTown(GameRng rng, int framesToTrigger, List<NpcEvent>? events = null)
    {
        OverworldSim sim = MapObjects.NewPalletTown(rng);
        sim.UpdateSpawns(PalletExitX, PalletExitY);

        for (int i = 0; i < FatManSpawnToControlFrames; i++)
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
        Action<int, OverworldSim>? onFrame = null)
    {
        pallet.FreezeAll(true);

        int respawn = CutsceneRespawnFrame(frames);
        int refreeze = CutsceneFreezeFrame(frames);

        for (int i = 0; i < frames; i++)
        {
            if (i == respawn) pallet.SetActive(MapObjects.PalletFatMan, true);

            if (i == refreeze) pallet.FreezeAll(true);

            pallet.StepFrame(events);
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

        int frozenBefore = Math.Min(LabLoadToReleaseFrames - 1, framesFrozen);
        for (int i = 0; i < frozenBefore; i++)
        {
            sim.StepFrame(events);
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
