using System.Text;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;
using Andy.Tools.Library.FileSystem;
using Andy.Tools.Library.Text;
using Xunit;

namespace Andy.Tools.Tests.Library.FileSystem;

/// <summary>
/// Regression tests: file write/edit tools must NOT prepend a UTF-8 BOM. Encoding.UTF8 emits
/// a BOM, which previously corrupted edited source files and their diffs.
/// </summary>
public class FileWriteBomTests : IDisposable
{
    private static readonly byte[] Bom = { 0xEF, 0xBB, 0xBF };

    private readonly string _dir;
    private readonly ToolExecutionContext _context;

    public FileWriteBomTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"andy_bom_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _context = new ToolExecutionContext { Permissions = new ToolPermissions { AllowedPaths = [_dir] } };
    }

    private static bool StartsWithBom(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == Bom[0] && bytes[1] == Bom[1] && bytes[2] == Bom[2];

    [Fact]
    public async Task WriteFile_Utf8_Does_Not_Emit_Bom()
    {
        var path = Path.Combine(_dir, "a.py");
        var tool = new WriteFileTool();
        await tool.InitializeAsync();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["file_path"] = path,
            ["content"] = "\"\"\"module\"\"\"\nx = 1\n",
        }, _context);

        Assert.True(result.IsSuccessful);
        Assert.False(StartsWithBom(await File.ReadAllBytesAsync(path)), "write_file must not prepend a BOM");
    }

    [Fact]
    public async Task ReplaceText_On_BomLess_File_Stays_BomLess()
    {
        // A typical source file with no BOM.
        var path = Path.Combine(_dir, "settings.py");
        await File.WriteAllTextAsync(path, "SECURE_REFERRER_POLICY = None\n", ToolHelpers.Utf8NoBom);
        Assert.False(StartsWithBom(await File.ReadAllBytesAsync(path)));

        var tool = new ReplaceTextTool();
        await tool.InitializeAsync();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["target_path"] = path,
            ["search_pattern"] = "None",
            ["replacement_text"] = "'same-origin'",
            ["search_type"] = "contains",
            ["create_backup"] = false,
        }, _context);

        Assert.True(result.IsSuccessful, $"replace failed: {result.ErrorMessage}");
        var bytes = await File.ReadAllBytesAsync(path);
        Assert.False(StartsWithBom(bytes), "replace_text must not introduce a BOM on a BOM-less file");
        Assert.Contains("'same-origin'", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task ReplaceText_Preserves_An_Existing_Bom()
    {
        // A file that legitimately starts with a BOM should keep it.
        var path = Path.Combine(_dir, "withbom.txt");
        var original = Bom.Concat(Encoding.UTF8.GetBytes("alpha beta\n")).ToArray();
        await File.WriteAllBytesAsync(path, original);

        var tool = new ReplaceTextTool();
        await tool.InitializeAsync();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["target_path"] = path,
            ["search_pattern"] = "beta",
            ["replacement_text"] = "gamma",
            ["search_type"] = "contains",
            ["create_backup"] = false,
        }, _context);

        Assert.True(result.IsSuccessful, $"replace failed: {result.ErrorMessage}");
        Assert.True(StartsWithBom(await File.ReadAllBytesAsync(path)), "an existing BOM must be preserved");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }
}
