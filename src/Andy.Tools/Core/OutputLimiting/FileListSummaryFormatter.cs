using System.Text;

namespace Andy.Tools.Core.OutputLimiting;

/// <summary>
/// Formats file list summaries in a user-friendly way.
/// </summary>
public static class FileListSummaryFormatter
{
    /// <summary>
    /// Formats a file list summary with statistics and helpful information.
    /// </summary>
    public static string FormatSummary(OutputSummary summary, string directoryPath)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Directory Summary: {directoryPath}");
        sb.AppendLine(new string('━', 50));
        sb.AppendLine();

        // Statistics section
        sb.AppendLine("📊 Statistics:");
        sb.AppendLine($"- Total files: {summary.Statistics.GetValueOrDefault("file_count", 0):N0}");
        sb.AppendLine($"- Total directories: {summary.Statistics.GetValueOrDefault("directory_count", 0):N0}");
        sb.AppendLine($"- Total items: {summary.TotalCount:N0}");

        if (summary.Statistics.TryGetValue("total_size", out var totalSize) && totalSize is long size)
        {
            sb.AppendLine($"- Total size: {FormatFileSize(size)}");
        }

        sb.AppendLine();

        // Top-level directories
        if (summary.Groups.Count > 0)
        {
            sb.AppendLine("📁 Top directories (by file count):");
            foreach (var group in summary.Groups.Take(5))
            {
                sb.AppendLine($"- {group.Name}/ ({group.Count} files)");
                if (group.SampleItems.Count > 0)
                {
                    foreach (var item in group.SampleItems.Take(3))
                    {
                        sb.AppendLine($"  • {item}");
                    }

                    if (group.Count > group.SampleItems.Count)
                    {
                        sb.AppendLine($"  • ... and {group.Count - group.SampleItems.Count} more");
                    }
                }
            }

            if (summary.Groups.Count > 5)
            {
                sb.AppendLine($"- ... and {summary.Groups.Count - 5} more directories");
            }

            sb.AppendLine();
        }

        // File types
        if (summary.Statistics.TryGetValue("top_extensions", out var extensions) && extensions is List<string> extList)
        {
            sb.AppendLine("📄 File types:");
            foreach (var ext in extList)
            {
                sb.AppendLine($"- {ext}");
            }

            if (summary.Statistics.TryGetValue("unique_extensions", out var uniqueCount) && (int)uniqueCount > extList.Count)
            {
                sb.AppendLine($"- ... and {(int)uniqueCount - extList.Count} other types");
            }

            sb.AppendLine();
        }

        // Truncation notice
        if (summary.ShownCount < summary.TotalCount)
        {
            sb.AppendLine($"⚠️ Showing {summary.ShownCount:N0} of {summary.TotalCount:N0} items");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats file size in human-readable format.
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Creates helpful suggestions based on the summary.
    /// </summary>
    public static List<string> GenerateSuggestions(OutputSummary summary, string currentQuery)
    {
        var suggestions = new List<string>();

        // If many files, suggest filtering
        if (summary.TotalCount > 100)
        {
            suggestions.Add("Use pattern parameter to filter files (e.g., pattern: '*.cs')");
        }

        // If many directories, suggest specific directory
        if (summary.Groups.Count > 10)
        {
            var topDir = summary.Groups.First();
            suggestions.Add($"List specific directory: '{topDir.Name}'");
        }

        // If deep recursion detected
        if (summary.Statistics.ContainsKey("max_depth_reached"))
        {
            suggestions.Add("Use max_depth parameter to limit recursion depth");
        }

        // General suggestions
        suggestions.Add("Set recursive=false for top-level only");
        suggestions.Add("Use sort_by parameter to order results");

        return suggestions.Take(3).ToList(); // Limit to 3 suggestions
    }
}
