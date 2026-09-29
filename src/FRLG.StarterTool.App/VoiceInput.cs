using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Voice;
using Vosk;

namespace FRLG.StarterTool.App;

internal static class VoiceInput
{
    private const int PreRollMs = 300;
    private const int TailMs = 250;
    private const int PollMs = 10;

    private const int StableMs = 120;
    private const int BytesPerMs = AudioDevices.Capture.SampleRate * 2 / 1000;

    public static bool Enabled => _enabled;

    private static volatile bool _enabled;

    private static volatile bool _held;

    private static Thread? _thread;
    private static volatile bool _running;
    private static volatile string _deviceId = "";
    private static volatile bool _reopenCapture;
    private static readonly object Gate = new();

    public static void Apply(AppSettings settings) => Configure(settings.PushToTalk.IsBound, settings.VoiceInputDevice);

    private static void Configure(bool enabled, string? deviceId)
    {
        lock (Gate)
        {
            string device = deviceId ?? "";
            if (device != _deviceId)
            {
                _deviceId = device;
                _reopenCapture = true;
            }

            if (enabled == _enabled) return;
            _enabled = enabled;

            if (enabled)
            {
                _running = true;
                _thread = new Thread(Run)
                {
                    IsBackground = true,
                    Name = "VoiceInput",
                    Priority = ThreadPriority.BelowNormal
                };
                _thread.Start();
            }
            else
            {
                Stop();
            }
        }
    }

    public static void Stop()
    {
        lock (Gate)
        {
            _enabled = false;
            _running = false;
            _held = false;
            _thread?.Join(2000);
            _thread = null;
        }
    }

    public static void Track(InputCode trigger, bool down, bool mayStart)
    {
        if (!_enabled) return;

        AppSettings settings = StarterTool.Settings;
        Hotkey ptt = settings.PushToTalk;
        if (!ptt.IsBound) return;

        bool held = ptt.IsDown(code => code == trigger ? down : InputState.IsDown(code));
        if (held == _held) return;

        if (held)
        {
            if (!mayStart) return;

            bool foreground = Win32.IsForeground(StarterTool.MainFormHandle);
            if (!foreground && (!ptt.Global || !settings.GlobalHotkeysEnabled)) return;
        }

        _held = held;
    }

    private static void Run()
    {
        Model? model = null;
        VoskRecognizer? recognizer = null;
        AudioDevices.Capture? capture = null;
        try
        {
            model = LoadModel();
            if (model == null) return;

            string grammar = JsonSerializer.Serialize(SpokenNumber.Vocabulary.Append("[unk]").ToArray());
            recognizer = new VoskRecognizer(model, AudioDevices.Capture.SampleRate, grammar);

            var preRoll = new Queue<byte[]>();
            int preRollBytes = 0;
            bool listening = false;

            var segments = new List<string>();
            string candidate = "";
            double candidateSince = 0;
            string shown = "";
            bool paused = false;
            bool fed = false;
            int press = 0;

            void Feed(VoskRecognizer rec, byte[] chunk, int length)
            {
                fed = true;
                if (!rec.AcceptWaveform(chunk, length)) return;
                string segment = TextOf(rec.Result(), "text");
                if (segment.Length > 0) segments.Add(segment);
                paused = true;
            }
            double tailUntil = 0;
            double retryAt = 0;
            int staleHeld = 0;

            while (_running)
            {
                if (_reopenCapture)
                {
                    _reopenCapture = false;
                    capture?.Dispose();
                    capture = null;
                    retryAt = 0;
                }

                if (capture == null && Win32.GetTime() >= retryAt)
                {
                    capture = AudioDevices.Capture.Open(_deviceId, Log);
                    if (capture == null) retryAt = Win32.GetTime() + 5000;
                }

                if (capture != null)
                {
                    VoskRecognizer rec = recognizer;
                    bool alive = capture.Drain((chunk, length) =>
                    {
                        if (listening)
                        {
                            Feed(rec, chunk, length);
                            return;
                        }

                        preRoll.Enqueue(chunk);
                        preRollBytes += length;
                        while (preRollBytes > PreRollMs * BytesPerMs && preRoll.Count > 1)
                        {
                            preRollBytes -= preRoll.Dequeue().Length;
                        }
                    });

                    if (!alive)
                    {
                        Log("voice: the recording device went away");
                        capture.Dispose();
                        capture = null;
                        listening = false;
                        continue;
                    }
                }

                if (listening && _held && !StarterTool.Settings.PushToTalk.IsDown(InputState.IsDown))
                {
                    if (++staleHeld >= 5) _held = false;
                }
                else
                {
                    staleHeld = 0;
                }

                bool held = _held;
                if (held && !listening && capture != null)
                {
                    listening = true;
                    tailUntil = 0;
                    press++;
                    segments.Clear();
                    candidate = shown = "";
                    paused = false;
                    recognizer.Reset();
                    foreach (byte[] chunk in preRoll) Feed(recognizer, chunk, chunk.Length);
                    preRoll.Clear();
                    preRollBytes = 0;
                }
                else if (listening && !held)
                {
                    if (tailUntil == 0) tailUntil = Win32.GetTime() + TailMs;
                    if (Win32.GetTime() >= tailUntil)
                    {
                        listening = false;
                        string last = TextOf(recognizer.FinalResult(), "text");
                        if (last.Length > 0) segments.Add(last);
                        Hand(string.Join(' ', segments), press, final: true, paused: true);
                    }
                }
                else if (listening && held)
                {
                    tailUntil = 0;

                    if (fed)
                    {
                        string partial = TextOf(recognizer.PartialResult(), "partial");
                        string reading = string.Join(' ', segments.Append(partial).Where(part => part.Length > 0));
                        if (reading != candidate)
                        {
                            candidate = reading;
                            candidateSince = Win32.GetTime();
                        }
                    }

                    bool settled = paused || Win32.GetTime() - candidateSince >= StableMs;
                    if (settled && candidate.Length > 0 && candidate != shown)
                    {
                        shown = candidate;
                        Hand(candidate, press, final: false, paused);
                    }

                    paused = false;
                }

                fed = false;

                Thread.Sleep(PollMs);
            }
        }
        catch (Exception e)
        {
            Log($"voice: stopped, {e.GetType().Name}: {e.Message}");
        }
        finally
        {
            capture?.Dispose();
            recognizer?.Dispose();
            model?.Dispose();
        }
    }

    private static void Hand(string text, int press, bool final, bool paused)
    {
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        SpokenId? spoken = SpokenNumber.TryParse(words, out SpokenId id) ? id : null;
        StarterTool.Post(() => StarterTool.MainForm.EnterSpokenTrainerId(text, spoken, press, final, paused));
    }

    private static string TextOf(string json, string field)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(field, out JsonElement value) ? value.GetString() ?? "" : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }

    private const string ModelResource = "vosk-model.zip";

    private static Model? LoadModel()
    {
        Vosk.Vosk.SetLogLevel(-1);

        string? path = ModelPath();
        if (path == null) return null;

        var started = Win32.GetTime();
        var model = new Model(path);
        Log(string.Format(CultureInfo.InvariantCulture, "voice: model {0} loaded in {1:F0} ms",
            Path.GetFileName(path), Win32.GetTime() - started));
        return model;
    }

    private static string? ModelPath()
    {
        string beside = Path.Combine(AppContext.BaseDirectory, "vosk-model");
        if (File.Exists(Path.Combine(beside, "am", "final.mdl"))) return beside;

        Assembly assembly = typeof(VoiceInput).Assembly;
        using Stream? zip = assembly.GetManifestResourceStream(ModelResource);
        if (zip == null)
        {
            Log("voice: this build has no speech model embedded");
            return null;
        }

        string root = Path.Combine(SettingsStore.DefaultDirectory, "voice");
        string marker = Path.Combine(root, "unpacked-" + zip.Length.ToString(CultureInfo.InvariantCulture));
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);
        string top = archive.Entries.Select(entry => entry.FullName.Split('/')[0]).FirstOrDefault(name => name.Length > 0) ?? "model";
        string folder = Path.Combine(root, top);

        if (!File.Exists(marker) || !Directory.Exists(folder))
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            Directory.CreateDirectory(root);
            archive.ExtractToDirectory(root, overwriteFiles: true);
            File.WriteAllText(marker, top);
            Log($"voice: speech model unpacked to {folder}");
        }

        return folder;
    }

    private static void Log(string line) => RunLog.Log(line);
}
