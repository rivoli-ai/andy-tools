namespace Andy.Tools.Advanced.MetricsCollection;

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
    public static TimeRange LastHours(int hours)
    {
        var now = DateTimeOffset.UtcNow;
        return new TimeRange
        {
            Start = now.AddHours(-hours),
            End = now
        };
    }

    /// <summary>
    /// Creates a time range for the last N days.
    /// </summary>
    public static TimeRange LastDays(int days)
    {
        var now = DateTimeOffset.UtcNow;
        return new TimeRange
        {
            Start = now.AddDays(-days),
            End = now
        };
    }
}
