using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace FRLG.StarterTool.App;

internal static class AudioDeviceReport
{
    public static IEnumerable<string> Describe(IntPtr propertyStore, string? deviceId)
    {
        var lines = new List<string>(2);
        try
        {
            lines.Add(DescribeDevice(propertyStore));
        }
        catch (Exception e)
        {
            lines.Add($"audio: device unreadable, {e.GetType().Name}");
        }

        try
        {
            lines.Add(DescribeEffects(propertyStore, deviceId));
        }
        catch (Exception e)
        {
            lines.Add($"audio: effects unreadable, {e.GetType().Name}");
        }

        return lines;
    }

    private static string DescribeDevice(IntPtr propertyStore)
    {
        var parts = new List<string>();
        string? name = ReadString(propertyStore, DeviceFriendlyName);
        parts.Add(name != null ? $"\"{name}\"" : "unnamed");

        if (ReadString(propertyStore, DeviceEnumeratorName) is { } bus)
        {
            string? kind = bus.ToUpperInvariant() switch
            {
                var b when b.StartsWith("BTH", StringComparison.Ordinal) => "Bluetooth",
                "ROOT" or "SWD" => "virtual",
                _ => null
            };
            parts.Add(kind != null ? $"bus {bus} ({kind})" : $"bus {bus}");
        }

        if (ReadUInt(propertyStore, EndpointFormFactor) is { } form && form < FormFactors.Length)
        {
            parts.Add(FormFactors[form]);
        }

        if (ReadString(propertyStore, DriverInfo) is { } driver)
        {
            string[] fields = driver.Split(':');
            parts.Add(fields.Length >= 4 ? $"driver {fields[0]} {fields[2]} {fields[3]}" : $"driver {driver}");
        }

        return "audio: device " + string.Join(", ", parts);
    }

    private static string DescribeEffects(IntPtr propertyStore, string? deviceId)
    {
        bool switchedOff = ReadUInt(propertyStore, EndpointDisableSysFx) == 1;

        var effects = new List<string>();
        string? guid = deviceId is { } id && id.LastIndexOf('{') is var at and >= 0 ? id[at..] : null;
        if (guid != null)
        {
            using RegistryKey? fx = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\" + guid + @"\FxProperties");
            if (fx != null)
            {
                foreach ((int pid, string slot) in FxSlots)
                {
                    object? value = fx.GetValue("{d04e05a6-594b-4fb6-a80d-01af5eed7d1d}," + pid.ToString(CultureInfo.InvariantCulture));
                    IEnumerable<string> clsids = value switch
                    {
                        string one => new[] { one },
                        string[] many => many,
                        _ => Array.Empty<string>()
                    };
                    foreach (string clsid in clsids)
                    {
                        if (!Guid.TryParse(clsid, out Guid parsed) || parsed == Guid.Empty) continue;
                        string entry = slot + " " + ApoName(clsid);
                        if (!effects.Contains(entry)) effects.Add(entry);
                    }
                }
            }
        }

        string list = effects.Count > 0 ? string.Join(", ", effects) : "none registered";
        return "audio: effects on the device: " + list + (switchedOff ? " (enhancements switched off)" : "");
    }

    private static string ApoName(string clsid)
    {
        try
        {
            using RegistryKey? apo = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\AudioEngine\AudioProcessingObjects\" + clsid);
            if (apo?.GetValue("FriendlyName") is string friendly && friendly.Length > 0) return friendly;

            using RegistryKey? com = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\CLSID\" + clsid);
            if (com?.GetValue(null) is string named && named.Length > 0) return named;
        }
        catch (Exception)
        {
        }

        return clsid;
    }

    private static string? ReadString(IntPtr propertyStore, PROPERTYKEY key)
    {
        return Read(propertyStore, key, v => v.vt == VT_LPWSTR && v.data != IntPtr.Zero ? Marshal.PtrToStringUni(v.data) : null);
    }

    private static uint? ReadUInt(IntPtr propertyStore, PROPERTYKEY key)
    {
        return Read<uint?>(propertyStore, key, v => v.vt is VT_UI4 or VT_I4 ? (uint)v.data.ToInt64() : null);
    }

    private static T? Read<T>(IntPtr propertyStore, PROPERTYKEY key, Func<PROPVARIANT, T?> convert)
    {
        var store = (IPropertyStore)Marshal.GetObjectForIUnknown(propertyStore);
        try
        {
            var value = new PROPVARIANT();
            if (store.GetValue(ref key, ref value) < 0) return default;
            try
            {
                return convert(value);
            }
            finally
            {
                PropVariantClear(ref value);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    private const ushort VT_I4 = 3;
    private const ushort VT_UI4 = 19;
    private const ushort VT_LPWSTR = 31;

    private static readonly Guid DeviceProperties = new("a45c254e-df1c-4efd-8020-67d146a850e0");
    private static readonly Guid EndpointProperties = new("1da5d803-d492-4edd-8c23-e0c0ffee7f0e");

    private static readonly PROPERTYKEY DeviceFriendlyName = new() { fmtid = DeviceProperties, pid = 14 };
    private static readonly PROPERTYKEY DeviceEnumeratorName = new() { fmtid = DeviceProperties, pid = 24 };
    private static readonly PROPERTYKEY EndpointFormFactor = new() { fmtid = EndpointProperties, pid = 0 };
    private static readonly PROPERTYKEY EndpointDisableSysFx = new() { fmtid = EndpointProperties, pid = 5 };
    private static readonly PROPERTYKEY DriverInfo = new() { fmtid = new Guid("83da6326-97a6-4088-9453-a1923f573b29"), pid = 3 };

    private static readonly string[] FormFactors =
    {
        "network device", "speakers", "line level", "headphones", "microphone", "headset", "handset",
        "digital passthrough", "S/PDIF", "HDMI/DisplayPort", "unknown form"
    };

    private static readonly (int Pid, string Slot)[] FxSlots =
    {
        (1, "LFX"), (2, "GFX"), (5, "SFX"), (6, "MFX"), (7, "EFX"), (13, "SFX"), (14, "MFX"), (15, "EFX")
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort reserved1;
        public ushort reserved2;
        public ushort reserved3;
        public IntPtr data;
        public IntPtr data2;
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT value);

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PROPERTYKEY key);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
    }
}
