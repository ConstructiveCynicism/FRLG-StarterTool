namespace FRLG.StarterTool.Core.Savestate;

public sealed class PartyScan
{
    public PartyScan(int offset, IReadOnlyList<Gen3Mon> party)
    {
        Offset = offset;
        Party = party;
    }

    public int Offset { get; }

    public IReadOnlyList<Gen3Mon> Party { get; }

    public bool Found => Offset >= 0;

    public uint Address => GseSavestate.EwramBase + (uint)Offset;
}

public static class PartyLocator
{
    public const int PartySize = 6;

    private const int PartyBytes = PartySize * Gen3Mon.Size;

    private const int CountOffset = -603;

    public static PartyScan Find(ReadOnlySpan<byte> ewram)
    {
        int best = -1;
        int bestScore = int.MinValue;

        for (int offset = PartyBytes; offset + Gen3Mon.Size <= ewram.Length; offset += 4)
        {
            if (Gen3Mon.Read(ewram, offset) == null) continue;

            if (Gen3Mon.Read(ewram, offset - PartyBytes) == null) continue;

            int run = 1;
            while (run < PartySize && Gen3Mon.Read(ewram, offset + run * Gen3Mon.Size) != null) run++;

            int counted = offset + CountOffset >= 0 ? ewram[offset + CountOffset] : 0;
            int score = (counted == run ? 100 : 0) + run;

            if (score > bestScore)
            {
                bestScore = score;
                best = offset;
            }
        }

        if (best < 0) return new PartyScan(-1, Array.Empty<Gen3Mon>());

        var party = new List<Gen3Mon>(PartySize);
        for (int slot = 0; slot < PartySize; slot++)
        {
            Gen3Mon? mon = Gen3Mon.Read(ewram, best + slot * Gen3Mon.Size);
            if (mon == null) break;
            party.Add(mon);
        }

        return new PartyScan(best, party);
    }
}
