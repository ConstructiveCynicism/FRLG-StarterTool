using FRLG.StarterTool.Core.Rng;

namespace FRLG.StarterTool.Core.Npc;

public sealed class GameRng
{
    private readonly Gen3Rng _rng;

    public GameRng(int seed, bool adapter = false)
    {
        _rng = new Gen3Rng(seed);
        Adapter = adapter;
    }

    private GameRng(Gen3Rng rng, bool adapter)
    {
        _rng = rng;
        Adapter = adapter;
    }

    public bool Adapter { get; }

    public int Advances => _rng.Frame;

    public int Value => _rng.Value;

    public int Random()
    {
        _rng.Advance();
        return _rng.GetTop();
    }

    public void VBlank() => _rng.Advance();

    public void MainLoop()
    {
        if (Adapter) _rng.Advance();
    }

    public void QuietFrame()
    {
        VBlank();
        MainLoop();
    }

    public GameRng Clone() => new(_rng.GetCopy(), Adapter);

    public static GameRng At(int seed, int advances, bool adapter = false)
    {
        var rng = new Gen3Rng(seed);
        if (advances > 0) rng.Advance(advances);
        return new GameRng(rng, adapter);
    }
}
