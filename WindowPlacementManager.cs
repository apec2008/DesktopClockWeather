using System.Windows;

namespace DesktopClockWeather;

internal sealed class WindowPlacementManager
{
    private readonly string _profile;
    private readonly ProfileManager _profiles;
    private ClockSettings _settings;

    public WindowPlacementManager(string profile, ClockSettings settings, ProfileManager profiles)
    {
        _profile = profile;
        _settings = settings;
        _profiles = profiles;
    }

    public void UpdateSettings(ClockSettings settings) => _settings = settings;

    public void ApplyInitialPlacement(Window window, int slotIndex, bool isNewProfile)
    {
        var hasSavedPlacement = !double.IsNaN(_settings.Left) && !double.IsNaN(_settings.Top);

        // Fresh V1.1 secondary/tertiary profiles must never appear almost on top
        // of the previous clock. Stack them below the previous slot with a small
        // gap. Saved user placement still wins on all later launches.
        if (slotIndex > 0 && (isNewProfile || !hasSavedPlacement))
        {
            StackBelowPrevious(window, slotIndex);
            return;
        }

        Restore(window, "InitialRestore");
    }

    public bool Restore(Window window, string reason)
    {
        var monitor = DisplayTopologyService.FindBestMonitor(_settings);
        var w = _settings.ActualWidth;
        var h = _settings.ActualHeight;
        var work = monitor.WorkAreaDip;

        EnsureAnchorModel(work, w, h);

        double left;
        double top;
        if (_settings.HasAnchoredPlacement)
        {
            left = _settings.HorizontalAnchor.Equals("Left", StringComparison.OrdinalIgnoreCase)
                ? work.Left + _settings.HorizontalOffset
                : work.Right - w - _settings.HorizontalOffset;
            top = _settings.VerticalAnchor.Equals("Bottom", StringComparison.OrdinalIgnoreCase)
                ? work.Bottom - h - _settings.VerticalOffset
                : work.Top + _settings.VerticalOffset;
        }
        else if (!double.IsNaN(_settings.Left) && !double.IsNaN(_settings.Top))
        {
            left = _settings.Left;
            top = _settings.Top;
        }
        else
        {
            left = work.Right - w - 12;
            top = work.Top + 12;
        }

        // Keep at least 40 DIPs of the window reachable on the target work area.
        left = Math.Clamp(left, work.Left - w + 40, work.Right - 40);
        top = Math.Clamp(top, work.Top - h + 40, work.Bottom - 40);

        window.Left = left;
        window.Top = top;
        _settings.MonitorDeviceName = monitor.DeviceName;

        DiagnosticsLog.Event(_profile, LifecycleState.Ready, "PlacementResolved",
            $"reason={reason} monitor={monitor.DeviceName} work=({work.Left:0},{work.Top:0},{work.Width:0},{work.Height:0}) pos=({left:0.0},{top:0.0}) anchor={_settings.HorizontalAnchor}/{_settings.VerticalAnchor} offset=({_settings.HorizontalOffset:0.0},{_settings.VerticalOffset:0.0})");
        return true;
    }

    public void CaptureUserPlacement(Window window)
    {
        var monitor = DisplayTopologyService.GetMonitors()
            .FirstOrDefault(m => m.WorkAreaDip.Contains(new Point(window.Left + window.ActualWidth / 2, window.Top + window.ActualHeight / 2)))
            ?? DisplayTopologyService.FindBestMonitor(_settings);

        var work = monitor.WorkAreaDip;
        var w = window.ActualWidth > 0 ? window.ActualWidth : _settings.ActualWidth;
        var h = window.ActualHeight > 0 ? window.ActualHeight : _settings.ActualHeight;
        var centerX = window.Left + w / 2;
        var centerY = window.Top + h / 2;

        _settings.HorizontalAnchor = centerX < work.Left + work.Width / 2 ? "Left" : "Right";
        _settings.VerticalAnchor = centerY < work.Top + work.Height / 2 ? "Top" : "Bottom";
        _settings.HorizontalOffset = _settings.HorizontalAnchor == "Left"
            ? window.Left - work.Left
            : work.Right - (window.Left + w);
        _settings.VerticalOffset = _settings.VerticalAnchor == "Top"
            ? window.Top - work.Top
            : work.Bottom - (window.Top + h);
        _settings.HasAnchoredPlacement = true;
        _settings.Left = window.Left;
        _settings.Top = window.Top;
        _settings.MonitorDeviceName = monitor.DeviceName;
        _profiles.Save(_settings, "UserDrag");

        DiagnosticsLog.Event(_profile, LifecycleState.Running, "UserDragSaved",
            $"monitor={monitor.DeviceName} pos=({window.Left:0.0},{window.Top:0.0}) anchor={_settings.HorizontalAnchor}/{_settings.VerticalAnchor}");
    }

    public void RepositionAfterScaleChangeFromVisualTopRight(
        Window window,
        Point capturedVisualScreenAnchor,
        Point newVisualLocalAnchor)
    {
        // The user visually aligns the clock content, not the transparent Window edge.
        // Keep that visible top-right point invariant across scale changes.
        var left = capturedVisualScreenAnchor.X - newVisualLocalAnchor.X;
        var top = capturedVisualScreenAnchor.Y - newVisualLocalAnchor.Y;
        var w = _settings.ActualWidth;
        var h = _settings.ActualHeight;

        window.Width = w;
        window.Height = h;
        window.Left = left;
        window.Top = top;

        // Persist the final Window geometry using the normal placement model.
        // Monitor metadata is only for later restore; it must not alter the visual
        // anchor captured above. No clamping is performed during the scale operation.
        var monitors = DisplayTopologyService.GetMonitors();
        var windowCenter = new Point(left + w / 2, top + h / 2);
        var monitor = monitors.FirstOrDefault(m => m.WorkAreaDip.Contains(windowCenter))
            ?? DisplayTopologyService.FindBestMonitor(_settings);

        var work = monitor.WorkAreaDip;
        _settings.HorizontalAnchor = windowCenter.X < work.Left + work.Width / 2 ? "Left" : "Right";
        _settings.VerticalAnchor = windowCenter.Y < work.Top + work.Height / 2 ? "Top" : "Bottom";
        _settings.HorizontalOffset = _settings.HorizontalAnchor == "Left"
            ? left - work.Left
            : work.Right - (left + w);
        _settings.VerticalOffset = _settings.VerticalAnchor == "Top"
            ? top - work.Top
            : work.Bottom - (top + h);
        _settings.HasAnchoredPlacement = true;
        _settings.Left = left;
        _settings.Top = top;
        _settings.MonitorDeviceName = monitor.DeviceName;
        _profiles.Save(_settings, "ScaleVisualTopRightAnchor");

        DiagnosticsLog.Event(_profile, LifecycleState.Running, "ScaleVisualTopRightAnchorApplied",
            $"screenAnchor=({capturedVisualScreenAnchor.X:0.0},{capturedVisualScreenAnchor.Y:0.0}) " +
            $"newLocal=({newVisualLocalAnchor.X:0.0},{newVisualLocalAnchor.Y:0.0}) " +
            $"window=({left:0.0},{top:0.0},{w:0.0},{h:0.0}) monitor={monitor.DeviceName}");
    }

    private void StackBelowPrevious(Window window, int slotIndex)
    {
        var previousProfile = slotIndex == 1 ? "Default" : $"Default{slotIndex}";
        var previous = _profiles.Load(previousProfile);
        var monitor = DisplayTopologyService.FindBestMonitor(previous);
        var work = monitor.WorkAreaDip;

        var previousLeft = !double.IsNaN(previous.Left)
            ? previous.Left
            : work.Right - previous.ActualWidth - 12;
        var previousTop = !double.IsNaN(previous.Top)
            ? previous.Top
            : work.Top + 12;

        const double gap = 12;
        var w = _settings.ActualWidth;
        var h = _settings.ActualHeight;
        var left = previousLeft + previous.ActualWidth - w; // right-align visible windows
        var top = previousTop + previous.ActualHeight + gap;

        // Keep the new clock wholly inside the target work area when practical.
        left = Math.Clamp(left, work.Left, Math.Max(work.Left, work.Right - w));
        top = Math.Clamp(top, work.Top, Math.Max(work.Top, work.Bottom - h));

        window.Left = left;
        window.Top = top;
        _settings.Left = left;
        _settings.Top = top;
        _settings.MonitorDeviceName = monitor.DeviceName;
        _settings.HasAnchoredPlacement = false;
        CaptureUserPlacement(window);

        DiagnosticsLog.Event(_profile, LifecycleState.StartupRestore, "FreshProfileStackPlacement",
            $"from={previousProfile} pos=({left:0.0},{top:0.0}) gap={gap:0} monitor={monitor.DeviceName}");
    }

    private void EnsureAnchorModel(Rect work, double w, double h)
    {
        if (_settings.HasAnchoredPlacement &&
            !double.IsNaN(_settings.HorizontalOffset) &&
            !double.IsNaN(_settings.VerticalOffset))
            return;

        if (double.IsNaN(_settings.Left) || double.IsNaN(_settings.Top))
            return;

        var centerX = _settings.Left + w / 2;
        var centerY = _settings.Top + h / 2;
        _settings.HorizontalAnchor = centerX < work.Left + work.Width / 2 ? "Left" : "Right";
        _settings.VerticalAnchor = centerY < work.Top + work.Height / 2 ? "Top" : "Bottom";
        _settings.HorizontalOffset = _settings.HorizontalAnchor == "Left"
            ? _settings.Left - work.Left
            : work.Right - (_settings.Left + w);
        _settings.VerticalOffset = _settings.VerticalAnchor == "Top"
            ? _settings.Top - work.Top
            : work.Bottom - (_settings.Top + h);
        _settings.HasAnchoredPlacement = true;
        _profiles.Save(_settings, "PlacementModelMigration");
    }
}
