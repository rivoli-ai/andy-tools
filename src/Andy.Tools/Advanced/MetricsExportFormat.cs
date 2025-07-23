namespace Andy.Tools.Advanced;

/// <summary>
/// Metrics export format.
/// </summary>
public enum MetricsExportFormat
{
    /// <summary>JSON format.</summary>
    Json,
    /// <summary>CSV format.</summary>
    Csv,
    /// <summary>Prometheus format.</summary>
    Prometheus,
    /// <summary>OpenTelemetry format.</summary>
    OpenTelemetry
}
