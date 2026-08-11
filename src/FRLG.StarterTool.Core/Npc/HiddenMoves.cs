namespace FRLG.StarterTool.Core.Npc;

public readonly record struct HiddenMoves(NpcId Npc, int OffScreen, int Bonks, int SilentTurns)
{
    public bool Known { get; init; } = true;

    public static HiddenMoves Unknown(NpcId npc) => new(npc, 0, 0, 0) { Known = false };

    public bool Partial { get; init; }

    public int Total => OffScreen + Bonks + SilentTurns;

    public static HiddenMoves Count(NpcId npc, IEnumerable<NpcEvent> events,
        Func<NpcEvent, bool> onScreen)
    {
        int off = 0, bonks = 0, silent = 0;

        foreach (NpcEvent e in events)
        {
            if (!onScreen(e)) off++;
            else if (!e.Silent) continue;
            else if (e.Blocked) bonks++;
            else silent++;
        }

        return new HiddenMoves(npc, off, bonks, silent);
    }

    public HiddenMoves Plus(HiddenMoves other) => new(
        Npc, OffScreen + other.OffScreen, Bonks + other.Bonks, SilentTurns + other.SilentTurns)
    {
        Known = Known && other.Known,
        Partial = Partial || other.Partial,
    };

    public override string ToString() => Known
        ? $"{Npc.ShortName()} {OffScreen} off screen, {Bonks} bonks, {SilentTurns} silent"
            + (Partial ? " (partial)" : "")
        : $"{Npc.ShortName()} unknown";
}
