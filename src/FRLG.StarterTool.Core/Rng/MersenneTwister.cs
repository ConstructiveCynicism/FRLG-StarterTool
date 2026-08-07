namespace FRLG.StarterTool.Core.Rng;

public class MersenneTwister
{
    private const int N = 624;
    private const int M = 397;
    private const int MatrixA = -1727483681;

    private int[] _mt = new int[N];
    private int _mti;
    private int[] _mag01 = new int[2];

    public MersenneTwister(long seed)
    {
        SetSeed(seed);
    }

    public void SetSeed(long seed)
    {
        _mt = new int[N];
        _mag01 = new int[2];
        _mag01[0] = 0;
        _mag01[1] = MatrixA;

        _mt[0] = (int)seed;
        for (_mti = 1; _mti < N; _mti++)
        {
            unchecked
            {
                _mt[_mti] = 1812433253 * (_mt[_mti - 1] ^ (int)((uint)_mt[_mti - 1] >> 30)) + _mti;
            }
        }
    }

    public int NextInt() => Next(32);

    private int Next(int bits)
    {
        int y;
        if (_mti >= N)
        {
            int[] mt = _mt;
            int[] mag01 = _mag01;

            int kk = 0;
            while (kk < N - M)
            {
                y = (mt[kk] & int.MinValue) | (mt[kk + 1] & int.MaxValue);
                mt[kk] = mt[kk + M] ^ (int)((uint)y >> 1) ^ mag01[y & 1];
                ++kk;
            }
            while (kk < N - 1)
            {
                y = (mt[kk] & int.MinValue) | (mt[kk + 1] & int.MaxValue);
                mt[kk] = mt[kk + (M - N)] ^ (int)((uint)y >> 1) ^ mag01[y & 1];
                ++kk;
            }
            y = (mt[N - 1] & int.MinValue) | (mt[0] & int.MaxValue);
            mt[N - 1] = mt[M - 1] ^ (int)((uint)y >> 1) ^ mag01[y & 1];

            _mti = 0;
        }

        y = _mt[_mti++];
        y ^= (int)((uint)y >> 11);
        y ^= (y << 7) & unchecked((int)0x9D2C5680);
        y ^= (y << 15) & unchecked((int)0xEFC60000);
        y ^= (int)((uint)y >> 18);

        return (int)((uint)y >> (32 - bits));
    }
}
