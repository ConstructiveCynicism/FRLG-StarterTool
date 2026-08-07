namespace FRLG.StarterTool.Core.Rng;

public class Seed
{
    public Seed()
    {
        Value = 0;
    }

    public Seed(int value)
    {
        Value = value;
    }

    public int Value { get; set; }

    public void Copy(Seed other) => Value = other.Value;

    public int GetTrainerId()
    {
        var random = new MersenneTwister(Value);
        random.NextInt();
        int a = random.NextInt();
        return a & 0xFFFF;
    }

    public int GetSecretId()
    {
        var random = new MersenneTwister(Value);
        random.NextInt();
        int a = random.NextInt();
        return (a >> 16) & 0xFFFF;
    }

    public int GetLotteryId()
    {
        var rng = new Gen3Rng(Value);
        rng.Advance(4);
        return rng.GetTop();
    }

    public int GetHour() => (Value >> 16) & 0xFF;

    public int GetFrame() => Value & 0xFFFF;

    public int PokerusFrame(int first, int last)
    {
        var rng = new Gen3Rng(Value);
        rng.Advance(first);
        for (int i = first; i <= last; i++)
        {
            int top = rng.GetTop();
            if (top == 16384 || top == 32768 || top == 49152)
            {
                return i;
            }
            rng.Advance();
        }
        return -1;
    }
}
