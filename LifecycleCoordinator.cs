using System.Windows.Interop;
using System.Windows.Threading;

namespace DesktopClockWeather;

internal sealed class LifecycleCoordinator : IDisposable
{
    private const int WM_DISPLAYCHANGE = 0x007E;
    private const int WM_POWERBROADCAST = 0x0218;
    private const int PBT_APMRESUMECRITICAL = 0x0006;
    private const int PBT_APMRESUMESUSPEND = 0x0007;
    private const int PBT_APMRESUMEAUTOMATIC = 0x0012;

    private readonly string _profile;
    private readonly System.Windows.Window _window;
    private readonly WindowPlacementManager _placement;
    private readonly Func<CancellationToken, Task<bool>> _refreshWeather;
    private readonly DispatcherTimer _heartbeat;
    private HwndSource? _source;
    private CancellationTokenSource? _recoveryCts;
    private DateTime _lastHeartbeatUtc = DateTime.UtcNow;
    private DateTime _lastRecoveryUtc = DateTime.MinValue;

    public LifecycleState State { get; private set; } = LifecycleState.ColdStart;

    public LifecycleCoordinator(
        string profile,
        System.Windows.Window window,
        WindowPlacementManager placement,
        Func<CancellationToken, Task<bool>> refreshWeather)
    {
        _profile = profile;
        _window = window;
        _placement = placement;
        _refreshWeather = refreshWeather;
        _heartbeat = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _heartbeat.Tick += Heartbeat_Tick;
    }

    public async Task InitializeAsync(int slotIndex, bool isNewProfile, CancellationToken ct = default)
    {
        State = LifecycleState.StartupRestore;
        DiagnosticsLog.Event(_profile, State, "WindowCreatedHidden", $"slot={slotIndex} new={isNewProfile}");

        _source = HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle);
        _source?.AddHook(WndProc);
        _heartbeat.Start();

        await WaitForDisplayStabilityAsync(TimeSpan.FromSeconds(8), ct);
        _placement.ApplyInitialPlacement(_window, slotIndex, isNewProfile);
        State = LifecycleState.Ready;
        DiagnosticsLog.Event(_profile, State, "StartupPlacementReady", DisplayTopologyService.Fingerprint());
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_DISPLAYCHANGE)
        {
            DiagnosticsLog.Event(_profile, State, "DisplayChanged", DisplayTopologyService.Fingerprint());
            ScheduleRecovery("DisplayChanged", weather: false);
        }
        else if (msg == WM_POWERBROADCAST)
        {
            var code = wParam.ToInt32();
            if (code is PBT_APMRESUMECRITICAL or PBT_APMRESUMESUSPEND or PBT_APMRESUMEAUTOMATIC)
            {
                DiagnosticsLog.Event(_profile, LifecycleState.Resuming, "ResumeDetected", $"code={code}");
                ScheduleRecovery($"PowerBroadcast:{code}", weather: true);
            }
        }
        return IntPtr.Zero;
    }

    private void Heartbeat_Tick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var gap = now - _lastHeartbeatUtc;
        _lastHeartbeatUtc = now;
        if (gap > TimeSpan.FromSeconds(45))
        {
            DiagnosticsLog.Event(_profile, LifecycleState.Resuming, "HeartbeatResumeDetected", $"gap={gap.TotalSeconds:0}s");
            ScheduleRecovery($"Heartbeat:{gap.TotalSeconds:0}s", weather: true);
        }
    }

    private void ScheduleRecovery(string reason, bool weather)
    {
        var now = DateTime.UtcNow;
        if (now - _lastRecoveryUtc < TimeSpan.FromMilliseconds(800)) return;
        _lastRecoveryUtc = now;

        _recoveryCts?.Cancel();
        _recoveryCts?.Dispose();
        _recoveryCts = new CancellationTokenSource();
        _ = RecoverAsync(reason, weather, _recoveryCts.Token);
    }

    private async Task RecoverAsync(string reason, bool weather, CancellationToken ct)
    {
        try
        {
            State = weather ? LifecycleState.Resuming : LifecycleState.DisplayReconfiguration;
            DiagnosticsLog.Event(_profile, State, "RecoveryStart", $"reason={reason}");

            await Task.Delay(800, ct); // debounce the first burst
            await WaitForDisplayStabilityAsync(TimeSpan.FromSeconds(12), ct);
            var placementOk = _placement.Restore(_window, reason);
            var weatherOk = !weather || await _refreshWeather(ct);

            State = LifecycleState.Ready;
            DiagnosticsLog.Event(_profile, State, "RecoveryComplete", $"placement={placementOk} weather={weatherOk}");
        }
        catch (OperationCanceledException)
        {
            DiagnosticsLog.Event(_profile, State, "RecoveryCancelled", reason);
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Event(_profile, State, "RecoveryFailed", ex.ToString());
        }
    }

    private async Task WaitForDisplayStabilityAsync(TimeSpan maxWait, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        string? previous = null;
        var stableCount = 0;
        while (DateTime.UtcNow - started < maxWait)
        {
            var current = DisplayTopologyService.Fingerprint();
            DiagnosticsLog.Event(_profile, State, "DisplayTopologySnapshot", current);
            if (string.Equals(previous, current, StringComparison.Ordinal))
                stableCount++;
            else
                stableCount = 0;

            if (stableCount >= 2) return; // three identical samples total
            previous = current;
            await Task.Delay(600, ct);
        }
    }

    public void Dispose()
    {
        _heartbeat.Stop();
        _recoveryCts?.Cancel();
        _recoveryCts?.Dispose();
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
    }
}
