namespace FRLG.StarterTool.Core.Npc;

public static class MapObjects
{
    public const int PalletSignLady = 0;

    public const int PalletFatMan = 1;

    public const int LabScientistLeft = 0;

    public const int LabAide = 1;

    public const int LabScientistRight = 2;

    public const int Elevation = 3;

    public static List<ObjectEventSim> PalletTown() => new()
    {
        new ObjectEventSim(PalletSignLady, NpcId.SignLady, MovementType.FaceFixed,
            5, 15, 1, 1, Elevation),
        new ObjectEventSim(PalletFatMan, NpcId.FatMan, MovementType.WanderAround,
            13, 17, 6, 2, Elevation),
    };

    public static List<ObjectEventSim> OaksLab() => new()
    {
        new ObjectEventSim(LabScientistLeft, NpcId.ScientistLeft, MovementType.LookAround,
            3, 11, 0, 0, Elevation),
        new ObjectEventSim(LabAide, NpcId.Aide, MovementType.WanderUpAndDown,
            2, 10, 0, 4, Elevation),
        new ObjectEventSim(LabScientistRight, NpcId.ScientistRight, MovementType.LookAround,
            11, 10, 0, 0, Elevation),
    };

    public static OverworldSim NewPalletTown(GameRng rng) =>
        new(GameMap.PalletTown, rng, PalletTown()) { AmbientCry = new AmbientCrySim { WaterMon = true } };

    public static OverworldSim NewOaksLab(GameRng rng) =>
        new(GameMap.OaksLab, rng, OaksLab());
}
