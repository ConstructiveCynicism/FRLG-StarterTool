using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App.Capture;

internal interface IRawFrame
{
    int Width { get; }

    int Height { get; }

    bool CopyRegion(Rectangle region, bool gamePixels, Func<int, byte[]> rent, Action<byte[], Size> done);
}

internal interface IFrameSource : IDisposable
{
    string DisplayName { get; }

    string? Error { get; }

    event Action<double, IRawFrame>? FrameArrived;

    void Start();

    bool Active { get; set; }

    double? FramesPerSecond { get; }
}

internal sealed record CaptureSourceInfo(VideoSourceKind Kind, string Id, string Label)
{
    public override string ToString() => Label;

    public static List<CaptureSourceInfo> List()
    {
        var sources = new List<CaptureSourceInfo>();
        foreach (WindowFrameSource.Candidate window in WindowFrameSource.Windows())
        {
            sources.Add(new CaptureSourceInfo(VideoSourceKind.Window, window.Id, "Window: " + window.Title));
        }
        foreach (string device in DeviceFrameSource.Devices())
        {
            sources.Add(new CaptureSourceInfo(VideoSourceKind.Device, device, "Device: " + device));
        }
        foreach (string camera in DirectShowFilterSource.Filters())
        {
            if (sources.Any(source => source.Kind == VideoSourceKind.Device && source.Id == camera)) continue;
            sources.Add(new CaptureSourceInfo(VideoSourceKind.Device, camera, "Device: " + camera));
        }
        return sources;
    }

    public static IFrameSource? Open(VideoSourceKind kind, string id) => kind switch
    {
        VideoSourceKind.Window when id.Length > 0 => new WindowFrameSource(id),
        VideoSourceKind.Device when id.Length > 0 => DirectShowFilterSource.Filters().Contains(id)
            ? new DirectShowFilterSource(id)
            : new DeviceFrameSource(id),
        _ => null,
    };
}
