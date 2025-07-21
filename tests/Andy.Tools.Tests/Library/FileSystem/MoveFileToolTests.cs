using System.IO;
using Andy.Tools.Core;
using Andy.Tools.Library.FileSystem;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Library.FileSystem;

public class MoveFileToolTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _sourceFile;
    private readonly string _destinationFile;
    private readonly string _sourceDirectory;
    private readonly string _destinationDirectory;
    private readonly string _backupDirectory;
    private readonly MoveFileTool _tool;

    public MoveFileToolTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "MoveFileToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        _sourceFile = Path.Combine(_testDirectory, "source.txt");
        _destinationFile = Path.Combine(_testDirectory, "destination.txt");
        _sourceDirectory = Path.Combine(_testDirectory, "source_dir");
        _destinationDirectory = Path.Combine(_testDirectory, "dest_dir");
        _backupDirectory = Path.Combine(_testDirectory, "backups");

        _tool = new MoveFileTool();
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
        metadata.Id.Should().NotBeNullOrEmpty();
        metadata.Name.Should().NotBeNullOrEmpty();
        metadata.Description.Should().NotBeNullOrEmpty();
        metadata.Category.Should().Be(ToolCategory.FileSystem);
        metadata.Id.Should().Be("move_file");
        metadata.Name.Should().Be("Move File");
        metadata.Description.Should().Contain("move");
        metadata.Parameters.Should().NotBeEmpty();
    }

    [Fact]
    public void Metadata_ShouldHaveRequiredParameters()
    {
        // Act
        var parameters = _tool.Metadata.Parameters;

        // Assert
        parameters.Should().Contain(p => p.Name == "source_path" && p.Required);
        parameters.Should().Contain(p => p.Name == "destination_path" && p.Required);
        parameters.Should().Contain(p => p.Name == "overwrite" && !p.Required);
        parameters.Should().Contain(p => p.Name == "create_backup" && !p.Required);
    }

    #endregion

    #region File Move Tests

    [Fact]
    public async Task ExecuteAsync_MoveValidFile_ShouldMoveSuccessfully()
    {
        // Arrange
        const string content = "Test file content";
        await File.WriteAllTextAsync(_sourceFile, content);

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceFile,
            ["destination_path"] = _destinationFile
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        File.Exists(_sourceFile).Should().BeFalse();
        File.Exists(_destinationFile).Should().BeTrue();
        (await File.ReadAllTextAsync(_destinationFile)).Should().Be(content);

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data.Should().ContainKey("moved_items");
        data!["moved_items"].Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_RenameFileInSameDirectory_ShouldRenameSuccessfully()
    {
        // Arrange
        const string content = "Test content for rename";
        await File.WriteAllTextAsync(_sourceFile, content);

        var newName = Path.Combine(_testDirectory, "renamed.txt");

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceFile,
            ["destination_path"] = newName
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        File.Exists(_sourceFile).Should().BeFalse();
        File.Exists(newName).Should().BeTrue();
        (await File.ReadAllTextAsync(newName)).Should().Be(content);
    }

    [Fact]
    public async Task ExecuteAsync_MoveFileWithoutOverwrite_WhenDestinationExists_ShouldFail()
    {
        // Arrange
        await File.WriteAllTextAsync(_sourceFile, "Source content");
        await File.WriteAllTextAsync(_destinationFile, "Existing content");

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceFile,
            ["destination_path"] = _destinationFile,
            ["overwrite"] = false
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Match(msg => msg.Contains("already exists") || msg.Contains("destination exists"));
        File.Exists(_sourceFile).Should().BeTrue();
        (await File.ReadAllTextAsync(_destinationFile)).Should().Be("Existing content");
    }

    [Fact]
    public async Task ExecuteAsync_MoveFileWithOverwrite_ShouldOverwriteDestination()
    {
        // Arrange
        const string sourceContent = "New content";
        await File.WriteAllTextAsync(_sourceFile, sourceContent);
        await File.WriteAllTextAsync(_destinationFile, "Old content");

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceFile,
            ["destination_path"] = _destinationFile,
            ["overwrite"] = true
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        File.Exists(_sourceFile).Should().BeFalse();
        (await File.ReadAllTextAsync(_destinationFile)).Should().Be(sourceContent);
    }

    [Fact]
    public async Task ExecuteAsync_MoveFileWithBackup_ShouldCreateBackupOfDestination()
    {
        // Arrange
        const string sourceContent = "New content";
        const string existingContent = "Existing content to backup";
        await File.WriteAllTextAsync(_sourceFile, sourceContent);
        await File.WriteAllTextAsync(_destinationFile, existingContent);

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceFile,
            ["destination_path"] = _destinationFile,
            ["overwrite"] = true,
            ["create_backup"] = true,
            ["backup_directory"] = _backupDirectory
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        File.Exists(_sourceFile).Should().BeFalse();
        (await File.ReadAllTextAsync(_destinationFile)).Should().Be(sourceContent);

        // Backup should exist
        Directory.Exists(_backupDirectory).Should().BeTrue();
        var backupFiles = Directory.GetFiles(_backupDirectory);
        backupFiles.Should().HaveCount(1);
        (await File.ReadAllTextAsync(backupFiles[0])).Should().Be(existingContent);

        var data = result.Data as Dictionary<string, object?>;
        data.Should().ContainKey("backup_created");
        data!["backup_created"].Should().Be(true);
    }

    #endregion

    #region Directory Move Tests

    [Fact]
    public async Task ExecuteAsync_MoveDirectory_ShouldMoveAllContents()
    {
        // Arrange
        Directory.CreateDirectory(_sourceDirectory);
        var subDir = Path.Combine(_sourceDirectory, "subdir");
        Directory.CreateDirectory(subDir);

        await File.WriteAllTextAsync(Path.Combine(_sourceDirectory, "file1.txt"), "Content 1");
        await File.WriteAllTextAsync(Path.Combine(subDir, "file2.txt"), "Content 2");

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceDirectory,
            ["destination_path"] = _destinationDirectory
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        Directory.Exists(_sourceDirectory).Should().BeFalse();
        Directory.Exists(_destinationDirectory).Should().BeTrue();
        File.Exists(Path.Combine(_destinationDirectory, "file1.txt")).Should().BeTrue();
        File.Exists(Path.Combine(_destinationDirectory, "subdir", "file2.txt")).Should().BeTrue();

        (await File.ReadAllTextAsync(Path.Combine(_destinationDirectory, "file1.txt"))).Should().Be("Content 1");
        (await File.ReadAllTextAsync(Path.Combine(_destinationDirectory, "subdir", "file2.txt"))).Should().Be("Content 2");
    }

    [Fact]
    public async Task ExecuteAsync_MoveEmptyDirectory_ShouldMoveSuccessfully()
    {
        // Arrange
        Directory.CreateDirectory(_sourceDirectory);

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceDirectory,
            ["destination_path"] = _destinationDirectory
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        Directory.Exists(_sourceDirectory).Should().BeFalse();
        Directory.Exists(_destinationDirectory).Should().BeTrue();
        Directory.GetFiles(_destinationDirectory).Should().BeEmpty();
        Directory.GetDirectories(_destinationDirectory).Should().BeEmpty();
    }

    #endregion

    #region Cross-Volume Move Tests

    [Fact]
    public async Task ExecuteAsync_CrossVolumeMove_ShouldHandleCorrectly()
    {
        // Arrange
        const string content = "Cross-volume content";
        await File.WriteAllTextAsync(_sourceFile, content);

        // For testing purposes, we'll simulate a cross-volume move by using a different temp directory
        var crossVolumeDestination = Path.Combine(Path.GetTempPath(), "CrossVolumeTest", "destination.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(crossVolumeDestination)!);

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceFile,
            ["destination_path"] = crossVolumeDestination
        };

        var context = new ToolExecutionContext();

        try
        {
            // Act
            var result = await _tool.ExecuteAsync(parameters, context);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccessful.Should().BeTrue();
            File.Exists(_sourceFile).Should().BeFalse();
            File.Exists(crossVolumeDestination).Should().BeTrue();
            (await File.ReadAllTextAsync(crossVolumeDestination)).Should().Be(content);
        }
        finally
        {
            // Cleanup
            if (File.Exists(crossVolumeDestination))
            {
                File.Delete(crossVolumeDestination);
            }

            var dir = Path.GetDirectoryName(crossVolumeDestination)!;
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_SourceNotFound_ShouldReturnFailureResult()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = Path.Combine(_testDirectory, "nonexistent.txt"),
            ["destination_path"] = _destinationFile
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Match(msg => msg.Contains("source") && (msg.Contains("not found") || msg.Contains("does not exist")));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidSourcePath_ShouldReturnFailureResult()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = "",
            ["destination_path"] = _destinationFile
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Match(msg => msg.Contains("source_path") || msg.Contains("invalid") || msg.Contains("empty"));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidDestinationPath_ShouldReturnFailureResult()
    {
        // Arrange
        await File.WriteAllTextAsync(_sourceFile, "Content");

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceFile,
            ["destination_path"] = ""
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Match(msg => msg.Contains("destination_path") || msg.Contains("invalid") || msg.Contains("empty"));
    }

    [Fact]
    public async Task ExecuteAsync_MissingRequiredParameters_ShouldReturnFailureResult()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceFile
            // Missing destination_path
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Match(msg => msg.Contains("destination_path") || msg.Contains("required"));
    }

    [Fact]
    public async Task ExecuteAsync_SameSourceAndDestination_ShouldReturnFailureResult()
    {
        // Arrange
        await File.WriteAllTextAsync(_sourceFile, "Content");

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceFile,
            ["destination_path"] = _sourceFile // Same as source
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Match(msg => msg.Contains("same") || msg.Contains("identical"));
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldCancelGracefully()
    {
        // Arrange
        // Create a large file to ensure the move operation takes some time
        await File.WriteAllTextAsync(_sourceFile, new string('A', 1000000)); // 1MB file

        var cts = new CancellationTokenSource();
        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceFile,
            ["destination_path"] = _destinationFile
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

    #region Validation Tests

    [Fact]
    public async Task ExecuteAsync_MoveToSubdirectory_ShouldReturnFailureResult()
    {
        // Arrange
        Directory.CreateDirectory(_sourceDirectory);
        var subdirectoryPath = Path.Combine(_sourceDirectory, "subdirectory");

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceDirectory,
            ["destination_path"] = subdirectoryPath
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Match(msg => msg.Contains("subdirectory") || msg.Contains("inside") || msg.Contains("circular"));
    }

    [Fact]
    public async Task ExecuteAsync_CreateDestinationDirectory_ShouldCreateParentDirectories()
    {
        // Arrange
        await File.WriteAllTextAsync(_sourceFile, "Content");
        var nestedDestination = Path.Combine(_testDirectory, "level1", "level2", "destination.txt");

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceFile,
            ["destination_path"] = nestedDestination,
            ["create_destination_directory"] = true
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        File.Exists(_sourceFile).Should().BeFalse();
        File.Exists(nestedDestination).Should().BeTrue();
        Directory.Exists(Path.GetDirectoryName(nestedDestination)!).Should().BeTrue();
    }

    #endregion

    #region Permission Tests

    [Fact]
    public async Task ExecuteAsync_MoveReadOnlyFile_ShouldHandlePermissions()
    {
        // Arrange
        await File.WriteAllTextAsync(_sourceFile, "Read-only content");
        File.SetAttributes(_sourceFile, FileAttributes.ReadOnly);

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceFile,
            ["destination_path"] = _destinationFile
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        File.Exists(_sourceFile).Should().BeFalse();
        File.Exists(_destinationFile).Should().BeTrue();

        // The read-only attribute should be preserved
        var attributes = File.GetAttributes(_destinationFile);
        attributes.Should().HaveFlag(FileAttributes.ReadOnly);
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task ExecuteAsync_ShouldProvideStatistics()
    {
        // Arrange
        const string content = "Test content for statistics";
        await File.WriteAllTextAsync(_sourceFile, content);
        var originalSize = new FileInfo(_sourceFile).Length;

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceFile,
            ["destination_path"] = _destinationFile
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
        data.Should().ContainKey("moved_items");
        data.Should().ContainKey("bytes_moved");

        data!["moved_items"].Should().Be(1);
        var bytesMoved = Convert.ToInt64(data["bytes_moved"]);
        bytesMoved.Should().Be(originalSize);
    }

    [Fact]
    public async Task ExecuteAsync_MoveDirectoryWithStatistics_ShouldCountAllItems()
    {
        // Arrange
        Directory.CreateDirectory(_sourceDirectory);
        await File.WriteAllTextAsync(Path.Combine(_sourceDirectory, "file1.txt"), "Content 1");
        await File.WriteAllTextAsync(Path.Combine(_sourceDirectory, "file2.txt"), "Content 2");

        var subDir = Path.Combine(_sourceDirectory, "subdir");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "file3.txt"), "Content 3");

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceDirectory,
            ["destination_path"] = _destinationDirectory
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

        var movedItems = Convert.ToInt32(data!["moved_items"]);
        movedItems.Should().BeGreaterThan(1); // Should include directory and all files

        var bytesMoved = Convert.ToInt64(data["bytes_moved"]);
        bytesMoved.Should().BeGreaterThan(0);
    }

    #endregion
}
