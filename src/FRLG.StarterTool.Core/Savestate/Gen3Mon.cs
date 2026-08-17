using FRLG.StarterTool.Core.Pokemon;

namespace FRLG.StarterTool.Core.Savestate;

public sealed class Gen3Mon
{
    public const int Size = 100;

    private const int BlockOffset = 32;
    private const int BlockSize = 48;
    private const int SubstructSize = 12;

    private static readonly string[] Orders =
    {
        "GAEM", "GAME", "GEAM", "GEMA", "GMAE", "GMEA",
        "AGEM", "AGME", "AEGM", "AEMG", "AMGE", "AMEG",
        "EGAM", "EGMA", "EAGM", "EAMG", "EMGA", "EMAG",
        "MGAE", "MGEA", "MAGE", "MAEG", "MEGA", "MEAG"
    };

    private static readonly int[] ToToolStat = { 0, 1, 2, 5, 3, 4 };

    private readonly byte[] _raw;

    private readonly byte[] _plain = new byte[BlockSize];

    private Gen3Mon(byte[] raw)
    {
        _raw = raw;
    }

    public int Offset { get; private init; }

    public uint Personality { get; private set; }

    public uint OtId { get; private set; }

    public int InternalSpecies { get; private set; }

    public int Species => SpeciesTable.ToNational(InternalSpecies);

    public int Level => _raw[84];

    public Nature Nature => new((int)(Personality % Nature.NatureCount));

    public bool IsShiny => Shiny(Personality, OtId);

    public int[] Ivs
    {
        get
        {
            uint word = ReadU32(_plain, SubstructIndex('M') * SubstructSize + 4);
            var ivs = new int[6];
            for (int game = 0; game < 6; game++)
            {
                ivs[ToToolStat[game]] = (int)((word >> (5 * game)) & 31);
            }
            return ivs;
        }
    }

    public int[] Evs
    {
        get
        {
            int at = SubstructIndex('E') * SubstructSize;
            var evs = new int[6];
            for (int game = 0; game < 6; game++)
            {
                evs[ToToolStat[game]] = _plain[at + game];
            }
            return evs;
        }
    }

    public static Gen3Mon? Read(ReadOnlySpan<byte> memory, int offset)
    {
        if (offset < 0 || offset + Size > memory.Length) return null;

        ReadOnlySpan<byte> slot = memory.Slice(offset, Size);
        uint personality = ReadU32(slot, 0);
        if (personality == 0) return null;

        uint otId = ReadU32(slot, 4);
        ushort checksum = (ushort)(slot[28] | (slot[29] << 8));

        uint key = personality ^ otId;
        Span<byte> plain = stackalloc byte[BlockSize];
        for (int at = 0; at < BlockSize; at += 4)
        {
            WriteU32(plain, at, ReadU32(slot, BlockOffset + at) ^ key);
        }

        int sum = 0;
        for (int half = 0; half < BlockSize; half += 2)
        {
            sum += plain[half] | (plain[half + 1] << 8);
        }
        if ((ushort)sum != checksum) return null;

        string order = Orders[personality % 24];
        Span<byte> canonical = stackalloc byte[BlockSize];
        for (int slotIndex = 0; slotIndex < 4; slotIndex++)
        {
            int target = "GAEM".IndexOf(order[slotIndex]);
            plain.Slice(slotIndex * SubstructSize, SubstructSize)
                .CopyTo(canonical.Slice(target * SubstructSize, SubstructSize));
        }

        int species = canonical[0] | (canonical[1] << 8);
        if (species < 1 || species > SpeciesTable.MaxInternalId) return null;

        var mon = new Gen3Mon(slot.ToArray())
        {
            Offset = offset,
            Personality = personality,
            OtId = otId,
            InternalSpecies = species
        };
        canonical.CopyTo(mon._plain);
        return mon;
    }

    public void SetIvs(IReadOnlyList<int> ivs)
    {
        int at = SubstructIndex('M') * SubstructSize + 4;
        uint word = ReadU32(_plain, at);

        for (int game = 0; game < 6; game++)
        {
            uint value = (uint)Math.Clamp(ivs[ToToolStat[game]], 0, 31);
            word = (word & ~(31u << (5 * game))) | (value << (5 * game));
        }

        WriteU32(_plain, at, word);
    }

    public void SetEvs(IReadOnlyList<int> evs)
    {
        int at = SubstructIndex('E') * SubstructSize;
        for (int game = 0; game < 6; game++)
        {
            _plain[at + game] = (byte)Math.Clamp(evs[ToToolStat[game]], 0, 255);
        }
    }

    public void Repersonalize(Nature nature, Random random)
    {
        if (Personality % Nature.NatureCount == (uint)nature.Id) return;

        uint low = Personality & 0xFF;
        bool shiny = IsShiny;

        for (int attempt = 0; attempt < 100000; attempt++)
        {
            uint candidate = ((uint)random.Next(1 << 24) << 8) | low;
            if (candidate % Nature.NatureCount != (uint)nature.Id) continue;
            if (Shiny(candidate, OtId) != shiny) continue;

            Personality = candidate;
            return;
        }

        for (uint candidate = low; candidate < uint.MaxValue - 256; candidate += 256)
        {
            if (candidate % Nature.NatureCount != (uint)nature.Id) continue;
            Personality = candidate;
            return;
        }
    }

    public void RecalculateStats(int[] baseStats)
    {
        int[] stats = StatCalculator.Calculate(baseStats, Ivs, Level, Nature);

        WriteU16(_raw, 86, stats[0]);
        WriteU16(_raw, 88, stats[0]);
        WriteU16(_raw, 90, stats[1]);
        WriteU16(_raw, 92, stats[2]);
        WriteU16(_raw, 94, stats[5]);
        WriteU16(_raw, 96, stats[3]);
        WriteU16(_raw, 98, stats[4]);
    }

    public void WriteTo(Span<byte> memory)
    {
        WriteU32(_raw, 0, Personality);

        int sum = 0;
        for (int half = 0; half < BlockSize; half += 2)
        {
            sum += _plain[half] | (_plain[half + 1] << 8);
        }
        WriteU16(_raw, 28, (ushort)sum);

        string order = Orders[Personality % 24];
        var permuted = new byte[BlockSize];
        for (int slotIndex = 0; slotIndex < 4; slotIndex++)
        {
            int source = "GAEM".IndexOf(order[slotIndex]);
            Array.Copy(_plain, source * SubstructSize, permuted, slotIndex * SubstructSize, SubstructSize);
        }

        uint key = Personality ^ OtId;
        for (int at = 0; at < BlockSize; at += 4)
        {
            WriteU32(_raw, BlockOffset + at, ReadU32(permuted, at) ^ key);
        }

        _raw.CopyTo(memory.Slice(Offset, Size));
    }

    private static bool Shiny(uint personality, uint otId) =>
        ((otId >> 16) ^ (otId & 0xFFFF) ^ (personality >> 16) ^ (personality & 0xFFFF)) < 8;

    private static int SubstructIndex(char which) => "GAEM".IndexOf(which);

    private static uint ReadU32(ReadOnlySpan<byte> data, int at) =>
        (uint)(data[at] | (data[at + 1] << 8) | (data[at + 2] << 16) | (data[at + 3] << 24));

    private static void WriteU32(Span<byte> data, int at, uint value)
    {
        data[at] = (byte)value;
        data[at + 1] = (byte)(value >> 8);
        data[at + 2] = (byte)(value >> 16);
        data[at + 3] = (byte)(value >> 24);
    }

    private static void WriteU16(Span<byte> data, int at, int value)
    {
        data[at] = (byte)value;
        data[at + 1] = (byte)(value >> 8);
    }
}
