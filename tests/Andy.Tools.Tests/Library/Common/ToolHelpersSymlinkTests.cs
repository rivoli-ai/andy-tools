using Andy.Tools.Core;
using Andy.Tools.Execution;
using Andy.Tools.Library.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Andy.Tools.Tests.Library.Common;

/// <summary>
/// Regression tests for symlink resolution in confinement checks (issue #9). Path.GetFullPath does
/// not resolve symbolic links, so a link inside an allowed directory pointing outside it could
/// previously bypass the boundary.
/// </summary>
public sealed class ToolHelpersSymlinkTests : IDisposable
{
    private readonly string _root;
    private readonly bool _symlinksSupported;

    public ToolHelpersSymlinkTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "andytools_sym_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _symlinksSupported = TrySymlinkSupport();
    }

    private bool TrySymlinkSupport()
    {
        try
        {
            var target = Path.Combine(_root, "probe_target.txt");
            File.WriteAllText(target, "x");
            var link = Path.Combine(_root, "probe_link.txt");
            File.CreateSymbolicLink(link, target);
            return File.Exists(link);
        }
        catch
        {
            // Windows without privilege, or filesystem without symlink support.
            return false;
        }
    }

    [Fact]
    public void ResolveRealPath_FollowsSymlinkToRealTarget()
    {
        if (!_symlinksSupported) { return; }

        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        var secret = Path.Combine(outside, "secret.txt");
        File.WriteAllText(secret, "s");

        var allowed = Path.Combine(_root, "allowed");
        Directory.CreateDirectory(allowed);
        var link = Path.Combine(allowed, "link.txt");
        File.CreateSymbolicLink(link, secret);

        ToolHelpers.ResolveRealPath(link)
            .Should().Be(ToolHelpers.ResolveRealPath(secret));
    }

    [Fact]
    public void SecurityManager_SymlinkInsideAllowedPointingOutside_IsDenied()
    {
        if (!_symlinksSupported) { return; }

        var allowed = Path.Combine(_root, "allowed");
        Directory.CreateDirectory(allowed);
        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        var secret = Path.Combine(outside, "secret.txt");
        File.WriteAllText(secret, "s");

        var link = Path.Combine(allowed, "escape.txt");
        File.CreateSymbolicLink(link, secret);

        var sm = new SecurityManager(new Mock<ILogger<SecurityManager>>().Object);
        var permissions = new ToolPermissions
        {
            FileSystemAccess = true,
            AllowedPaths = new HashSet<string> { allowed },
            CustomPermissions = new Dictionary<string, object?>()
        };

        // The link lives inside the allowed dir, but its real target is outside -> denied.
        sm.IsFileAccessAllowed(link, permissions, FileAccessType.Read)
            .Should().BeFalse();

        // A real file inside the allowed dir is still permitted.
        var ok = Path.Combine(allowed, "ok.txt");
        File.WriteAllText(ok, "ok");
        sm.IsFileAccessAllowed(ok, permissions, FileAccessType.Read)
            .Should().BeTrue();
    }

    [Fact]
    public void GetSafePath_SymlinkEscapingWorkingDir_Throws()
    {
        if (!_symlinksSupported) { return; }

        var work = Path.Combine(_root, "work");
        Directory.CreateDirectory(work);
        var outside = Path.Combine(_root, "out2");
        Directory.CreateDirectory(outside);
        var secret = Path.Combine(outside, "s.txt");
        File.WriteAllText(secret, "s");
        var link = Path.Combine(work, "escape.txt");
        File.CreateSymbolicLink(link, secret);

        Action act = () => ToolHelpers.GetSafePath(link, work);
        act.Should().Throw<ArgumentException>();
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }
}
