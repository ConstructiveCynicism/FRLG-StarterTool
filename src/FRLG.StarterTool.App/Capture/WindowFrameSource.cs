using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace FRLG.StarterTool.App.Capture;

internal sealed class WindowFrameSource : IFrameSource
{
    internal readonly record struct Candidate(IntPtr Handle, string Process, string Title)
    {
        public string Id => Process + "|" + Title;
    }

    private static readonly Guid GraphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    private const int PoolBuffers = 4;

    private readonly string _id;
    private readonly object _lock = new();
    private System.Threading.Timer? _retry;
    private IDirect3DDevice? _device;
    private D3D11Readback? _readback;
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _pool;
    private GraphicsCaptureSession? _session;
    private SizeInt32 _poolSize;
    private IntPtr _hwnd;
    private long _lastFrameTicks;
    private bool _active;

    private const long StaleMs = 10000;
    private bool _disposed;

    public WindowFrameSource(string id)
    {
        _id = id;
        int bar = id.IndexOf('|');
        DisplayName = bar >= 0 ? id[(bar + 1)..] : id;
    }

    public string DisplayName { get; }

    public string? Error { get; private set; } = IdleText;

    private const string IdleText = "Ready - opens when a manip run starts";

    public event Action<double, IRawFrame>? FrameArrived;

    public double? FramesPerSecond => null;

    public void Start()
    {
        _retry = new System.Threading.Timer(_ => TryOpen(), null, Timeout.Infinite, 2000);
    }

    public bool Active
    {
        get
        {
            lock (_lock) return _active;
        }
        set
        {
            lock (_lock)
            {
                if (_disposed || _active == value) return;
                _active = value;
                if (value)
                {
                    _retry?.Change(0, 2000);
                }
                else
                {
                    _retry?.Change(Timeout.Infinite, 2000);
                    CloseLocked();
                    Error = IdleText;
                }
            }
        }
    }

    public static List<Candidate> Windows()
    {
        var windows = new List<Candidate>();
        int self = Environment.ProcessId;
        Win32.EnumWindows((hwnd, _) =>
        {
            if (!Win32.IsWindowVisible(hwnd) || Win32.IsCloaked(hwnd)) return true;
            string title = Win32.WindowText(hwnd);
            if (title.Length == 0) return true;
            Win32.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == self) return true;
            windows.Add(new Candidate(hwnd, ProcessName(pid), title));
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private static string ProcessName(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch (Exception)
        {
            return "";
        }
    }

    private IntPtr Find()
    {
        int bar = _id.IndexOf('|');
        string process = bar >= 0 ? _id[..bar] : "";
        string title = bar >= 0 ? _id[(bar + 1)..] : _id;
        List<Candidate> windows = Windows();

        foreach (Candidate window in windows)
        {
            if (window.Process == process && window.Title == title) return window.Handle;
        }
        if (process.Length > 0)
        {
            foreach (Candidate window in windows)
            {
                if (string.Equals(window.Process, process, StringComparison.OrdinalIgnoreCase)) return window.Handle;
            }
        }
        foreach (Candidate window in windows)
        {
            if (window.Title == title) return window.Handle;
        }
        return IntPtr.Zero;
    }

    private void TryOpen()
    {
        lock (_lock)
        {
            if (_disposed || !_active) return;

            if (_session != null)
            {
                bool gone = !Win32.IsWindow(_hwnd) || !Win32.IsWindowVisible(_hwnd);
                bool stale = Environment.TickCount64 - Interlocked.Read(ref _lastFrameTicks) > StaleMs;
                if (!gone && !stale) return;

                CloseLocked();
                if (!gone)
                {
                    _device?.Dispose();
                    _device = null;
                    _readback?.Dispose();
                    _readback = null;
                }
                string why = gone ? "the window went" : "no frames for 10 s";
                StarterTool.Post(() => ContextSession.Log("capture: " + DisplayName + " - " + why + ", reconnecting"));
            }

            IntPtr hwnd = Find();
            if (hwnd == IntPtr.Zero)
            {
                Error = "Window not found";
                return;
            }

            try
            {
                if (!GraphicsCaptureSession.IsSupported())
                {
                    Error = "Window capture is not supported on this Windows";
                    return;
                }

                if (_device == null)
                {
                    _device = D3D11Readback.Create(hwnd, out D3D11Readback readback);
                    _readback = readback;
                    string adapter = readback.Adapter;
                    StarterTool.Post(() => ContextSession.Log("capture: window capture on " + adapter));
                }
                GraphicsCaptureItem item = CreateItem(hwnd);
                _item = item;
                item.Closed += (_, _) => Close(item, "Window closed - looking for it again");
                _poolSize = _item.Size;
                _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    _device, DirectXPixelFormat.B8G8R8A8UIntNormalized, PoolBuffers, _poolSize);
                _pool.FrameArrived += OnFrameArrived;
                _session = _pool.CreateCaptureSession(_item);
                _session.IsCursorCaptureEnabled = false;
                if (global::Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent("Windows.Graphics.Capture.GraphicsCaptureSession", "IsBorderRequired"))
                {
                    try
                    {
                        GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Borderless)
                            .AsTask().GetAwaiter().GetResult();
                        _session.IsBorderRequired = false;
                    }
                    catch (Exception)
                    {
                    }
                }
                _session.StartCapture();
                _hwnd = hwnd;
                Interlocked.Exchange(ref _lastFrameTicks, Environment.TickCount64);
                Error = null;
            }
            catch (Exception e)
            {
                CloseLocked();
                Error = "Window capture failed: " + e.Message;
            }
        }
    }

    private void Close(GraphicsCaptureItem item, string error)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(_item, item)) return;
            CloseLocked();
            Error = error;
        }
    }

    private void CloseLocked()
    {
        if (_pool != null) _pool.FrameArrived -= OnFrameArrived;
        _session?.Dispose();
        _pool?.Dispose();
        _session = null;
        _pool = null;
        _item = null;
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool pool, object args)
    {
        while (pool.TryGetNextFrame() is { } frame)
        {
            using (frame)
            {
                Deliver(frame);
            }
        }
    }

    private void Deliver(Direct3D11CaptureFrame frame)
    {
        D3D11Readback? readback = _readback;
        if (readback == null) return;

        Interlocked.Exchange(ref _lastFrameTicks, Environment.TickCount64);
        double stampMs = Win32.SystemRelativeToMs(frame.SystemRelativeTime);
        SizeInt32 content = frame.ContentSize;

        var raw = new RawWindowFrame(frame, readback, content.Width, content.Height);
        FrameArrived?.Invoke(stampMs, raw);

        if (content.Width != _poolSize.Width || content.Height != _poolSize.Height)
        {
            lock (_lock)
            {
                if (_pool == null || _device == null) return;
                _poolSize = content;
                _pool.Recreate(_device, DirectXPixelFormat.B8G8R8A8UIntNormalized, PoolBuffers, content);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            _retry?.Dispose();
            CloseLocked();
            _device?.Dispose();
            _device = null;
            _readback?.Dispose();
            _readback = null;
            Error = "Closed";
        }
    }

    private sealed class RawWindowFrame : IRawFrame
    {
        private readonly Direct3D11CaptureFrame _frame;
        private readonly D3D11Readback _readback;

        public RawWindowFrame(Direct3D11CaptureFrame frame, D3D11Readback readback, int width, int height)
        {
            _frame = frame;
            _readback = readback;
            Width = width;
            Height = height;
        }

        public int Width { get; }

        public int Height { get; }

        public bool CopyRegion(Rectangle region, bool gamePixels, Func<int, byte[]> rent, Action<byte[], Size> done)
        {
            Rectangle clip = Clip(region, Width, Height);
            return !clip.IsEmpty && _readback.Enqueue(_frame.Surface, clip, gamePixels, rent, done);
        }
    }

    internal static Rectangle Clip(Rectangle region, int width, int height)
    {
        var whole = new Rectangle(0, 0, width, height);
        return region.Width <= 0 || region.Height <= 0 ? whole : Rectangle.Intersect(region, whole);
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);

        IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
    }

    private static GraphicsCaptureItem CreateItem(IntPtr hwnd)
    {
        IObjectReference factory = ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");
        var interop = factory.AsInterface<IGraphicsCaptureItemInterop>();
        Guid iid = GraphicsCaptureItemIid;
        IntPtr pointer = interop.CreateForWindow(hwnd, ref iid);
        try
        {
            return GraphicsCaptureItem.FromAbi(pointer);
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }
}
