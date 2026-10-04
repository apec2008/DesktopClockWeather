using System.IO;
using System.Text.Json;
using System.Threading;

namespace DesktopClockWeather;

/// <summary>
/// Stores the user's desired set of clock windows for the next Windows startup.
/// This is intentionally separate from per-profile settings and from the Startup
/// shortcut/configuration.  The Startup launcher reads this file only to decide
/// WHICH profiles should be restored; each profile JSON remains authoritative for
/// city, color, scale, temperature unit and placement.
/// </summary>
internal static class StartupStateManager
{
    private const int CurrentSchemaVersion = 1;
    private const string StateMutexName = "Local\\DesktopClockWeather_V11_StartupState";
    private static volatile bool _systemSessionEnding;

    public static string StatePath => Path.Combine(ProfileManager.VersionFolder, "StartupState.json");

    public static bool IsSystemSessionEnding => _systemSessionEnding;

    public static void BeginSystemSessionEnding()
    {
        _systemSessionEnding = true;
        DiagnosticsLog.Event("Launcher", LifecycleState.Exiting, "SystemSessionEnding", "startup-state changes are now frozen");
    }

    /// <summary>
    /// Creates the one-time initial startup state.  The product default is exactly
    /// two windows: Seoul(Default) and Ann Arbor(Default2).
    /// </summary>
    public static void EnsureInitialized()
    {
        WithMutex(() =>
        {
            if (File.Exists(StatePath)) return;

            var state = new StartupState
            {
                SchemaVersion = CurrentSchemaVersion,
                Initialized = true,
                DesiredProfiles = new List<string> { "Default", "Default2" },
                LastUpdatedUtc = DateTime.UtcNow
            };
            WriteStateUnsafe(state);
            DiagnosticsLog.Event("Launcher", LifecycleState.ColdStart, "StartupStateInitialized",
                "profiles=Default,Default2 source=first-run-default");
        });
    }

    public static IReadOnlyList<string> GetDesiredProfiles()
    {
        EnsureInitialized();
        return WithMutex(() =>
        {
            var state = ReadStateUnsafe();
            return NormalizeProfiles(state.DesiredProfiles).ToList();
        });
    }

    /// <summary>
    /// A successful normal/manual launch means this profile is part of the user's
    /// desired working set and should be restored after the next reboot.
    /// </summary>
    public static void MarkProfileOpen(string profile)
    {
        if (_systemSessionEnding) return;
        WithMutex(() =>
        {
            var state = ReadOrCreateUnsafe();
            var profiles = NormalizeProfiles(state.DesiredProfiles).ToList();
            if (!profiles.Contains(profile, StringComparer.OrdinalIgnoreCase))
            {
                profiles.Add(profile);
                state.DesiredProfiles = NormalizeProfiles(profiles).ToList();
                state.LastUpdatedUtc = DateTime.UtcNow;
                WriteStateUnsafe(state);
                DiagnosticsLog.Event(profile, LifecycleState.Running, "StartupStateProfileAdded",
                    $"profiles={string.Join(',', state.DesiredProfiles)}");
            }
        });
    }

    /// <summary>
    /// Called only for a user-initiated/normal window close. During Windows logoff
    /// or shutdown each process receives SessionEnding first, so removals are
    /// suppressed and the saved working set survives reboot.
    /// </summary>
    public static void MarkProfileClosedByUser(string profile)
    {
        if (_systemSessionEnding)
        {
            DiagnosticsLog.Event(profile, LifecycleState.Exiting, "StartupStateCloseIgnored",
                "reason=system-session-ending");
            return;
        }

        WithMutex(() =>
        {
            var state = ReadOrCreateUnsafe();
            var profiles = NormalizeProfiles(state.DesiredProfiles)
                .Where(p => !p.Equals(profile, StringComparison.OrdinalIgnoreCase))
                .ToList();
            state.DesiredProfiles = profiles;
            state.LastUpdatedUtc = DateTime.UtcNow;
            WriteStateUnsafe(state);
            DiagnosticsLog.Event(profile, LifecycleState.Exiting, "StartupStateProfileRemoved",
                $"profiles={string.Join(',', profiles)}");
        });
    }

    private static StartupState ReadOrCreateUnsafe()
    {
        if (!File.Exists(StatePath))
        {
            var fresh = new StartupState
            {
                SchemaVersion = CurrentSchemaVersion,
                Initialized = true,
                DesiredProfiles = new List<string> { "Default", "Default2" },
                LastUpdatedUtc = DateTime.UtcNow
            };
            WriteStateUnsafe(fresh);
            return fresh;
        }
        return ReadStateUnsafe();
    }

    private static StartupState ReadStateUnsafe()
    {
        try
        {
            var json = File.ReadAllText(StatePath);
            var state = JsonSerializer.Deserialize<StartupState>(json) ?? new StartupState();
            state.SchemaVersion = CurrentSchemaVersion;
            state.Initialized = true;
            state.DesiredProfiles = NormalizeProfiles(state.DesiredProfiles).ToList();
            return state;
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Event("Launcher", LifecycleState.ColdStart, "StartupStateReadFailed", ex.Message);
            // A corrupt state must not resurrect the historical 3-window beta default.
            var fallback = new StartupState
            {
                SchemaVersion = CurrentSchemaVersion,
                Initialized = true,
                DesiredProfiles = new List<string> { "Default", "Default2" },
                LastUpdatedUtc = DateTime.UtcNow
            };
            WriteStateUnsafe(fallback);
            return fallback;
        }
    }

    private static IEnumerable<string> NormalizeProfiles(IEnumerable<string>? profiles)
    {
        var allowed = new[] { "Default", "Default2", "Default3" };
        var set = new HashSet<string>(profiles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        return allowed.Where(set.Contains); // canonical slot order
    }

    private static void WriteStateUnsafe(StartupState state)
    {
        Directory.CreateDirectory(ProfileManager.VersionFolder);
        var tmp = StatePath + ".tmp";
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(tmp, json);
        File.Move(tmp, StatePath, overwrite: true);
    }

    private static void WithMutex(Action action)
    {
        using var mutex = new Mutex(false, StateMutexName);
        var acquired = false;
        try
        {
            try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new TimeoutException("Startup state mutex timeout.");
            action();
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Event("Launcher", LifecycleState.Running, "StartupStateUpdateFailed", ex.Message);
        }
        finally
        {
            if (acquired) try { mutex.ReleaseMutex(); } catch { }
        }
    }

    private static T WithMutex<T>(Func<T> func)
    {
        using var mutex = new Mutex(false, StateMutexName);
        var acquired = false;
        try
        {
            try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new TimeoutException("Startup state mutex timeout.");
            return func();
        }
        finally
        {
            if (acquired) try { mutex.ReleaseMutex(); } catch { }
        }
    }

    private sealed class StartupState
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public bool Initialized { get; set; }
        public List<string> DesiredProfiles { get; set; } = new();
        public DateTime LastUpdatedUtc { get; set; }
    }
}
