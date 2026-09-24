namespace FRLG.StarterTool.App;

internal interface IBeepOutput : IDisposable
{
    bool IsOpen { get; }

    string Description { get; }

    bool NeedsReopen { get; }

    event Action? DeviceChanged;

    bool Write(byte[] pcm);

    double StartDelayMs();

    void Stop();

    int PlayedBytes();

    int CommittedBytes();

    void Silence(int fromByte);
}
