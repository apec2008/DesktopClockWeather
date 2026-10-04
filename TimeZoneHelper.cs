namespace DesktopClockWeather;

public static class TimeZoneHelper
{
    public static TimeZoneInfo FindTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch
        {
            // On Windows, convert IANA -> Windows if the IANA ID is not accepted directly.
            try
            {
                if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId) &&
                    !string.IsNullOrWhiteSpace(windowsId))
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }
            }
            catch { }

            return TimeZoneInfo.Local;
        }
    }

    public static string ToIana(string id)
    {
        // Already looks like an IANA ID.
        if (id.Contains('/'))
            return id;

        try
        {
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var iana) &&
                !string.IsNullOrWhiteSpace(iana))
                return iana;
        }
        catch { }

        return id switch
        {
            "Korea Standard Time" => "Asia/Seoul",
            "Eastern Standard Time" => "America/New_York",
            "Central Standard Time" => "America/Chicago",
            "Mountain Standard Time" => "America/Denver",
            "Pacific Standard Time" => "America/Los_Angeles",
            _ => "UTC"
        };
    }
}
