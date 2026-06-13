using Andy.Tools.Core;
using Andy.Tools.Library.FileSystem;
using Xunit;

namespace Andy.Tools.Tests.Library.FileSystem;

// Regression tests: writing UTF-8 must not prepend a byte-order mark.
// Encoding.UTF8 emits an EF BB BF BOM when handed to File.WriteAllText, which
// silently corrupts source files that had none and breaks diffs/patches.
public class WriteFileToolBomTests : IDisposable
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private readonly string _testDirectory;
    private readonly WriteFileTool _tool;
    private readonly ToolExecutionContext _context;

    public WriteFileToolBomTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"andy_bom_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        _tool = new WriteFileTool();
        _tool.InitializeAsync().GetAwaiter().GetResult();
        _context = new ToolExecutionContext
        {
            Permissions = new ToolPermissions { AllowedPaths = [_testDirectory] }
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_NewUtf8File_DoesNotEmitBom()
    {
        var filePath = Path.Combine(_testDirectory, "new.py");
        var content = "def add(a, b):\n    return a + b\n";

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["file_path"] = filePath,
            ["content"] = content,
        }, _context);

        Assert.True(result.IsSuccessful);
        var bytes = await File.ReadAllBytesAsync(filePath);
        Assert.False(StartsWithBom(bytes), "write_file must not prepend a UTF-8 BOM");
        Assert.Equal((byte)'d', bytes[0]);
        Assert.Equal(content, await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task ExecuteAsync_ExplicitUtf8_DoesNotEmitBom()
    {
        var filePath = Path.Combine(_testDirectory, "explicit.txt");

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["file_path"] = filePath,
            ["content"] = "hello",
            ["encoding"] = "utf-8",
        }, _context);

        Assert.True(result.IsSuccessful);
        var bytes = await File.ReadAllBytesAsync(filePath);
        Assert.False(StartsWithBom(bytes));
    }

    [Fact]
    public async Task ExecuteAsync_OverwriteNonBomFile_StaysNonBom()
    {
        var filePath = Path.Combine(_testDirectory, "existing.txt");
        await File.WriteAllTextAsync(filePath, "original");

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["file_path"] = filePath,
            ["content"] = "updated",
            ["create_backup"] = false,
        }, _context);

        Assert.True(result.IsSuccessful);
        var bytes = await File.ReadAllBytesAsync(filePath);
        Assert.False(StartsWithBom(bytes));
    }

    private static bool StartsWithBom(byte[] bytes)
        => bytes.Length >= 3 && bytes[0] == Utf8Bom[0] && bytes[1] == Utf8Bom[1] && bytes[2] == Utf8Bom[2];
}
