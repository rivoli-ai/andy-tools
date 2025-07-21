using System.Text;
using Andy.Tools.Core.OutputLimiting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Andy.Tools.Tests.Core.OutputLimiting;

public class ToolOutputLimiterTests
{
    private readonly ToolOutputLimiter _limiter;
    private readonly ToolOutputLimiterOptions _options;

    public ToolOutputLimiterTests()
    {
        _options = new ToolOutputLimiterOptions
        {
            MaxOutputCharacters = 1000,
            MaxFileListCharacters = 500,
            MaxFileListEntries = 10,
            MaxFileContentCharacters = 2000,
            MaxLinesPerFile = 20,
            EnableSmartSummaries = true,
            ShowTruncationWarning = true
        };
        _limiter = new ToolOutputLimiter(Options.Create(_options));
    }

    [Fact]
    public void NeedsLimiting_SmallText_ReturnsFalse()
    {
        // Arrange
        var smallText = "This is a small text";

        // Act
        var result = _limiter.NeedsLimiting(smallText, OutputType.Text);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void NeedsLimiting_LargeText_ReturnsTrue()
    {
        // Arrange
        var largeText = new string('x', 2000);

        // Act
        var result = _limiter.NeedsLimiting(largeText, OutputType.Text);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void NeedsLimiting_LargeFileList_ReturnsTrue()
    {
        // Arrange
        var fileList = new List<object>();
        for (int i = 0; i < 100; i++)
        {
            fileList.Add(new { name = $"file{i}.txt", type = "file", size = 1024 });
        }

        // Act
        var result = _limiter.NeedsLimiting(fileList, OutputType.FileList);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void LimitOutput_SmallText_ReturnsUnchanged()
    {
        // Arrange
        var text = "Small text that doesn't need limiting";

        // Act
        var result = _limiter.LimitOutput(text, OutputType.Text);

        // Assert
        Assert.False(result.WasTruncated);
        Assert.Equal(text, result.Content);
    }

    [Fact]
    public void LimitOutput_LargeText_TruncatesCorrectly()
    {
        // Arrange
        var text = new string('x', 2000);

        // Act
        var result = _limiter.LimitOutput(text, OutputType.Text);

        // Assert
        Assert.True(result.WasTruncated);
        Assert.True(result.Content.ToString()!.Length < text.Length);
        Assert.Contains("truncated", result.Content.ToString());
        Assert.Equal(2000, result.OriginalSize);
    }

    [Fact]
    public void LimitOutput_FileList_CreatesSmartSummary()
    {
        // Arrange
        var fileList = new List<object>();
        for (int i = 0; i < 50; i++)
        {
            fileList.Add(new
            {
                name = $"file{i}.cs",
                full_path = $"/project/src/file{i}.cs",
                type = "file",
                size = 1024 * (i + 1)
            });
        }

        // Act
        var result = _limiter.LimitOutput(fileList, OutputType.FileList);

        // Assert
        Assert.True(result.WasTruncated);
        Assert.NotNull(result.Summary);
        Assert.Equal(50, result.Summary.TotalCount);
        Assert.True(result.Summary.ShownCount < result.Summary.TotalCount);
        Assert.Contains("file_count", result.Summary.Statistics);
        Assert.True(result.Suggestions.Count > 0);
    }

    [Fact]
    public void LimitOutput_FileContent_TruncatesAtLineLimit()
    {
        // Arrange
        var lines = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            lines.Add($"Line {i}: This is some content on line {i}");
        }

        var content = string.Join('\n', lines);

        // Act
        var result = _limiter.LimitOutput(content, OutputType.FileContent);

        // Assert
        Assert.True(result.WasTruncated);
        var truncatedLines = result.Content.ToString()!.Split('\n');
        Assert.True(truncatedLines.Length <= _options.MaxLinesPerFile + 2); // +2 for truncation message
        Assert.Contains("more lines", result.Content.ToString());
    }

    [Fact]
    public void LimitOutput_Logs_ShowsHeadAndTail()
    {
        // Arrange
        var lines = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            lines.Add($"[LOG] Entry {i}: Event occurred at timestamp {i}");
        }

        var logs = string.Join('\n', lines);

        // Act
        var result = _limiter.LimitOutput(logs, OutputType.Logs);

        // Assert
        Assert.True(result.WasTruncated);
        var outputStr = result.Content.ToString()!;
        Assert.Contains("[LOG] Entry 0:", outputStr); // Should contain first entries
        Assert.Contains("[LOG] Entry 99:", outputStr); // Should contain last entries
        Assert.Contains("lines omitted", outputStr);
    }

    [Fact]
    public void LimitOutput_WithCustomContext_RespectsLimits()
    {
        // Arrange
        var text = new string('x', 2000);
        var context = new OutputLimitContext
        {
            MaxCharacters = 100,
            IncludeSummary = false,
            ProvideSuggestions = false
        };

        // Act
        var result = _limiter.LimitOutput(text, OutputType.Text, context);

        // Assert
        Assert.True(result.WasTruncated);
        Assert.True(result.Content.ToString()!.Length <= 120); // 100 + truncation message
        Assert.Empty(result.Suggestions);
    }

    [Fact]
    public void GetTruncationMessage_FormatsCorrectly()
    {
        // Arrange
        var limitedOutput = new LimitedOutput
        {
            WasTruncated = true,
            OriginalSize = 10000,
            TruncatedSize = 1000,
            TruncationReason = "File list exceeded limit",
            Suggestions = new List<string>
            {
                "Use pattern parameter to filter files",
                "List specific directory"
            }
        };

        // Act
        var message = limitedOutput.GetTruncationMessage();

        // Assert
        Assert.Contains("10,000", message);
        Assert.Contains("1,000", message);
        Assert.Contains("File list exceeded limit", message);
        Assert.Contains("Use pattern parameter", message);
        Assert.Contains("List specific directory", message);
    }

    [Fact]
    public void LimitOutput_StructuredData_PreservesValidJson()
    {
        // Arrange
        var data = new
        {
            items = Enumerable.Range(0, 100).Select(i => new
            {
                id = i,
                name = $"Item {i}",
                description = $"This is a description for item {i}"
            }).ToArray()
        };

        // Act
        var result = _limiter.LimitOutput(data, OutputType.StructuredData);

        // Assert
        Assert.True(result.WasTruncated);
        // The truncated output should still be valid (though incomplete) JSON
        Assert.Contains("truncated", result.Content.ToString());
    }

    [Fact]
    public void FileListSummaryFormatter_GeneratesReadableOutput()
    {
        // Arrange
        var summary = new OutputSummary
        {
            TotalCount = 500,
            ShownCount = 100,
            Statistics = new Dictionary<string, object>
            {
                ["file_count"] = 400,
                ["directory_count"] = 100,
                ["unique_extensions"] = 5,
                ["top_extensions"] = new List<string> { ".cs: 200", ".json: 50", ".xml: 30" }
            },
            Groups = new List<OutputGroup>
            {
                new() { Name = "/src", Count = 200, SampleItems = ["Program.cs", "Startup.cs", "Config.cs"] },
                new() { Name = "/tests", Count = 150, SampleItems = ["Test1.cs", "Test2.cs"] }
            }
        };

        // Act
        var formatted = FileListSummaryFormatter.FormatSummary(summary, "/project");

        // Assert
        Assert.Contains("Directory Summary: /project", formatted);
        Assert.Contains("Total files: 400", formatted);
        Assert.Contains("Total directories: 100", formatted);
        Assert.Contains("/src/ (200 files)", formatted);
        Assert.Contains("• Program.cs", formatted);
        Assert.Contains(".cs: 200", formatted);
        Assert.Contains("Showing 100 of 500 items", formatted);
    }
}
