using System.Globalization;

namespace ErshadReferral.Helpers;

public static class PersianDateHelper
{
    private static readonly PersianCalendar Pc = new();

    public static string ToPersianDate(this DateTime date)
    {
        return $"{Pc.GetYear(date):0000}/{Pc.GetMonth(date):00}/{Pc.GetDayOfMonth(date):00}";
    }

    public static string ToPersianDateTime(this DateTime date)
    {
        return $"{date.ToPersianDate()} {date:HH:mm}";
    }

    public static int GetPersianYear(DateTime date) => Pc.GetYear(date);

    public static DateTime? ParsePersianDate(string? persianDate)
    {
        if (string.IsNullOrWhiteSpace(persianDate)) return null;
        var parts = persianDate.Split('/', '-');
        if (parts.Length != 3) return null;
        if (!int.TryParse(parts[0], out var year)) return null;
        if (!int.TryParse(parts[1], out var month)) return null;
        if (!int.TryParse(parts[2], out var day)) return null;
        try
        {
            return Pc.ToDateTime(year, month, day, 0, 0, 0, 0);
        }
        catch
        {
            return null;
        }
    }
}
