using Andy.Tools.Core;
using Andy.Tools.Semantic;
using FluentAssertions;

namespace Andy.Tools.Semantic.Tests;

/// <summary>
/// Tests for the semantic_search tool (issue #72). A deterministic fake embedding provider is used
/// so no network I/O occurs.
/// </summary>
public sealed class SemanticSearchToolTests
{
    private static ToolExecutionContext Context() => new()
    {
        Permissions = new ToolPermissions { FileSystemAccess = true }
    };

    [Fact]
    public void CosineSimilarity_IdenticalVectors_IsOne()
    {
        var a = new[] { 1f, 2f, 3f };
        var b = new[] { 1f, 2f, 3f };

        SemanticSearchTool.CosineSimilarity(a, b).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_IsZero()
    {
        var a = new[] { 1f, 0f };
        var b = new[] { 0f, 1f };

        SemanticSearchTool.CosineSimilarity(a, b).Should().BeApproximately(0.0, 1e-9);
    }

    [Fact]
    public void CosineSimilarity_OppositeVectors_IsMinusOne()
    {
        var a = new[] { 1f, 2f, 3f };
        var b = new[] { -1f, -2f, -3f };

        SemanticSearchTool.CosineSimilarity(a, b).Should().BeApproximately(-1.0, 1e-9);
    }

    [Fact]
    public async Task ParameterlessConstructor_Execute_ReturnsNoProviderFailure()
    {
        // The parameterless constructor must not throw (the registry probes metadata via
        // Activator.CreateInstance), but executing without a provider must fail clearly.
        var tool = new SemanticSearchTool();
        tool.Metadata.Id.Should().Be("semantic_search");
        tool.Metadata.RequiredPermissions.Should().Be(ToolPermissionFlags.FileSystemRead);

        await tool.InitializeAsync();

        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?>
            {
                ["query"] = "anything",
                ["paths"] = new[] { "." }
            },
            Context());

        result.IsSuccessful.Should().BeFalse();
        result.Metadata["error_code"].Should().Be("NO_EMBEDDING_PROVIDER");
    }

    [Fact]
    public async Task Search_RanksMatchingFileFirst_AndRespectsMaxResults()
    {
        var dir = Path.Combine(Path.GetTempPath(), "andy-semantic-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            var dbFile = Path.Combine(dir, "database.txt");
            var uiFile = Path.Combine(dir, "ui.txt");
            var miscFile = Path.Combine(dir, "misc.txt");

            await File.WriteAllTextAsync(dbFile, "connect to the database and run the query using sql");
            await File.WriteAllTextAsync(uiFile, "render the button and the menu on the screen");
            await File.WriteAllTextAsync(miscFile, "the weather is warm today");

            var provider = new FakeEmbeddingProvider(
                "connect", "database", "query", "sql", "button", "menu", "screen", "weather", "warm");

            var tool = new SemanticSearchTool(provider);
            await tool.InitializeAsync();

            var result = await tool.ExecuteAsync(
                new Dictionary<string, object?>
                {
                    ["query"] = "how do I query the sql database",
                    ["paths"] = new[] { dir },
                    ["max_results"] = 2
                },
                Context());

            result.IsSuccessful.Should().BeTrue(result.ErrorMessage);

            var items = (List<Dictionary<string, object?>>)result.Data!;
            items.Should().HaveCount(2); // max_results respected (3 files matched)

            // The database file shares the most query terms, so it ranks first.
            ((string)items[0]["file"]!).Should().Be(dbFile);
            ((double)items[0]["score"]!).Should().BeGreaterThan((double)items[1]["score"]!);
            items[0].Should().ContainKey("start_line");
            items[0].Should().ContainKey("end_line");
            items[0].Should().ContainKey("snippet");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
