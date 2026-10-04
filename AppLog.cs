namespace DesktopClockWeather;

// Compatibility wrapper for older call sites. New code should use DiagnosticsLog.Event.
internal static class AppLog
{
    public static void Write(string profile, string message) =>
        DiagnosticsLog.Event(profile, LifecycleState.Running, "LegacyLog", message);
}
