using System.IO;
using Andy.Tools.Core;
using Andy.Tools.Library.FileSystem;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Library.FileSystem;

public class CopyFileToolTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _sourceFile;
    private readonly string _destinationFile;
    private readonly string _sourceDirectory;
    private readonly string _destinationDirectory;
    private readonly CopyFileTool _tool;

    public CopyFileToolTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "CopyFileToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        _sourceFile = Path.Combine(_testDirectory, "source.txt");
        _destinationFile = Path.Combine(_testDirectory, "destination.txt");
        _sourceDirectory = Path.Combine(_testDirectory, "source_dir");
        _destinationDirectory = Path.Combine(_testDirectory, "dest_dir");

        _tool = new CopyFileTool();
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
        metadata.Id.Should().Be("copy_file");
        metadata.Name.Should().Be("Copy File");
        metadata.Description.Should().Contain("Copies");
        metadata.Category.Should().Be(ToolCategory.FileSystem);
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
        parameters.Should().Contain(p => p.Name == "recursive" && !p.Required);
    }

    #endregion

    #region File Copy Tests

    [Fact]
    public async Task ExecuteAsync_CopyValidFile_ShouldCopySuccessfully()
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
        File.Exists(_destinationFile).Should().BeTrue();
        (await File.ReadAllTextAsync(_destinationFile)).Should().Be(content);

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data.Should().ContainKey("files_copied");
        data.Should().ContainKey("bytes_copied");
    }

    [Fact]
    public async Task ExecuteAsync_CopyFileWithOverwriteDisabled_ShouldFailWhenDestinationExists()
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
        result.ErrorMessage.Should().Contain("already exists");
        (await File.ReadAllTextAsync(_destinationFile)).Should().Be("Existing content");
    }

    [Fact]
    public async Task ExecuteAsync_CopyFileWithOverwriteEnabled_ShouldOverwriteExistingFile()
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
        (await File.ReadAllTextAsync(_destinationFile)).Should().Be(sourceContent);
    }

    [Fact]
    public async Task ExecuteAsync_CopyFileWithTimestampPreservation_ShouldPreserveTimestamps()
    {
        // Arrange
        await File.WriteAllTextAsync(_sourceFile, "Content");
        var originalTime = DateTime.Now.AddHours(-1);
        File.SetLastWriteTime(_sourceFile, originalTime);

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceFile,
            ["destination_path"] = _destinationFile,
            ["preserve_timestamps"] = true
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        File.GetLastWriteTime(_destinationFile).Should().BeCloseTo(originalTime, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Directory Copy Tests

    [Fact]
    public async Task ExecuteAsync_CopyDirectory_ShouldCopyRecursively()
    {
        // Arrange
        Directory.CreateDirectory(_sourceDirectory);
        var subDir = Path.Combine(_sourceDirectory, "subdir");
        Directory.CreateDirectory(subDir);

        var file1 = Path.Combine(_sourceDirectory, "file1.txt");
        var file2 = Path.Combine(subDir, "file2.txt");

        await File.WriteAllTextAsync(file1, "Content 1");
        await File.WriteAllTextAsync(file2, "Content 2");

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceDirectory,
            ["destination_path"] = _destinationDirectory,
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

        Directory.Exists(_destinationDirectory).Should().BeTrue();
        File.Exists(Path.Combine(_destinationDirectory, "file1.txt")).Should().BeTrue();
        File.Exists(Path.Combine(_destinationDirectory, "subdir", "file2.txt")).Should().BeTrue();

        (await File.ReadAllTextAsync(Path.Combine(_destinationDirectory, "file1.txt"))).Should().Be("Content 1");
        (await File.ReadAllTextAsync(Path.Combine(_destinationDirectory, "subdir", "file2.txt"))).Should().Be("Content 2");
    }

    [Fact]
    public async Task ExecuteAsync_CopyEmptyDirectory_ShouldCreateEmptyDestination()
    {
        // Arrange
        Directory.CreateDirectory(_sourceDirectory);

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceDirectory,
            ["destination_path"] = _destinationDirectory,
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
        Directory.Exists(_destinationDirectory).Should().BeTrue();
        Directory.GetFiles(_destinationDirectory).Should().BeEmpty();
        Directory.GetDirectories(_destinationDirectory).Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_CopyDirectoryWithExcludePatterns_ShouldExcludeMatchingFiles()
    {
        // Arrange
        Directory.CreateDirectory(_sourceDirectory);

        await File.WriteAllTextAsync(Path.Combine(_sourceDirectory, "important.txt"), "Keep this");
        await File.WriteAllTextAsync(Path.Combine(_sourceDirectory, "temp.tmp"), "Exclude this");
        await File.WriteAllTextAsync(Path.Combine(_sourceDirectory, "backup.bak"), "Exclude this too");

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceDirectory,
            ["destination_path"] = _destinationDirectory,
            ["recursive"] = true,
            ["exclude_patterns"] = new[] { "*.tmp", "*.bak" }
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        File.Exists(Path.Combine(_destinationDirectory, "important.txt")).Should().BeTrue();
        File.Exists(Path.Combine(_destinationDirectory, "temp.tmp")).Should().BeFalse();
        File.Exists(Path.Combine(_destinationDirectory, "backup.bak")).Should().BeFalse();
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
        result.ErrorMessage.Should().Contain("Source path");
        result.ErrorMessage.Should().Match(msg => msg.Contains("not found") || msg.Contains("does not exist"));
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
    public async Task ExecuteAsync_WithCancellation_ShouldCancelGracefully()
    {
        // Arrange
        // Create a large file to copy to test cancellation
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
        cts.Cancel(); // Cancel immediately
        var result = await task;

        // Assert
        result.Should().NotBeNull();
        // The result could be successful if the operation completed quickly,
        // or cancelled if it was interrupted
        if (!result.IsSuccessful)
        {
            result.ErrorMessage.Should().Match(msg => msg.Contains("cancel") || msg.Contains("Cancel"));
        }
    }

    #endregion

    #region Path Security Tests

    [Fact]
    public async Task ExecuteAsync_PathTraversalAttempt_ShouldBeHandledSafely()
    {
        // Arrange
        await File.WriteAllTextAsync(_sourceFile, "Content");

        var parameters = new Dictionary<string, object?>
        {
            ["source_path"] = _sourceFile,
            ["destination_path"] = Path.Combine(_testDirectory, "..", "..", "dangerous.txt")
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        // The tool should either handle path traversal safely or reject it
        if (!result.IsSuccessful)
        {
            result.ErrorMessage.Should().NotBeEmpty();
        }
        else
        {
            // If successful, the file should be created in a safe location
            File.Exists(Path.Combine(_testDirectory, "..", "..", "dangerous.txt")).Should().BeFalse();
        }
    }

    #endregion

    #region Progress and Statistics Tests

    [Fact]
    public async Task ExecuteAsync_ShouldProvideStatistics()
    {
        // Arrange
        const string content = "Test content for statistics";
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

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data.Should().ContainKey("files_copied");
        data.Should().ContainKey("bytes_copied");

        data!["files_copied"].Should().Be(1);
        var bytesCopied = Convert.ToInt64(data["bytes_copied"]);
        bytesCopied.Should().BeGreaterThan(0);
        bytesCopied.Should().Be(content.Length);
    }

    #endregion
}
