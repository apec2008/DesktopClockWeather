using System.Threading.Tasks;
using System.Threading;
using System;
using System.Net.Http;
using System.Text.Json;

namespace DesktopClockWeather;

public sealed record WeatherSnapshot(
    string Symbol,
    string SymbolColor,
    double Current,
    double Max,
    double Min,
    int WeatherCode,
    bool IsDay);

public sealed record GeoResult(
    string Name,
    string? Admin1,
    string Country,
    string CountryCode,
    double Latitude,
    double Longitude,
    string TimeZone);

public sealed class WeatherService
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public async Task<WeatherSnapshot?> GetDailyAsync(
        double latitude,
        double longitude,
        string ianaTimeZone,
        string temperatureUnit,
        CancellationToken ct = default)
    {
        var tz = Uri.EscapeDataString(ianaTimeZone);
        var unit = temperatureUnit.Equals("F", StringComparison.OrdinalIgnoreCase) ? "fahrenheit" : "celsius";
        var url =
            $"https://api.open-meteo.com/v1/forecast" +
            $"?latitude={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            $"&longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            $"&current=temperature_2m,weather_code,is_day" +
            $"&daily=weather_code,temperature_2m_max,temperature_2m_min" +
            $"&temperature_unit={unit}&timezone={tz}&forecast_days=1";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var current = doc.RootElement.GetProperty("current");
        var currentTemp = current.GetProperty("temperature_2m").GetDouble();
        var currentCode = current.GetProperty("weather_code").GetInt32();
        var isDay = current.TryGetProperty("is_day", out var dayProp) && dayProp.GetInt32() == 1;

        var daily = doc.RootElement.GetProperty("daily");
        var max = daily.GetProperty("temperature_2m_max")[0].GetDouble();
        var min = daily.GetProperty("temperature_2m_min")[0].GetDouble();
        var (symbol, color) = SymbolFor(currentCode, isDay);
        return new WeatherSnapshot(symbol, color, currentTemp, max, min, currentCode, isDay);
    }

    public async Task<GeoResult?> ResolveCityAsync(string city, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(city))
            return null;

        var q = Uri.EscapeDataString(city.Trim());
        var url = $"https://geocoding-api.open-meteo.com/v1/search?name={q}&count=1&language=en&format=json";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            return null;

        var r = results[0];
        var name = r.GetProperty("name").GetString() ?? city.Trim();
        var country = r.TryGetProperty("country", out var c) ? (c.GetString() ?? "") : "";
        var countryCode = r.TryGetProperty("country_code", out var cc) ? (cc.GetString() ?? "") : "";
        var admin1 = r.TryGetProperty("admin1", out var a) ? a.GetString() : null;
        var lat = r.GetProperty("latitude").GetDouble();
        var lon = r.GetProperty("longitude").GetDouble();
        var tz = r.TryGetProperty("timezone", out var t) ? (t.GetString() ?? "UTC") : "UTC";

        return new GeoResult(name, admin1, country, countryCode, lat, lon, tz);
    }

    private static (string Symbol, string Color) SymbolFor(int code, bool isDay) => code switch
    {
        0 => isDay ? ("☀", "#FFFFC107") : ("☾", "#FFFFE082"),
        1 or 2 => isDay ? ("☀☁", "#FFFFD966") : ("☾☁", "#FFDCE6F1"),
        3 => ("☁", "#FFD7DCE2"),
        45 or 48 => ("≋", "#FFB0BEC5"),
        51 or 53 or 55 or 56 or 57 => ("☂", "#FF64B5F6"),
        61 or 63 or 65 or 66 or 67 => ("☂", "#FF42A5F5"),
        71 or 73 or 75 or 77 => ("❄", "#FFB3E5FC"),
        80 or 81 or 82 => ("☔", "#FF2196F3"),
        85 or 86 => ("❄", "#FFB3E5FC"),
        95 or 96 or 99 => ("⚡", "#FFFFB74D"),
        _ => ("☁", "#FFD7DCE2")
    };
}
