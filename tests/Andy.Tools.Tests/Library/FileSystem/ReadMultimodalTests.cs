using Andy.Tools.Core;
using Andy.Tools.Library.FileSystem;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Library.FileSystem;

public class ReadMultimodalTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ReadFileTool _readFile;
    private readonly ReadManyFilesTool _readMany;
    private readonly ToolExecutionContext _context;

    public ReadMultimodalTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"andy_mm_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        _readFile = new ReadFileTool();
        _readFile.InitializeAsync().GetAwaiter().GetResult();
        _readMany = new ReadManyFilesTool();
        _readMany.InitializeAsync().GetAwaiter().GetResult();

        _context = new ToolExecutionContext
        {
            WorkingDirectory = _testDirectory,
            Permissions = new ToolPermissions { AllowedPaths = [_testDirectory] }
        };
    }

    [Fact]
    public async Task ReadFile_OnPng_ReturnsBase64AndMediaType()
    {
        // Arrange: minimal PNG signature bytes.
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03 };
        var filePath = Path.Combine(_testDirectory, "pixel.png");
        await File.WriteAllBytesAsync(filePath, pngBytes);

        // Act
        var result = await _readFile.ExecuteAsync(new Dictionary<string, object?> { ["file_path"] = filePath }, _context);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["media_type"].Should().Be("image/png");
        data["encoding_format"].Should().Be("base64");
        data["content"].Should().Be(Convert.ToBase64String(pngBytes));
    }

    [Fact]
    public async Task ReadFile_OnTextFile_StillReturnsText()
    {
        // Arrange
        var content = "Hello, World!\nplain text";
        var filePath = Path.Combine(_testDirectory, "notes.txt");
        await File.WriteAllTextAsync(filePath, content);

        // Act
        var result = await _readFile.ExecuteAsync(new Dictionary<string, object?> { ["file_path"] = filePath }, _context);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be(content);
        data["content_type"].Should().Be("text/plain");
        data.Should().NotContainKey("encoding_format");
    }

    [Fact]
    public async Task ReadFile_AsBase64Flag_ReturnsBase64ForNonImage()
    {
        // Arrange
        var bytes = new byte[] { 0x10, 0x20, 0x30 };
        var filePath = Path.Combine(_testDirectory, "blob.bin");
        await File.WriteAllBytesAsync(filePath, bytes);

        // Act
        var result = await _readFile.ExecuteAsync(
            new Dictionary<string, object?> { ["file_path"] = filePath, ["as_base64"] = true },
            _context);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["encoding_format"].Should().Be("base64");
        data["media_type"].Should().Be("application/octet-stream");
        data["content"].Should().Be(Convert.ToBase64String(bytes));
    }

    [Fact]
    public async Task ReadManyFiles_TwoExplicitFiles_ReadsBoth()
    {
        // Arrange
        var f1 = Path.Combine(_testDirectory, "a.txt");
        var f2 = Path.Combine(_testDirectory, "b.txt");
        await File.WriteAllTextAsync(f1, "alpha");
        await File.WriteAllTextAsync(f2, "beta");

        // Act
        var result = await _readMany.ExecuteAsync(
            new Dictionary<string, object?> { ["paths"] = new List<string> { "a.txt", "b.txt" } },
            _context);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        var files = data!["files"] as List<Dictionary<string, object?>>;
        files.Should().NotBeNull();
        files!.Should().HaveCount(2);
        files!.Select(f => (string)f["content"]!).Should().BeEquivalentTo(new[] { "alpha", "beta" });
    }

    [Fact]
    public async Task ReadManyFiles_RespectsMaxFiles()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(_testDirectory, $"f{i}.txt"), $"content {i}");
        }

        // Act
        var result = await _readMany.ExecuteAsync(
            new Dictionary<string, object?>
            {
                ["paths"] = new List<string> { "f0.txt", "f1.txt", "f2.txt", "f3.txt", "f4.txt" },
                ["max_files"] = 2
            },
            _context);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        var files = data!["files"] as List<Dictionary<string, object?>>;
        files!.Should().HaveCount(2);
        result.Metadata["limit_reached"].Should().Be(true);
    }

    [Fact]
    public async Task ReadManyFiles_GlobMatchesMultipleFiles()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "one.md"), "# one");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "two.md"), "# two");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "ignore.txt"), "nope");

        // Act
        var result = await _readMany.ExecuteAsync(
            new Dictionary<string, object?> { ["paths"] = new List<string> { "*.md" } },
            _context);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        var files = data!["files"] as List<Dictionary<string, object?>>;
        files!.Should().HaveCount(2);
        files!.Select(f => Path.GetFileName((string)f["path"]!)).Should().BeEquivalentTo(new[] { "one.md", "two.md" });
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Ignore cleanup errors.
        }
    }
}
