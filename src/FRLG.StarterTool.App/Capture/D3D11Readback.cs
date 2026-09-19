using System.Runtime.InteropServices;
using FRLG.StarterTool.Core.Video;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace FRLG.StarterTool.App.Capture;

internal sealed unsafe class D3D11Readback : IDisposable
{
    public const int Slots = 6;

    private static readonly Guid DxgiInterfaceAccessIid = new("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");
    private static readonly Guid Texture2DIid = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");
    private static readonly Guid Multithread10Iid = new("9B7E4E00-342C-4106-A19F-4F2704F689F0");
    private static readonly Guid DxgiDeviceIid = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
    private static readonly Guid DxgiFactory1Iid = new("770AAE78-F26F-4DBA-A829-253C83D1B387");

    private const int CreateTexture2DSlot = 5;
    private const int MapSlot = 14;
    private const int UnmapSlot = 15;
    private const int CopySubresourceRegionSlot = 46;
    private const int FlushSlot = 111;
    private const int SetMultithreadProtectedSlot = 5;
    private const int GetInterfaceSlot = 3;
    private const int EnumAdapters1Slot = 12;
    private const int AdapterEnumOutputsSlot = 7;
    private const int AdapterGetDescSlot = 8;
    private const int OutputGetDescSlot = 7;

    private const uint DxgiFormatB8G8R8A8Unorm = 87;
    private const uint UsageStaging = 3;
    private const uint CpuAccessRead = 0x20000;
    private const uint MapRead = 1;
    private const uint MapFlagDoNotWait = 0x100000;
    private const int WasStillDrawing = unchecked((int)0x887A000A);
    private const int D3dDriverTypeUnknown = 0;
    private const int D3dDriverTypeHardware = 1;
    private const uint CreateDeviceBgraSupport = 0x20;
    private const uint SdkVersion = 7;

    [StructLayout(LayoutKind.Sequential)]
    private struct Texture2DDesc
    {
        public uint Width, Height, MipLevels, ArraySize, Format, SampleCount, SampleQuality, Usage, BindFlags, CpuAccessFlags, MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Box
    {
        public uint Left, Top, Front, Right, Bottom, Back;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MappedSubresource
    {
        public IntPtr Data;
        public uint RowPitch, DepthPitch;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OutputDesc
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        public int Left, Top, Right, Bottom;
        public int AttachedToDesktop, Rotation;
        public IntPtr Monitor;
    }

    private sealed class Slot
    {
        public IntPtr Texture;
        public Size Size;
        public bool Busy;
    }

    private sealed record Pending(Slot Slot, bool GamePixels, Func<int, byte[]> Rent, Action<byte[], Size> Done);

    private readonly object _lock = new();
    private readonly IntPtr _device;
    private readonly IntPtr _context;
    private readonly List<Slot> _slots = new();
    private readonly Queue<Pending> _pending = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly Thread _worker;
    private long _dropped;
    private bool _disposed;

    private D3D11Readback(IntPtr device, IntPtr context, string adapter)
    {
        _device = device;
        _context = context;
        Adapter = adapter;
        Marshal.AddRef(device);
        Marshal.AddRef(context);

        Guid iid = Multithread10Iid;
        if (Marshal.QueryInterface(context, ref iid, out IntPtr multithread) >= 0)
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, int, int>)VSlot(multithread, SetMultithreadProtectedSlot))(multithread, 1);
            Marshal.Release(multithread);
        }

        _worker = new Thread(Work) { IsBackground = true, Name = "capture readback", Priority = ThreadPriority.BelowNormal };
        _worker.Start();
    }

    public string Adapter { get; }

    public long Dropped => Interlocked.Read(ref _dropped);

    private static IntPtr VSlot(IntPtr com, int index) => (*(IntPtr**)com)[index];

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags,
        IntPtr featureLevels, uint featureLevelCount, uint sdkVersion,
        out IntPtr device, out int featureLevel, out IntPtr immediateContext);

    [DllImport("d3d11.dll")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr factory);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    public static IDirect3DDevice Create(IntPtr hwnd, out D3D11Readback readback)
    {
        IntPtr adapter = AdapterFor(hwnd, out string name);
        IntPtr device, context;
        try
        {
            Marshal.ThrowExceptionForHR(D3D11CreateDevice(adapter,
                adapter != IntPtr.Zero ? D3dDriverTypeUnknown : D3dDriverTypeHardware, IntPtr.Zero,
                CreateDeviceBgraSupport, IntPtr.Zero, 0, SdkVersion, out device, out _, out context));
        }
        finally
        {
            if (adapter != IntPtr.Zero) Marshal.Release(adapter);
        }

        IntPtr dxgi = IntPtr.Zero, inspectable = IntPtr.Zero;
        try
        {
            Guid iid = DxgiDeviceIid;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(device, ref iid, out dxgi));
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out inspectable));
            IDirect3DDevice wrapped = MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
            readback = new D3D11Readback(device, context, name);
            return wrapped;
        }
        finally
        {
            if (inspectable != IntPtr.Zero) Marshal.Release(inspectable);
            if (dxgi != IntPtr.Zero) Marshal.Release(dxgi);
            Marshal.Release(context);
            Marshal.Release(device);
        }
    }

    private static IntPtr AdapterFor(IntPtr hwnd, out string name)
    {
        name = "default adapter";
        IntPtr monitor = MonitorFromWindow(hwnd, 2 );
        Guid factoryIid = DxgiFactory1Iid;
        if (monitor == IntPtr.Zero || CreateDXGIFactory1(ref factoryIid, out IntPtr factory) < 0) return IntPtr.Zero;

        try
        {
            for (uint a = 0; ; a++)
            {
                IntPtr adapter;
                if (((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)VSlot(factory, EnumAdapters1Slot))(factory, a, &adapter) < 0) break;

                bool found = false;
                for (uint o = 0; !found; o++)
                {
                    IntPtr output;
                    if (((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)VSlot(adapter, AdapterEnumOutputsSlot))(adapter, o, &output) < 0) break;
                    IntPtr desc = Marshal.AllocHGlobal(Marshal.SizeOf<OutputDesc>());
                    try
                    {
                        if (((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)VSlot(output, OutputGetDescSlot))(output, desc) >= 0)
                        {
                            found = Marshal.PtrToStructure<OutputDesc>(desc).Monitor == monitor;
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(desc);
                        Marshal.Release(output);
                    }
                }

                if (found)
                {
                    name = AdapterName(adapter);
                    return adapter;
                }
                Marshal.Release(adapter);
            }
            return IntPtr.Zero;
        }
        finally
        {
            Marshal.Release(factory);
        }
    }

    private static string AdapterName(IntPtr adapter)
    {
        IntPtr desc = Marshal.AllocHGlobal(512);
        try
        {
            return ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)VSlot(adapter, AdapterGetDescSlot))(adapter, desc) >= 0
                ? Marshal.PtrToStringUni(desc) ?? "adapter"
                : "adapter";
        }
        finally
        {
            Marshal.FreeHGlobal(desc);
        }
    }

    public bool Enqueue(IDirect3DSurface surface, Rectangle region, bool gamePixels, Func<int, byte[]> rent, Action<byte[], Size> done)
    {
        IntPtr texture = TextureOf(surface);
        try
        {
            lock (_lock)
            {
                if (_disposed) return false;
                Slot? slot = FreeSlot(region.Size);
                if (slot == null)
                {
                    _dropped++;
                    return false;
                }

                var box = new Box
                {
                    Left = (uint)region.Left, Top = (uint)region.Top, Front = 0,
                    Right = (uint)region.Right, Bottom = (uint)region.Bottom, Back = 1,
                };
                ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, uint, uint, uint, IntPtr, uint, Box*, void>)
                    VSlot(_context, CopySubresourceRegionSlot))(_context, slot.Texture, 0, 0, 0, 0, texture, 0, &box);
                ((delegate* unmanaged[Stdcall]<IntPtr, void>)VSlot(_context, FlushSlot))(_context);

                slot.Busy = true;
                _pending.Enqueue(new Pending(slot, gamePixels, rent, done));
            }
            _signal.Release();
            return true;
        }
        finally
        {
            Marshal.Release(texture);
        }
    }

    private Slot? FreeSlot(Size size)
    {
        Slot? free = _slots.FirstOrDefault(slot => !slot.Busy && slot.Size == size);
        if (free != null) return free;

        if (_slots.Count(slot => slot.Size == size) >= Slots) return null;
        foreach (Slot idle in _slots.Where(slot => !slot.Busy && slot.Size != size).ToList())
        {
            Marshal.Release(idle.Texture);
            _slots.Remove(idle);
        }

        var desc = new Texture2DDesc
        {
            Width = (uint)size.Width, Height = (uint)size.Height, MipLevels = 1, ArraySize = 1,
            Format = DxgiFormatB8G8R8A8Unorm, SampleCount = 1, SampleQuality = 0,
            Usage = UsageStaging, BindFlags = 0, CpuAccessFlags = CpuAccessRead, MiscFlags = 0,
        };
        IntPtr texture;
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<IntPtr, Texture2DDesc*, IntPtr, IntPtr*, int>)
            VSlot(_device, CreateTexture2DSlot))(_device, &desc, IntPtr.Zero, &texture));
        var slot = new Slot { Texture = texture, Size = size };
        _slots.Add(slot);
        return slot;
    }

    private void Work()
    {
        while (true)
        {
            _signal.Wait();
            Pending? next;
            lock (_lock)
            {
                if (_disposed) return;
                if (!_pending.TryPeek(out next)) continue;
            }

            MappedSubresource mapped;
            while (true)
            {
                int hr;
                lock (_lock)
                {
                    if (_disposed) return;
                    hr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, uint, uint, MappedSubresource*, int>)
                        VSlot(_context, MapSlot))(_context, next.Slot.Texture, 0, MapRead, MapFlagDoNotWait, &mapped);
                }
                if (hr != WasStillDrawing)
                {
                    if (hr < 0) mapped.Data = IntPtr.Zero;
                    break;
                }
                Thread.Sleep(1);
            }

            byte[]? bytes = null;
            Size size = next.Slot.Size;
            IntPtr texture = next.Slot.Texture;
            try
            {
                if (mapped.Data != IntPtr.Zero && next.GamePixels)
                {
                    var source = new ReadOnlySpan<byte>((void*)mapped.Data, checked((int)mapped.RowPitch * size.Height));
                    bytes = next.Rent(GamePixels.Bytes);
                    GamePixels.Sample(source, (int)mapped.RowPitch, 0, 0, size.Width, size.Height, bytes);
                    size = new Size(GamePixels.Width, GamePixels.Height);
                }
                else if (mapped.Data != IntPtr.Zero)
                {
                    int stride = size.Width * 4;
                    bytes = next.Rent(stride * size.Height);
                    fixed (byte* target = bytes)
                    {
                        for (int y = 0; y < size.Height; y++)
                        {
                            Buffer.MemoryCopy((byte*)mapped.Data + (long)y * mapped.RowPitch, target + (long)y * stride, stride, stride);
                        }
                    }
                }
            }
            finally
            {
                lock (_lock)
                {
                    if (mapped.Data != IntPtr.Zero)
                    {
                        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, void>)VSlot(_context, UnmapSlot))(_context, texture, 0);
                    }
                    next.Slot.Busy = false;
                    _pending.Dequeue();
                }
            }

            if (bytes != null)
            {
                try
                {
                    next.Done(bytes, size);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    private static IntPtr TextureOf(IDirect3DSurface surface)
    {
        IntPtr abi = MarshalInterface<IDirect3DSurface>.FromManaged(surface);
        try
        {
            Guid accessIid = DxgiInterfaceAccessIid;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(abi, ref accessIid, out IntPtr access));
            try
            {
                Guid textureIid = Texture2DIid;
                IntPtr texture;
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)
                    VSlot(access, GetInterfaceSlot))(access, &textureIid, &texture));
                return texture;
            }
            finally
            {
                Marshal.Release(access);
            }
        }
        finally
        {
            Marshal.Release(abi);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _signal.Release();
        _worker.Join(500);

        lock (_lock)
        {
            foreach (Slot slot in _slots) Marshal.Release(slot.Texture);
            _slots.Clear();
            _pending.Clear();
            Marshal.Release(_context);
            Marshal.Release(_device);
        }
        _signal.Dispose();
    }
}
