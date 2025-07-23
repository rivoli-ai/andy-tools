using System.Text;
using Andy.Tools.Core;
using Andy.Tools.Library.FileSystem;
using Xunit;

namespace Andy.Tools.Tests.Library.FileSystem;

public class WriteFileToolTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly WriteFileTool _tool;
    private readonly ToolExecutionContext _context;

    public WriteFileToolTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"andy_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        _tool = new WriteFileTool();
        _tool.InitializeAsync().GetAwaiter().GetResult();
        _context = new ToolExecutionContext
        {
            Permissions = new ToolPermissions { AllowedPaths = [_testDirectory] }
        };
    }

    [Fact]
    public async Task ExecuteAsync_WriteNewFile_Success()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "new.txt");
        var content = "This is new file content";

        var parameters = new Dictionary<string, object?>
        {
            ["file_path"] = filePath,
            ["content"] = content
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        Assert.True(File.Exists(filePath));
        Assert.Equal(content, await File.ReadAllTextAsync(filePath));

        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);
        Assert.Equal(filePath, data["file_path"]);
        // File size might differ due to encoding/line endings, just check it's positive
        Assert.True((long)data["file_size"]! > 0);
        // The metadata contains additional data added by FileSuccess, check if backup was created
        var hasBackup = result.Metadata.ContainsKey("created_backup") && result.Metadata["created_backup"] is bool backup && backup;
        Assert.False(hasBackup);
    }

    [Fact]
    public async Task ExecuteAsync_OverwriteWithBackup_CreatesBackup()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "existing.txt");
        var originalContent = "Original content";
        var newContent = "New content";
        await File.WriteAllTextAsync(filePath, originalContent);

        var parameters = new Dictionary<string, object?>
        {
            ["file_path"] = filePath,
            ["content"] = newContent,
            ["create_backup"] = true
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        Assert.Equal(newContent, await File.ReadAllTextAsync(filePath));

        // Check backup creation - FileSuccess puts everything in Data
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);
        
        var hasBackup = data.ContainsKey("created_backup") && data["created_backup"] is bool backup && backup;
        Assert.True(hasBackup, $"Backup was not created. Data keys: {string.Join(", ", data.Keys)}");
        
        var backupPath = data["backup_path"] as string;
        Assert.NotNull(backupPath);
        Assert.True(File.Exists(backupPath));
        Assert.Equal(originalContent, await File.ReadAllTextAsync(backupPath));
    }

    [Fact]
    public async Task ExecuteAsync_CreateDirectories_CreatesPath()
    {
        // Arrange
        var subDir = Path.Combine(_testDirectory, "sub", "dir");
        var filePath = Path.Combine(subDir, "file.txt");
        var content = "Content in subdirectory";

        var parameters = new Dictionary<string, object?>
        {
            ["file_path"] = filePath,
            ["content"] = content,
            ["overwrite"] = true
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        Assert.True(Directory.Exists(subDir));
        Assert.True(File.Exists(filePath));
        Assert.Equal(content, await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task ExecuteAsync_AppendMode_AppendsContent()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "append.txt");
        var originalContent = "Line 1\n";
        var appendContent = "Line 2\n";
        await File.WriteAllTextAsync(filePath, originalContent);

        var parameters = new Dictionary<string, object?>
        {
            ["file_path"] = filePath,
            ["content"] = appendContent,
            ["append"] = true
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        var finalContent = await File.ReadAllTextAsync(filePath);
        Assert.Equal(originalContent + appendContent, finalContent);
    }

    [Fact]
    public async Task ExecuteAsync_DifferentEncoding_WritesCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "unicode.txt");
        var content = "Unicode: 你好世界 🌍";

        var parameters = new Dictionary<string, object?>
        {
            ["file_path"] = filePath,
            ["content"] = content,
            ["encoding"] = "utf-16"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        var readContent = await File.ReadAllTextAsync(filePath, Encoding.Unicode);
        Assert.Equal(content, readContent);
        
        // FileSuccess puts everything in Data
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);
        Assert.Contains("encoding", data.Keys);
        Assert.Equal(Encoding.Unicode.EncodingName, data["encoding"]);
    }

    [Fact]
    public async Task ExecuteAsync_PathOutsideAllowed_ReturnsError()
    {
        // Arrange
        var filePath = Path.Combine(Path.GetTempPath(), "outside.txt");

        var parameters = new Dictionary<string, object?>
        {
            ["file_path"] = filePath,
            ["content"] = "test"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.False(result.IsSuccessful);
        Assert.Contains("not within allowed paths", result.ErrorMessage);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidEncoding_ReturnsError()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");

        var parameters = new Dictionary<string, object?>
        {
            ["file_path"] = filePath,
            ["content"] = "test",
            ["encoding"] = "invalid-encoding"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.False(result.IsSuccessful);
        Assert.Contains("encoding", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_OverwriteFalse_FailsForExistingFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "existing.txt");
        await File.WriteAllTextAsync(filePath, "existing content");

        var parameters = new Dictionary<string, object?>
        {
            ["file_path"] = filePath,
            ["content"] = "new content",
            ["overwrite"] = false
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.False(result.IsSuccessful);
        Assert.Contains("already exists", result.ErrorMessage);
    }

    [Fact]
    public void Metadata_HasCorrectConfiguration()
    {
        // Assert
        Assert.Equal("write_file", _tool.Metadata.Id);
        Assert.Equal("Write File", _tool.Metadata.Name);
        Assert.Equal(ToolCategory.FileSystem, _tool.Metadata.Category);
        Assert.Equal(ToolPermissionFlags.FileSystemWrite, _tool.Metadata.RequiredPermissions);

        var pathParam = _tool.Metadata.Parameters.First(p => p.Name == "file_path");
        Assert.True(pathParam.Required);

        var contentParam = _tool.Metadata.Parameters.First(p => p.Name == "content");
        Assert.True(contentParam.Required);
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
