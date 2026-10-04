using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopClockWeather;

internal sealed class ProfileManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
    private readonly object _sync = new();

    public static string RootFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopClockWeather");

    // V1.1 profiles are isolated from legacy V1.0/beta files so that an older
    // executable can never overwrite newer fields such as ScalePercent.
    public static string VersionFolder => Path.Combine(RootFolder, "V1.1");
    public static string SettingsFolder => Path.Combine(VersionFolder, "profiles");
    public static string LegacySettingsFolder => RootFolder;

    public static string PathFor(string profile) => Path.Combine(SettingsFolder, $"{profile}.json");
    public static string LegacyPathFor(string profile) => Path.Combine(LegacySettingsFolder, $"{profile}.json");

    public ClockSettings Load(string profile)
    {
        Directory.CreateDirectory(SettingsFolder);
        var path = PathFor(profile);

        if (!File.Exists(path))
        {
            // V1.1 policy: a missing V1.1 profile is a true first-run
            // profile. Do NOT auto-import V1.0 or old-beta JSON from the shared
            // root folder. That legacy import reintroduced stale scale/position
            // values after a clean uninstall. Existing V1.1 profiles are still
            // preserved and migrated normally.
            var fresh = CreateDefault(profile);
            Save(fresh, "CreateDefaultV11");
            DiagnosticsLog.Event(profile, LifecycleState.ColdStart, "FreshV11ProfileCreated",
                $"path={path} city={fresh.City} scale={fresh.ScalePercent}% color={fresh.CityColor}");
            return fresh;
        }

        try
        {
            var json = File.ReadAllText(path);
            ClockSettings settings = JsonSerializer.Deserialize<ClockSettings>(json, JsonOptions) ?? CreateDefault(profile);
            settings.ProfileName = profile;
            var loadedVersion = settings.SettingsVersion;
            var changed = MigrateAndValidate(settings);

            // Beta 1.6: do not forcibly reset secondary-window placement during migration.
            // Existing user positions are preserved. Visibility is handled by WindowPlacementManager.

            if (changed)
                Save(settings, "Migration");
            DiagnosticsLog.Event(profile, LifecycleState.ColdStart, "ProfileLoaded", $"path={path} city={settings.City} scale={settings.ScalePercent}% color={settings.CityColor}");
            return settings;
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Event(profile, LifecycleState.ColdStart, "ProfileLoadFailed", ex.Message);
            try
            {
                var corrupt = path + $".corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
                File.Copy(path, corrupt, overwrite: false);
            }
            catch { }

            var fallback = CreateDefault(profile);
            Save(fallback, "CorruptFallback");
            return fallback;
        }
    }

    public void Save(ClockSettings settings, string reason)
    {
        settings.SettingsVersion = ClockSettings.CurrentSettingsVersion;
        Directory.CreateDirectory(SettingsFolder);
        var path = PathFor(settings.ProfileName);
        var tmp = path + ".tmp";
        var bak = path + ".bak";

        lock (_sync)
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(tmp, json);
            if (File.Exists(path))
            {
                try { File.Copy(path, bak, overwrite: true); } catch { }
            }
            File.Move(tmp, path, overwrite: true);
        }

        DiagnosticsLog.Event(settings.ProfileName, LifecycleState.Running, "ProfileSaved",
            $"reason={reason} city={settings.City} color={settings.CityColor} scale={settings.ScalePercent}% pos=({settings.Left:0.0},{settings.Top:0.0}) monitor={settings.MonitorDeviceName}");
    }

    private static bool MigrateAndValidate(ClockSettings s)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(s.FontColor)) { s.FontColor = "#FFFFFFFF"; changed = true; }
        if (string.IsNullOrWhiteSpace(s.CityColor)) { s.CityColor = "#FF90CAF9"; changed = true; }
        if (string.IsNullOrWhiteSpace(s.GlowColor)) { s.GlowColor = "#FF000000"; changed = true; }

        if (!s.TemperatureUnit.Equals("F", StringComparison.OrdinalIgnoreCase) &&
            !s.TemperatureUnit.Equals("C", StringComparison.OrdinalIgnoreCase))
        {
            s.TemperatureUnit = "C";
            changed = true;
        }

        var clampedOffset = Math.Clamp(s.ManualTimeOffsetHours, -2, 2);
        if (clampedOffset != s.ManualTimeOffsetHours) { s.ManualTimeOffsetHours = clampedOffset; changed = true; }

        var allowedScales = new[] { -30, -20, -10, 0, 10, 20, 30 };
        if (!allowedScales.Contains(s.ScalePercent))
        {
            // Invalid or missing values fall back to the canonical 100% size.
            s.ScalePercent = 0;
            changed = true;
        }

        if (s.WeatherRefreshMinutes < 10 || s.WeatherRefreshMinutes > 180)
        {
            s.WeatherRefreshMinutes = Math.Clamp(s.WeatherRefreshMinutes, 10, 180);
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(s.TimeZoneId))
        {
            s.TimeZoneId = "Asia/Seoul";
            changed = true;
        }

        s.Width = ClockSettings.BaseWidth;
        s.Height = ClockSettings.BaseHeight;
        s.SettingsVersion = ClockSettings.CurrentSettingsVersion;
        return changed;
    }

    private static ClockSettings CreateDefault(string profile)
    {
        var s = new ClockSettings { ProfileName = profile };
        switch (profile.ToUpperInvariant())
        {
            case "DEFAULT2":
                s.City = "Ann Arbor";
                s.Latitude = 42.2808;
                s.Longitude = -83.7430;
                s.TimeZoneId = "America/Detroit";
                break;
            case "DEFAULT3":
                s.City = "Saratoga";
                s.Latitude = 37.2638;
                s.Longitude = -122.0230;
                s.TimeZoneId = "America/Los_Angeles";
                break;
            default:
                s.City = "Seoul";
                s.Latitude = 37.5665;
                s.Longitude = 126.9780;
                s.TimeZoneId = "Asia/Seoul";
                break;
        }
        return s;
    }
}
