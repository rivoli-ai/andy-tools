using System.Collections.Generic;
using System.Linq;
using Andy.Tools.CodeAnalysis;
using Andy.Tools.Core;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.CodeAnalysis.Tests;

public class ListDefinitionsToolTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ListDefinitionsTool _tool;
    private readonly ToolExecutionContext _context;

    public ListDefinitionsToolTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"andy_listdefs_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        _tool = new ListDefinitionsTool();
        _tool.InitializeAsync().GetAwaiter().GetResult();
        _context = new ToolExecutionContext
        {
            WorkingDirectory = _testDirectory,
            Permissions = new ToolPermissions
            {
                FileSystemAccess = true,
                AllowedPaths = [_testDirectory]
            }
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }

        GC.SuppressFinalize(this);
    }

    private static List<Dictionary<string, object?>> GetDefinitions(ToolResult result)
    {
        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        var data = result.Data.Should().BeAssignableTo<IEnumerable<object?>>().Subject;
        return data.Cast<Dictionary<string, object?>>().ToList();
    }

    [Fact]
    public async Task ExecuteAsync_FindsNamespaceClassMembersAndRecord()
    {
        var source =
            "namespace Sample.App\n" +
            "{\n" +
            "    public class Calculator\n" +
            "    {\n" +
            "        private int _count;\n" +
            "\n" +
            "        public int Total { get; set; }\n" +
            "\n" +
            "        public int Add(int a, int b)\n" +
            "        {\n" +
            "            return a + b;\n" +
            "        }\n" +
            "    }\n" +
            "\n" +
            "    public record Point(int X, int Y);\n" +
            "}\n";

        var filePath = Path.Combine(_testDirectory, "Sample.cs");
        await File.WriteAllTextAsync(filePath, source);

        var result = await _tool.ExecuteAsync(
            new Dictionary<string, object?> { ["file_path"] = filePath },
            _context);

        var defs = GetDefinitions(result);

        defs.Should().Contain(d => (string)d["kind"]! == "namespace" && (string)d["name"]! == "Sample.App");
        defs.Should().Contain(d => (string)d["kind"]! == "class" && (string)d["name"]! == "Calculator");
        defs.Should().Contain(d => (string)d["kind"]! == "field" && (string)d["name"]! == "_count");
        defs.Should().Contain(d => (string)d["kind"]! == "property" && (string)d["name"]! == "Total");
        defs.Should().Contain(d => (string)d["kind"]! == "method" && (string)d["name"]! == "Add");
        defs.Should().Contain(d => (string)d["kind"]! == "record" && (string)d["name"]! == "Point");
    }

    [Fact]
    public async Task ExecuteAsync_ReportsPlausibleOneBasedLineNumbersAndContainer()
    {
        var source =
            "namespace Sample.App\n" + // line 1
            "{\n" +                     // line 2
            "    public class Calculator\n" + // line 3
            "    {\n" +
            "        public int Add(int a, int b) { return a + b; }\n" + // line 5
            "    }\n" +
            "}\n";

        var filePath = Path.Combine(_testDirectory, "Lines.cs");
        await File.WriteAllTextAsync(filePath, source);

        var result = await _tool.ExecuteAsync(
            new Dictionary<string, object?> { ["file_path"] = filePath },
            _context);

        var defs = GetDefinitions(result);

        var cls = defs.Single(d => (string)d["kind"]! == "class");
        ((int)cls["startLine"]!).Should().Be(3);
        ((int)cls["endLine"]!).Should().Be(6);

        var method = defs.Single(d => (string)d["kind"]! == "method");
        ((int)method["startLine"]!).Should().Be(5);
        ((string?)method["containerName"]).Should().Be("Calculator");
        ((string)method["signature"]!).Should().Contain("Add");

        // Definitions are ordered by start line.
        var startLines = defs.Select(d => (int)d["startLine"]!).ToList();
        startLines.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ExecuteAsync_ToleratesSyntaxErrors_StillReturnsParsedDefinitions()
    {
        // Missing closing brace on the method body / class.
        var source =
            "public class Broken\n" +
            "{\n" +
            "    public void Go() {\n" +
            "}\n";

        var filePath = Path.Combine(_testDirectory, "Broken.cs");
        await File.WriteAllTextAsync(filePath, source);

        var result = await _tool.ExecuteAsync(
            new Dictionary<string, object?> { ["file_path"] = filePath },
            _context);

        var defs = GetDefinitions(result);
        defs.Should().Contain(d => (string)d["kind"]! == "class" && (string)d["name"]! == "Broken");
        defs.Should().Contain(d => (string)d["kind"]! == "method" && (string)d["name"]! == "Go");
    }

    [Fact]
    public async Task ExecuteAsync_PathOutsideAllowedPaths_IsRejected()
    {
        // A second directory that is NOT in AllowedPaths.
        var outsideDir = Path.Combine(Path.GetTempPath(), $"andy_listdefs_outside_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outsideDir);
        try
        {
            var filePath = Path.Combine(outsideDir, "Outside.cs");
            await File.WriteAllTextAsync(filePath, "public class Outside { }\n");

            // Use a context whose working directory is the outside dir so GetSafePath resolves it,
            // but whose AllowedPaths still only contains the test directory.
            var context = new ToolExecutionContext
            {
                WorkingDirectory = outsideDir,
                Permissions = new ToolPermissions
                {
                    FileSystemAccess = true,
                    AllowedPaths = [_testDirectory]
                }
            };

            var result = await _tool.ExecuteAsync(
                new Dictionary<string, object?> { ["file_path"] = filePath },
                context);

            result.IsSuccessful.Should().BeFalse();
            result.Metadata.Should().ContainKey("error_code");
            result.Metadata["error_code"].Should().Be("PATH_NOT_ALLOWED");
        }
        finally
        {
            Directory.Delete(outsideDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_NonCsFile_IsRejected()
    {
        var filePath = Path.Combine(_testDirectory, "notes.txt");
        await File.WriteAllTextAsync(filePath, "not csharp");

        var result = await _tool.ExecuteAsync(
            new Dictionary<string, object?> { ["file_path"] = filePath },
            _context);

        result.IsSuccessful.Should().BeFalse();
        result.Metadata["error_code"].Should().Be("UNSUPPORTED_FILE_TYPE");
    }
}
