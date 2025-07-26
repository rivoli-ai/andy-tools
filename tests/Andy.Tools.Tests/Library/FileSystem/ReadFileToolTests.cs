using System.Text;
using Andy.Tools.Core;
using Andy.Tools.Library.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Andy.Tools.Tests.Library.FileSystem;

public class ReadFileToolTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ReadFileTool _tool;
    private readonly ToolExecutionContext _context;

    public ReadFileToolTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"andy_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        _tool = new ReadFileTool();
        _tool.InitializeAsync().GetAwaiter().GetResult();
        _context = new ToolExecutionContext
        {
            Permissions = new ToolPermissions { AllowedPaths = [_testDirectory] }
        };
    }

    [Fact]
    public async Task ExecuteAsync_ReadTextFile_Success()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        var content = "Hello, World!\nThis is a test file.";
        await File.WriteAllTextAsync(filePath, content);

        var parameters = new Dictionary<string, object?>
        {
            ["file_path"] = filePath
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);
        Assert.Equal(content, data["content"]);
        Assert.Equal("text/plain", data["content_type"]);
        Assert.Equal(Encoding.UTF8.EncodingName, data["encoding"]);
    }

    [Fact]
    public async Task ExecuteAsync_ReadWithMaxSize_RejectsTooLargeFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "large.txt");
        var content = new string('A', 200000);  // ~200KB
        await File.WriteAllTextAsync(filePath, content);

        var parameters = new Dictionary<string, object?>
        {
            ["file_path"] = filePath,
            ["max_size_mb"] = 0.1  // 0.1 MB = ~100KB, smaller than file
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.False(result.IsSuccessful);
        Assert.Contains("large", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ReadWithDifferentEncoding_Success()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "utf16.txt");
        var content = "Unicode text: 你好世界";
        await File.WriteAllTextAsync(filePath, content, Encoding.Unicode);

        var parameters = new Dictionary<string, object?>
        {
            ["file_path"] = filePath,
            ["encoding"] = "utf-16"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);
        Assert.Equal(content, data["content"]);
        Assert.Equal(Encoding.Unicode.EncodingName, data["encoding"]);
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ReturnsError()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "nonexistent.txt");
        var parameters = new Dictionary<string, object?>
        {
            ["file_path"] = filePath
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.False(result.IsSuccessful);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_PathOutsideAllowed_ReturnsError()
    {
        // Arrange
        var filePath = Path.Combine(Path.GetTempPath(), "outside.txt");
        await File.WriteAllTextAsync(filePath, "test");

        try
        {
            var parameters = new Dictionary<string, object?>
            {
                ["file_path"] = filePath
            };

            // Act
            var result = await _tool.ExecuteAsync(parameters, _context);

            // Assert
            Assert.False(result.IsSuccessful);
            Assert.Contains("not within allowed paths", result.ErrorMessage);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ReadBinaryFile_ReadsAsText()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "binary.dat");
        var binaryContent = new byte[] { 0x41, 0x42, 0x43, 0x44 }; // "ABCD"
        await File.WriteAllBytesAsync(filePath, binaryContent);

        var parameters = new Dictionary<string, object?>
        {
            ["file_path"] = filePath
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);
        Assert.Equal("ABCD", data["content"]);
        Assert.Equal("text/plain", data["content_type"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithLineRange_ReturnsSpecificLines()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "multiline.txt");
        var lines = Enumerable.Range(1, 10).Select(i => $"Line {i}").ToArray();
        await File.WriteAllLinesAsync(filePath, lines);

        var parameters = new Dictionary<string, object?>
        {
            ["file_path"] = filePath,
            ["start_line"] = 3,
            ["end_line"] = 5
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);
        var resultContent = data["content"] as string;
        Assert.NotNull(resultContent);
        Assert.Contains("Line 3", resultContent);
        Assert.Contains("Line 4", resultContent);
        Assert.Contains("Line 5", resultContent);
        Assert.DoesNotContain("Line 2", resultContent);
        Assert.DoesNotContain("Line 6", resultContent);
    }

    [Fact]
    public void Metadata_HasCorrectConfiguration()
    {
        // Assert
        Assert.Equal("read_file", _tool.Metadata.Id);
        Assert.Equal("Read File", _tool.Metadata.Name);
        Assert.Equal(ToolCategory.FileSystem, _tool.Metadata.Category);
        Assert.Equal(ToolPermissionFlags.FileSystemRead, _tool.Metadata.RequiredPermissions);

        var pathParam = _tool.Metadata.Parameters.First(p => p.Name == "file_path");
        Assert.True(pathParam.Required);
        Assert.Equal("string", pathParam.Type);
    }

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
