using Andy.Tools.Core;
using Andy.Tools.Framework;
using Andy.Tools.Pdf.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Pdf.Tests;

/// <summary>
/// End-to-end smoke test exercising only the public surface a consumer uses: register with
/// <see cref="ServiceCollectionExtensions.AddAndyPdfTools(IServiceCollection)"/>, resolve the
/// executor, and run a <c>pdf_*</c> tool by id through the framework.
/// </summary>
public sealed class PdfPublicApiSmokeTests : PdfToolTestBase
{
    [Fact]
    public async Task Consumer_can_register_and_run_a_pdf_tool_through_the_executor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAndyTools();
        services.AddAndyPdfTools();
        using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IToolLifecycleManager>().InitializeAsync();
        var executor = provider.GetRequiredService<IToolExecutor>();

        var path = WriteFixture(TestPdfs.TwoPageWithOutline());
        var context = new ToolExecutionContext
        {
            WorkingDirectory = WorkingDirectory,
            Permissions = new ToolPermissions { AllowedPaths = new HashSet<string> { WorkingDirectory } },
        };

        var info = await executor.ExecuteAsync(
            "pdf_info", new Dictionary<string, object?> { ["path"] = path }, context);
        info.IsSuccessful.Should().BeTrue(info.ErrorMessage);

        var search = await executor.ExecuteAsync(
            "pdf_search",
            new Dictionary<string, object?> { ["path"] = path, ["query"] = "guidance" },
            context);
        search.IsSuccessful.Should().BeTrue(search.ErrorMessage);
    }
}
