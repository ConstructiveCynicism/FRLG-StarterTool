using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using FRLG.StarterTool.Core.Settings;
using FRLG.StarterTool.Core.Tips;

namespace FRLG.StarterTool.App;

internal static class RunLog
{
    public static string Directory => Path.Combine(SettingsStore.DefaultDirectory, "runs");

    private static readonly object Gate = new();

    private static string? _stamp;

    private static int _trainerId;

    private static string? _path;

    public static string? CurrentPath
    {
        get { lock (Gate) return _path; }
    }

    public static void StartRun()
    {
        lock (Gate)
        {
            _stamp = null;
            _trainerId = 0;
            _path = null;
            Open();
        }
    }

    public static void SetTrainerId(int trainerId)
    {
        if (trainerId <= 0) return;

        lock (Gate)
        {
            if (_trainerId == trainerId || _stamp == null || _path == null) return;

            string renamed = Path.Combine(Directory, _stamp + "_" + trainerId.ToString(
                CultureInfo.InvariantCulture) + ".txt");

            try
            {
                if (!string.Equals(renamed, _path, StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(_path, renamed, overwrite: true);
                }

                _path = renamed;
                _trainerId = trainerId;
            }
            catch (Exception)
            {
            }
        }
    }

    public static void Log(string line)
    {
        lock (Gate)
        {
            if (_path == null) Open();
            if (_path == null) return;

            try
            {
                File.AppendAllText(_path,
                    DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + line
                        + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch (Exception)
            {
            }
        }
    }

    public static void LogAttempt(TipAttempt attempt) => Log(TipAttemptLog.Format(attempt));

    public static IReadOnlyList<TipAttempt> RecentAttempts(int count)
    {
        var found = new List<TipAttempt>(count);
        if (count <= 0) return found;

        try
        {
            IEnumerable<FileInfo> newest = new DirectoryInfo(Directory)
                .EnumerateFiles("*.txt")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(ScanLimit);

            foreach (FileInfo file in newest)
            {
                if (TryReadAttempt(file.FullName, out TipAttempt attempt)) found.Add(attempt);
                if (found.Count == count) break;
            }
        }
        catch (Exception)
        {
        }

        found.Reverse();
        return found;
    }

    private const int ScanLimit = 200;

    private static bool TryReadAttempt(string path, out TipAttempt attempt)
    {
        attempt = new TipAttempt();
        bool found = false;

        try
        {
            foreach (string line in File.ReadLines(path))
            {
                if (TipAttemptLog.TryParse(line, out TipAttempt parsed))
                {
                    attempt = parsed;
                    found = true;
                }
            }
        }
        catch (Exception)
        {
            return false;
        }

        return found;
    }

    private static void Open()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);

            DateTime now = DateTime.Now;
            string stamp = now.ToString("HHmmss_yyyy-MM-dd", CultureInfo.InvariantCulture);

            string candidate = stamp;
            for (int i = 2; Taken(candidate); i++)
            {
                candidate = stamp + "-" + i.ToString(CultureInfo.InvariantCulture);
            }

            _stamp = candidate;
            _path = Path.Combine(Directory, candidate + ".txt");
            _trainerId = 0;

            File.AppendAllText(_path, "", Encoding.UTF8);
        }
        catch (Exception)
        {
            _stamp = null;
            _path = null;
        }
    }

    private static bool Taken(string stamp) =>
        System.IO.Directory.EnumerateFiles(Directory, stamp + ".txt").Any()
        || System.IO.Directory.EnumerateFiles(Directory, stamp + "_*.txt").Any();
}
