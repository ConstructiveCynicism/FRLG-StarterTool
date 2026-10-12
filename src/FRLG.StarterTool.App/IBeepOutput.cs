namespace FRLG.StarterTool.App;

internal interface IBeepOutput : IDisposable
{
    bool IsOpen { get; }

    string Description { get; }

    bool KeepAwake { get => false; set { } }

    double ClockRatio => 1.0;

    bool NeedsReopen { get; }

    event Action? DeviceChanged;

    bool Write(byte[] pcm, int length, int skippableBytes = 0);

    double StartDelayMs();

    void Stop();

    int PlayedBytes();

    int CommittedBytes();

    void Silence(int fromByte, int byteCount);
}
