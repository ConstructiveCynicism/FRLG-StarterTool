namespace FRLG.StarterTool.Core.Rng;

public class PokemonMethod1 : PokemonRng
{
    public PokemonMethod1(int pid, Gen3Rng rng) : base(pid, rng)
    {
    }

    public PokemonMethod1(Seed seed, int frame)
    {
        Frame = frame;
        Seed = seed.Value;

        var rng1 = new Gen3Rng(seed);
        var rng2 = new Gen3Rng(seed);
        rng1.Advance(frame);
        rng2.Copy(rng1);
        rng1.Advance();

        Pid = ((long)rng1.GetTop() << 16) + rng2.GetTop();
        Nature = new Pokemon.Nature((int)(Pid % 25L));
        Generate(rng1);
    }
}
