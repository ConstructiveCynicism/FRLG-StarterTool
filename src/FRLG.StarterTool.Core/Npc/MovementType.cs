namespace FRLG.StarterTool.Core.Npc;

public enum MovementType
{
    FaceFixed,

    WanderAround,

    WanderUpAndDown,

    LookAround,
}

public static class MovementTypes
{
    public static Direction InitialFacing(MovementType type) => type switch
    {
        MovementType.WanderUpAndDown => Direction.North,
        _ => Direction.South,
    };

    public static Direction[] DirectionPool(MovementType type) => type switch
    {
        MovementType.WanderAround => Directions.Standard,
        MovementType.LookAround => Directions.Standard,
        MovementType.WanderUpAndDown => Directions.UpAndDown,
        _ => Array.Empty<Direction>(),
    };

    public static bool Walks(MovementType type) =>
        type is MovementType.WanderAround or MovementType.WanderUpAndDown;

    public static bool RollsRng(MovementType type) => type != MovementType.FaceFixed;
}
