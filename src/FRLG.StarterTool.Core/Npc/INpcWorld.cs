namespace FRLG.StarterTool.Core.Npc;

public interface INpcWorld
{
    GameMap Map { get; }

    bool DoesObjectCollideWithObjectAt(ObjectEventSim self, int x, int y);
}
