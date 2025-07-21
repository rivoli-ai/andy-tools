using System.IO;
using Andy.Tools.Core;
using Andy.Tools.Library.FileSystem;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Library.FileSystem;

public class DeleteFileToolTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFile;
    private readonly string _testDirectory2;
    private readonly string _backupDirectory;
    private readonly DeleteFileTool _tool;

    public DeleteFileToolTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "DeleteFileToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        _testFile = Path.Combine(_testDirectory, "test.txt");
        _testDirectory2 = Path.Combine(_testDirectory, "testdir");
        _backupDirectory = Path.Combine(_testDirectory, "backups");

        _tool = new DeleteFileTool();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    #region Metadata Tests

    [Fact]
    public void Metadata_ShouldHaveCorrectValues()
    {
        // Act
        var metadata = _tool.Metadata;

        // Assert
        metadata.Should().NotBeNull();
        metadata.Id.Should().Be("delete_file");
        metadata.Name.Should().Be("Delete File");
        metadata.Description.Should().ContainAny("delete", "Delete");
        metadata.Category.Should().Be(ToolCategory.FileSystem);
        metadata.Parameters.Should().NotBeEmpty();
    }

    [Fact]
    public void Metadata_ShouldHaveRequiredParameters()
    {
        // Act
        var parameters = _tool.Metadata.Parameters;

        // Assert
        parameters.Should().Contain(p => p.Name == "target_path" && p.Required);
        parameters.Should().Contain(p => p.Name == "force" && !p.Required);
        parameters.Should().Contain(p => p.Name == "recursive" && !p.Required);
        parameters.Should().Contain(p => p.Name == "create_backup" && !p.Required);
    }

    #endregion

    #region File Deletion Tests

    [Fact]
    public async Task ExecuteAsync_DeleteValidFile_ShouldDeleteSuccessfully()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFile, "Test content");

        var parameters = new Dictionary<string, object?>
        {
            ["target_path"] = _testFile
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        File.Exists(_testFile).Should().BeFalse();

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data.Should().ContainKey("deleted_items");
        data!["deleted_items"].Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_DeleteFileWithBackup_ShouldCreateBackupBeforeDeletion()
    {
        // Arrange
        const string content = "Important content";
        await File.WriteAllTextAsync(_testFile, content);

        var parameters = new Dictionary<string, object?>
        {
            ["target_path"] = _testFile,
            ["create_backup"] = true,
            ["backup_location"] = _backupDirectory
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        File.Exists(_testFile).Should().BeFalse();

        // Backup should exist
        Directory.Exists(_backupDirectory).Should().BeTrue();
        var backupFiles = Directory.GetFiles(_backupDirectory);
        backupFiles.Should().HaveCount(1);
        (await File.ReadAllTextAsync(backupFiles[0])).Should().Be(content);
    }

    [Fact]
    public async Task ExecuteAsync_DeleteReadOnlyFile_WithForce_ShouldDeleteSuccessfully()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFile, "Read-only content");
        File.SetAttributes(_testFile, FileAttributes.ReadOnly);

        var parameters = new Dictionary<string, object?>
        {
            ["target_path"] = _testFile,
            ["force"] = true
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        File.Exists(_testFile).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_DeleteReadOnlyFile_WithoutForce_ShouldFail()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFile, "Read-only content");
        File.SetAttributes(_testFile, FileAttributes.ReadOnly);

        var parameters = new Dictionary<string, object?>
        {
            ["target_path"] = _testFile,
            ["force"] = false
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        File.Exists(_testFile).Should().BeTrue();
        result.ErrorMessage.Should().Match(msg => msg.Contains("read-only") || msg.Contains("access") || msg.Contains("permission"));
    }

    #endregion

    #region Directory Deletion Tests

    [Fact]
    public async Task ExecuteAsync_DeleteEmptyDirectory_ShouldDeleteSuccessfully()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory2);

        var parameters = new Dictionary<string, object?>
        {
            ["target_path"] = _testDirectory2
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        Directory.Exists(_testDirectory2).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_DeleteDirectoryRecursively_ShouldDeleteAllContents()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory2);
        var subDir = Path.Combine(_testDirectory2, "subdir");
        Directory.CreateDirectory(subDir);

        await File.WriteAllTextAsync(Path.Combine(_testDirectory2, "file1.txt"), "Content 1");
        await File.WriteAllTextAsync(Path.Combine(subDir, "file2.txt"), "Content 2");

        var parameters = new Dictionary<string, object?>
        {
            ["target_path"] = _testDirectory2,
            ["recursive"] = true
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        Directory.Exists(_testDirectory2).Should().BeFalse();

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        var deletedItems = Convert.ToInt32(data!["deleted_items"]);
        deletedItems.Should().BeGreaterThan(1); // Should include directory and files
    }

    [Fact]
    public async Task ExecuteAsync_DeleteDirectoryNotEmpty_WithoutRecursive_ShouldFail()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory2);
        await File.WriteAllTextAsync(Path.Combine(_testDirectory2, "file.txt"), "Content");

        var parameters = new Dictionary<string, object?>
        {
            ["target_path"] = _testDirectory2,
            ["recursive"] = false
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        Directory.Exists(_testDirectory2).Should().BeTrue();
        result.ErrorMessage.Should().Match(msg => msg.Contains("not empty") || msg.Contains("contains files"));
    }

    #endregion

    #region Safety and Security Tests

    [Fact]
    public async Task ExecuteAsync_DeleteWithSizeLimit_ShouldRespectLimit()
    {
        // Arrange
        var largeContent = new string('A', 1000000); // 1MB
        await File.WriteAllTextAsync(_testFile, largeContent);

        var parameters = new Dictionary<string, object?>
        {
            ["target_path"] = _testFile,
            ["max_size_mb"] = 0.5 // 500KB limit
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        File.Exists(_testFile).Should().BeTrue();
        result.ErrorMessage.Should().Match(msg => msg.Contains("size") || msg.Contains("limit") || msg.Contains("large"));
    }

    [Fact]
    public async Task ExecuteAsync_DeleteWithExclusionPattern_ShouldExcludeMatchingFiles()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory2);
        await File.WriteAllTextAsync(Path.Combine(_testDirectory2, "important.txt"), "Keep this");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory2, "temp.tmp"), "Delete this");

        var parameters = new Dictionary<string, object?>
        {
            ["target_path"] = Path.Combine(_testDirectory2, "important.txt"),
            ["exclude_patterns"] = new[] { "*.txt" }
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        File.Exists(Path.Combine(_testDirectory2, "important.txt")).Should().BeTrue();
        result.ErrorMessage.Should().Match(msg => msg.Contains("excluded") || msg.Contains("pattern"));
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ShouldReturnFailureResult()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["target_path"] = Path.Combine(_testDirectory, "nonexistent.txt")
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Match(msg => msg.Contains("not found") || msg.Contains("does not exist"));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidPath_ShouldReturnFailureResult()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["target_path"] = ""
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Match(msg => msg.Contains("path") || msg.Contains("invalid") || msg.Contains("empty"));
    }

    [Fact]
    public async Task ExecuteAsync_MissingRequiredParameter_ShouldReturnFailureResult()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            // Missing path parameter
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Match(msg => msg.Contains("path") || msg.Contains("required"));
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldCancelGracefully()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory2);
        for (int i = 0; i < 100; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(_testDirectory2, $"file{i}.txt"), "Content");
        }

        var cts = new CancellationTokenSource();
        var parameters = new Dictionary<string, object?>
        {
            ["target_path"] = _testDirectory2,
            ["recursive"] = true
        };

        var context = new ToolExecutionContext { CancellationToken = cts.Token };

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var task = _tool.ExecuteAsync(parameters, context);
        cts.Cancel();
        var result = await task;

        // Assert
        result.Should().NotBeNull();
        if (!result.IsSuccessful)
        {
            result.ErrorMessage.Should().Match(msg => msg.Contains("cancel") || msg.Contains("Cancel"));
        }
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task ExecuteAsync_ShouldProvideStatistics()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFile, "Test content");
        var fileInfo = new FileInfo(_testFile);
        var originalSize = fileInfo.Length;

        var parameters = new Dictionary<string, object?>
        {
            ["target_path"] = _testFile
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data.Should().ContainKey("deleted_items");
        data.Should().ContainKey("bytes_freed");

        data!["deleted_items"].Should().Be(1);
        var bytesFreed = Convert.ToInt64(data["bytes_freed"]);
        bytesFreed.Should().Be(originalSize);
    }

    #endregion

    #region Backup Tests

    [Fact]
    public async Task ExecuteAsync_BackupToDefaultLocation_ShouldCreateBackup()
    {
        // Arrange
        const string content = "Backup test content";
        await File.WriteAllTextAsync(_testFile, content);

        var parameters = new Dictionary<string, object?>
        {
            ["target_path"] = _testFile,
            ["create_backup"] = true
            // No backup_directory specified - should use default
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        File.Exists(_testFile).Should().BeFalse();

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data.Should().ContainKey("backup_location");

        var backupLocation = data!["backup_location"] as string;
        backupLocation.Should().NotBeNull();
        File.Exists(backupLocation!).Should().BeTrue();
        (await File.ReadAllTextAsync(backupLocation!)).Should().Be(content);
    }

    #endregion
}
