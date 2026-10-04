using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopClockWeather;

/// <summary>
/// Startup registration for V1.1.
/// V1.1 prefers a Task Scheduler logon trigger to reduce the long and variable
/// Windows Startup-folder delay observed during validation.
/// If Task Scheduler registration fails, it automatically falls back to the
/// proven Startup-folder ShellLink method.
/// </summary>
internal static class StartupRegistrationManager
{
    private const string ShortcutFileName = "Desktop Clock Weather V1.1.lnk";
    private const string TaskName = "DesktopClockWeather V1.1 Fast Startup";
    private const string BuildId = "V1.1";
    private const string InstalledExeName = "DesktopClockWeather_V1.1.exe";

    public static string ReportPath => Path.Combine(ProfileManager.VersionFolder, "STARTUP_INSTALL_REPORT.txt");

    public static int InstallFastStartupWithFallback()
    {
        var lines = NewReportHeader("Preferred method: Task Scheduler logon trigger (3-second delay); automatic fallback: Startup-folder ShellLink");

        try
        {
            StartupStateManager.EnsureInitialized();
            var (sourceExe, installDir, installedExe) = PrepareInstalledCopy();

            string activeMode;
            string? fastError = null;
            try
            {
                RemoveStartupShortcut();
                InstallScheduledTask(installedExe, installDir);
                VerifyScheduledTask(installedExe);
                activeMode = "TaskSchedulerFast";
            }
            catch (Exception ex)
            {
                fastError = ex.ToString();
                // Roll back immediately to the known-good standard startup mechanism.
                try { RemoveScheduledTask(); } catch { }
                InstallStandardShortcut(installedExe, installDir);
                activeMode = "StartupFolderFallback";
            }

            lines.Add("Success: True");
            lines.Add($"ActiveMode: {activeMode}");
            lines.Add($"Source build: {sourceExe}");
            lines.Add($"Installed target: {installedExe}");
            lines.Add("Arguments: --startup");
            lines.Add($"Task name: {TaskName}");
            lines.Add($"Startup shortcut: {GetShortcutPath()}");
            lines.Add($"Startup state: {StartupStateManager.StatePath}");
            lines.Add($"Startup state exists: {File.Exists(StartupStateManager.StatePath)}");
            if (!string.IsNullOrWhiteSpace(fastError))
            {
                lines.Add("FastStartupError: " + fastError.Replace("\r", " ").Replace("\n", " | "));
            }
            WriteReport(lines);
            return 0;
        }
        catch (Exception ex)
        {
            lines.Add("Success: False");
            lines.Add("Error: " + ex);
            try { WriteReport(lines); } catch { }
            return 1;
        }
    }

    public static int InstallStandardStartup()
    {
        var lines = NewReportHeader("Method: Standard Startup-folder ShellLink (rollback/baseline mode)");
        try
        {
            StartupStateManager.EnsureInitialized();
            var (sourceExe, installDir, installedExe) = PrepareInstalledCopy();
            try { RemoveScheduledTask(); } catch { }
            InstallStandardShortcut(installedExe, installDir);

            lines.Add("Success: True");
            lines.Add("ActiveMode: StartupFolderStandard");
            lines.Add($"Source build: {sourceExe}");
            lines.Add($"Installed target: {installedExe}");
            lines.Add($"Startup shortcut: {GetShortcutPath()}");
            lines.Add("Arguments: --startup");
            lines.Add($"Startup state: {StartupStateManager.StatePath}");
            WriteReport(lines);
            return 0;
        }
        catch (Exception ex)
        {
            lines.Add("Success: False");
            lines.Add("Error: " + ex);
            try { WriteReport(lines); } catch { }
            return 1;
        }
    }

    public static int RemoveStartupRegistration()
    {
        try
        {
            try { RemoveScheduledTask(); } catch { }
            try { RemoveStartupShortcut(); } catch { }
            try { RemoveLegacyRunEntries(); } catch { }
            return 0;
        }
        catch { return 1; }
    }

    private static void RemoveLegacyRunEntries()
    {
        const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        using var runKey = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true);
        if (runKey is null) return;

        foreach (var name in new[] { "DesktopClockWeatherV11", "DesktopClockWeather", "Desktop Clock Weather" })
        {
            try { runKey.DeleteValue(name, throwOnMissingValue: false); } catch { }
        }
    }

    private static List<string> NewReportHeader(string method) => new()
    {
        "Desktop Clock Weather V1.1 startup registration",
        $"Build: {BuildId}",
        $"Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
        method,
        "No VBScript/cscript.exe; no HKCU Run"
    };

    private static (string SourceExe, string InstallDir, string InstalledExe) PrepareInstalledCopy()
    {
        Directory.CreateDirectory(ProfileManager.VersionFolder);

        var sourceExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(sourceExe) || !File.Exists(sourceExe))
            throw new FileNotFoundException("Current executable path could not be resolved.", sourceExe);

        var installDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopClockWeather", "V1.1");
        Directory.CreateDirectory(installDir);
        var installedExe = Path.Combine(installDir, InstalledExeName);

        if (!Path.GetFullPath(sourceExe).Equals(Path.GetFullPath(installedExe), StringComparison.OrdinalIgnoreCase))
            File.Copy(sourceExe, installedExe, overwrite: true);
        if (!File.Exists(installedExe))
            throw new IOException("Installed executable was not created.");

        return (sourceExe, installDir, installedExe);
    }

    private static void InstallScheduledTask(string installedExe, string installDir)
    {
        var serviceType = Type.GetTypeFromProgID("Schedule.Service")
            ?? throw new InvalidOperationException("Windows Task Scheduler COM service is unavailable.");
        dynamic? service = null;
        dynamic? root = null;
        dynamic? definition = null;
        dynamic? trigger = null;
        dynamic? action = null;
        try
        {
            service = Activator.CreateInstance(serviceType)
                ?? throw new InvalidOperationException("Could not create Task Scheduler COM service.");
            service.Connect();
            root = service.GetFolder("\\");

            try { root.DeleteTask(TaskName, 0); } catch { }

            definition = service.NewTask(0);
            definition.RegistrationInfo.Description = "Desktop Clock Weather V1.1 fast startup launcher";
            definition.Settings.Enabled = true;
            definition.Settings.StartWhenAvailable = true;
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.ExecutionTimeLimit = "PT2M";
            definition.Settings.MultipleInstances = 2; // TASK_INSTANCES_IGNORE_NEW

            var userId = $"{Environment.UserDomainName}\\{Environment.UserName}";
            definition.Principal.UserId = userId;
            definition.Principal.LogonType = 3; // TASK_LOGON_INTERACTIVE_TOKEN
            definition.Principal.RunLevel = 0;  // LUA / no elevation

            trigger = definition.Triggers.Create(9); // TASK_TRIGGER_LOGON
            trigger.Enabled = true;
            trigger.UserId = userId;
            // Small deterministic delay: enough for Explorer/display stack to settle,
            // while still targeting first visible clock well inside 15 seconds.
            trigger.Delay = "PT3S";

            action = definition.Actions.Create(0); // TASK_ACTION_EXEC
            action.Path = installedExe;
            action.Arguments = "--startup";
            action.WorkingDirectory = installDir;

            // TASK_CREATE_OR_UPDATE=6, TASK_LOGON_INTERACTIVE_TOKEN=3
            root.RegisterTaskDefinition(TaskName, definition, 6, userId, null, 3, null);
        }
        finally
        {
            ReleaseCom(action);
            ReleaseCom(trigger);
            ReleaseCom(definition);
            ReleaseCom(root);
            ReleaseCom(service);
        }
    }

    private static void VerifyScheduledTask(string installedExe)
    {
        var serviceType = Type.GetTypeFromProgID("Schedule.Service")
            ?? throw new InvalidOperationException("Windows Task Scheduler COM service is unavailable during verification.");
        dynamic? service = null;
        dynamic? root = null;
        dynamic? task = null;
        dynamic? definition = null;
        dynamic? action = null;
        try
        {
            service = Activator.CreateInstance(serviceType)
                ?? throw new InvalidOperationException("Could not create Task Scheduler COM service during verification.");
            service.Connect();
            root = service.GetFolder("\\");
            task = root.GetTask(TaskName);
            definition = task.Definition;
            action = definition.Actions.Item(1);

            var path = ((string)action.Path).Trim();
            var args = ((string)action.Arguments).Trim();
            var targetOk = Path.GetFullPath(path).Equals(Path.GetFullPath(installedExe), StringComparison.OrdinalIgnoreCase);
            var argsOk = args.Equals("--startup", StringComparison.OrdinalIgnoreCase);
            if (!targetOk || !argsOk)
                throw new IOException($"Task Scheduler verification failed. targetOk={targetOk} argsOk={argsOk}");
        }
        finally
        {
            ReleaseCom(action);
            ReleaseCom(definition);
            ReleaseCom(task);
            ReleaseCom(root);
            ReleaseCom(service);
        }
    }

    private static void RemoveScheduledTask()
    {
        var serviceType = Type.GetTypeFromProgID("Schedule.Service");
        if (serviceType is null) return;
        dynamic? service = null;
        dynamic? root = null;
        try
        {
            service = Activator.CreateInstance(serviceType);
            if (service is null) return;
            service.Connect();
            root = service.GetFolder("\\");
            try { root.DeleteTask(TaskName, 0); } catch { }
        }
        finally
        {
            ReleaseCom(root);
            ReleaseCom(service);
        }
    }

    private static void InstallStandardShortcut(string installedExe, string installDir)
    {
        var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (string.IsNullOrWhiteSpace(startupFolder))
            throw new InvalidOperationException("Windows Startup folder could not be resolved.");
        Directory.CreateDirectory(startupFolder);

        RemoveLegacyStartupEntries(startupFolder);
        var shortcutPath = GetShortcutPath();
        CreateShortcut(shortcutPath, installedExe, "--startup", installDir,
            "Desktop Clock Weather V1.1 startup launcher", installedExe);

        if (!File.Exists(shortcutPath))
            throw new IOException("Startup shortcut was not created.");
        var verification = ReadShortcut(shortcutPath);
        var targetOk = Path.GetFullPath(verification.TargetPath)
            .Equals(Path.GetFullPath(installedExe), StringComparison.OrdinalIgnoreCase);
        var argsOk = verification.Arguments.Trim().Equals("--startup", StringComparison.OrdinalIgnoreCase);
        if (!targetOk || !argsOk)
            throw new IOException($"Shortcut verification failed. targetOk={targetOk} argsOk={argsOk}");
    }

    private static void RemoveStartupShortcut()
    {
        var path = GetShortcutPath();
        if (File.Exists(path)) File.Delete(path);
    }

    private static string GetShortcutPath()
    {
        var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (string.IsNullOrWhiteSpace(startupFolder))
            throw new InvalidOperationException("Windows Startup folder could not be resolved.");
        return Path.Combine(startupFolder, ShortcutFileName);
    }

    private static void RemoveLegacyStartupEntries(string startupFolder)
    {
        foreach (var file in Directory.EnumerateFiles(startupFolder))
        {
            var name = Path.GetFileName(file).ToLowerInvariant();
            if (name.Contains("desktopclockweather") ||
                name.Contains("desktop clock weather") ||
                name.Contains("start_desktopclockweather"))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }

    public static IReadOnlyList<string> InspectLegacyRegistrations()
    {
        var results = new List<string>();
        try
        {
            const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using var runKey = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: false);
            if (runKey is not null)
            {
                foreach (var name in new[] { "DesktopClockWeatherV11", "DesktopClockWeather", "Desktop Clock Weather" })
                {
                    var value = runKey.GetValue(name)?.ToString();
                    if (!string.IsNullOrWhiteSpace(value)) results.Add($"HKCU Run: {name} = {value}");
                }
            }
        }
        catch (Exception ex) { results.Add("HKCU Run inspection failed: " + ex.Message); }

        try
        {
            var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (Directory.Exists(startupFolder))
            {
                foreach (var path in Directory.EnumerateFiles(startupFolder))
                {
                    var lower = Path.GetFileName(path).ToLowerInvariant();
                    if (lower.Contains("desktopclockweather") || lower.Contains("desktop clock weather") || lower.Contains("start_desktopclockweather"))
                        results.Add("Startup folder: " + path);
                }
            }
        }
        catch (Exception ex) { results.Add("Startup folder inspection failed: " + ex.Message); }
        return results;
    }

    private static void WriteReport(IEnumerable<string> lines)
    {
        Directory.CreateDirectory(ProfileManager.VersionFolder);
        File.WriteAllLines(ReportPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void ReleaseCom(object? obj)
    {
        if (obj is not null && Marshal.IsComObject(obj))
        {
            try { Marshal.FinalReleaseComObject(obj); } catch { }
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string workingDirectory, string description, string iconPath)
    {
        var link = (IShellLinkW)new ShellLink();
        try
        {
            link.SetPath(targetPath);
            link.SetArguments(arguments);
            link.SetWorkingDirectory(workingDirectory);
            link.SetDescription(description);
            link.SetIconLocation(iconPath, 0);
            ((IPersistFile)link).Save(shortcutPath, true);
        }
        finally
        {
            if (Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link);
        }
    }

    private static (string TargetPath, string Arguments) ReadShortcut(string shortcutPath)
    {
        var link = (IShellLinkW)new ShellLink();
        try
        {
            ((IPersistFile)link).Load(shortcutPath, 0);
            var path = new StringBuilder(32768);
            var args = new StringBuilder(4096);
            link.GetPath(path, path.Capacity, IntPtr.Zero, 0);
            link.GetArguments(args, args.Capacity);
            return (path.ToString(), args.ToString());
        }
        finally
        {
            if (Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName, bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
