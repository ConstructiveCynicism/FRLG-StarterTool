using FRLG.StarterTool.Core.Rng;

namespace FRLG.StarterTool.Core.Npc;

public sealed class GameRng
{
    private readonly Gen3Rng _rng;

    public GameRng(int seed) => _rng = new Gen3Rng(seed);

    private GameRng(Gen3Rng rng) => _rng = rng;

    public int Advances => _rng.Frame;

    public int Value => _rng.Value;

    public int Random()
    {
        _rng.Advance();
        return _rng.GetTop();
    }

    public void VBlank() => _rng.Advance();

    public GameRng Clone() => new(_rng.GetCopy());

    public static GameRng At(int seed, int advances)
    {
        var rng = new Gen3Rng(seed);
        if (advances > 0) rng.Advance(advances);
        return new GameRng(rng);
    }
}
