using System.IO;
using System.Linq;
using System.Collections;
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

        var context = new ToolExecutionContext { WorkingDirectory = _testDirectory };

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

        var items = data!["items"] as List<FileSystemEntry>;
        items.Should().NotBeNull();
        items.Should().HaveCount(3); // 2 files + 1 directory

        var itemNames = items!.Select(item => item.Name).ToList();
        itemNames.Should().Contain("file1.txt");
        itemNames.Should().Contain("file2.doc");
        itemNames.Should().Contain("subdirectory");
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

        var context = new ToolExecutionContext { WorkingDirectory = _testDirectory };

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

        var items = data!["items"] as List<FileSystemEntry>;
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

        var context = new ToolExecutionContext { WorkingDirectory = _testDirectory };

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        
        var items = data!["items"] as List<FileSystemEntry>;
        items.Should().NotBeNull();
        items.Should().HaveCount(2);
        
        var itemNames = items!.Select(item => item.Name).ToList();
        itemNames.Should().Contain("document1.txt");
        itemNames.Should().Contain("document2.txt");
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

        var context = new ToolExecutionContext { WorkingDirectory = _testDirectory };

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var data = result.Data as Dictionary<string, object?>;
        var items = data!["items"] as List<FileSystemEntry>;
        items.Should().HaveCountGreaterThan(3); // Should include nested files and directories

        // Should include files from subdirectories
        var itemPaths = items!.Select(item => item.FullPath ?? item.Name).ToList();
        itemPaths.Any(path => path.Contains("sub.txt")).Should().BeTrue();
        itemPaths.Any(path => path.Contains("deep.txt")).Should().BeTrue();
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

        var context = new ToolExecutionContext { WorkingDirectory = _testDirectory };

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var data = result.Data as Dictionary<string, object?>;
        var items = data!["items"] as List<FileSystemEntry>;
        items.Should().HaveCount(3);

        items![0].Name.Should().Be("alpha.txt");
        items[1].Name.Should().Be("beta.txt");
        items[2].Name.Should().Be("zebra.txt");
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

        var context = new ToolExecutionContext { WorkingDirectory = _testDirectory };

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var data = result.Data as Dictionary<string, object?>;
        var items = data!["items"] as List<FileSystemEntry>;
        items.Should().HaveCount(3);

        // Should be sorted by size (ascending)
        var sizes = items!.Select(item => item.Size ?? 0L).ToArray();
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

        var context = new ToolExecutionContext { WorkingDirectory = _testDirectory };

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var data = result.Data as Dictionary<string, object?>;
        var items = data!["items"] as List<FileSystemEntry>;
        items.Should().HaveCount(3);

        items![0].Name.Should().Be("oldest.txt");
        items[1].Name.Should().Be("middle.txt");
        items[2].Name.Should().Be("newest.txt");
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

        var context = new ToolExecutionContext { WorkingDirectory = _testDirectory };

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var data = result.Data as Dictionary<string, object?>;
        var items = data!["items"] as List<FileSystemEntry>;

        var itemNames = items!.Select(item => item.Name).ToList();
        itemNames.Should().Contain(".hidden");
        itemNames.Should().Contain(".hidden.txt");
        itemNames.Should().Contain("visible.txt");
    }

    [Fact]
    public async Task ExecuteAsync_HiddenFiles_WithoutShowHidden_ShouldExcludeHiddenFiles()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "visible.txt"), "Content");

        var hiddenFile = Path.Combine(_testDirectory, ".hidden.txt");
        await File.WriteAllTextAsync(hiddenFile, "Hidden content");
        
        // On Windows, we need to set the hidden attribute explicitly
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(hiddenFile, File.GetAttributes(hiddenFile) | FileAttributes.Hidden);
            
            // Verify the attribute was set
            var attrs = File.GetAttributes(hiddenFile);
            attrs.Should().HaveFlag(FileAttributes.Hidden, "Hidden attribute should be set on Windows");
        }

        var parameters = new Dictionary<string, object?>
        {
            ["directory_path"] = _testDirectory,
            ["include_hidden"] = false
        };

        var context = new ToolExecutionContext { WorkingDirectory = _testDirectory };

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var data = result.Data as Dictionary<string, object?>;
        var items = data!["items"] as List<FileSystemEntry>;

        var itemNames = items!.Select(item => item.Name).ToList();
        
        // Debug output on test failure
        if (itemNames.Contains(".hidden.txt"))
        {
            Console.WriteLine($"Test failure: hidden file was included in results");
            Console.WriteLine($"Operating System: {(OperatingSystem.IsWindows() ? "Windows" : "Non-Windows")}");
            Console.WriteLine($"Files found: {string.Join(", ", itemNames)}");
            foreach (var item in items!.Where(i => i.Name == ".hidden.txt"))
            {
                Console.WriteLine($"Hidden file attributes: {item.Attributes}");
                Console.WriteLine($"Hidden file IsHidden: {item.IsHidden}");
            }
        }
        
        itemNames.Should().Contain("visible.txt");
        
        // The hidden file should be excluded regardless of OS
        // On Windows, it's hidden because we set the Hidden attribute
        // On Unix-like systems, it's hidden because it starts with .
        itemNames.Should().NotContain(".hidden.txt");
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

        var context = new ToolExecutionContext { WorkingDirectory = _testDirectory };

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var data = result.Data as Dictionary<string, object?>;
        var items = data!["items"] as List<FileSystemEntry>;

        // With max_depth=2:
        // Depth 0: root.txt, subdirectory
        // Depth 1: level1.txt, level2 directory  
        // Depth 2 and beyond: NOT scanned
        var itemNames = items!.Select(item => item.Name).ToList();
        itemNames.Should().Contain("root.txt");
        itemNames.Should().Contain("subdirectory");
        itemNames.Should().Contain("level1.txt");
        itemNames.Should().Contain("level2"); // directory name
        itemNames.Should().NotContain("level2.txt"); // file inside level2 dir
        itemNames.Should().NotContain("level3.txt");
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

        var context = new ToolExecutionContext { WorkingDirectory = _testDirectory };

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

        var context = new ToolExecutionContext { WorkingDirectory = _testDirectory };

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
    public async Task ExecuteAsync_MissingRequiredParameter_ShouldUseDefaultDirectory()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            // Missing path parameter - should use default "."
        };

        var context = new ToolExecutionContext { WorkingDirectory = _testDirectory };

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue(); // Should succeed with default directory
        
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data.Should().ContainKey("items");
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

        var context = new ToolExecutionContext { WorkingDirectory = _testDirectory };

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue(); // Should succeed even with some access denied

        var data = result.Data as Dictionary<string, object?>;
        var items = data!["items"] as List<FileSystemEntry>;

        var itemNames = items!.Select(item => item.Name).ToList();
        itemNames.Should().Contain("accessible.txt");

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

        var context = new ToolExecutionContext { WorkingDirectory = _testDirectory };

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var data = result.Data as Dictionary<string, object?>;
        var items = data!["items"] as List<FileSystemEntry>;

        var fileItem = items!.First(item => item.Name == "test.txt");
        fileItem.Size.Should().NotBeNull();
        fileItem.Type.Should().NotBeNull();
        fileItem.Created.Should().NotBeNull();
        fileItem.Modified.Should().NotBeNull();
        fileItem.FullPath.Should().NotBeNull();

        fileItem.Size.Should().Be((long)content.Length);
        fileItem.Type.Should().Be("file");
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

        var context = new ToolExecutionContext { WorkingDirectory = _testDirectory };

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var data = result.Data as Dictionary<string, object?>;
        data.Should().ContainKey("total_count");
        data.Should().ContainKey("count");
        
        // These should be in metadata, not in data
        var metadata = result.Metadata;
        metadata.Should().ContainKey("file_count");
        metadata.Should().ContainKey("directory_count");

        data!["total_count"].Should().Be(3);
        metadata["file_count"].Should().Be(2);
        metadata["directory_count"].Should().Be(1);

        // Note: ListDirectoryTool doesn't calculate total_size
    }

    #endregion
}
