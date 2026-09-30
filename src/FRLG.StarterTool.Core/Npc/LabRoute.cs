namespace FRLG.StarterTool.Core.Npc;

public enum LabInput
{
    None,
    Up,
    Down,
    Left,
    Right,
    A,
}

public enum LabMoveKind
{
    Stand,

    Turn,

    Step,

    Bonk,

    Press,
}

public readonly record struct LabMove(LabMoveKind Kind, Direction Facing, int FromX, int FromY, int ToX, int ToY,
    int Start, int End, LabInput Input)
{
    public int Frames => End - Start;
}

public readonly record struct LabRouteState(float X, float Y, Direction Facing, bool Walking, LabMove Move, bool Pressed);

public sealed class LabRoute
{
    public const int StartX = 6;

    public const int StartY = 4;

    public const int BallX = 9;

    public const int BallY = 4;

    public const int OakX = 6;

    public const int OakY = 3;

    public const int RivalX = 5;

    public const int RivalY = 4;

    public const int TurnFrames = 8;

    public const int StepFrames = ObjectEventSim.NormalWalkFrames;

    public const int BonkFrames = 1;

    public const int PressAfterTurnFrames = 2;

    public const int PressAfterStepFrames = 1;

    public const int DirectPressFrame = TurnFrames + 4 * StepFrames + PressAfterTurnFrames;

    public const int HeldOkPressFrame = 62 + ObjectEventSim.NormalWalkFrames + 1;

    public const int LeadFrames = 30;

    public const int HoldFrames = 75;

    private LabRoute(string name, bool needWalking, int ladyDelay, IReadOnlyList<LabMove> moves)
    {
        Name = name;
        NeedWalking = needWalking;
        LadyDelay = ladyDelay;
        Moves = moves;
        PressFrame = moves[^1].Start;
    }

    public string Name { get; }

    public bool NeedWalking { get; }

    public int LadyDelay { get; }

    public int LadyWalkEnd => LadyDelay + ObjectEventSim.NormalWalkFrames;

    public IReadOnlyList<LabMove> Moves { get; }

    public int PressFrame { get; }

    public bool PressInsideWalk => PressFrame >= LadyDelay && PressFrame < LadyWalkEnd;

    public int LoopFrames => LeadFrames + PressFrame + HoldFrames;

    public static LabRoute? For(LabParity parity)
    {
        int delay = parity.LadyDelay;
        if (delay <= 0) return null;

        if (!parity.NeedWalking)
        {
            int direct = DirectPressFrame;
            int press = delay < direct ? HeldOkPressFrame : direct;
            return Direct(parity, press);
        }

        int extra = (int)Math.Round((delay - 62) / 32.0);
        return extra < 0 ? null : Bonk(parity, extra);
    }

    private static LabRoute Direct(LabParity parity, int pressFrame)
    {
        var b = new Builder();
        b.Turn(Direction.South, LabInput.Down);
        b.Step(Direction.South, LabInput.Down);
        b.Step(Direction.East, LabInput.Right);
        b.Step(Direction.East, LabInput.Right);
        b.Step(Direction.East, LabInput.Right);
        b.PressFromEast(pressFrame);
        return new LabRoute("direct", parity.NeedWalking, parity.LadyDelay, b.Moves);
    }

    private static LabRoute Bonk(LabParity parity, int extraPairs)
    {
        var b = new Builder();
        b.Bonk();
        b.Step(Direction.East, LabInput.Right);
        b.Step(Direction.South, LabInput.Down);
        for (int i = 0; i < extraPairs; i++) b.Step(Direction.South, LabInput.Down);
        b.Step(Direction.East, LabInput.Right);
        b.Step(Direction.East, LabInput.Right);

        if (extraPairs == 0)
        {
            b.PressFromEast(b.Frame + PressAfterTurnFrames);
        }
        else
        {
            for (int i = 0; i < extraPairs; i++) b.Step(Direction.North, LabInput.Up);
            b.Press(b.Frame + PressAfterStepFrames);
        }

        string name = extraPairs == 0 ? "bonk" : "bonk +" + extraPairs;
        return new LabRoute(name, parity.NeedWalking, parity.LadyDelay, b.Moves);
    }

    public LabRouteState At(int frame)
    {
        int f = frame - LeadFrames;
        LabMove first = Moves[0];
        if (f < 0)
        {
            return new LabRouteState(first.FromX, first.FromY, Direction.North, false,
                new LabMove(LabMoveKind.Stand, Direction.North, first.FromX, first.FromY, first.FromX, first.FromY,
                    -LeadFrames, 0, LabInput.None), false);
        }

        LabMove move = Moves[^1];
        foreach (LabMove m in Moves)
        {
            if (f >= m.Start && f < m.End) { move = m; break; }
        }

        if (move.Kind == LabMoveKind.Press)
        {
            return new LabRouteState(move.ToX, move.ToY, move.Facing, false, move, true);
        }

        if (move.Kind != LabMoveKind.Step)
        {
            return new LabRouteState(move.FromX, move.FromY, move.Facing, false, move, false);
        }

        int into = f - move.Start;
        float t = (float)into / StepFrames;
        return new LabRouteState(move.FromX + (move.ToX - move.FromX) * t, move.FromY + (move.ToY - move.FromY) * t,
            move.Facing, into < StepFrames / 2, move, false);
    }

    private sealed class Builder
    {
        private int _x = StartX, _y = StartY;

        public int Frame { get; private set; }

        public List<LabMove> Moves { get; } = new();

        public void Turn(Direction facing, LabInput input)
        {
            Moves.Add(new LabMove(LabMoveKind.Turn, facing, _x, _y, _x, _y, Frame, Frame + TurnFrames, input));
            Frame += TurnFrames;
        }

        public void Bonk()
        {
            Moves.Add(new LabMove(LabMoveKind.Bonk, Direction.North, _x, _y, _x, _y, Frame, Frame + BonkFrames, LabInput.Up));
            Frame += BonkFrames;
        }

        public void Step(Direction direction, LabInput input)
        {
            int tx = _x, ty = _y;
            Directions.MoveCoords(direction, ref tx, ref ty);
            Moves.Add(new LabMove(LabMoveKind.Step, direction, _x, _y, tx, ty, Frame, Frame + StepFrames, input));
            _x = tx; _y = ty;
            Frame += StepFrames;
        }

        public void PressFromEast(int pressFrame)
        {
            int turnAt = pressFrame - 1;
            if (turnAt > Frame)
            {
                Moves.Add(new LabMove(LabMoveKind.Stand, Direction.East, _x, _y, _x, _y, Frame, turnAt, LabInput.None));
            }
            Moves.Add(new LabMove(LabMoveKind.Turn, Direction.North, _x, _y, _x, _y, turnAt, pressFrame, LabInput.Up));
            Frame = pressFrame;
            Press(pressFrame);
        }

        public void Press(int pressFrame)
        {
            if (pressFrame > Frame)
            {
                Moves.Add(new LabMove(LabMoveKind.Stand, Direction.North, _x, _y, _x, _y, Frame, pressFrame, LabInput.None));
            }
            Moves.Add(new LabMove(LabMoveKind.Press, Direction.North, _x, _y, _x, _y, pressFrame, int.MaxValue, LabInput.A));
            Frame = pressFrame;
        }
    }
}
