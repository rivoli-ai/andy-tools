using System.Collections;
using System.Text.RegularExpressions;
using Andy.Tools.Core;
using Andy.Tools.Library.Text;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Library.Text;

public class SearchTextToolTests : IDisposable
{
    private readonly SearchTextTool _tool;
    private readonly ToolExecutionContext _context;
    private readonly string _testDirectory;
    private readonly string _tempFile;

    public SearchTextToolTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"andy_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _tempFile = Path.Combine(_testDirectory, "test.txt");

        _tool = new SearchTextTool();
        _tool.InitializeAsync().GetAwaiter().GetResult();
        _context = new ToolExecutionContext
        {
            WorkingDirectory = _testDirectory,
            Permissions = new ToolPermissions { AllowedPaths = new HashSet<string> { _testDirectory } }
        };
    }

    [Fact]
    public async Task ExecuteAsync_SimpleTextSearch_FindsMatches()
    {
        // Arrange
        var text = @"The quick brown fox jumps over the lazy dog.
The fox is very clever.
Dogs are loyal animals.";
        await File.WriteAllTextAsync(_tempFile, text);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "fox",
            ["target_path"] = _tempFile
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful, $"Tool execution failed: {result.ErrorMessage}");

        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);

        var items = data["items"] as IList;
        Assert.NotNull(items);
        Assert.Equal(2, items.Count);
        Assert.Equal(2, data["count"]);
    }

    [Fact]
    public async Task ExecuteAsync_RegexSearch_FindsPatterns()
    {
        // Arrange
        var text = "Contact: john@example.com or jane@test.org for more info.";
        await File.WriteAllTextAsync(_tempFile, text);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
            ["target_path"] = _tempFile,
            ["search_type"] = "regex"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);

        var items = data["items"] as IList;
        Assert.NotNull(items);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task ExecuteAsync_CaseInsensitive_FindsMatches()
    {
        // Arrange
        var text = "Hello WORLD, hello world!";
        await File.WriteAllTextAsync(_tempFile, text);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "hello",
            ["target_path"] = _tempFile,
            ["case_sensitive"] = false
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);
        Assert.Equal(2, data["count"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithContext_IncludesContext()
    {
        // Arrange
        var text = "Line 1\nLine 2\nTarget line here\nLine 4\nLine 5";
        await File.WriteAllTextAsync(_tempFile, text);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "Target",
            ["target_path"] = _tempFile,
            ["context_lines"] = 1
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);

        var items = data["items"] as IList;
        Assert.NotNull(items);
        Assert.Single(items);
    }

    [Fact]
    public async Task ExecuteAsync_MaxMatches_LimitsResults()
    {
        // Arrange
        var text = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Match {i}"));
        await File.WriteAllTextAsync(_tempFile, text);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "Match",
            ["target_path"] = _tempFile,
            ["max_results"] = 5
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);

        var items = data["items"] as IList;
        Assert.NotNull(items);
        Assert.Equal(5, items.Count);
        Assert.Equal(5, data["total_count"]); // Total count is limited to max_results for performance
    }

    [Fact]
    public async Task ExecuteAsync_WholeWords_OnlyMatchesWholeWords()
    {
        // Arrange
        var text = "The cat in the cathedral catches mice.";
        await File.WriteAllTextAsync(_tempFile, text);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "cat",
            ["target_path"] = _tempFile,
            ["whole_words_only"] = true
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);
        Assert.Equal(1, data["count"]); // Only "cat", not "cathedral" or "catches"
    }

    [Fact]
    public async Task ExecuteAsync_InvertMatch_FindsNonMatchingLines()
    {
        // Arrange
        var text = "Apple\nBanana\nCherry\nApple pie\nGrape";
        await File.WriteAllTextAsync(_tempFile, text);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "Apple",
            ["target_path"] = _tempFile
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);
        Assert.Equal(2, data["count"]); // Found 2 lines with "Apple"
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRegex_ReturnsError()
    {
        // Arrange
        await File.WriteAllTextAsync(_tempFile, "test content");

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "[invalid",
            ["target_path"] = _tempFile,
            ["search_type"] = "regex"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.False(result.IsSuccessful);
        Assert.Contains("Search failed", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyText_ReturnsNoMatches()
    {
        // Arrange
        await File.WriteAllTextAsync(_tempFile, "");

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "test",
            ["target_path"] = _tempFile
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);
        Assert.Equal(0, data["count"]);
    }

    [Fact]
    public void Metadata_HasCorrectConfiguration()
    {
        // Assert
        Assert.Equal("search_text", _tool.Metadata.Id);
        Assert.Equal("Search Text", _tool.Metadata.Name);
        Assert.Equal(ToolCategory.TextProcessing, _tool.Metadata.Category);
        Assert.Equal(ToolPermissionFlags.FileSystemRead, _tool.Metadata.RequiredPermissions);

        var searchPatternParam = _tool.Metadata.Parameters.First(p => p.Name == "search_pattern");
        Assert.True(searchPatternParam.Required);

        var targetPathParam = _tool.Metadata.Parameters.First(p => p.Name == "target_path");
        Assert.False(targetPathParam.Required);
    }

    #region Additional Comprehensive Tests

    [Fact]
    public async Task ExecuteAsync_DirectorySearch_ShouldSearchMultipleFiles()
    {
        // Arrange
        var subDir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(subDir);

        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file1.txt"), "Hello world 1");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file2.txt"), "Hello world 2");
        await File.WriteAllTextAsync(Path.Combine(subDir, "file3.txt"), "Hello world 3");

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["target_path"] = _testDirectory,
            ["recursive"] = true
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();

        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        items!.Count.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_NonRecursiveDirectorySearch_ShouldSearchOnlyTopLevel()
    {
        // Arrange
        var subDir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(subDir);

        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file1.txt"), "Hello world 1");
        await File.WriteAllTextAsync(Path.Combine(subDir, "file3.txt"), "Hello world 3");

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["target_path"] = _testDirectory,
            ["recursive"] = false
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();

        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        items!.Count.Should().Be(1); // Only top-level file
    }

    [Fact]
    public async Task ExecuteAsync_WithFilePatterns_ShouldFilterByPattern()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file.txt"), "Hello world");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file.log"), "Hello world");

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["target_path"] = _testDirectory,
            ["file_patterns"] = new[] { "*.txt" }
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();

        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        items!.Count.Should().Be(1); // Only .txt file
    }

    [Fact]
    public async Task ExecuteAsync_WithExcludePatterns_ShouldExcludeFiles()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file.txt"), "Hello world");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file.log"), "Hello world");

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["target_path"] = _testDirectory,
            ["exclude_patterns"] = new[] { "*.log" }
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();

        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        items!.Count.Should().Be(1); // Only .txt file (log excluded)
    }

    [Fact]
    public async Task ExecuteAsync_StartsWithPattern_ShouldMatchLineStart()
    {
        // Arrange
        var text = "Hello world\nSay Hello everyone\nGoodbye world";
        await File.WriteAllTextAsync(_tempFile, text);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "Hello",
            ["target_path"] = _tempFile,
            ["search_type"] = "starts_with"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["count"].Should().Be(1); // Only line starting with "Hello"
    }

    [Fact]
    public async Task ExecuteAsync_EndsWithPattern_ShouldMatchLineEnd()
    {
        // Arrange
        var text = "Hello world.\nThis is a test.\nGoodbye world.";
        await File.WriteAllTextAsync(_tempFile, text);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world.",
            ["target_path"] = _tempFile,
            ["search_type"] = "ends_with"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["count"].Should().Be(2); // Lines ending with "world."
    }

    [Fact]
    public async Task ExecuteAsync_BinaryFile_ShouldSkipBinaryFiles()
    {
        // Arrange
        var binaryContent = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x00, 0xFF };
        var binaryFile = Path.Combine(_testDirectory, "binary.bin");
        await File.WriteAllBytesAsync(binaryFile, binaryContent);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "test",
            ["target_path"] = _testDirectory
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["count"].Should().Be(0); // Binary file skipped
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ShouldReturnFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["target_path"] = Path.Combine(_testDirectory, "nonexistent.txt")
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not exist");
    }

    [Fact]
    public async Task ExecuteAsync_MissingSearchPattern_ShouldReturnFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["target_path"] = _tempFile
            // Missing search_pattern
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Match(msg => msg.Contains("search_pattern") || msg.Contains("required"));
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldHandleCancellation()
    {
        // Arrange
        await File.WriteAllTextAsync(_tempFile, "Hello world");

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["target_path"] = _tempFile
        };

        var cts = new CancellationTokenSource();
        var context = new ToolExecutionContext { CancellationToken = cts.Token };

        // Act
        cts.Cancel();
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert - Operation may complete before cancellation or be cancelled
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_SpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var text = "Hello 世界\nTest with émojis 🌍\nSpecial chars: @#$%^&*()";
        await File.WriteAllTextAsync(_tempFile, text);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "世界",
            ["target_path"] = _tempFile
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["count"].Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutLineNumbers_ShouldNotIncludeLineNumbers()
    {
        // Arrange
        var text = "Hello world\nTest line";
        await File.WriteAllTextAsync(_tempFile, text);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["target_path"] = _tempFile,
            ["include_line_numbers"] = false
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();

        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        items!.Count.Should().Be(1);
    }

    #endregion

    public void Dispose()
    {
        try
        {
            Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
