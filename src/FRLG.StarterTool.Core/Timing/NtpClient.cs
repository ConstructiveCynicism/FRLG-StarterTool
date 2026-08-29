using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace FRLG.StarterTool.Core.Timing;

public readonly record struct NtpSample(double LocalSeconds, double AtomicSeconds, double RoundTripSeconds);

public static class NtpClient
{
    public static readonly string[] DefaultHosts =
    [
        "time.google.com",
        "time.cloudflare.com",
        "pool.ntp.org",
        "time.windows.com"
    ];

    private const double NtpToUnix = 2208988800.0;

    private static readonly double TicksPerSecond = Stopwatch.Frequency;

    public static NtpSample Query(string host, int timeoutMs = 2000)
    {
        IPAddress[] addresses = Dns.GetHostAddresses(host);
        IPAddress? address = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
        if (address == null) throw new SocketException((int)SocketError.HostNotFound);

        var packet = new byte[48];
        packet[0] = 0x23;

        using var socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        socket.ReceiveTimeout = timeoutMs;
        socket.SendTimeout = timeoutMs;
        socket.Connect(new IPEndPoint(address, 123));

        long t1 = Stopwatch.GetTimestamp();
        socket.Send(packet);
        var reply = new byte[48];
        int read = socket.Receive(reply);
        long t4 = Stopwatch.GetTimestamp();
        if (read < 48) throw new InvalidDataException("Short NTP reply.");
        if ((reply[0] & 0x07) != 4) throw new InvalidDataException("Not an NTP server reply.");
        if (reply[1] == 0) throw new InvalidDataException("Kiss-of-death / unsynchronised server.");

        double t2 = ReadTimestamp(reply, 32);
        double t3 = ReadTimestamp(reply, 40);
        if (t2 <= 0 || t3 <= 0) throw new InvalidDataException("Zero NTP timestamps.");

        double localSpan = (t4 - t1) / TicksPerSecond;
        double roundTrip = Math.Max(0, localSpan - (t3 - t2));

        return new NtpSample((t1 + t4) / 2.0 / TicksPerSecond, (t2 + t3) / 2.0 - NtpToUnix, roundTrip);
    }

    public static NtpSample? Best(int attempts = 4, IEnumerable<string>? hosts = null, int timeoutMs = 2000)
    {
        NtpSample? best = null;
        foreach (string host in hosts ?? DefaultHosts)
        {
            int failures = 0;
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    NtpSample sample = Query(host, timeoutMs);
                    if (best == null || sample.RoundTripSeconds < best.Value.RoundTripSeconds) best = sample;
                }
                catch (Exception)
                {
                    failures++;
                }
            }

            if (failures < attempts) break;
        }

        return best;
    }

    private static double ReadTimestamp(byte[] buffer, int offset)
    {
        ulong seconds = ((ulong)buffer[offset] << 24) | ((ulong)buffer[offset + 1] << 16)
            | ((ulong)buffer[offset + 2] << 8) | buffer[offset + 3];
        ulong fraction = ((ulong)buffer[offset + 4] << 24) | ((ulong)buffer[offset + 5] << 16)
            | ((ulong)buffer[offset + 6] << 8) | buffer[offset + 7];
        return seconds + fraction / 4294967296.0;
    }
}
