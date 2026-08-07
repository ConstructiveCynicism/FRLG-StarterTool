using System.Reflection;

namespace FRLG.StarterTool.App;

public static class BeepSounds
{
    public const string Default = "ping1";

    public const string Tone = "tone";

    public static readonly string[] Names = { "ping1", "ping2", "click1", "clack", Tone };

    public static bool IsKnown(string? name) =>
        name != null && Array.Exists(Names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

    public static string DisplayName(string name) =>
        name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];

    public static short[]? LoadPcm(string name, int sampleRate, int channels)
    {
        Assembly assembly = typeof(BeepSounds).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(
            $"FRLG.StarterTool.App.Resources.beeps.{name}.wav");
        if (stream == null) return null;

        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        return Decode(memory.ToArray(), sampleRate, channels);
    }

    private static short[]? Decode(byte[] wav, int targetRate, int targetChannels)
    {
        if (wav.Length < 12) return null;
        if (BitConverter.ToUInt32(wav, 0) != 0x46464952) return null;
        if (BitConverter.ToUInt32(wav, 8) != 0x45564157) return null;

        int sourceRate = 0, sourceChannels = 0, bitsPerSample = 0;
        int dataOffset = -1, dataLength = 0;

        int pos = 12;
        while (pos + 8 <= wav.Length)
        {
            uint chunkId = BitConverter.ToUInt32(wav, pos);
            int chunkLength = BitConverter.ToInt32(wav, pos + 4);
            int body = pos + 8;
            if (chunkLength < 0 || body + chunkLength > wav.Length) chunkLength = wav.Length - body;

            if (chunkId == 0x20746D66 && chunkLength >= 16)
            {
                if (BitConverter.ToUInt16(wav, body) != 1) return null;
                sourceChannels = BitConverter.ToUInt16(wav, body + 2);
                sourceRate = BitConverter.ToInt32(wav, body + 4);
                bitsPerSample = BitConverter.ToUInt16(wav, body + 14);
            }
            else if (chunkId == 0x61746164)
            {
                dataOffset = body;
                dataLength = chunkLength;
            }

            pos = body + chunkLength + (chunkLength & 1);
        }

        if (dataOffset < 0 || bitsPerSample != 16 || sourceChannels <= 0 || sourceRate <= 0) return null;

        int sourceFrames = dataLength / (sourceChannels * 2);
        if (sourceFrames == 0) return null;

        var mono = new double[sourceFrames];
        for (int frame = 0; frame < sourceFrames; frame++)
        {
            double sum = 0;
            for (int channel = 0; channel < sourceChannels; channel++)
            {
                sum += BitConverter.ToInt16(wav, dataOffset + (frame * sourceChannels + channel) * 2);
            }
            mono[frame] = sum / sourceChannels;
        }

        int targetFrames = sourceRate == targetRate
            ? sourceFrames
            : (int)((long)sourceFrames * targetRate / sourceRate);
        if (targetFrames <= 0) return null;

        var pcm = new short[targetFrames * targetChannels];
        for (int frame = 0; frame < targetFrames; frame++)
        {
            double sample;
            if (sourceRate == targetRate)
            {
                sample = mono[frame];
            }
            else
            {
                double sourcePosition = frame * (double)sourceRate / targetRate;
                int index = (int)sourcePosition;
                double fraction = sourcePosition - index;
                double next = index + 1 < sourceFrames ? mono[index + 1] : mono[index];
                sample = mono[index] + (next - mono[index]) * fraction;
            }

            var value = (short)Math.Clamp(sample, short.MinValue, short.MaxValue);
            for (int channel = 0; channel < targetChannels; channel++)
            {
                pcm[frame * targetChannels + channel] = value;
            }
        }

        return pcm;
    }
}
