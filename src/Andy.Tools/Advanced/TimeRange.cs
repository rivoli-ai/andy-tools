namespace Andy.Tools.Advanced;

/// <summary>
/// Time range for metrics queries.
/// </summary>
public class TimeRange
{
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTimeOffset Start { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTimeOffset End { get; set; }

    /// <summary>
    /// Creates a time range for the last N hours.
    /// </summary>
    public static TimeRange LastHours(int hours) => new()
    {
        Start = DateTimeOffset.UtcNow.AddHours(-hours),
        End = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Creates a time range for the last N days.
    /// </summary>
    public static TimeRange LastDays(int days) => new()
    {
        Start = DateTimeOffset.UtcNow.AddDays(-days),
        End = DateTimeOffset.UtcNow
    };
}
