using System.Diagnostics;
using System.IO;
using System.Threading;

namespace DesktopClockWeather;

internal static class StartupLauncher
{
    private const string StartupLauncherMutexName = "Local\\DesktopClockWeather_V11_StartupLauncher";

    public static async Task RunAsync()
    {
        using var launcherMutex = new Mutex(true, StartupLauncherMutexName, out var ownsLauncherMutex);
        if (!ownsLauncherMutex)
        {
            DiagnosticsLog.Event("Launcher", LifecycleState.ColdStart, "DuplicateStartupLauncherBlocked");
            return;
        }

        var packagedConfig = Path.Combine(AppContext.BaseDirectory, "StartupProfiles.txt");
        var userConfig = Path.Combine(ProfileManager.VersionFolder, "StartupProfiles.txt");
        EnsureUserStartupConfig(packagedConfig, userConfig);

        var delaySeconds = 2;
        var betweenMs = 500;

        try
        {
            if (File.Exists(userConfig))
            {
                foreach (var raw in File.ReadAllLines(userConfig))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith('#')) continue;
                    if (line.StartsWith("DelaySeconds=", StringComparison.OrdinalIgnoreCase) && int.TryParse(line.Split('=', 2)[1], out var d))
                        delaySeconds = Math.Clamp(d, 0, 30);
                    else if (line.StartsWith("BetweenMilliseconds=", StringComparison.OrdinalIgnoreCase) && int.TryParse(line.Split('=', 2)[1], out var b))
                        betweenMs = Math.Clamp(b, 200, 5000);
                    // V1.1 deliberately ignores historical StartupCount=/Profile=
                    // directives. Window selection now has one authoritative source:
                    // StartupState.json.
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Event("Launcher", LifecycleState.ColdStart, "StartupConfigReadFailed", ex.Message);
        }

        StartupStateManager.EnsureInitialized();
        var profiles = StartupStateManager.GetDesiredProfiles().ToList();

        var exe = Environment.ProcessPath;
        DiagnosticsLog.Event("Launcher", LifecycleState.ColdStart, "StartupLauncherStart",
            $"build={DiagnosticsLog.BuildId} exe={exe} base={AppContext.BaseDirectory} state={StartupStateManager.StatePath} delay={delaySeconds}s profiles={string.Join(',', profiles)}");

        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            DiagnosticsLog.Event("Launcher", LifecycleState.ColdStart, "StartupExeMissing", $"exe={exe}");
            return;
        }

        if (profiles.Count == 0)
        {
            DiagnosticsLog.Event("Launcher", LifecycleState.StartupRestore, "StartupNoProfilesRequested");
            return;
        }

        foreach (var profile in profiles)
        {
            try
            {
                using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, StartupReadySignal.EventName(profile));
                ready.Reset();

                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = true,
                    WorkingDirectory = AppContext.BaseDirectory
                };
                psi.ArgumentList.Add("--profile");
                psi.ArgumentList.Add(profile);
                var process = Process.Start(psi);
                DiagnosticsLog.Event("Launcher", LifecycleState.StartupRestore, "StartupProfileLaunch",
                    $"profile={profile} pid={process?.Id} exe={exe}");

                var signaled = ready.WaitOne(TimeSpan.FromSeconds(15));
                DiagnosticsLog.Event("Launcher", LifecycleState.StartupRestore, "StartupProfileReadyWait",
                    $"profile={profile} signaled={signaled}");
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Event("Launcher", LifecycleState.StartupRestore, "StartupProfileLaunchFailed", $"{profile}: {ex}");
            }

            await Task.Delay(betweenMs);
        }
    }

    private static void EnsureUserStartupConfig(string packagedConfig, string userConfig)
    {
        try
        {
            Directory.CreateDirectory(ProfileManager.VersionFolder);
            if (File.Exists(userConfig)) return;

            if (File.Exists(packagedConfig))
            {
                File.Copy(packagedConfig, userConfig, overwrite: false);
                DiagnosticsLog.Event("Launcher", LifecycleState.ColdStart, "StartupConfigInitialized", $"from={packagedConfig} to={userConfig}");
            }
            else
            {
                File.WriteAllText(userConfig,
                    "# Desktop Clock Weather V1.1 startup timing configuration\r\n" +
                    "# Window selection is stored separately in StartupState.json.\r\n" +
                    "DelaySeconds=2\r\n" +
                    "BetweenMilliseconds=500\r\n");
                DiagnosticsLog.Event("Launcher", LifecycleState.ColdStart, "StartupConfigInitialized", $"generated={userConfig}");
            }
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Event("Launcher", LifecycleState.ColdStart, "StartupConfigInitializeFailed", ex.Message);
        }
    }
}
