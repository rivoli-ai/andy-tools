using System.Text;
using Andy.Tools.Core;
using Andy.Tools.Library.Text;
using Xunit;

namespace Andy.Tools.Tests.Library.Text;

// Regression tests: replacing text in a file must preserve its BOM-ness.
// A non-BOM file must stay non-BOM (Encoding.UTF8 would otherwise inject an
// EF BB BF BOM and corrupt the file / break the patch), and a file that
// genuinely had a BOM must keep it.
public class ReplaceTextToolBomTests : IDisposable
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private readonly string _testDirectory;
    private readonly ReplaceTextTool _tool;

    public ReplaceTextToolBomTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"andy_replace_bom_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _tool = new ReplaceTextTool();
        _tool.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_NonBomFile_StaysNonBom()
    {
        var filePath = Path.Combine(_testDirectory, "calc.py");
        var original = "def add(a, b):\n    return a - b\n";
        await File.WriteAllBytesAsync(filePath, Encoding.UTF8.GetBytes(original)); // bytes only, no BOM

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["search_pattern"] = "return a - b",
            ["replacement_text"] = "return a + b",
            ["target_path"] = filePath,
            ["create_backup"] = false,
        }, new ToolExecutionContext());

        Assert.True(result.IsSuccessful);
        var bytes = await File.ReadAllBytesAsync(filePath);
        Assert.False(StartsWithBom(bytes), "replace_text must not introduce a UTF-8 BOM");
        Assert.Equal("def add(a, b):\n    return a + b\n", await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task ExecuteAsync_FileWithBom_PreservesBom()
    {
        var filePath = Path.Combine(_testDirectory, "withbom.txt");
        var bom = new List<byte>(Utf8Bom);
        bom.AddRange(Encoding.UTF8.GetBytes("hello world"));
        await File.WriteAllBytesAsync(filePath, bom.ToArray());

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "there",
            ["target_path"] = filePath,
            ["create_backup"] = false,
        }, new ToolExecutionContext());

        Assert.True(result.IsSuccessful);
        var bytes = await File.ReadAllBytesAsync(filePath);
        Assert.True(StartsWithBom(bytes), "a file that originally had a BOM should keep it");
        Assert.Equal("hello there", await File.ReadAllTextAsync(filePath));
    }

    private static bool StartsWithBom(byte[] bytes)
        => bytes.Length >= 3 && bytes[0] == Utf8Bom[0] && bytes[1] == Utf8Bom[1] && bytes[2] == Utf8Bom[2];
}
