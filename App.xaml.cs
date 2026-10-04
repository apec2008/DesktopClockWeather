using System.IO;
using System.Threading;
using System.Windows;

namespace DesktopClockWeather;

public partial class App : Application
{
    public static string ProfileName { get; private set; } = "Default";
    private Mutex? _profileMutex;
    private Mutex? _slotMutex;
    private bool _ownsProfileMutex;
    private int _slotIndex = -1;
    private bool _explicitProfile;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Freeze startup-set updates during Windows logoff/shutdown. Otherwise the
        // automatic closing of each clock window would be mistaken for a user close.
        SessionEnding += (_, _) => StartupStateManager.BeginSystemSessionEnding();

        // Explicit startup-registration modes used by build/test utilities.
        // V1.1 prefers Task Scheduler for faster logon startup, but keeps the
        // proven Startup-folder mechanism as an immediate rollback path.
        if (e.Args.Any(a => a.Equals("--install-fast-startup", StringComparison.OrdinalIgnoreCase)))
        {
            var rc = StartupRegistrationManager.InstallFastStartupWithFallback();
            Shutdown(rc);
            return;
        }

        if (e.Args.Any(a => a.Equals("--install-standard-startup", StringComparison.OrdinalIgnoreCase)))
        {
            var rc = StartupRegistrationManager.InstallStandardStartup();
            Shutdown(rc);
            return;
        }

        if (e.Args.Any(a => a.Equals("--remove-startup", StringComparison.OrdinalIgnoreCase)))
        {
            var rc = StartupRegistrationManager.RemoveStartupRegistration();
            Shutdown(rc);
            return;
        }

        if (e.Args.Any(a => a.Equals("--startup", StringComparison.OrdinalIgnoreCase)))
        {
            await StartupLauncher.RunAsync();
            Shutdown();
            return;
        }

        for (var i = 0; i < e.Args.Length - 1; i++)
        {
            if (!e.Args[i].Equals("--profile", StringComparison.OrdinalIgnoreCase)) continue;
            ProfileName = SanitizeProfile(e.Args[i + 1]);
            _explicitProfile = true;
            break;
        }

        if (_explicitProfile)
        {
            if (!AcquireNamedProfile(ProfileName))
            {
                DiagnosticsLog.Event(ProfileName, LifecycleState.ColdStart, "DuplicateProfileBlocked");
                Shutdown();
                return;
            }

            if (TryGetDefaultSlotIndex(ProfileName, out var slot))
            {
                _slotIndex = slot;
                if (!TryAcquireSpecificSlot(slot, out _slotMutex))
                {
                    DiagnosticsLog.Event(ProfileName, LifecycleState.ColdStart, "SlotAlreadyRunning", $"slot={slot}");
                    Shutdown();
                    return;
                }
            }
        }
        else
        {
            if (!TryAcquireNormalSlot(out _slotIndex, out _slotMutex))
            {
                DiagnosticsLog.Event("Launcher", LifecycleState.ColdStart, "InstanceLimitReached", "maxInstances=3");
                MessageBox.Show(
                    "Desktop Clock Weather supports up to 3 clock windows.\n\nUse Settings in each clock to choose the city you want.",
                    "Desktop Clock Weather",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            ProfileName = _slotIndex == 0 ? "Default" : $"Default{_slotIndex + 1}";
            DiagnosticsLog.Event(ProfileName, LifecycleState.ColdStart, "NormalSlotAcquired", $"slot={_slotIndex} maxInstances=3");
        }

        DispatcherUnhandledException += (_, args) =>
        {
            DiagnosticsLog.Event(ProfileName, LifecycleState.Running, "UnhandledException", args.Exception.ToString());
        };

        var profiles = new ProfileManager();
        var profilePath = ProfileManager.PathFor(ProfileName);
        var isNewProfile = !File.Exists(profilePath);
        var settings = profiles.Load(ProfileName);

        DiagnosticsLog.Event(ProfileName, LifecycleState.ColdStart, "AppStart",
            $"build={DiagnosticsLog.BuildId} exe={Environment.ProcessPath} explicit={_explicitProfile} slot={_slotIndex} newProfile={isNewProfile}");

        var window = new MainWindow(ProfileName, _slotIndex, isNewProfile, settings, profiles);
        MainWindow = window;
        window.Show(); // Window starts fully transparent and reveals itself after lifecycle initialization.
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DiagnosticsLog.Event(ProfileName, LifecycleState.Exiting, "AppExit");
        try
        {
            if (_ownsProfileMutex) _profileMutex?.ReleaseMutex();
        }
        catch { }
        try { _slotMutex?.ReleaseMutex(); } catch { }
        _profileMutex?.Dispose();
        _slotMutex?.Dispose();
        base.OnExit(e);
    }

    private bool AcquireNamedProfile(string profile)
    {
        try
        {
            _profileMutex = new Mutex(true, $"Local\\DesktopClockWeather_Profile_{SanitizeMutexName(profile)}", out var created);
            _ownsProfileMutex = created;
            return created;
        }
        catch
        {
            _ownsProfileMutex = true;
            return true;
        }
    }

    private static bool TryGetDefaultSlotIndex(string profile, out int slot)
    {
        slot = -1;
        if (profile.Equals("Default", StringComparison.OrdinalIgnoreCase)) { slot = 0; return true; }
        if (profile.StartsWith("Default", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(profile[7..], out var number) && number >= 2)
        {
            slot = number - 1;
            return true;
        }
        return false;
    }

    private static bool TryAcquireSpecificSlot(int slot, out Mutex? mutex)
    {
        mutex = null;
        try
        {
            mutex = new Mutex(true, $"Local\\DesktopClockWeather_NormalSlot_{slot}", out var created);
            if (created) return true;
            mutex.Dispose();
            mutex = null;
        }
        catch { }
        return false;
    }

    private static bool TryAcquireNormalSlot(out int slotIndex, out Mutex? slotMutex)
    {
        // V1.1 acceptance scope: exactly three independent normal slots.
        // Slot 0 = Default, Slot 1 = Default2, Slot 2 = Default3.
        // A fourth launch is intentionally rejected instead of silently creating
        // another profile that is outside the tested/defined product behavior.
        for (var slot = 0; slot < 3; slot++)
        {
            try
            {
                var mutex = new Mutex(true, $"Local\\DesktopClockWeather_NormalSlot_{slot}", out var created);
                if (created)
                {
                    slotIndex = slot;
                    slotMutex = mutex;
                    return true;
                }
                mutex.Dispose();
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Event("Launcher", LifecycleState.ColdStart, "SlotAcquireFailed", $"slot={slot} error={ex.Message}");
            }
        }

        slotIndex = -1;
        slotMutex = null;
        return false;
    }

    private static string SanitizeProfile(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return string.IsNullOrWhiteSpace(value) ? "Default" : value.Trim();
    }

    private static string SanitizeMutexName(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray();
        var result = new string(chars);
        return string.IsNullOrWhiteSpace(result) ? "Default" : result;
    }
}
