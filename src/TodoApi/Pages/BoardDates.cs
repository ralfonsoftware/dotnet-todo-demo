using System.Globalization;

namespace TodoApi.Pages;

/// <summary>
/// Formats board dates in one time zone: the <see cref="TimeProvider"/>'s
/// local zone, which also produces the "Today" label. Stored timestamps stay
/// UTC; only the display text is converted.
/// </summary>
internal static class BoardDates
{
    /// <summary>A short day such as <c>9 Nov</c>, in the given zone.</summary>
    public static string ShortDay(DateTimeOffset value, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(value, zone).ToString("d MMM", CultureInfo.InvariantCulture);

    /// <summary>The ISO 8601 round-trip value for a <c>datetime</c> attribute, unconverted.</summary>
    public static string Machine(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>
    /// A short day such as <c>12 Nov</c> for a calendar date. A Due Date has no
    /// time of day or zone, so it is formatted as stored.
    /// </summary>
    public static string ShortDay(DateOnly value) =>
        value.ToString("d MMM", CultureInfo.InvariantCulture);

    /// <summary>The ISO 8601 value for a date-only <c>datetime</c> attribute.</summary>
    public static string Machine(DateOnly value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
