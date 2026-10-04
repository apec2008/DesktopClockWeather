using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace DesktopClockWeather;

internal sealed record MonitorSnapshot(
    string DeviceName,
    Rect WorkAreaDip,
    bool IsPrimary,
    uint DpiX,
    uint DpiY);

internal static class DisplayTopologyService
{
    private const uint MONITORINFOF_PRIMARY = 1;
    private const int MDT_EFFECTIVE_DPI = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr rect, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    public static IReadOnlyList<MonitorSnapshot> GetMonitors()
    {
        var list = new List<MonitorSnapshot>();
        try
        {
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, _, _) =>
            {
                var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>(), szDevice = string.Empty };
                if (!GetMonitorInfo(hMonitor, ref info)) return true;

                uint dpiX = 96, dpiY = 96;
                try
                {
                    if (GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out var x, out var y) == 0)
                    {
                        dpiX = x; dpiY = y;
                    }
                }
                catch { }

                var sx = 96.0 / Math.Max(1, dpiX);
                var sy = 96.0 / Math.Max(1, dpiY);
                var work = new Rect(
                    info.rcWork.Left * sx,
                    info.rcWork.Top * sy,
                    (info.rcWork.Right - info.rcWork.Left) * sx,
                    (info.rcWork.Bottom - info.rcWork.Top) * sy);

                list.Add(new MonitorSnapshot(
                    info.szDevice,
                    work,
                    (info.dwFlags & MONITORINFOF_PRIMARY) != 0,
                    dpiX,
                    dpiY));
                return true;
            }, IntPtr.Zero);
        }
        catch { }

        if (list.Count == 0)
            list.Add(new MonitorSnapshot("PRIMARY", SystemParameters.WorkArea, true, 96, 96));
        return list;
    }

    public static string Fingerprint()
    {
        var monitors = GetMonitors()
            .OrderBy(m => m.DeviceName, StringComparer.OrdinalIgnoreCase)
            .Select(m => $"{m.DeviceName}:{m.WorkAreaDip.X:0},{m.WorkAreaDip.Y:0},{m.WorkAreaDip.Width:0},{m.WorkAreaDip.Height:0}:{m.DpiX}:{m.IsPrimary}");
        return string.Join("|", monitors);
    }

    public static MonitorSnapshot FindBestMonitor(ClockSettings settings)
    {
        var monitors = GetMonitors();
        var exact = monitors.FirstOrDefault(m => m.DeviceName.Equals(settings.MonitorDeviceName, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        if (!double.IsNaN(settings.Left) && !double.IsNaN(settings.Top))
        {
            var point = new Point(settings.Left, settings.Top);
            var containing = monitors.FirstOrDefault(m => m.WorkAreaDip.Contains(point));
            if (containing is not null) return containing;

            return monitors.OrderBy(m => DistanceSquared(point, Center(m.WorkAreaDip))).First();
        }

        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }

    private static Point Center(Rect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);
    private static double DistanceSquared(Point a, Point b)
    {
        var dx = a.X - b.X; var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
