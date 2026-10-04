using System.IO;
namespace DesktopClockWeather;

internal enum LifecycleState
{
    ColdStart,
    StartupRestore,
    Running,
    Sleeping,
    Resuming,
    DisplayReconfiguration,
    Ready,
    Exiting
}

internal static class DiagnosticsLog
{
    private static readonly object Sync = new();
    private static bool _cleaned;
    public const string BuildId = "V1.1";

    public static string LogsFolder => Path.Combine(ProfileManager.VersionFolder, "logs");

    public static void Event(string profile, LifecycleState state, string eventName, string details = "")
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogsFolder);
                if (!_cleaned)
                {
                    CleanupOldLogs();
                    _cleaned = true;
                }

                var path = Path.Combine(LogsFolder, $"DesktopClockWeather-{DateTime.Now:yyyyMMdd}.log");
                var pid = Environment.ProcessId;
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [PID:{pid}] [{profile}] [{state}] {eventName}";
                if (!string.IsNullOrWhiteSpace(details)) line += " " + details;
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch { }
    }

    private static void CleanupOldLogs()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(LogsFolder, "DesktopClockWeather-*.log"))
            {
                if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-7))
                    File.Delete(file);
            }
        }
        catch { }
    }
}
