namespace FRLG.StarterTool.Core.Npc;

public enum MovementAction
{
    None,

    Face,

    WalkNormal,
}

public sealed class ObjectEventSim
{
    public static readonly int[] MovementDelaysMedium = { 32, 64, 96, 128 };

    public const int NormalWalkFrames = 16;

    public ObjectEventSim(int slot, NpcId id, MovementType movementType,
        int x, int y, int rangeX, int rangeY, int elevation)
    {
        Slot = slot;
        Id = id;
        MovementType = movementType;
        InitialX = x;
        InitialY = y;
        Elevation = elevation;

        if (MovementTypes.Walks(movementType))
        {
            RangeX = rangeX == 0 ? 1 : rangeX;
            RangeY = rangeY == 0 ? 1 : rangeY;
        }
        else
        {
            RangeX = rangeX;
            RangeY = rangeY;
        }

        Reset();
    }

    public int Slot { get; }

    public NpcId Id { get; }

    public string Name => Id.Name();

    public MovementType MovementType { get; }

    public int InitialX { get; }

    public int InitialY { get; }

    public int RangeX { get; }

    public int RangeY { get; }

    public int Elevation { get; }

    public bool Active { get; set; }

    public bool Frozen { get; set; }

    public int X { get; private set; }

    public int Y { get; private set; }

    public int PreviousX { get; private set; }

    public int PreviousY { get; private set; }

    public Direction FacingDirection { get; private set; }

    public Direction MovementDirection { get; private set; }

    public int StepState { get; private set; }

    public int ActionStep { get; private set; }

    public int MovementDelay { get; private set; }

    public bool SingleMovementActive { get; private set; }

    public MovementAction Action { get; private set; }

    public int WalkStepNo { get; private set; }

    public void Reset()
    {
        X = PreviousX = InitialX;
        Y = PreviousY = InitialY;
        FacingDirection = MovementDirection = MovementTypes.InitialFacing(MovementType);
        StepState = 0;
        ActionStep = 0;
        MovementDelay = 0;
        SingleMovementActive = false;
        Action = MovementAction.None;
        WalkStepNo = 0;
        Frozen = false;
    }

    public void Update(GameRng rng, INpcWorld world, int frame, List<NpcEvent>? events)
    {
        if (!Active || Frozen) return;
        if (!MovementTypes.RollsRng(MovementType)) return;

        while (Step(rng, world, frame, events))
        {
        }
    }

    private bool Step(GameRng rng, INpcWorld world, int frame, List<NpcEvent>? events)
    {
        bool walks = MovementTypes.Walks(MovementType);

        switch (StepState)
        {
            case 0:
                SingleMovementActive = false;
                Action = MovementAction.None;
                ActionStep = 0;
                StepState = 1;
                return true;

            case 1:
                SetSingleMovement(MovementAction.Face);
                StepState = 2;
                return true;

            case 2:
            {
                bool done = ExecSingleMovementAction();
                if (!done) return false;

                MovementDelay = MovementDelaysMedium[rng.Random() & 3];
                StepState = 3;
                if (!walks) SingleMovementActive = false;
                return walks;
            }

            case 3:
                if (WaitForMovementDelay())
                {
                    StepState = 4;
                    return true;
                }
                return false;

            case 4:
            {
                Direction[] pool = MovementTypes.DirectionPool(MovementType);
                Direction chosen = pool[rng.Random() & (pool.Length - 1)];

                bool silent = chosen == FacingDirection;
                SetObjectEventDirection(chosen);

                if (!walks)
                {
                    StepState = 1;
                    events?.Add(new NpcEvent(frame, rng.Advances, Slot, Id, NpcEventKind.Face, chosen,
                        false, silent));
                    return true;
                }

                StepState = 5;
                if (GetCollisionInDirection(world, chosen) != Collision.None)
                {
                    StepState = 1;
                    events?.Add(new NpcEvent(frame, rng.Advances, Slot, Id, NpcEventKind.Face, chosen,
                        true, silent));
                }
                return true;
            }

            case 5:
                SetSingleMovement(MovementAction.WalkNormal);
                SingleMovementActive = true;
                StepState = 6;
                events?.Add(new NpcEvent(frame, rng.Advances, Slot, Id, NpcEventKind.Step, MovementDirection, false));
                return true;

            case 6:
                if (ExecSingleMovementAction())
                {
                    SingleMovementActive = false;
                    StepState = 1;
                }
                return false;

            default:
                return false;
        }
    }

    private void SetSingleMovement(MovementAction action)
    {
        Action = action;
        ActionStep = 0;
    }

    private bool ExecSingleMovementAction()
    {
        bool finished = Action switch
        {
            MovementAction.Face => ExecFace(),
            MovementAction.WalkNormal => ExecWalkNormal(),
            _ => true,
        };

        if (finished)
        {
            Action = MovementAction.None;
            ActionStep = 0;
        }
        return finished;
    }

    private bool ExecFace()
    {
        SetObjectEventDirection(MovementDirection);
        ShiftStillObjectEventCoords();
        return true;
    }

    private bool ExecWalkNormal()
    {
        if (ActionStep == 0)
        {
            InitNpcForMovement(MovementDirection);
            ActionStep = 1;
        }

        if (WalkStepNo >= NormalWalkFrames) return false;

        WalkStepNo++;
        if (WalkStepNo < NormalWalkFrames) return false;

        ShiftStillObjectEventCoords();
        return true;
    }

    private void InitNpcForMovement(Direction direction)
    {
        int x = X, y = Y;
        SetObjectEventDirection(direction);
        Directions.MoveCoords(direction, ref x, ref y);
        ShiftObjectEventCoords(x, y);
        WalkStepNo = 0;
    }

    private void ShiftObjectEventCoords(int x, int y)
    {
        PreviousX = X;
        PreviousY = Y;
        X = x;
        Y = y;
    }

    private void ShiftStillObjectEventCoords() => ShiftObjectEventCoords(X, Y);

    private void SetObjectEventDirection(Direction direction)
    {
        FacingDirection = direction;
        MovementDirection = direction;
    }

    private bool WaitForMovementDelay()
    {
        MovementDelay--;
        return MovementDelay == 0;
    }

    private Collision GetCollisionInDirection(INpcWorld world, Direction direction)
    {
        int x = X, y = Y;
        Directions.MoveCoords(direction, ref x, ref y);

        if (IsCoordOutsideMovementRange(x, y)) return Collision.OutsideRange;

        GameMap map = world.Map;
        if (map.CollisionAt(x, y) != 0 || !map.InBounds(x, y)) return Collision.Impassable;
        if (map.IsElevationMismatchAt(Elevation, x, y)) return Collision.ElevationMismatch;
        if (world.DoesObjectCollideWithObjectAt(this, x, y)) return Collision.ObjectEvent;

        return Collision.None;
    }

    public bool IsCoordOutsideMovementRange(int x, int y)
    {
        if (RangeX != 0 && (InitialX - RangeX > x || InitialX + RangeX < x)) return true;
        if (RangeY != 0 && (InitialY - RangeY > y || InitialY + RangeY < y)) return true;
        return false;
    }

    public void CopyStateTo(ObjectEventSim other)
    {
        other.Active = Active;
        other.Frozen = Frozen;
        other.X = X;
        other.Y = Y;
        other.PreviousX = PreviousX;
        other.PreviousY = PreviousY;
        other.FacingDirection = FacingDirection;
        other.MovementDirection = MovementDirection;
        other.StepState = StepState;
        other.ActionStep = ActionStep;
        other.MovementDelay = MovementDelay;
        other.SingleMovementActive = SingleMovementActive;
        other.Action = Action;
        other.WalkStepNo = WalkStepNo;
    }
}
