namespace FRLG.StarterTool.Core.Npc;

public sealed class OverworldSim : INpcWorld
{
    private readonly List<ObjectEventSim> _objects;

    public OverworldSim(GameMap map, GameRng rng, IEnumerable<ObjectEventSim> objects)
    {
        Map = map;
        Rng = rng;
        _objects = objects.OrderBy(o => o.Slot).ToList();
    }

    public GameMap Map { get; }

    public GameRng Rng { get; }

    public IReadOnlyList<ObjectEventSim> Objects => _objects;

    public AmbientCrySim? AmbientCry { get; init; }

    public bool ControlsLocked
    {
        get => AmbientCry?.ControlsLocked ?? true;
        set { if (AmbientCry != null) AmbientCry.ControlsLocked = value; }
    }

    public int Frame { get; private set; }

    public int Advances => Rng.Advances;

    public int StepFrame(List<NpcEvent>? events = null) => StepFrame(events, -1);

    public int StepFrame(List<NpcEvent>? events, int preVBlankSlot)
    {
        int before = Rng.Advances;

        if (preVBlankSlot >= 0)
        {
            _objects.FirstOrDefault(o => o.Slot == preVBlankSlot)
                ?.Update(Rng, this, Frame, events);
        }

        Rng.VBlank();

        AmbientCry?.Step(Rng);

        foreach (ObjectEventSim o in _objects)
        {
            if (o.Slot == preVBlankSlot) continue;
            o.Update(Rng, this, Frame, events);
        }

        Frame++;
        return Rng.Advances - before;
    }

    public List<NpcEvent> Run(int frames)
    {
        var events = new List<NpcEvent>();
        for (int i = 0; i < frames; i++)
        {
            StepFrame(events);
        }
        return events;
    }

    public void FreezeAll(bool frozen)
    {
        foreach (ObjectEventSim o in _objects)
        {
            o.Frozen = frozen;
        }
        ControlsLocked = frozen;
    }

    public void SetActive(int slot, bool active)
    {
        ObjectEventSim? o = _objects.FirstOrDefault(x => x.Slot == slot);
        if (o == null) return;

        if (active && !o.Active)
        {
            o.Reset();
        }
        o.Active = active;
    }

    public void UpdateSpawns(int playerX, int playerY)
    {
        foreach (ObjectEventSim o in _objects)
        {
            bool inBox = IsInSpawnBox(playerX, playerY, o.InitialX, o.InitialY);

            if (o.Active && !inBox && IsInSpawnBox(playerX, playerY, o.X, o.Y)) continue;

            SetActive(o.Slot, inBox);
        }
    }

    public static bool IsInSpawnBox(int playerX, int playerY, int objectX, int objectY)
    {
        int left = playerX - 2;
        int right = playerX + GameMap.MapOffsetW + 2;
        int top = playerY;
        int bottom = playerY + GameMap.MapOffsetH + 2;

        int npcX = objectX + GameMap.MapOffset;
        int npcY = objectY + GameMap.MapOffset;

        return left <= npcX && npcX <= right && top <= npcY && npcY <= bottom;
    }

    public bool DoesObjectCollideWithObjectAt(ObjectEventSim self, int x, int y)
    {
        foreach (ObjectEventSim other in _objects)
        {
            if (!other.Active || ReferenceEquals(other, self)) continue;

            if ((other.X == x && other.Y == y) || (other.PreviousX == x && other.PreviousY == y))
            {
                if (GameMap.AreElevationsCompatible(self.Elevation, other.Elevation)) return true;
            }
        }
        return false;
    }

    public OverworldSim Clone()
    {
        var objects = new List<ObjectEventSim>(_objects.Count);
        foreach (ObjectEventSim o in _objects)
        {
            var copy = new ObjectEventSim(o.Slot, o.Id, o.MovementType,
                o.InitialX, o.InitialY, o.RangeX, o.RangeY, o.Elevation);
            o.CopyStateTo(copy);
            objects.Add(copy);
        }

        return new OverworldSim(Map, Rng.Clone(), objects)
        {
            Frame = Frame,
            AmbientCry = AmbientCry?.Clone(),
        };
    }
}
