using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using FRLG.StarterTool.Core.Video;

namespace FRLG.StarterTool.App.Capture;

internal sealed class DirectShowFilterSource : IFrameSource
{
    private const int GapWindow = 300;

    private readonly string _name;
    private readonly Queue<double> _gaps = new();
    private readonly object _lock = new();
    private object? _graph;
    private IMediaControl? _control;
    private SampleGrabberCallback? _grabber;
    private System.Threading.Timer? _watch;
    private long _lastBufferTicks;
    private int _opening;
    private bool _disposed;

    private const long StaleMs = 5000;

    public DirectShowFilterSource(string name)
    {
        _name = name;
        DisplayName = name;
    }

    public string DisplayName { get; }

    public string? Error { get; private set; } = "Not started";

    public event Action<double, IRawFrame>? FrameArrived;

    public bool Active { get; set; }

    public double? FramesPerSecond { get; private set; }

    public static List<string> Filters()
    {
        var names = new List<string>();
        try
        {
            foreach ((string name, bool device, IMoniker moniker) in Monikers())
            {
                if (!device && !names.Contains(name)) names.Add(name);
                Marshal.ReleaseComObject(moniker);
            }
        }
        catch (Exception)
        {
        }
        return names;
    }

    public void Start()
    {
        Interlocked.Exchange(ref _lastBufferTicks, Environment.TickCount64);
        _watch = new System.Threading.Timer(_ => Watch(), null, 0, 2000);
    }

    private void Watch()
    {
        if (_disposed || Interlocked.CompareExchange(ref _opening, 1, 0) != 0) return;
        try
        {
            bool quiet = Environment.TickCount64 - Interlocked.Read(ref _lastBufferTicks) > StaleMs;
            object? graph;
            lock (_lock) graph = _graph;
            if (graph != null && !quiet) return;
            if (graph != null)
            {
                Teardown();
                Error = "No frames - reconnecting";
                StarterTool.Post(() => ContextSession.Log("capture: " + DisplayName + " - no frames for 5 s, reconnecting"));
            }
            Interlocked.Exchange(ref _lastBufferTicks, Environment.TickCount64);
            Open();
        }
        finally
        {
            Interlocked.Exchange(ref _opening, 0);
        }
    }

    private void Open()
    {
        IMoniker? moniker = null;
        try
        {
            foreach ((string name, bool device, IMoniker found) in Monikers())
            {
                if (moniker == null && !device && name == _name) moniker = found;
                else Marshal.ReleaseComObject(found);
            }
            if (moniker == null)
            {
                Error = "Camera not found";
                return;
            }

            Guid baseFilter = Iid.BaseFilter;
            moniker.BindToObject(null!, null!, ref baseFilter, out object bound);
            var source = (IBaseFilter)bound;
            var graph = (IGraphBuilder)Activator.CreateInstance(Type.GetTypeFromCLSID(Clsid.FilterGraph)!)!;
            var builder = (ICaptureGraphBuilder2)Activator.CreateInstance(Type.GetTypeFromCLSID(Clsid.CaptureGraphBuilder2)!)!;
            var grabberFilter = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(Clsid.SampleGrabber)!)!;
            var renderer = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(Clsid.NullRenderer)!)!;
            Marshal.ThrowExceptionForHR(builder.SetFiltergraph(graph));
            Marshal.ThrowExceptionForHR(graph.AddFilter(source, "Source"));
            Marshal.ThrowExceptionForHR(graph.AddFilter(grabberFilter, "Grabber"));
            Marshal.ThrowExceptionForHR(graph.AddFilter(renderer, "Renderer"));

            Guid subtype = PickFormat(builder, source);

            var grabber = (ISampleGrabber)grabberFilter;
            IntPtr wanted = Marshal.AllocCoTaskMem(Marshal.SizeOf<AmMediaType>());
            try
            {
                Marshal.StructureToPtr(new AmMediaType { MajorType = MediaType.Video, SubType = subtype }, wanted, false);
                Marshal.ThrowExceptionForHR(grabber.SetMediaType(wanted));
            }
            finally
            {
                Marshal.FreeCoTaskMem(wanted);
            }

            int hr;
            using (var capture = new GuidPtr(PinCategory.Capture))
            using (var video = new GuidPtr(MediaType.Video))
            {
                hr = builder.RenderStream(capture.Pointer, video.Pointer, source, grabberFilter, renderer);
                if (hr < 0) hr = builder.RenderStream(IntPtr.Zero, video.Pointer, source, grabberFilter, renderer);
            }
            Marshal.ThrowExceptionForHR(hr);

            Marshal.ThrowExceptionForHR(((IMediaFilter)graph).SetSyncSource(null));

            Format format = Connected(grabber);
            FramesPerSecond = format.Fps > 0 ? format.Fps : null;
            var callback = new SampleGrabberCallback((time, buffer, length) => OnBuffer(time, buffer, length, format));
            Marshal.ThrowExceptionForHR(grabber.SetBufferSamples(false));
            Marshal.ThrowExceptionForHR(grabber.SetOneShot(false));
            Marshal.ThrowExceptionForHR(grabber.SetCallback(callback, 1));

            lock (_lock)
            {
                if (_disposed) return;
                _graph = graph;
                _grabber = callback;
                _control = (IMediaControl)graph;
            }
            Marshal.ThrowExceptionForHR(_control.Run());
            Error = null;
        }
        catch (Exception e)
        {
            Error = "Camera capture failed: " + e.Message;
        }
        finally
        {
            if (moniker != null) Marshal.ReleaseComObject(moniker);
        }
    }

    private void OnBuffer(double sampleTime, IntPtr buffer, int length, Format format)
    {
        Interlocked.Exchange(ref _lastBufferTicks, Environment.TickCount64);
        double arrivedMs = Win32.GetTime();
        double bufferMs = sampleTime * 1000.0;
        _gaps.Enqueue(arrivedMs - bufferMs);
        while (_gaps.Count > GapWindow) _gaps.Dequeue();
        double stampMs = bufferMs + _gaps.Min();

        FrameArrived?.Invoke(stampMs, new RawFilterFrame(buffer, length, format));
    }

    public void Dispose()
    {
        _disposed = true;
        _watch?.Dispose();
        Teardown();
        Error = "Closed";
    }

    private void Teardown()
    {
        IMediaControl? control;
        object? graph;
        lock (_lock)
        {
            control = _control;
            graph = _graph;
            _control = null;
            _graph = null;
            _grabber = null;
        }
        if (graph == null) return;

        Task.Run(() =>
        {
            try
            {
                control?.Stop();
                Marshal.FinalReleaseComObject(graph);
            }
            catch (Exception)
            {
            }
        });
    }

    internal sealed record Format(Guid SubType, int Width, int Height, bool BottomUp, int BitsPerPixel, double Fps);

    private static readonly Guid[] Preference = { MediaType.Nv12, MediaType.Yuy2, MediaType.I420, MediaType.Iyuv, MediaType.Rgb32, MediaType.Rgb24 };

    private static Guid PickFormat(ICaptureGraphBuilder2 builder, IBaseFilter source)
    {
        Guid streamConfig = Iid.AmStreamConfig;
        object? config = null;
        using (var capture = new GuidPtr(PinCategory.Capture))
        using (var video = new GuidPtr(MediaType.Video))
        {
            if (builder.FindInterface(capture.Pointer, video.Pointer, source, ref streamConfig, out config) < 0)
            {
                builder.FindInterface(IntPtr.Zero, video.Pointer, source, ref streamConfig, out config);
            }
        }
        if (config is not IAmStreamConfig stream) return MediaType.Nv12;

        try
        {
            Marshal.ThrowExceptionForHR(stream.GetNumberOfCapabilities(out int count, out int size));
            IntPtr caps = Marshal.AllocCoTaskMem(Math.Max(size, 128));
            IntPtr best = IntPtr.Zero;
            (double Fps, long Pixels, int Rank) bestKey = (-1, -1, int.MaxValue);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    if (stream.GetStreamCaps(i, out IntPtr pmt, caps) < 0 || pmt == IntPtr.Zero) continue;
                    var mt = Marshal.PtrToStructure<AmMediaType>(pmt);
                    int rank = Array.IndexOf(Preference, mt.SubType);
                    VideoInfo? info = VideoInfo.Read(mt);
                    if (rank < 0 || info == null)
                    {
                        FreeMediaType(pmt);
                        continue;
                    }

                    (double, long, int) key = (Math.Round(info.Value.Fps), (long)info.Value.Width * Math.Abs(info.Value.Height), rank);
                    bool better = key.Item1 > bestKey.Fps
                        || (key.Item1 == bestKey.Fps && (key.Item2 > bestKey.Pixels || (key.Item2 == bestKey.Pixels && key.Item3 < bestKey.Rank)));
                    if (better)
                    {
                        if (best != IntPtr.Zero) FreeMediaType(best);
                        best = pmt;
                        bestKey = key;
                    }
                    else
                    {
                        FreeMediaType(pmt);
                    }
                }

                if (best == IntPtr.Zero) return MediaType.Nv12;
                stream.SetFormat(best);
                return Marshal.PtrToStructure<AmMediaType>(best).SubType;
            }
            finally
            {
                if (best != IntPtr.Zero) FreeMediaType(best);
                Marshal.FreeCoTaskMem(caps);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(stream);
        }
    }

    private static Format Connected(ISampleGrabber grabber)
    {
        IntPtr pmt = Marshal.AllocCoTaskMem(Marshal.SizeOf<AmMediaType>());
        try
        {
            Marshal.ThrowExceptionForHR(grabber.GetConnectedMediaType(pmt));
            var mt = Marshal.PtrToStructure<AmMediaType>(pmt);
            VideoInfo info = VideoInfo.Read(mt) ?? throw new InvalidOperationException("the camera's format has no picture size");
            bool rgb = mt.SubType == MediaType.Rgb32 || mt.SubType == MediaType.Rgb24;
            var format = new Format(mt.SubType, info.Width, Math.Abs(info.Height), rgb && info.Height > 0, info.BitCount, info.Fps);
            if (mt.FormatSize > 0) Marshal.FreeCoTaskMem(mt.Format);
            if (mt.Unknown != IntPtr.Zero) Marshal.Release(mt.Unknown);
            return format;
        }
        finally
        {
            Marshal.FreeCoTaskMem(pmt);
        }
    }

    private static void FreeMediaType(IntPtr pmt)
    {
        var mt = Marshal.PtrToStructure<AmMediaType>(pmt);
        if (mt.FormatSize > 0 && mt.Format != IntPtr.Zero) Marshal.FreeCoTaskMem(mt.Format);
        if (mt.Unknown != IntPtr.Zero) Marshal.Release(mt.Unknown);
        Marshal.FreeCoTaskMem(pmt);
    }

    private readonly record struct VideoInfo(int Width, int Height, int BitCount, double Fps)
    {
        public static VideoInfo? Read(AmMediaType mt)
        {
            int header;
            if (mt.FormatType == FormatType.VideoInfo && mt.FormatSize >= 88) header = 48;
            else if (mt.FormatType == FormatType.VideoInfo2 && mt.FormatSize >= 112) header = 72;
            else return null;

            long frameTime = Marshal.ReadInt64(mt.Format, 40);
            int width = Marshal.ReadInt32(mt.Format, header + 4);
            int height = Marshal.ReadInt32(mt.Format, header + 8);
            int bits = Marshal.ReadInt16(mt.Format, header + 14);
            if (width <= 0 || height == 0) return null;
            return new VideoInfo(width, height, bits, frameTime > 0 ? 1e7 / frameTime : 0);
        }
    }

    private sealed unsafe class RawFilterFrame : IRawFrame
    {
        private readonly byte* _data;
        private readonly int _length;
        private readonly Format _format;

        public RawFilterFrame(IntPtr data, int length, Format format)
        {
            _data = (byte*)data;
            _length = length;
            _format = format;
        }

        public int Width => _format.Width;

        public int Height => _format.Height;

        public bool CopyRegion(Rectangle region, bool gamePixels, Func<int, byte[]> rent, Action<byte[], Size> done)
        {
            Rectangle clip = WindowFrameSource.Clip(region, Width, Height);
            if (clip.IsEmpty || !Fits()) return false;

            Size size = gamePixels ? new Size(GamePixels.Width, GamePixels.Height) : clip.Size;
            var columns = new int[size.Width];
            for (int tx = 0; tx < size.Width; tx++) columns[tx] = clip.X + (gamePixels ? GamePixels.SourceX(tx, clip.Width) : tx);

            byte[] bytes = rent(size.Width * size.Height * 4);
            Kind kind = KindOf(_format.SubType);
            fixed (byte* target = bytes)
            {
                for (int ty = 0; ty < size.Height; ty++)
                {
                    int y = clip.Y + (gamePixels ? GamePixels.SourceY(ty, clip.Height) : ty);
                    Row(kind, y, columns, target + (long)ty * size.Width * 4);
                }
            }
            done(bytes, size);
            return true;
        }

        private enum Kind { Nv12, Yuy2, I420, Rgb32, Rgb24 }

        private static Kind KindOf(Guid subtype) =>
            subtype == MediaType.Yuy2 ? Kind.Yuy2
            : subtype == MediaType.I420 || subtype == MediaType.Iyuv ? Kind.I420
            : subtype == MediaType.Rgb32 ? Kind.Rgb32
            : subtype == MediaType.Rgb24 ? Kind.Rgb24
            : Kind.Nv12;

        private bool Fits()
        {
            long pixels = (long)Width * Height;
            long needed = KindOf(_format.SubType) switch
            {
                Kind.Yuy2 => pixels * 2,
                Kind.Rgb32 => pixels * 4,
                Kind.Rgb24 => (long)((Width * 3 + 3) & ~3) * Height,
                _ => pixels * 3 / 2,
            };
            return _length >= needed;
        }

        private void Row(Kind kind, int y, int[] columns, byte* o)
        {
            int w = Width, h = Height;
            switch (kind)
            {
                case Kind.Rgb32:
                case Kind.Rgb24:
                {
                    int bytesPerPixel = kind == Kind.Rgb32 ? 4 : 3;
                    byte* row = _data + (long)(_format.BottomUp ? h - 1 - y : y) * ((w * bytesPerPixel + 3) & ~3);
                    foreach (int x in columns)
                    {
                        byte* p = row + x * bytesPerPixel;
                        o[0] = p[0];
                        o[1] = p[1];
                        o[2] = p[2];
                        o[3] = 255;
                        o += 4;
                    }
                    return;
                }
                case Kind.Yuy2:
                {
                    byte* row = _data + (long)y * w * 2;
                    foreach (int x in columns)
                    {
                        byte* p = row + (x & ~1) * 2;
                        Yuv((x & 1) == 0 ? p[0] : p[2], p[1], p[3], o);
                        o += 4;
                    }
                    return;
                }
                case Kind.Nv12:
                {
                    byte* luma = _data + (long)y * w;
                    byte* chroma = _data + (long)w * h + (long)(y / 2) * w;
                    foreach (int x in columns)
                    {
                        byte* uv = chroma + (x & ~1);
                        Yuv(luma[x], uv[0], uv[1], o);
                        o += 4;
                    }
                    return;
                }
                default:
                {
                    byte* luma = _data + (long)y * w;
                    byte* us = _data + (long)w * h + (long)(y / 2) * (w / 2);
                    byte* vs = us + (long)(w / 2) * (h / 2);
                    foreach (int x in columns)
                    {
                        Yuv(luma[x], us[x / 2], vs[x / 2], o);
                        o += 4;
                    }
                    return;
                }
            }
        }

        private static void Yuv(int luma, int u, int v, byte* o)
        {
            int c = 298 * (luma - 16) + 128, d = u - 128, e = v - 128;
            o[0] = Clamp((c + 541 * d) >> 8);
            o[1] = Clamp((c - 55 * d - 136 * e) >> 8);
            o[2] = Clamp((c + 459 * e) >> 8);
            o[3] = 255;
        }

        private static byte Clamp(int value) => (byte)(value < 0 ? 0 : value > 255 ? 255 : value);
    }

    private static IEnumerable<(string Name, bool Device, IMoniker Moniker)> Monikers()
    {
        var enumerator = (ICreateDevEnum)Activator.CreateInstance(Type.GetTypeFromCLSID(Clsid.SystemDeviceEnum)!)!;
        try
        {
            Guid category = Clsid.VideoInputDeviceCategory;
            if (enumerator.CreateClassEnumerator(ref category, out IEnumMoniker? monikers, 0) != 0 || monikers == null) yield break;
            try
            {
                var one = new IMoniker[1];
                while (monikers.Next(1, one, IntPtr.Zero) == 0)
                {
                    IMoniker moniker = one[0];
                    string? name = null;
                    bool device = false;
                    Guid bagIid = Iid.PropertyBag;
                    moniker.BindToStorage(null!, null!, ref bagIid, out object bagObject);
                    if (bagObject is IPropertyBag bag)
                    {
                        object? value = null, path = null;
                        if (bag.Read("FriendlyName", ref value, IntPtr.Zero) >= 0) name = (value as string)?.Trim();
                        device = bag.Read("DevicePath", ref path, IntPtr.Zero) >= 0 && path is string text && text.Trim().Length > 0;
                        Marshal.ReleaseComObject(bag);
                    }
                    if (string.IsNullOrEmpty(name))
                    {
                        Marshal.ReleaseComObject(moniker);
                        continue;
                    }
                    yield return (name, device, moniker);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(monikers);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    private sealed class GuidPtr : IDisposable
    {
        public GuidPtr(Guid guid)
        {
            Pointer = Marshal.AllocCoTaskMem(16);
            Marshal.StructureToPtr(guid, Pointer, false);
        }

        public IntPtr Pointer { get; }

        public void Dispose() => Marshal.FreeCoTaskMem(Pointer);
    }

    private static class Clsid
    {
        public static readonly Guid SystemDeviceEnum = new("62BE5D10-60EB-11D0-BD3B-00A0C911CE86");
        public static readonly Guid VideoInputDeviceCategory = new("860BB310-5D01-11D0-BD3B-00A0C911CE86");
        public static readonly Guid FilterGraph = new("E436EBB3-524F-11CE-9F53-0020AF0BA770");
        public static readonly Guid CaptureGraphBuilder2 = new("BF87B6E1-8C27-11D0-B3F0-00AA003761C5");
        public static readonly Guid SampleGrabber = new("C1F400A0-3F08-11D3-9F0B-006008039E37");
        public static readonly Guid NullRenderer = new("C1F400A4-3F08-11D3-9F0B-006008039E37");
    }

    private static class Iid
    {
        public static readonly Guid BaseFilter = new("56A86895-0AD4-11CE-B03A-0020AF0BA770");
        public static readonly Guid PropertyBag = new("55272A00-42CB-11CE-8135-00AA004BB851");
        public static readonly Guid AmStreamConfig = new("C6E13340-30AC-11D0-A18C-00A0C9118956");
    }

    private static class PinCategory
    {
        public static readonly Guid Capture = new("FB6C4281-0353-11D1-905F-0000C0CC16BA");
    }

    private static class MediaType
    {
        public static readonly Guid Video = new("73646976-0000-0010-8000-00AA00389B71");
        public static readonly Guid Nv12 = new("3231564E-0000-0010-8000-00AA00389B71");
        public static readonly Guid Yuy2 = new("32595559-0000-0010-8000-00AA00389B71");
        public static readonly Guid I420 = new("30323449-0000-0010-8000-00AA00389B71");
        public static readonly Guid Iyuv = new("56555949-0000-0010-8000-00AA00389B71");
        public static readonly Guid Rgb32 = new("E436EB7E-524F-11CE-9F53-0020AF0BA770");
        public static readonly Guid Rgb24 = new("E436EB7D-524F-11CE-9F53-0020AF0BA770");
    }

    private static class FormatType
    {
        public static readonly Guid VideoInfo = new("05589F80-C356-11CE-BF01-00AA0055595A");
        public static readonly Guid VideoInfo2 = new("F72A76A0-EB0A-11D0-ACE4-0000C0CC16BA");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AmMediaType
    {
        public Guid MajorType;
        public Guid SubType;
        public int FixedSizeSamples;
        public int TemporalCompression;
        public int SampleSize;
        public Guid FormatType;
        public IntPtr Unknown;
        public int FormatSize;
        public IntPtr Format;
    }

    [ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICreateDevEnum
    {
        [PreserveSig]
        int CreateClassEnumerator([In] ref Guid category, out IEnumMoniker? enumerator, int flags);
    }

    [ComImport, Guid("55272A00-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyBag
    {
        [PreserveSig]
        int Read([MarshalAs(UnmanagedType.LPWStr)] string name, ref object? value, IntPtr errorLog);
    }

    [ComImport, Guid("56A86895-0AD4-11CE-B03A-0020AF0BA770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IBaseFilter
    {
    }

    [ComImport, Guid("56A868A9-0AD4-11CE-B03A-0020AF0BA770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphBuilder
    {
        [PreserveSig]
        int AddFilter(IBaseFilter filter, [MarshalAs(UnmanagedType.LPWStr)] string name);
    }

    [ComImport, Guid("93E5A4E0-2D50-11D2-ABFA-00A0C9C6E38D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICaptureGraphBuilder2
    {
        [PreserveSig]
        int SetFiltergraph(IGraphBuilder graph);

        [PreserveSig]
        int GetFiltergraph([MarshalAs(UnmanagedType.IUnknown)] out object graph);

        [PreserveSig]
        int SetOutputFileName(IntPtr type, IntPtr file, IntPtr multiplexer, IntPtr sink);

        [PreserveSig]
        int FindInterface(IntPtr category, IntPtr type, IBaseFilter filter,
            ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object? found);

        [PreserveSig]
        int RenderStream(IntPtr category, IntPtr type, [MarshalAs(UnmanagedType.IUnknown)] object source,
            IBaseFilter? compressor, IBaseFilter? renderer);
    }

    [ComImport, Guid("C6E13340-30AC-11D0-A18C-00A0C9118956"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAmStreamConfig
    {
        [PreserveSig]
        int SetFormat(IntPtr mediaType);

        [PreserveSig]
        int GetFormat(out IntPtr mediaType);

        [PreserveSig]
        int GetNumberOfCapabilities(out int count, out int size);

        [PreserveSig]
        int GetStreamCaps(int index, out IntPtr mediaType, IntPtr caps);
    }

    [ComImport, Guid("6B652FFF-11FE-4FCE-92AD-0266B5D7C78F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISampleGrabber
    {
        [PreserveSig]
        int SetOneShot([MarshalAs(UnmanagedType.Bool)] bool oneShot);

        [PreserveSig]
        int SetMediaType(IntPtr mediaType);

        [PreserveSig]
        int GetConnectedMediaType(IntPtr mediaType);

        [PreserveSig]
        int SetBufferSamples([MarshalAs(UnmanagedType.Bool)] bool buffer);

        [PreserveSig]
        int GetCurrentBuffer(ref int size, IntPtr buffer);

        [PreserveSig]
        int GetCurrentSample(out IntPtr sample);

        [PreserveSig]
        int SetCallback(ISampleGrabberCB callback, int which);
    }

    [ComImport, Guid("56A86899-0AD4-11CE-B03A-0020AF0BA770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMediaFilter
    {
        [PreserveSig]
        int GetClassID(out Guid classId);

        [PreserveSig]
        int Stop();

        [PreserveSig]
        int Pause();

        [PreserveSig]
        int Run(long start);

        [PreserveSig]
        int GetState(int timeout, out int state);

        [PreserveSig]
        int SetSyncSource([MarshalAs(UnmanagedType.IUnknown)] object? clock);
    }

    [ComImport, Guid("56A868B1-0AD4-11CE-B03A-0020AF0BA770"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    private interface IMediaControl
    {
        [PreserveSig]
        int Run();

        [PreserveSig]
        int Pause();

        [PreserveSig]
        int Stop();
    }
}

[ComImport, Guid("0579154A-2B53-4994-B0D0-E773148EFF85"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ISampleGrabberCB
{
    [PreserveSig]
    int SampleCB(double sampleTime, IntPtr sample);

    [PreserveSig]
    int BufferCB(double sampleTime, IntPtr buffer, int length);
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class SampleGrabberCallback : ISampleGrabberCB
{
    private readonly Action<double, IntPtr, int> _buffer;

    public SampleGrabberCallback(Action<double, IntPtr, int> buffer) => _buffer = buffer;

    public int SampleCB(double sampleTime, IntPtr sample) => 0;

    public int BufferCB(double sampleTime, IntPtr buffer, int length)
    {
        try
        {
            _buffer(sampleTime, buffer, length);
        }
        catch (Exception)
        {
        }
        return 0;
    }
}
