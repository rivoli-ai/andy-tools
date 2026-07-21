using Andy.Tools.Pdf.Tests.Fixtures;
using FluentAssertions;

namespace Andy.Tools.Pdf.Tests;

/// <summary>
/// Enforcement of <see cref="Andy.Tools.Core.ToolPermissions.AllowedPaths"/> and
/// <see cref="Andy.Tools.Core.ToolPermissions.BlockedPaths"/> for the <c>pdf_*</c> tools: a PDF that
/// sits inside the working directory but outside the allowed boundary, inside a blocked directory,
/// behind a sibling-prefix directory, or behind an escaping symlink must be rejected before it is
/// opened. Uses <c>pdf_info</c> as the representative tool since all six share
/// <see cref="PdfToolBase.OpenPdf"/>.
/// </summary>
public sealed class PdfSecurityTests : PdfToolTestBase
{
    private const string Denied = "Access to the requested path is denied.";

    private string MakeDir(params string[] segments)
    {
        var dir = Path.Combine(new[] { WorkingDirectory }.Concat(segments).ToArray());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string WritePdfInto(string dir, string name = "doc.pdf")
    {
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, TestPdfs.ThreePagesMarked());
        return path;
    }

    [Fact]
    public async Task Path_inside_allowed_boundary_is_accepted()
    {
        var allowed = MakeDir("allowed");
        var path = WritePdfInto(allowed);

        var result = await ExecuteAsync(
            new PdfInfoTool(), new() { ["path"] = path }, Context(allowedPaths: new[] { allowed }));

        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
    }

    [Fact]
    public async Task Path_inside_working_dir_but_outside_allowed_boundary_is_rejected()
    {
        var allowed = MakeDir("allowed");
        var privateDir = MakeDir("private");
        var path = WritePdfInto(privateDir, "secret.pdf");

        var result = await ExecuteAsync(
            new PdfInfoTool(), new() { ["path"] = path }, Context(allowedPaths: new[] { allowed }));

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Be(Denied);
    }

    [Fact]
    public async Task Blocked_path_is_rejected_even_when_also_allowed()
    {
        var allowed = MakeDir("allowed");
        var blocked = MakeDir("allowed", "blocked");
        var path = WritePdfInto(blocked, "secret.pdf");

        var result = await ExecuteAsync(
            new PdfInfoTool(),
            new() { ["path"] = path },
            Context(allowedPaths: new[] { allowed }, blockedPaths: new[] { blocked }));

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Be(Denied);
    }

    [Fact]
    public async Task Sibling_directory_sharing_a_prefix_is_rejected()
    {
        var allowed = MakeDir("allowed");
        // "allowed-secret" shares the textual prefix "allowed" but is a different directory.
        var sibling = MakeDir("allowed-secret");
        var path = WritePdfInto(sibling, "sibling.pdf");

        var result = await ExecuteAsync(
            new PdfInfoTool(), new() { ["path"] = path }, Context(allowedPaths: new[] { allowed }));

        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Be(Denied);
    }

    [Fact]
    public async Task Path_outside_working_directory_is_rejected()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"andy_pdf_outside_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);
        try
        {
            var path = WritePdfInto(outside, "ext.pdf");
            var result = await ExecuteAsync(
                new PdfInfoTool(), new() { ["path"] = path }, Context(allowedPaths: new[] { WorkingDirectory }));

            result.IsSuccessful.Should().BeFalse();
            result.ErrorMessage.Should().Be(Denied);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task Symlink_inside_allowed_dir_pointing_outside_it_is_rejected()
    {
        var allowed = MakeDir("allowed");
        var outsideAllowed = MakeDir("elsewhere");
        var target = WritePdfInto(outsideAllowed, "target.pdf");
        var link = Path.Combine(allowed, "link.pdf");

        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Symlink creation needs privileges on some hosts (e.g. non-elevated Windows). The CI
            // Linux runners always support it, so the escape case is still exercised there; treat an
            // unsupported host as a no-op rather than a failure.
            return;
        }

        var result = await ExecuteAsync(
            new PdfInfoTool(), new() { ["path"] = link }, Context(allowedPaths: new[] { allowed }));

        result.IsSuccessful.Should().BeFalse(
            "the symlink's real target resolves outside the allowed boundary");
        result.ErrorMessage.Should().Be(Denied);
    }
}
