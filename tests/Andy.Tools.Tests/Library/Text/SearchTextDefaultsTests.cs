using System.Collections;
using System.Text;
using Andy.Tools.Core;
using Andy.Tools.Library.Text;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Library.Text;

/// <summary>
/// Tests for the issue #63 fixes: default noise-directory excludes, corrected <c>exact</c>
/// whole-line semantics, and searching UTF-16 encoded files.
/// </summary>
public class SearchTextDefaultsTests : IDisposable
{
    private readonly SearchTextTool _tool;
    private readonly ToolExecutionContext _context;
    private readonly string _testDirectory;

    public SearchTextDefaultsTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"andy_search_defaults_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        _tool = new SearchTextTool();
        _tool.InitializeAsync().GetAwaiter().GetResult();
        _context = new ToolExecutionContext
        {
            WorkingDirectory = _testDirectory,
            Permissions = new ToolPermissions { AllowedPaths = new HashSet<string> { _testDirectory } }
        };
    }

    private static int Count(ToolResult result)
    {
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        return items!.Count;
    }

    private async Task PlantTreeAsync()
    {
        var src = Path.Combine(_testDirectory, "src");
        var bin = Path.Combine(_testDirectory, "bin");
        var git = Path.Combine(_testDirectory, ".git");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(bin);
        Directory.CreateDirectory(git);

        await File.WriteAllTextAsync(Path.Combine(src, "code.cs"), "var NEEDLE = 1;");
        await File.WriteAllTextAsync(Path.Combine(bin, "code.cs"), "var NEEDLE = 1;");
        await File.WriteAllTextAsync(Path.Combine(git, "config.txt"), "var NEEDLE = 1;");
    }

    [Fact]
    public async Task DefaultExcludes_SkipsBinAndGit_FindsSrc()
    {
        await PlantTreeAsync();

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["search_pattern"] = "NEEDLE",
            ["target_path"] = _testDirectory
        }, _context);

        result.IsSuccessful.Should().BeTrue();
        Count(result).Should().Be(1); // only src/code.cs; bin/ and .git/ skipped
    }

    [Fact]
    public async Task DefaultExcludesDisabled_FindsBinAndGitToo()
    {
        await PlantTreeAsync();

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["search_pattern"] = "NEEDLE",
            ["target_path"] = _testDirectory,
            ["use_default_excludes"] = false
        }, _context);

        result.IsSuccessful.Should().BeTrue();
        Count(result).Should().Be(3); // src, bin, .git all found
    }

    [Fact]
    public async Task ExactSearch_MatchesWholeLineOnly_NotSubstring()
    {
        var text = "needle\nthis line has needle inside it\nNEEDLE";
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file.txt"), text);

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["search_pattern"] = "needle",
            ["target_path"] = _testDirectory,
            ["search_type"] = "exact",
            ["case_sensitive"] = true
        }, _context);

        result.IsSuccessful.Should().BeTrue();
        Count(result).Should().Be(1); // only the whole-line "needle"
    }

    [Fact]
    public async Task Utf16File_IsSearched_NotSkippedAsBinary()
    {
        var path = Path.Combine(_testDirectory, "utf16.txt");
        // UTF-16 LE with BOM: contains 0x00 bytes that the old heuristic treated as binary.
        await File.WriteAllTextAsync(path, "hello NEEDLE world", new UnicodeEncoding(bigEndian: false, byteOrderMark: true));

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["search_pattern"] = "NEEDLE",
            ["target_path"] = _testDirectory
        }, _context);

        result.IsSuccessful.Should().BeTrue();
        Count(result).Should().Be(1);
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
