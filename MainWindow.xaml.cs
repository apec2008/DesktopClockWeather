using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DesktopClockWeather;

public partial class MainWindow : Window
{
    private readonly string _profile;
    private readonly int _slotIndex;
    private readonly bool _isNewProfile;
    private readonly ProfileManager _profiles;
    private readonly WindowPlacementManager _placement;
    private readonly WeatherCoordinator _weather;
    private readonly ClockViewModel _vm = new();
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _weatherTimer;
    private readonly DispatcherTimer _hoverTimer;
    private LifecycleCoordinator? _lifecycle;
    private ClockSettings _settings;

    internal MainWindow(
        string profile,
        int slotIndex,
        bool isNewProfile,
        ClockSettings settings,
        ProfileManager profiles)
    {
        InitializeComponent();
        _profile = profile;
        _slotIndex = slotIndex;
        _isNewProfile = isNewProfile;
        _settings = settings;
        _profiles = profiles;
        _placement = new WindowPlacementManager(profile, settings, profiles);
        _weather = new WeatherCoordinator(profile, profiles);

        // V1.1 rule: no default/placeholder state is allowed to flash on screen.
        Opacity = 0;
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        _clockTimer = new DispatcherTimer();
        _clockTimer.Tick += (_, _) => UpdateClock();

        _weatherTimer = new DispatcherTimer();
        _weatherTimer.Tick += async (_, _) => await RefreshWeatherAsync(CancellationToken.None);

        _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _hoverTimer.Tick += (_, _) => RefreshHoverToolbar();

        ApplyAllSettingsToView();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            DiagnosticsLog.Event(_profile, LifecycleState.ColdStart, "ProfileAppliedBeforeShow",
                $"city={_settings.City} color={_settings.CityColor} scale={_settings.ScalePercent}%");

            _lifecycle = new LifecycleCoordinator(_profile, this, _placement, RefreshWeatherAsync);
            await _lifecycle.InitializeAsync(_slotIndex, _isNewProfile);

            UpdateAlignedRows();
            Opacity = 1;
            DiagnosticsLog.Event(_profile, LifecycleState.Ready, "WindowShown",
                $"pos=({Left:0.0},{Top:0.0}) size=({Width:0.0},{Height:0.0}) city={_settings.City} color={_settings.CityColor} scale={_settings.ScalePercent}%");
            StartupReadySignal.Signal(_profile);
            // One source of truth for reboot restore: the set of windows the user
            // currently keeps open. Startup-launched windows are already in this set;
            // manual launches are added here.
            StartupStateManager.MarkProfileOpen(_profile);

            _clockTimer.Start();
            _weatherTimer.Start();
            _hoverTimer.Start();

            // Fresh weather is intentionally asynchronous after the fully configured
            // cached third row is already visible.
            _ = RefreshWeatherAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Event(_profile, LifecycleState.ColdStart, "StartupInitializationFailed", ex.ToString());
            Opacity = 1; // fail visible rather than leaving an invisible process
            MessageBox.Show(
                $"Clock profile '{_profile}' could not finish startup.\n\n{ex.Message}\n\nSee the V1.1 diagnostic log for details.",
                "Desktop Clock Weather - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _clockTimer.Stop();
        _weatherTimer.Stop();
        _hoverTimer.Stop();
        _lifecycle?.Dispose();
        _weather.Dispose();
        StartupStateManager.MarkProfileClosedByUser(_profile);
    }

    private void ApplyAllSettingsToView()
    {
        ApplyScale();
        ApplyConfiguredColors();
        ConfigureTimers();
        UpdateClock();
        RenderCachedWeather();
    }

    private void ApplyScale()
    {
        var scale = _settings.ScaleFactor;
        Root.LayoutTransform = new ScaleTransform(scale, scale);
        Width = _settings.ActualWidth;
        Height = _settings.ActualHeight;
        DiagnosticsLog.Event(_profile, LifecycleState.Running, "ScaleApplied", $"percent={_settings.ScalePercent} factor={scale:0.00} size=({Width:0},{Height:0})");
    }

    private void ConfigureTimers()
    {
        _clockTimer.Interval = _settings.ShowSeconds ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(10);
        _weatherTimer.Interval = TimeSpan.FromMinutes(Math.Clamp(_settings.WeatherRefreshMinutes, 10, 180));
    }

    private DateTime GetLocalTime()
    {
        try
        {
            var tz = TimeZoneHelper.FindTimeZone(_settings.TimeZoneId);
            return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime
                .AddHours(_settings.ManualTimeOffsetHours);
        }
        catch
        {
            return DateTime.Now.AddHours(_settings.ManualTimeOffsetHours);
        }
    }

    private void UpdateClock()
    {
        var t = GetLocalTime();
        var format = _settings.Use12Hour
            ? (_settings.ShowSeconds ? "h:mm:ss tt" : "h:mm tt")
            : (_settings.ShowSeconds ? "HH:mm:ss" : "HH:mm");

        _vm.TimeText = t.ToString(format, CultureInfo.InvariantCulture);
        _vm.DateText = t.ToString("d MMMM · dddd", CultureInfo.InvariantCulture).ToUpperInvariant();
        _vm.YearText = t.Year.ToString(CultureInfo.InvariantCulture);

        TimeText.Text = TimeShadow.Text = _vm.TimeText;
        DateLineText.Text = DateLineShadow.Text = _vm.DateText;
        YearText.Text = YearShadow.Text = _vm.YearText;
        YearText.Visibility = YearShadow.Visibility = _settings.ShowYear ? Visibility.Visible : Visibility.Collapsed;

        var weatherVisibility = _settings.ShowWeather ? Visibility.Visible : Visibility.Collapsed;
        CityText.Visibility = CityShadow.Visibility = weatherVisibility;
        WeatherIconText.Visibility = WeatherIconShadow.Visibility = weatherVisibility;
        WeatherText.Visibility = WeatherShadow.Visibility = weatherVisibility;
        UpdateAlignedRows();
    }

    private void UpdateAlignedRows()
    {
        // Layout values are in canonical (unscaled) DIPs because Root.LayoutTransform
        // uniformly scales the entire widget afterward.
        var totalWidth = Math.Max(320.0, TimeRow.ActualWidth);
        DateRow.Width = totalWidth;
        DateViewbox.Width = totalWidth;
        WeatherRow.Width = totalWidth;
    }

    private void RenderCachedWeather()
    {
        _vm.CityText = (_settings.City ?? string.Empty).Trim().ToUpperInvariant();
        var unit = _settings.TemperatureUnit.Equals("F", StringComparison.OrdinalIgnoreCase) ? "F" : "C";
        var unitSymbol = unit == "F" ? "℉" : "℃";

        if (_settings.LastWeatherCurrent.HasValue)
        {
            _vm.WeatherText = $"{Math.Round(_settings.LastWeatherCurrent.Value):0}{unitSymbol}";
            _vm.WeatherSymbol = string.IsNullOrWhiteSpace(_settings.LastWeatherSymbol) ? "–" : _settings.LastWeatherSymbol;
            _vm.WeatherSymbolColor = string.IsNullOrWhiteSpace(_settings.LastWeatherSymbolColor) ? "#FFD7DCE2" : _settings.LastWeatherSymbolColor;
            if (_settings.LastWeatherMax.HasValue && _settings.LastWeatherMin.HasValue)
                _vm.WeatherTooltip = BuildWeatherTooltip(
                    _settings.City,
                    _settings.LastWeatherCurrent.Value,
                    _settings.LastWeatherMax.Value,
                    _settings.LastWeatherMin.Value,
                    unitSymbol);
        }
        else
        {
            _vm.WeatherText = $"--{unitSymbol}";
            _vm.WeatherSymbol = "–";
            _vm.WeatherSymbolColor = "#FFD7DCE2";
            _vm.WeatherTooltip = "";
        }

        RenderViewModelWeather();
        DiagnosticsLog.Event(_profile, LifecycleState.ColdStart, "WeatherCacheUsed",
            _settings.LastWeatherCurrent.HasValue ? $"value={_vm.WeatherText}" : "no-cache");
    }

    private void RenderWeather(WeatherSnapshot snapshot)
    {
        var unit = _settings.TemperatureUnit.Equals("F", StringComparison.OrdinalIgnoreCase) ? "F" : "C";
        var unitSymbol = unit == "F" ? "℉" : "℃";
        _vm.CityText = (_settings.City ?? string.Empty).Trim().ToUpperInvariant();
        _vm.WeatherText = $"{Math.Round(snapshot.Current):0}{unitSymbol}";
        _vm.WeatherSymbol = snapshot.Symbol;
        _vm.WeatherSymbolColor = snapshot.SymbolColor;
        _vm.WeatherTooltip = BuildWeatherTooltip(
            _settings.City, snapshot.Current, snapshot.Max, snapshot.Min, unitSymbol);
        RenderViewModelWeather();
    }

    private static string BuildWeatherTooltip(string? city, double current, double max, double min, string unitSymbol)
    {
        var displayCity = string.IsNullOrWhiteSpace(city) ? "" : city.Trim();
        var isKoreanUi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals("ko", StringComparison.OrdinalIgnoreCase);

        return isKoreanUi
            ? $"{displayCity}: 현재 {current:0.#}{unitSymbol}, 최고 {max:0.#}{unitSymbol}, 최저 {min:0.#}{unitSymbol}"
            : $"{displayCity}: Current {current:0.#}{unitSymbol}, High {max:0.#}{unitSymbol}, Low {min:0.#}{unitSymbol}";
    }

    private void RenderViewModelWeather()
    {
        CityText.Text = CityShadow.Text = _vm.CityText;
        // V1.1: when the city label is shortened with an ellipsis,
        // hovering the visible city text shows the complete city name.
        CityText.ToolTip = _vm.CityText.Length >= 6 ? _vm.CityText : null;
        WeatherText.Text = WeatherShadow.Text = _vm.WeatherText;
        WeatherIconText.Text = WeatherIconShadow.Text = _vm.WeatherSymbol;
        WeatherIconText.Foreground = BrushFromHex(_vm.WeatherSymbolColor, Brushes.LightGray);
        WeatherText.ToolTip = string.IsNullOrWhiteSpace(_vm.WeatherTooltip) ? null : _vm.WeatherTooltip;
        ApplyConfiguredColors();
    }

    private async Task<bool> RefreshWeatherAsync(CancellationToken ct)
    {
        if (!_settings.ShowWeather) return true;
        var snapshot = await _weather.RefreshAsync(_settings, ct);
        if (snapshot is null)
        {
            RenderCachedWeather();
            return false;
        }
        RenderWeather(snapshot);
        return true;
    }

    private static Brush BrushFromHex(string value, Brush fallback)
    {
        try { return (Brush)new BrushConverter().ConvertFromString(value)!; }
        catch { return fallback; }
    }

    private static Brush SubtleGlowBrush(string value)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(value)!;
            color.A = 0x82;
            return new SolidColorBrush(color);
        }
        catch { return new SolidColorBrush(Color.FromArgb(0x82, 0, 0, 0)); }
    }

    private void ApplyConfiguredColors()
    {
        var fontBrush = BrushFromHex(_settings.FontColor, Brushes.White);
        var cityBrush = BrushFromHex(_settings.CityColor, Brushes.LightBlue);
        var glowBrush = SubtleGlowBrush(_settings.GlowColor);

        TimeText.Foreground = YearText.Foreground = DateLineText.Foreground = WeatherText.Foreground = fontBrush;
        CityText.Foreground = cityBrush;
        TimeShadow.Foreground = YearShadow.Foreground = DateLineShadow.Foreground = CityShadow.Foreground = WeatherIconShadow.Foreground = WeatherShadow.Foreground = glowBrush;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    private bool IsCursorOverTemperature(POINT cursor)
    {
        try
        {
            if (!WeatherText.IsVisible || WeatherText.ActualWidth <= 0) return false;
            var a = WeatherText.PointToScreen(new Point(0, 0));
            var b = WeatherText.PointToScreen(new Point(WeatherText.ActualWidth, WeatherText.ActualHeight));
            return cursor.X >= a.X && cursor.X <= b.X && cursor.Y >= a.Y && cursor.Y <= b.Y;
        }
        catch { return false; }
    }

    private bool IsCursorOverCity(POINT cursor)
    {
        try
        {
            if (!CityText.IsVisible || CityText.ActualWidth <= 0) return false;
            var a = CityText.PointToScreen(new Point(0, 0));
            var b = CityText.PointToScreen(new Point(CityText.ActualWidth, CityText.ActualHeight));
            return cursor.X >= a.X && cursor.X <= b.X && cursor.Y >= a.Y && cursor.Y <= b.Y;
        }
        catch { return false; }
    }

    private void RefreshHoverToolbar()
    {
        try
        {
            if (!IsLoaded || !GetCursorPos(out var cursor)) return;
            if (IsCursorOverTemperature(cursor) || IsCursorOverCity(cursor))
            {
                HoverToolbar.Visibility = Visibility.Collapsed;
                return;
            }
            var a = PointToScreen(new Point(0, 0));
            var b = PointToScreen(new Point(ActualWidth, ActualHeight));
            var inside = cursor.X >= a.X && cursor.X <= b.X && cursor.Y >= a.Y && cursor.Y <= b.Y;
            HoverToolbar.Visibility = inside ? Visibility.Visible : Visibility.Collapsed;
        }
        catch { HoverToolbar.Visibility = Visibility.Collapsed; }
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e) => RefreshHoverToolbar();
    private void Window_MouseLeave(object sender, MouseEventArgs e) => RefreshHoverToolbar();

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (HoverToolbar.IsMouseOver || e.LeftButton != MouseButtonState.Pressed) return;
        DragAndPersist(e);
    }

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragAndPersist(e);
        e.Handled = true;
    }

    private void DragAndPersist(MouseButtonEventArgs e)
    {
        try
        {
            DiagnosticsLog.Event(_profile, LifecycleState.Running, "UserDragStart");
            DragMove();
            _placement.CaptureUserPlacement(this);
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Event(_profile, LifecycleState.Running, "UserDragFailed", ex.Message);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var previousScalePercent = _settings.ScalePercent;

        // Preserve the *visible clock content* top-right point, not the transparent
        // WPF Window top-right point. The widget intentionally has transparent margin
        // to the right of the visible content; that margin scales too. Anchoring the
        // Window edge therefore makes the visible clock drift when the scale changes.
        UpdateLayout();
        var previousVisualAnchorLocal = GetVisibleClockTopRightLocalPoint();
        var previousVisualAnchorScreen = new Point(
            Left + previousVisualAnchorLocal.X,
            Top + previousVisualAnchorLocal.Y);

        DiagnosticsLog.Event(_profile, LifecycleState.Running, "ScaleVisualAnchorCaptured",
            $"scale={previousScalePercent}% window=({Left:0.0},{Top:0.0},{Width:0.0},{Height:0.0}) " +
            $"local=({previousVisualAnchorLocal.X:0.0},{previousVisualAnchorLocal.Y:0.0}) " +
            $"screen=({previousVisualAnchorScreen.X:0.0},{previousVisualAnchorScreen.Y:0.0})");

        var dialog = new SettingsWindow(_settings) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _settings = dialog.Result;
            _settings.ProfileName = _profile;
            _profiles.Save(_settings, "SettingsOK");
            _placement.UpdateSettings(_settings);

            ApplyAllSettingsToView();

            if (_settings.ScalePercent != previousScalePercent)
            {
                // Finish the new scaled layout first, measure where the same visible
                // top-right reference point now lies inside the Window, then move the
                // Window by exactly the delta required to put that visible point back
                // on its original screen coordinate. This makes 0 <-> +/-30 reversible
                // and keeps the Settings hover area reachable near the screen edge.
                UpdateLayout();
                var newVisualAnchorLocal = GetVisibleClockTopRightLocalPoint();
                _placement.RepositionAfterScaleChangeFromVisualTopRight(
                    this, previousVisualAnchorScreen, newVisualAnchorLocal);
            }
            else
            {
                _placement.Restore(this, "SettingsChanged");
            }

            await RefreshWeatherAsync(CancellationToken.None);
        }
        RefreshHoverToolbar();
    }

    private Point GetVisibleClockTopRightLocalPoint()
    {
        // TimeRow defines the visual right boundary used by DateRow/WeatherRow.
        // TranslatePoint includes Root.LayoutTransform, so this is the true scaled
        // visible boundary in Window DIPs rather than the transparent Window edge.
        var width = Math.Max(0.0, TimeRow.ActualWidth);
        return TimeRow.TranslatePoint(new Point(width, 0), this);
    }
}
