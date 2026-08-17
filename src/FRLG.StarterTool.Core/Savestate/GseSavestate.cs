namespace FRLG.StarterTool.Core.Savestate;

public sealed class GseSavestate
{
    private static readonly byte[] EwramLabel = "ctx->console.extWorkRam"u8.ToArray();

    private const int EwramSize = 0x40000;

    public const uint EwramBase = 0x02000000;

    private readonly byte[] _file;
    private readonly int _ewramAt;

    private GseSavestate(byte[] file, int ewramAt)
    {
        _file = file;
        _ewramAt = ewramAt;
    }

    public Span<byte> Ewram => _file.AsSpan(_ewramAt, EwramSize);

    public static GseSavestate Load(string path)
    {
        byte[] file = File.ReadAllBytes(path);

        int label = IndexOf(file, EwramLabel);
        if (label < 0)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(path)} holds no GBA work RAM - is it a GSE state from a GBA game?");
        }

        int length = label + EwramLabel.Length + 1;
        if (length + 4 + EwramSize > file.Length
            || file[label + EwramLabel.Length] != 0
            || ReadU32(file, length) != EwramSize)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(path)} is a GSE state this tool does not understand - its work RAM block is not the size it should be.");
        }

        return new GseSavestate(file, length + 4);
    }

    public void Save(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, _file);
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int at = 0; at + needle.Length <= haystack.Length; at++)
        {
            int i = 0;
            while (i < needle.Length && haystack[at + i] == needle[i]) i++;
            if (i == needle.Length) return at;
        }
        return -1;
    }

    private static uint ReadU32(byte[] data, int at) =>
        (uint)(data[at] | (data[at + 1] << 8) | (data[at + 2] << 16) | (data[at + 3] << 24));
}
