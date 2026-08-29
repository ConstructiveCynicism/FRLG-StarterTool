using System.Drawing.Imaging;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using FRLG.StarterTool.Core.Settings;

namespace FRLG.StarterTool.App;

public sealed class StatServer : IDisposable
{
    private const int MaxConnections = 8;

    private const int HeaderLimitBytes = 8 * 1024;

    private const int SocketTimeoutMs = 5000;

    private const int KeepAliveMs = 15000;

    private readonly object _pulse = new();

    private readonly System.Threading.Timer _postRunExpiry;

    private readonly SemaphoreSlim _workers = new(MaxConnections, MaxConnections);

    private TcpListener? _listener;
    private Thread? _acceptThread;
    private volatile bool _running;

    private volatile Snapshot _state = Snapshot.Empty;

    private string _prefix = "/";

    private volatile bool _transparent;

    private volatile int _stripSide;

    private volatile bool _postRunEnabled = true;

    private volatile int _postRunMs =
        AppSettings.DefaultStatServerPostRunSeconds * 1000;

    public string? Url { get; private set; }

    public string? LastError { get; private set; }

    public bool Running => _running;

    public StatServer()
    {
        _postRunExpiry = new System.Threading.Timer(
            _ => ClearPostRun(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start(AppSettings settings)
    {
        Stop();
        LastError = null;
        if (settings == null || !settings.StatServerEnabled) return;

        if (settings.StatServerRequireToken && string.IsNullOrEmpty(settings.StatServerToken))
        {
            settings.StatServerToken = NewToken();
        }

        _prefix = settings.StatServerRequireToken ? "/" + settings.StatServerToken + "/" : "/";
        _transparent = settings.StatServerTransparent;
        _stripSide = (int)settings.StatServerStripSide;
        _postRunEnabled = settings.StatServerPostRun;
        _postRunMs = Math.Clamp(
            settings.StatServerPostRunSeconds,
            AppSettings.MinStatServerPostRunSeconds,
            AppSettings.MaxStatServerPostRunSeconds) * 1000;

        int port = settings.StatServerPort;
        if (port is < 1 or > 65535)
        {
            LastError = "Port " + port.ToString(CultureInfo.InvariantCulture) + " is not a port.";
            return;
        }

        IPAddress address = settings.StatServerAllowNetwork ? IPAddress.Any : IPAddress.Loopback;
        try
        {
            _listener = new TcpListener(address, port);
            _listener.Start();
        }
        catch (SocketException error)
        {
            _listener = null;
            LastError = error.SocketErrorCode == SocketError.AddressAlreadyInUse
                ? "Port " + port.ToString(CultureInfo.InvariantCulture) + " is already in use."
                : error.Message;
            return;
        }
        catch (Exception error)
        {
            _listener = null;
            LastError = error.Message;
            return;
        }

        _running = true;
        Url = "http://"
              + (settings.StatServerAllowNetwork ? LocalAddress() : "127.0.0.1")
              + ":" + port.ToString(CultureInfo.InvariantCulture)
              + _prefix;

        _acceptThread = new Thread(AcceptLoop)
        {
            IsBackground = true,
            Name = "StatServer"
        };
        _acceptThread.Start();
    }

    public void Stop()
    {
        _running = false;
        Url = null;

        try
        {
            _listener?.Stop();
        }
        catch (Exception)
        {
        }
        _listener = null;

        lock (_pulse) Monitor.PulseAll(_pulse);

        _acceptThread?.Join(500);
        _acceptThread = null;
    }

    public void Dispose()
    {
        Stop();
        _postRunExpiry.Dispose();
    }

    public void Publish(in StatBoxContent ivs, in StatBoxContent stats)
    {
        var palette = StatBoxPalette.Current;
        Snapshot current = _state;
        if (current.Ivs.Equals(ivs) && current.Stats.Equals(stats) && current.Palette == palette) return;

        _state = new Snapshot(ivs, stats, palette, current.Version + 1) { Card = current.Card };
        lock (_pulse) Monitor.PulseAll(_pulse);
    }

    public void PublishPostRun(in PostRunCard card)
    {
        if (!_running || !_postRunEnabled || !card.Any) return;

        Snapshot current = _state;
        _state = current with { Card = card, Version = current.Version + 1 };
        lock (_pulse) Monitor.PulseAll(_pulse);

        try
        {
            _postRunExpiry.Change(_postRunMs, Timeout.Infinite);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void ClearPostRun()
    {
        Snapshot current = _state;
        if (current.Card == null) return;

        _state = current with { Card = null, Version = current.Version + 1 };
        lock (_pulse) Monitor.PulseAll(_pulse);
    }

    public static string NewToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();

    private static string LocalAddress()
    {
        try
        {
            foreach (IPAddress address in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(address))
                {
                    return address.ToString();
                }
            }
        }
        catch (Exception)
        {
        }

        return "<this-pc>";
    }

    private void AcceptLoop()
    {
        while (_running)
        {
            TcpClient client;
            try
            {
                client = _listener!.AcceptTcpClient();
            }
            catch (Exception)
            {
                return;
            }

            if (!_workers.Wait(0))
            {
                client.Close();
                continue;
            }

            var worker = new Thread(() =>
            {
                try
                {
                    Serve(client);
                }
                catch (Exception)
                {
                }
                finally
                {
                    _workers.Release();
                    client.Dispose();
                }
            })
            {
                IsBackground = true,
                Name = "StatServerConnection"
            };
            worker.Start();
        }
    }

    private void Serve(TcpClient client)
    {
        client.ReceiveTimeout = SocketTimeoutMs;
        client.SendTimeout = SocketTimeoutMs;
        client.NoDelay = true;

        using NetworkStream stream = client.GetStream();

        string? head = ReadHead(stream);
        if (head == null)
        {
            Write(stream, 400, "text/plain", Encoding.UTF8.GetBytes("Bad Request"));
            return;
        }

        string requestLine = head.Split('\n')[0].TrimEnd('\r');
        string[] parts = requestLine.Split(' ');
        if (parts.Length < 2)
        {
            Write(stream, 400, "text/plain", Encoding.UTF8.GetBytes("Bad Request"));
            return;
        }

        if (!string.Equals(parts[0], "GET", StringComparison.Ordinal))
        {
            Write(stream, 405, "text/plain", Encoding.UTF8.GetBytes("Method Not Allowed"));
            return;
        }

        string target = parts[1];
        int mark = target.IndexOf('?');
        string path = mark < 0 ? target : target[..mark];
        string query = mark < 0 ? "" : target[(mark + 1)..];

        Snapshot state = _state;
        if (path == _prefix || path == _prefix.TrimEnd('/') + "/index.html")
        {
            string page = StatServerPage.Build(_prefix, ScaleOf(query), LayoutOf(query), BoxOf(query));
            Write(stream, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(page));
        }
        else if (path == _prefix + "ivs.png")
        {
            Write(stream, 200, "image/png",
                Png(state.Ivs, ScaleOf(query), Served(state.Palette), (StatStripSide)_stripSide));
        }
        else if (path == _prefix + "stats.png")
        {
            Write(stream, 200, "image/png",
                Png(state.Stats, ScaleOf(query), Served(state.Palette), (StatStripSide)_stripSide));
        }
        else if (path == _prefix + "post.png")
        {
            Write(stream, 200, "image/png",
                PostRunPng(
                    state.Card ?? default,
                    ScaleOf(query),
                    BoxOf(query) == StatServerPage.BoxBoth,
                    Served(state.Palette),
                    (StatStripSide)_stripSide));
        }
        else if (path == _prefix + "events")
        {
            ServeEvents(client, stream);
        }
        else
        {
            Write(stream, 404, "text/plain", Encoding.UTF8.GetBytes("Not Found"));
        }
    }

    private void ServeEvents(TcpClient client, NetworkStream stream)
    {
        client.ReceiveTimeout = 0;

        var head = new StringBuilder();
        head.Append("HTTP/1.1 200 OK\r\n");
        head.Append("Content-Type: text/event-stream\r\n");
        head.Append("Cache-Control: no-store\r\n");
        head.Append("Connection: close\r\n");
        head.Append("Access-Control-Allow-Origin: *\r\n");
        head.Append("\r\n");
        byte[] bytes = Encoding.UTF8.GetBytes(head.ToString());
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();

        long sent = -1;
        while (_running)
        {
            Snapshot state = _state;
            long version = state.Version;
            if (version != sent)
            {
                sent = version;

                byte[] message = Encoding.UTF8.GetBytes(
                    "data: " + version.ToString(CultureInfo.InvariantCulture)
                             + (state.Card == null ? ":0" : ":1") + "\n\n");
                stream.Write(message, 0, message.Length);
                stream.Flush();
                continue;
            }

            lock (_pulse)
            {
                if (_state.Version == sent && _running) Monitor.Wait(_pulse, KeepAliveMs);
            }

            if (_state.Version != sent || !_running) continue;

            byte[] keepalive = Encoding.UTF8.GetBytes(": keepalive\n\n");
            stream.Write(keepalive, 0, keepalive.Length);
            stream.Flush();
        }
    }

    private StatBoxPalette Served(in StatBoxPalette palette) =>
        _transparent ? palette with { Fill = Color.Transparent } : palette;

    private static byte[] Png(
        in StatBoxContent content, int scale, in StatBoxPalette palette, StatStripSide side)
    {
        using Bitmap bitmap = StatBoxPanel.Render(content, scale, palette, side);
        using var buffer = new MemoryStream();
        bitmap.Save(buffer, ImageFormat.Png);
        return buffer.ToArray();
    }

    private static byte[] PostRunPng(
        in PostRunCard card, int scale, bool pair, in StatBoxPalette palette, StatStripSide side)
    {
        using Bitmap bitmap = PostRunCard.Render(card, scale, pair, palette, side);
        using var buffer = new MemoryStream();
        bitmap.Save(buffer, ImageFormat.Png);
        return buffer.ToArray();
    }

    private static string? ReadHead(NetworkStream stream)
    {
        var head = new MemoryStream();
        byte[] buffer = new byte[1024];

        while (head.Length < HeaderLimitBytes)
        {
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0) return null;

            head.Write(buffer, 0, read);
            string text = Encoding.UTF8.GetString(head.GetBuffer(), 0, (int)head.Length);
            if (text.Contains("\r\n\r\n", StringComparison.Ordinal)
                || text.Contains("\n\n", StringComparison.Ordinal))
            {
                return text;
            }
        }

        return null;
    }

    private static void Write(NetworkStream stream, int status, string contentType, byte[] body)
    {
        var head = new StringBuilder();
        head.Append("HTTP/1.1 ").Append(status.ToString(CultureInfo.InvariantCulture)).Append(' ')
            .Append(Reason(status)).Append("\r\n");
        head.Append("Content-Type: ").Append(contentType).Append("\r\n");
        head.Append("Content-Length: ")
            .Append(body.Length.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        head.Append("Cache-Control: no-store\r\n");
        head.Append("Access-Control-Allow-Origin: *\r\n");
        head.Append("Connection: close\r\n");
        head.Append("\r\n");

        byte[] bytes = Encoding.UTF8.GetBytes(head.ToString());
        stream.Write(bytes, 0, bytes.Length);
        stream.Write(body, 0, body.Length);
        stream.Flush();
    }

    private static string Reason(int status) => status switch
    {
        200 => "OK",
        400 => "Bad Request",
        404 => "Not Found",
        405 => "Method Not Allowed",
        _ => "Error"
    };

    private static int ScaleOf(string query)
    {
        string? value = QueryValue(query, "scale");
        if (value == null
            || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int scale))
        {
            return DefaultScale;
        }

        return Math.Clamp(scale, StatBoxPanel.MinRenderScale, StatBoxPanel.MaxRenderScale);
    }

    private const int DefaultScale = 4;

    private static string LayoutOf(string query) =>
        QueryValue(query, "layout") == StatServerPage.LayoutColumn
            ? StatServerPage.LayoutColumn
            : StatServerPage.LayoutRow;

    private static string BoxOf(string query) => QueryValue(query, "box") switch
    {
        StatServerPage.BoxBoth => StatServerPage.BoxBoth,
        StatServerPage.BoxStats => StatServerPage.BoxStats,
        _ => StatServerPage.BoxIvs
    };

    private static string? QueryValue(string query, string key)
    {
        foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int mark = pair.IndexOf('=');
            if (mark > 0 && pair[..mark] == key) return pair[(mark + 1)..];
        }

        return null;
    }

    private sealed record Snapshot(
        StatBoxContent Ivs, StatBoxContent Stats, StatBoxPalette Palette, long Version)
    {
        public PostRunCard? Card { get; init; }

        public static readonly Snapshot Empty = new(
            new StatBoxContent(new int[6], "", "FRAME", "00000", ""),
            new StatBoxContent(new int[6], "", "LEVEL", "6", ""),
            StatBoxPalette.Current,
            0);
    }
}
