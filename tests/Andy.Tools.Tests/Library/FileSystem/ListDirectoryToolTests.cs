using System.IO;
using Andy.Tools.Core;
using Andy.Tools.Library.FileSystem;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Library.FileSystem;

public class ListDirectoryToolTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _subDirectory;
    private readonly string _hiddenDirectory;
    private readonly ListDirectoryTool _tool;

    public ListDirectoryToolTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "ListDirectoryToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        _subDirectory = Path.Combine(_testDirectory, "subdirectory");
        _hiddenDirectory = Path.Combine(_testDirectory, ".hidden");

        _tool = new ListDirectoryTool();
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
        metadata.Id.Should().Be("list_directory");
        metadata.Name.Should().Be("List Directory");
        metadata.Description.Should().Contain("directory");
        metadata.Category.Should().Be(ToolCategory.FileSystem);
        metadata.Parameters.Should().NotBeEmpty();
    }

    [Fact]
    public void Metadata_ShouldHaveRequiredParameters()
    {
        // Act
        var parameters = _tool.Metadata.Parameters;

        // Assert
        parameters.Should().Contain(p => p.Name == "directory_path" && !p.Required);
        parameters.Should().Contain(p => p.Name == "recursive" && !p.Required);
        parameters.Should().Contain(p => p.Name == "include_hidden" && !p.Required);
        parameters.Should().Contain(p => p.Name == "pattern" && !p.Required);
    }

    #endregion

    #region Basic Listing Tests

    [Fact]
    public async Task ExecuteAsync_ListValidDirectory_ShouldReturnFileList()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file1.txt"), "Content 1");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file2.doc"), "Content 2");
        Directory.CreateDirectory(_subDirectory);

        var parameters = new Dictionary<string, object?>
        {
            ["directory_path"] = _testDirectory
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
        data.Should().ContainKey("items");

        var items = data!["items"] as List<Dictionary<string, object?>>;
        items.Should().NotBeNull();
        items.Should().HaveCount(3); // 2 files + 1 directory

        items.Should().Contain(item =>
            item.ContainsKey("name") && item["name"]!.ToString() == "file1.txt");
        items.Should().Contain(item =>
            item.ContainsKey("name") && item["name"]!.ToString() == "file2.doc");
        items.Should().Contain(item =>
            item.ContainsKey("name") && item["name"]!.ToString() == "subdirectory");
    }

    [Fact]
    public async Task ExecuteAsync_ListEmptyDirectory_ShouldReturnEmptyList()
    {
        // Arrange
        var emptyDir = Path.Combine(_testDirectory, "empty");
        Directory.CreateDirectory(emptyDir);

        var parameters = new Dictionary<string, object?>
        {
            ["directory_path"] = emptyDir
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
        data.Should().ContainKey("items");

        var items = data!["items"] as List<Dictionary<string, object?>>;
        items.Should().NotBeNull();
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ListWithPattern_ShouldFilterByPattern()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "document1.txt"), "Content");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "document2.txt"), "Content");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "image.jpg"), "Content");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "data.csv"), "Content");

        var parameters = new Dictionary<string, object?>
        {
            ["directory_path"] = _testDirectory,
            ["pattern"] = "*.txt"
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
        var items = data!["items"] as List<Dictionary<string, object?>>;
        items.Should().HaveCount(2);
        items.Should().Contain(item => item["name"]!.ToString() == "document1.txt");
        items.Should().Contain(item => item["name"]!.ToString() == "document2.txt");
    }

    [Fact]
    public async Task ExecuteAsync_ListRecursively_ShouldIncludeSubdirectories()
    {
        // Arrange
        Directory.CreateDirectory(_subDirectory);
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "root.txt"), "Root file");
        await File.WriteAllTextAsync(Path.Combine(_subDirectory, "sub.txt"), "Sub file");

        var deepDir = Path.Combine(_subDirectory, "deep");
        Directory.CreateDirectory(deepDir);
        await File.WriteAllTextAsync(Path.Combine(deepDir, "deep.txt"), "Deep file");

        var parameters = new Dictionary<string, object?>
        {
            ["directory_path"] = _testDirectory,
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

        var data = result.Data as Dictionary<string, object?>;
        var items = data!["items"] as List<Dictionary<string, object?>>;
        items.Should().HaveCountGreaterThan(3); // Should include nested files and directories

        // Should include files from subdirectories
        items.Should().Contain(item =>
            item.ContainsKey("path") &&
            item["path"]!.ToString()!.Contains("sub.txt"));
        items.Should().Contain(item =>
            item.ContainsKey("path") &&
            item["path"]!.ToString()!.Contains("deep.txt"));
    }

    #endregion

    #region Sorting Tests

    [Fact]
    public async Task ExecuteAsync_SortByName_ShouldSortAlphabetically()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "zebra.txt"), "Content");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "alpha.txt"), "Content");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "beta.txt"), "Content");

        var parameters = new Dictionary<string, object?>
        {
            ["directory_path"] = _testDirectory,
            ["sort_by"] = "name"
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
        var items = data!["items"] as List<Dictionary<string, object?>>;
        items.Should().HaveCount(3);

        items[0]["name"]!.ToString().Should().Be("alpha.txt");
        items[1]["name"]!.ToString().Should().Be("beta.txt");
        items[2]["name"]!.ToString().Should().Be("zebra.txt");
    }

    [Fact]
    public async Task ExecuteAsync_SortBySize_ShouldSortByFileSize()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "large.txt"), new string('A', 1000));
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "small.txt"), "A");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "medium.txt"), new string('A', 100));

        var parameters = new Dictionary<string, object?>
        {
            ["directory_path"] = _testDirectory,
            ["sort_by"] = "size"
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
        var items = data!["items"] as List<Dictionary<string, object?>>;
        items.Should().HaveCount(3);

        // Should be sorted by size (ascending)
        var sizes = items.Select(item => Convert.ToInt64(item["size"])).ToArray();
        sizes.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ExecuteAsync_SortByModifiedDate_ShouldSortByTimestamp()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "oldest.txt");
        var file2 = Path.Combine(_testDirectory, "newest.txt");
        var file3 = Path.Combine(_testDirectory, "middle.txt");

        await File.WriteAllTextAsync(file1, "Content");
        await File.WriteAllTextAsync(file2, "Content");
        await File.WriteAllTextAsync(file3, "Content");

        // Set different modification times
        File.SetLastWriteTime(file1, DateTime.Now.AddHours(-2));
        File.SetLastWriteTime(file2, DateTime.Now);
        File.SetLastWriteTime(file3, DateTime.Now.AddHours(-1));

        var parameters = new Dictionary<string, object?>
        {
            ["directory_path"] = _testDirectory,
            ["sort_by"] = "modified"
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
        var items = data!["items"] as List<Dictionary<string, object?>>;
        items.Should().HaveCount(3);

        items[0]["name"]!.ToString().Should().Be("oldest.txt");
        items[1]["name"]!.ToString().Should().Be("middle.txt");
        items[2]["name"]!.ToString().Should().Be("newest.txt");
    }

    #endregion

    #region Hidden Files Tests

    [Fact]
    public async Task ExecuteAsync_HiddenFiles_WithShowHidden_ShouldIncludeHiddenFiles()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "visible.txt"), "Content");

        // Create hidden directory and file
        Directory.CreateDirectory(_hiddenDirectory);
        var hiddenFile = Path.Combine(_testDirectory, ".hidden.txt");
        await File.WriteAllTextAsync(hiddenFile, "Hidden content");

        // Set hidden attribute on Windows
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(hiddenFile, FileAttributes.Hidden);
            File.SetAttributes(_hiddenDirectory, FileAttributes.Hidden);
        }

        var parameters = new Dictionary<string, object?>
        {
            ["directory_path"] = _testDirectory,
            ["include_hidden"] = true
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
        var items = data!["items"] as List<Dictionary<string, object?>>;

        items.Should().Contain(item => item["name"]!.ToString() == ".hidden");
        items.Should().Contain(item => item["name"]!.ToString() == ".hidden.txt");
        items.Should().Contain(item => item["name"]!.ToString() == "visible.txt");
    }

    [Fact]
    public async Task ExecuteAsync_HiddenFiles_WithoutShowHidden_ShouldExcludeHiddenFiles()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "visible.txt"), "Content");

        var hiddenFile = Path.Combine(_testDirectory, ".hidden.txt");
        await File.WriteAllTextAsync(hiddenFile, "Hidden content");

        var parameters = new Dictionary<string, object?>
        {
            ["directory_path"] = _testDirectory,
            ["include_hidden"] = false
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
        var items = data!["items"] as List<Dictionary<string, object?>>;

        items.Should().Contain(item => item["name"]!.ToString() == "visible.txt");
        items.Should().NotContain(item => item["name"]!.ToString()!.StartsWith("."));
    }

    #endregion

    #region Depth Limit Tests

    [Fact]
    public async Task ExecuteAsync_WithMaxDepth_ShouldRespectDepthLimit()
    {
        // Arrange
        Directory.CreateDirectory(_subDirectory);
        var level2 = Path.Combine(_subDirectory, "level2");
        Directory.CreateDirectory(level2);
        var level3 = Path.Combine(level2, "level3");
        Directory.CreateDirectory(level3);

        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "root.txt"), "Root");
        await File.WriteAllTextAsync(Path.Combine(_subDirectory, "level1.txt"), "Level 1");
        await File.WriteAllTextAsync(Path.Combine(level2, "level2.txt"), "Level 2");
        await File.WriteAllTextAsync(Path.Combine(level3, "level3.txt"), "Level 3");

        var parameters = new Dictionary<string, object?>
        {
            ["directory_path"] = _testDirectory,
            ["recursive"] = true,
            ["max_depth"] = 2
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
        var items = data!["items"] as List<Dictionary<string, object?>>;

        // Should include root.txt, subdirectory, level1.txt, level2 directory, level2.txt
        // Should NOT include level3 directory or level3.txt
        items.Should().Contain(item => item["name"]!.ToString() == "root.txt");
        items.Should().Contain(item => item["name"]!.ToString() == "level1.txt");
        items.Should().Contain(item => item["name"]!.ToString() == "level2.txt");
        items.Should().NotContain(item => item["name"]!.ToString() == "level3.txt");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_DirectoryNotFound_ShouldReturnFailureResult()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["directory_path"] = Path.Combine(_testDirectory, "nonexistent")
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
            ["directory_path"] = ""
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
    public async Task ExecuteAsync_AccessDenied_ShouldSkipInaccessibleDirectories()
    {
        // Arrange - This test might be platform-specific
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "accessible.txt"), "Content");

        // On Unix systems, create a directory with no read permissions
        if (!OperatingSystem.IsWindows())
        {
            var restrictedDir = Path.Combine(_testDirectory, "restricted");
            Directory.CreateDirectory(restrictedDir);
            await File.WriteAllTextAsync(Path.Combine(restrictedDir, "restricted.txt"), "Content");

            // Remove read permissions
            File.SetUnixFileMode(restrictedDir, UnixFileMode.None);
        }

        var parameters = new Dictionary<string, object?>
        {
            ["directory_path"] = _testDirectory,
            ["recursive"] = true
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue(); // Should succeed even with some access denied

        var data = result.Data as Dictionary<string, object?>;
        var items = data!["items"] as List<Dictionary<string, object?>>;

        items.Should().Contain(item => item["name"]!.ToString() == "accessible.txt");

        // Clean up - restore permissions
        if (!OperatingSystem.IsWindows())
        {
            var restrictedDir = Path.Combine(_testDirectory, "restricted");
            if (Directory.Exists(restrictedDir))
            {
                File.SetUnixFileMode(restrictedDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldCancelGracefully()
    {
        // Arrange
        // Create many files to ensure the operation takes some time
        for (int i = 0; i < 1000; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(_testDirectory, $"file{i}.txt"), "Content");
        }

        var cts = new CancellationTokenSource();
        var parameters = new Dictionary<string, object?>
        {
            ["directory_path"] = _testDirectory,
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

    #region File Information Tests

    [Fact]
    public async Task ExecuteAsync_ShouldProvideDetailedFileInformation()
    {
        // Arrange
        const string content = "Test file content";
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "test.txt"), content);

        var parameters = new Dictionary<string, object?>
        {
            ["directory_path"] = _testDirectory,
            ["include_details"] = true
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
        var items = data!["items"] as List<Dictionary<string, object?>>;

        var fileItem = items!.First(item => item["name"]!.ToString() == "test.txt");
        fileItem.Should().ContainKey("size");
        fileItem.Should().ContainKey("type");
        fileItem.Should().ContainKey("created");
        fileItem.Should().ContainKey("modified");
        fileItem.Should().ContainKey("path");

        fileItem["size"].Should().Be((long)content.Length);
        fileItem["type"].Should().Be("file");
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task ExecuteAsync_ShouldProvideStatistics()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file1.txt"), "Content 1");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file2.txt"), "Content 2");
        Directory.CreateDirectory(_subDirectory);

        var parameters = new Dictionary<string, object?>
        {
            ["directory_path"] = _testDirectory
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
        data.Should().ContainKey("total_items");
        data.Should().ContainKey("total_files");
        data.Should().ContainKey("total_directories");
        data.Should().ContainKey("total_size");

        data!["total_items"].Should().Be(3);
        data["total_files"].Should().Be(2);
        data["total_directories"].Should().Be(1);

        var totalSize = Convert.ToInt64(data["total_size"]);
        totalSize.Should().BeGreaterThan(0);
    }

    #endregion
}
