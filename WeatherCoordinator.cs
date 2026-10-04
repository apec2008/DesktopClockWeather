namespace DesktopClockWeather;

internal sealed class WeatherCoordinator : IDisposable
{
    private readonly string _profile;
    private readonly ProfileManager _profiles;
    private readonly WeatherService _service = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _active;

    public WeatherCoordinator(string profile, ProfileManager profiles)
    {
        _profile = profile;
        _profiles = profiles;
    }

    public async Task<WeatherSnapshot?> RefreshAsync(ClockSettings settings, CancellationToken outerCt = default)
    {
        if (!settings.ShowWeather) return null;

        CancelActive();
        _active = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        _active.CancelAfter(TimeSpan.FromSeconds(12));
        var ct = _active.Token;

        DiagnosticsLog.Event(_profile, LifecycleState.Running, "WeatherRequestStart",
            $"city={settings.City} unit={settings.TemperatureUnit}");

        try
        {
            await _gate.WaitAsync(ct);
            try
            {
                var snapshot = await _service.GetDailyAsync(
                    settings.Latitude,
                    settings.Longitude,
                    TimeZoneHelper.ToIana(settings.TimeZoneId),
                    settings.TemperatureUnit,
                    ct);

                if (snapshot is null) return null;
                settings.LastWeatherCurrent = snapshot.Current;
                settings.LastWeatherMax = snapshot.Max;
                settings.LastWeatherMin = snapshot.Min;
                settings.LastWeatherUnit = settings.TemperatureUnit;
                settings.LastWeatherSymbol = snapshot.Symbol;
                settings.LastWeatherSymbolColor = snapshot.SymbolColor;
                settings.LastWeatherUpdatedUtc = DateTime.UtcNow;
                _profiles.Save(settings, "WeatherCache");
                DiagnosticsLog.Event(_profile, LifecycleState.Running, "WeatherRequestSuccess",
                    $"current={snapshot.Current:0.#}{settings.TemperatureUnit}");
                return snapshot;
            }
            finally { _gate.Release(); }
        }
        catch (OperationCanceledException) when (!outerCt.IsCancellationRequested)
        {
            DiagnosticsLog.Event(_profile, LifecycleState.Running, "WeatherRequestTimeout", "cached value retained");
            return null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            DiagnosticsLog.Event(_profile, LifecycleState.Running, "WeatherRequestFailed", ex.Message);
            return null;
        }
    }

    public void CancelActive()
    {
        try { _active?.Cancel(); } catch { }
        try { _active?.Dispose(); } catch { }
        _active = null;
    }

    public void Dispose()
    {
        CancelActive();
        _gate.Dispose();
    }
}
