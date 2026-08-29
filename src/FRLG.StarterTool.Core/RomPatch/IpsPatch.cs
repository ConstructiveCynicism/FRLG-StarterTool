using System.Text;

namespace FRLG.StarterTool.Core.RomPatch;

public static class IpsPatch
{
    public static byte[] Apply(byte[] rom, byte[] patch)
    {
        if (patch.Length < 8 || Encoding.ASCII.GetString(patch, 0, 5) != "PATCH")
        {
            throw new InvalidDataException("not an IPS patch");
        }

        var output = new List<byte>(rom);
        int at = 5;

        while (true)
        {
            if (at + 3 > patch.Length) throw new InvalidDataException("the patch ends mid-record");
            if (patch[at] == 'E' && patch[at + 1] == 'O' && patch[at + 2] == 'F') return output.ToArray();

            if (at + 5 > patch.Length) throw new InvalidDataException("the patch ends mid-record");
            int offset = (patch[at] << 16) | (patch[at + 1] << 8) | patch[at + 2];
            int length = (patch[at + 3] << 8) | patch[at + 4];
            at += 5;

            byte[] data;
            if (length == 0)
            {
                if (at + 3 > patch.Length) throw new InvalidDataException("the patch ends mid-run");
                int run = (patch[at] << 8) | patch[at + 1];
                data = Enumerable.Repeat(patch[at + 2], run).ToArray();
                at += 3;
            }
            else
            {
                if (at + length > patch.Length) throw new InvalidDataException("the patch ends mid-record");
                data = patch[at..(at + length)];
                at += length;
            }

            while (output.Count < offset + data.Length) output.Add(0);
            for (int i = 0; i < data.Length; i++) output[offset + i] = data[i];
        }
    }
}
