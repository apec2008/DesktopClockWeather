using System.Text.Json.Serialization;

namespace DesktopClockWeather;

public sealed class ClockSettings
{
    public const int CurrentSettingsVersion = 19;
    public const double BaseWidth = 720;
    public const double BaseHeight = 238;

    public int SettingsVersion { get; set; } = CurrentSettingsVersion;
    public string ProfileName { get; set; } = "Default";

    public string City { get; set; } = "Seoul";
    public double Latitude { get; set; } = 37.5665;
    public double Longitude { get; set; } = 126.9780;
    public string TimeZoneId { get; set; } = "Asia/Seoul";
    public int ManualTimeOffsetHours { get; set; }

    public bool Use12Hour { get; set; } = true;
    public bool ShowSeconds { get; set; }
    public bool ShowYear { get; set; } = true;
    public bool ShowWeather { get; set; } = true;

    public string FontColor { get; set; } = "#FFFFFFFF";
    public string CityColor { get; set; } = "#FF90CAF9";
    public string GlowColor { get; set; } = "#FF000000";
    public string TemperatureUnit { get; set; } = "C";

    // V1.1: canonical layout size + uniform scaling. 0% means the canonical 100% size.
    public int ScalePercent { get; set; } = 0;

    // Kept for backwards compatibility only. V1.1 derives actual size from ScalePercent.
    public double Width { get; set; } = BaseWidth;
    public double Height { get; set; } = BaseHeight;

    // Absolute fallback position in WPF DIPs.
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public string MonitorDeviceName { get; set; } = "";

    // V1.1 placement model. Offsets are relative to a monitor work-area edge.
    public string HorizontalAnchor { get; set; } = "Right"; // Left | Right
    public string VerticalAnchor { get; set; } = "Top";     // Top | Bottom
    public double HorizontalOffset { get; set; } = double.NaN;
    public double VerticalOffset { get; set; } = double.NaN;
    public bool HasAnchoredPlacement { get; set; }

    public int WeatherRefreshMinutes { get; set; } = 30;

    public double? LastWeatherCurrent { get; set; }
    public double? LastWeatherMax { get; set; }
    public double? LastWeatherMin { get; set; }
    public string LastWeatherUnit { get; set; } = "";
    public string LastWeatherSymbol { get; set; } = "";
    public string LastWeatherSymbolColor { get; set; } = "";
    public DateTime? LastWeatherUpdatedUtc { get; set; }

    [JsonIgnore]
    public double ScaleFactor => Math.Clamp((100.0 + ScalePercent) / 100.0, 0.7, 1.3);

    [JsonIgnore]
    public double ActualWidth => BaseWidth * ScaleFactor;

    [JsonIgnore]
    public double ActualHeight => BaseHeight * ScaleFactor;
}
