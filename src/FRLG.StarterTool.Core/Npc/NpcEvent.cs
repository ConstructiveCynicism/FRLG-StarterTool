namespace FRLG.StarterTool.Core.Npc;

public enum NpcEventKind
{
    Face,

    Step,
}

public readonly record struct NpcEvent(
    int Frame,
    int Advances,
    int Slot,
    NpcId Npc,
    NpcEventKind Kind,
    Direction Direction,
    bool Blocked,
    bool Silent = false)
{
    public override string ToString()
    {
        string verb = Kind == NpcEventKind.Step ? "steps" : "faces";
        string tail = (Blocked ? " (blocked)" : "") + (Silent ? " (silent)" : "");
        return $"f{Frame} {Npc.ShortName()} {verb} {Directions.Letter(Direction)}{tail}";
    }
}
