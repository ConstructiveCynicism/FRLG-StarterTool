using System.Drawing.Imaging;
using FlashCap;
using FRLG.StarterTool.Core.Video;

namespace FRLG.StarterTool.App.Capture;

internal sealed class DeviceFrameSource : IFrameSource
{
    private const int GapWindow = 300;

    private readonly string _name;
    private readonly Queue<double> _gaps = new();
    private CaptureDevice? _device;
    private System.Threading.Timer? _watch;
    private long _lastFrameTicks;
    private int _opening;

    private const long StaleMs = 5000;
    private Size _size;
    private double? _fps;
    private bool _disposed;

    public DeviceFrameSource(string name)
    {
        _name = name;
        DisplayName = name;
    }

    public string DisplayName { get; }

    public string? Error { get; private set; } = "Not started";

    public event Action<double, IRawFrame>? FrameArrived;

    public bool Active { get; set; }

    public double? FramesPerSecond => _fps;

    public static List<string> Devices()
    {
        try
        {
            return new CaptureDevices().EnumerateDescriptors()
                .Where(descriptor => descriptor.DeviceType == DeviceTypes.DirectShow)
                .Select(descriptor => descriptor.Name)
                .Distinct()
                .ToList();
        }
        catch (Exception)
        {
            return new List<string>();
        }
    }

    public void Start()
    {
        Interlocked.Exchange(ref _lastFrameTicks, Environment.TickCount64);
        _watch = new System.Threading.Timer(_ => Watch(), null, 0, 2000);
    }

    private void Watch()
    {
        if (_disposed || Interlocked.CompareExchange(ref _opening, 1, 0) != 0) return;
        try
        {
            bool quiet = Environment.TickCount64 - Interlocked.Read(ref _lastFrameTicks) > StaleMs;
            CaptureDevice? device = _device;
            if (device != null && !quiet) return;
            if (device != null)
            {
                _device = null;
                Close(device);
                Error = "No frames - reconnecting";
                StarterTool.Post(() => ContextSession.Log("capture: " + DisplayName + " - no frames for 5 s, reconnecting"));
            }
            Interlocked.Exchange(ref _lastFrameTicks, Environment.TickCount64);
            OpenAsync().GetAwaiter().GetResult();
        }
        finally
        {
            Interlocked.Exchange(ref _opening, 0);
        }
    }

    private async Task OpenAsync()
    {
        try
        {
            CaptureDeviceDescriptor? descriptor = new CaptureDevices().EnumerateDescriptors()
                .FirstOrDefault(d => d.DeviceType == DeviceTypes.DirectShow && d.Name == _name);
            if (descriptor == null)
            {
                Error = "Device not found";
                return;
            }

            VideoCharacteristics? format = descriptor.Characteristics
                .Where(c => c.PixelFormat != PixelFormats.Unknown)
                .OrderByDescending(c => Math.Round((double)c.FramesPerSecond.Numerator / Math.Max(1, c.FramesPerSecond.Denominator)))
                .ThenByDescending(c => c.Width * c.Height)
                .ThenBy(c => c.IsCompression ? 1 : 0)
                .FirstOrDefault();
            if (format == null)
            {
                Error = "Device offers no format this can read";
                return;
            }

            CaptureDevice device = await descriptor.OpenAsync(
                format, TranscodeFormats.Auto, false, 4, OnPixelBuffer);
            if (_disposed)
            {
                await device.DisposeAsync();
                return;
            }
            _size = new Size(format.Width, format.Height);
            _fps = (double)format.FramesPerSecond.Numerator / Math.Max(1, format.FramesPerSecond.Denominator);
            _device = device;
            await device.StartAsync();
            Error = null;
        }
        catch (Exception e)
        {
            Error = "Device capture failed: " + e.Message;
        }
    }

    private void OnPixelBuffer(PixelBufferScope scope)
    {
        Interlocked.Exchange(ref _lastFrameTicks, Environment.TickCount64);
        double arrivedMs = Win32.GetTime();
        double bufferMs = scope.Buffer.Timestamp.TotalMilliseconds;

        _gaps.Enqueue(arrivedMs - bufferMs);
        while (_gaps.Count > GapWindow) _gaps.Dequeue();
        double stampMs = bufferMs + _gaps.Min();

        FrameArrived?.Invoke(stampMs, new RawDeviceFrame(scope.Buffer, _size));
    }

    public void Dispose()
    {
        _disposed = true;
        _watch?.Dispose();
        CaptureDevice? device = _device;
        _device = null;
        if (device != null) Close(device);
        Error = "Closed";
    }

    private static void Close(CaptureDevice device)
    {
        Task.Run(async () =>
        {
            try
            {
                await device.StopAsync();
                await device.DisposeAsync();
            }
            catch (Exception)
            {
            }
        });
    }

    private sealed class RawDeviceFrame : IRawFrame
    {
        private readonly PixelBuffer _buffer;
        private ArraySegment<byte> _image;
        private int _pixels;
        private int _bitsPerPixel;
        private bool _topDown;
        private bool _jpeg;

        public RawDeviceFrame(PixelBuffer buffer, Size size)
        {
            _buffer = buffer;
            Width = size.Width;
            Height = size.Height;
        }

        public int Width { get; private set; }

        public int Height { get; private set; }

        private void Refer()
        {
            _image = _buffer.ReferImage();
            byte[] b = _image.Array!;
            int o = _image.Offset;
            if (_image.Count > 54 && b[o] == (byte)'B' && b[o + 1] == (byte)'M')
            {
                _pixels = BitConverter.ToInt32(b, o + 10);
                Width = BitConverter.ToInt32(b, o + 18);
                int height = BitConverter.ToInt32(b, o + 22);
                _topDown = height < 0;
                Height = Math.Abs(height);
                _bitsPerPixel = BitConverter.ToInt16(b, o + 28);
            }
            else if (_image.Count > 2 && b[o] == 0xFF && b[o + 1] == 0xD8)
            {
                _jpeg = true;
            }
        }

        public bool CopyRegion(Rectangle region, bool gamePixels, Func<int, byte[]> rent, Action<byte[], Size> done)
        {
            if (_image.Array == null) Refer();
            byte[]? bytes = _jpeg ? CopyJpeg(region, gamePixels, rent, out Size size) : CopyBitmap(region, gamePixels, rent, out size);
            if (bytes == null) return false;
            done(bytes, size);
            return true;
        }

        private byte[]? CopyBitmap(Rectangle region, bool gamePixels, Func<int, byte[]> rent, out Size size)
        {
            size = Size.Empty;
            if (_bitsPerPixel is not (24 or 32) || Width <= 0 || Height <= 0) return null;

            Rectangle clip = WindowFrameSource.Clip(region, Width, Height);
            if (clip.IsEmpty) return null;

            byte[] b = _image.Array!;
            int bytesPerPixel = _bitsPerPixel / 8;
            int sourceStride = (Width * bytesPerPixel + 3) & ~3;
            int start = _image.Offset + _pixels;

            if (gamePixels)
            {
                byte[] sampled = rent(GamePixels.Bytes);
                int s = 0;
                for (int gy = 0; gy < GamePixels.Height; gy++)
                {
                    int y = clip.Y + GamePixels.SourceY(gy, clip.Height);
                    int row = start + (_topDown ? y : Height - 1 - y) * sourceStride;
                    for (int gx = 0; gx < GamePixels.Width; gx++)
                    {
                        int i = row + (clip.X + GamePixels.SourceX(gx, clip.Width)) * bytesPerPixel;
                        sampled[s++] = b[i];
                        sampled[s++] = b[i + 1];
                        sampled[s++] = b[i + 2];
                        sampled[s++] = 255;
                    }
                }
                size = new Size(GamePixels.Width, GamePixels.Height);
                return sampled;
            }
            byte[] bytes = rent(clip.Width * clip.Height * 4);
            int o = 0;
            for (int y = 0; y < clip.Height; y++)
            {
                int row = _topDown ? clip.Y + y : Height - 1 - (clip.Y + y);
                int i = start + row * sourceStride + clip.X * bytesPerPixel;
                for (int x = 0; x < clip.Width; x++, i += bytesPerPixel)
                {
                    bytes[o++] = b[i];
                    bytes[o++] = b[i + 1];
                    bytes[o++] = b[i + 2];
                    bytes[o++] = 255;
                }
            }
            size = clip.Size;
            return bytes;
        }

        private unsafe byte[]? CopyJpeg(Rectangle region, bool gamePixels, Func<int, byte[]> rent, out Size size)
        {
            size = Size.Empty;
            using var stream = new MemoryStream(_image.Array!, _image.Offset, _image.Count, writable: false);
            using var bitmap = new Bitmap(stream);
            Width = bitmap.Width;
            Height = bitmap.Height;
            Rectangle clip = WindowFrameSource.Clip(region, Width, Height);
            if (clip.IsEmpty) return null;

            BitmapData data = bitmap.LockBits(clip, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
            try
            {
                if (gamePixels)
                {
                    byte[] sampled = rent(GamePixels.Bytes);
                    var source = new ReadOnlySpan<byte>((void*)data.Scan0, data.Stride * clip.Height);
                    GamePixels.Sample(source, data.Stride, 0, 0, clip.Width, clip.Height, sampled);
                    size = new Size(GamePixels.Width, GamePixels.Height);
                    return sampled;
                }

                byte[] bytes = rent(clip.Width * clip.Height * 4);
                for (int y = 0; y < clip.Height; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(
                        data.Scan0 + y * data.Stride, bytes, y * clip.Width * 4, clip.Width * 4);
                }
                for (int i = 3; i < bytes.Length; i += 4) bytes[i] = 255;
                size = clip.Size;
                return bytes;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }
}
