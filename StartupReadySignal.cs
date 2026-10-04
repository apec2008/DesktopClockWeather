using System.Threading;

namespace DesktopClockWeather;

internal static class StartupReadySignal
{
    public static string EventName(string profile)
    {
        var safe = new string(profile.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
        if (string.IsNullOrWhiteSpace(safe)) safe = "Default";
        return $"Local\\DesktopClockWeather_Ready_{safe}";
    }

    public static void Signal(string profile)
    {
        try
        {
            using var ready = EventWaitHandle.OpenExisting(EventName(profile));
            ready.Set();
        }
        catch
        {
            // No launcher is waiting. Normal interactive launches reach here too.
        }
    }
}
