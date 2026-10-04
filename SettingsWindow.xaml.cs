using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace DesktopClockWeather;

public partial class SettingsWindow : Window
{
    private readonly ClockSettings _source;
    private readonly WeatherService _weather = new();
    private GeoResult? _resolved;
    private bool _initializing = true;

    private sealed record ColorChoice(string Name, string Hex)
    {
        public override string ToString() => Name;
    }

    private sealed record TemperatureChoice(string Name, string Code)
    {
        public override string ToString() => Name;
    }

    private sealed record OffsetChoice(string Name, int Hours)
    {
        public override string ToString() => Name;
    }

    private sealed record ScaleChoice(string Name, int Percent)
    {
        public override string ToString() => Name;
    }

    private static readonly TemperatureChoice[] TemperatureUnits =
    {
        new("Celsius (℃)", "C"),
        new("Fahrenheit (℉)", "F")
    };

    private static readonly OffsetChoice[] ManualOffsets =
    {
        new("-2 hours", -2),
        new("-1 hour", -1),
        new("0 (automatic)", 0),
        new("+1 hour", 1),
        new("+2 hours", 2)
    };

    private static readonly ScaleChoice[] ScaleChoices =
    {
        new("+30%", 30),
        new("+20%", 20),
        new("+10%", 10),
        new("0% (Default / 100%)", 0),
        new("-10%", -10),
        new("-20%", -20),
        new("-30%", -30)
    };

    private static readonly ColorChoice[] Colors =
    {
        new("White", "#FFFFFFFF"),
        new("Amber", "#FFFFBF00"),
        new("Light Gray", "#FFD9D9D9"),
        new("Cyan", "#FF80DEEA"),
        new("Light Blue", "#FF90CAF9"),
        new("Green", "#FFA5D6A7"),
        new("Black", "#FF000000"),
        new("Dark Gray", "#FF404040")
    };

    public ClockSettings Result { get; private set; }

    public SettingsWindow(ClockSettings settings)
    {
        InitializeComponent();
        _source = settings;

        CityBox.Text = settings.City;

        ManualOffsetBox.ItemsSource = ManualOffsets;
        ManualOffsetBox.SelectedItem =
            ManualOffsets.First(x => x.Hours == Math.Clamp(settings.ManualTimeOffsetHours, -2, 2));

        Hour12.IsChecked = settings.Use12Hour;
        Hour24.IsChecked = !settings.Use12Hour;
        SecondsBox.IsChecked = settings.ShowSeconds;
        YearBox.IsChecked = settings.ShowYear;
        WeatherBox.IsChecked = settings.ShowWeather;

        FontColorBox.ItemsSource = Colors;
        CityColorBox.ItemsSource = Colors;
        GlowColorBox.ItemsSource = Colors;

        SelectColor(FontColorBox, settings.FontColor);
        SelectColor(CityColorBox, settings.CityColor);
        SelectColor(GlowColorBox, settings.GlowColor);

        TemperatureUnitBox.ItemsSource = TemperatureUnits;
        TemperatureUnitBox.SelectedItem =
            TemperatureUnits.FirstOrDefault(x =>
                x.Code.Equals(settings.TemperatureUnit, StringComparison.OrdinalIgnoreCase))
            ?? TemperatureUnits.First(x => x.Code == "C");

        ScaleBox.ItemsSource = ScaleChoices;
        ScaleBox.SelectedItem = ScaleChoices.FirstOrDefault(x => x.Percent == settings.ScalePercent)
            ?? ScaleChoices.First(x => x.Percent == 0);

        RefreshBox.Text = settings.WeatherRefreshMinutes.ToString(CultureInfo.InvariantCulture);

        ResolvedLocationText.Text =
            $"{settings.City}  ({settings.Latitude:0.0000}, {settings.Longitude:0.0000})";

        CityBox.TextChanged += CityBox_TextChanged;

        Result = settings;
        _initializing = false;
    }

    private void CityBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_initializing)
            return;

        _resolved = null;
        ResolvedLocationText.Text = "City not resolved";

        // A manual correction belongs to the old city.
        // Reset it only when the city is changed, not every time the app starts.
        ManualOffsetBox.SelectedItem = ManualOffsets.First(x => x.Hours == 0);
    }

    private static void SelectColor(System.Windows.Controls.ComboBox box, string hex)
    {
        var match = Colors.FirstOrDefault(c =>
                        c.Hex.Equals(hex, StringComparison.OrdinalIgnoreCase))
                    ?? Colors.First(c => c.Name == "White");
        box.SelectedItem = match;
    }

    private async void ResolveButton_Click(object sender, RoutedEventArgs e)
    {
        await ResolveCityFromInputAsync();
    }

    private async void CityBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        // Prevent Enter from activating the default OK button.
        e.Handled = true;

        // Treat Enter exactly like pressing "Find city".
        // The main clock is not changed here; settings are applied only after OK.
        await ResolveCityFromInputAsync();
    }

    private async Task<bool> ResolveCityFromInputAsync()
    {
        ResolveButton.IsEnabled = false;
        ResolvedLocationText.Text = "Searching...";

        try
        {
            var candidate = await _weather.ResolveCityAsync(CityBox.Text);

            if (candidate is null)
            {
                _resolved = null;
                ResolvedLocationText.Text = "City not found";
                return false;
            }

            _resolved = candidate;

            var parts = new[] { _resolved.Name, _resolved.Admin1, _resolved.Country }
                .Where(x => !string.IsNullOrWhiteSpace(x));

            ResolvedLocationText.Text =
                $"{string.Join(", ", parts)}  ({_resolved.Latitude:0.0000}, {_resolved.Longitude:0.0000})";

            return true;
        }
        catch
        {
            _resolved = null;
            ResolvedLocationText.Text = "City lookup failed";
            return false;
        }
        finally
        {
            ResolveButton.IsEnabled = true;
        }
    }

    private async void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RefreshBox.Text,
                          NumberStyles.Integer,
                          CultureInfo.InvariantCulture,
                          out var refresh))
        {
            MessageBox.Show(this,
                "Please check the weather refresh interval.",
                "Invalid setting",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var cityChanged =
            !CityBox.Text.Trim().Equals(_source.City, StringComparison.OrdinalIgnoreCase);

        if (_resolved is null && cityChanged)
        {
            var found = await ResolveCityFromInputAsync();
            if (!found)
            {
                // Keep the currently running clock/weather unchanged.
                // The settings window stays open so the user can correct the city.
                MessageBox.Show(this,
                    "City not found. The current clock and weather location were not changed.",
                    "City not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        var city = _resolved?.Name ?? _source.City;
        var lat = _resolved?.Latitude ?? _source.Latitude;
        var lon = _resolved?.Longitude ?? _source.Longitude;
        var timeZoneId = _resolved?.TimeZone ?? _source.TimeZoneId;

        var fontColor =
            (FontColorBox.SelectedItem as ColorChoice)?.Hex ?? "#FFFFFFFF";
        var cityColor =
            (CityColorBox.SelectedItem as ColorChoice)?.Hex ?? "#FF90CAF9";
        var glowColor =
            (GlowColorBox.SelectedItem as ColorChoice)?.Hex ?? "#CC000000";
        var tempUnit =
            (TemperatureUnitBox.SelectedItem as TemperatureChoice)?.Code ?? "C";
        var manualOffset =
            (ManualOffsetBox.SelectedItem as OffsetChoice)?.Hours ?? 0;
        var scalePercent =
            (ScaleBox.SelectedItem as ScaleChoice)?.Percent ?? 0;

        double? cachedCurrent = _source.LastWeatherCurrent;
        double? cachedMax = _source.LastWeatherMax;
        double? cachedMin = _source.LastWeatherMin;
        var cachedUnit = string.IsNullOrWhiteSpace(_source.LastWeatherUnit)
            ? _source.TemperatureUnit
            : _source.LastWeatherUnit;

        if (!cityChanged && !cachedUnit.Equals(tempUnit, StringComparison.OrdinalIgnoreCase))
        {
            static double CtoF(double c) => c * 9.0 / 5.0 + 32.0;
            static double FtoC(double f) => (f - 32.0) * 5.0 / 9.0;
            Func<double, double> convert = tempUnit == "F" ? CtoF : FtoC;
            if (cachedCurrent.HasValue) cachedCurrent = convert(cachedCurrent.Value);
            if (cachedMax.HasValue) cachedMax = convert(cachedMax.Value);
            if (cachedMin.HasValue) cachedMin = convert(cachedMin.Value);
            cachedUnit = tempUnit;
        }

        Result = new ClockSettings
        {
            ProfileName = _source.ProfileName,
            City = city,
            Latitude = lat,
            Longitude = lon,
            TimeZoneId = timeZoneId,
            ManualTimeOffsetHours = Math.Clamp(manualOffset, -2, 2),

            Use12Hour = Hour12.IsChecked == true,
            ShowSeconds = SecondsBox.IsChecked == true,
            ShowYear = YearBox.IsChecked == true,
            ShowWeather = WeatherBox.IsChecked == true,

            Width = ClockSettings.BaseWidth,
            Height = ClockSettings.BaseHeight,
            ScalePercent = scalePercent,
            Left = _source.Left,
            Top = _source.Top,
            MonitorDeviceName = _source.MonitorDeviceName,
            HorizontalAnchor = _source.HorizontalAnchor,
            VerticalAnchor = _source.VerticalAnchor,
            HorizontalOffset = _source.HorizontalOffset,
            VerticalOffset = _source.VerticalOffset,
            HasAnchoredPlacement = _source.HasAnchoredPlacement,

            LastWeatherCurrent = cityChanged ? null : cachedCurrent,
            LastWeatherMax = cityChanged ? null : cachedMax,
            LastWeatherMin = cityChanged ? null : cachedMin,
            LastWeatherUnit = cityChanged ? "" : cachedUnit,
            LastWeatherSymbol = cityChanged ? "" : _source.LastWeatherSymbol,
            LastWeatherSymbolColor = cityChanged ? "" : _source.LastWeatherSymbolColor,
            LastWeatherUpdatedUtc = cityChanged ? null : _source.LastWeatherUpdatedUtc,

            WeatherRefreshMinutes = Math.Clamp(refresh, 10, 180),
            FontColor = fontColor,
            CityColor = cityColor,
            GlowColor = glowColor,
            TemperatureUnit = tempUnit,
            SettingsVersion = ClockSettings.CurrentSettingsVersion
        };

        DialogResult = true;
    }
}
