using Andy.Tools.Core;
using Andy.Tools.Execution;
using Andy.Tools.Library.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Andy.Tools.Tests.Library.Common;

/// <summary>
/// Regression tests for the path-confinement boundary check (issue #8). A bare
/// <c>StartsWith</c> prefix test let a sibling directory sharing a textual prefix
/// (e.g. <c>/app-secrets</c> vs <c>/app</c>) escape the boundary.
/// </summary>
public class ToolHelpersPathBoundaryTests
{
    // A unique, rooted, but non-existent tree. The checks are pure path-string logic
    // after Path.GetFullPath, so the directories do not need to exist.
    private static readonly string Root =
        Path.Combine(Path.GetTempPath(), "andytools_pb_" + Guid.NewGuid().ToString("N"));
    private static readonly string App = Path.Combine(Root, "app");
    private static readonly string Sibling = Path.Combine(Root, "app-secrets");

    [Fact]
    public void IsPathWithinBoundary_FileBeneathBoundary_ReturnsTrue()
    {
        ToolHelpers.IsPathWithinBoundary(Path.Combine(App, "sub", "file.txt"), App)
            .Should().BeTrue();
    }

    [Fact]
    public void IsPathWithinBoundary_BoundaryItself_ReturnsTrue()
    {
        ToolHelpers.IsPathWithinBoundary(App, App).Should().BeTrue();
    }

    [Fact]
    public void IsPathWithinBoundary_SiblingSharingTextualPrefix_ReturnsFalse()
    {
        // "/<root>/app-secrets/x" starts with the string "/<root>/app" but is NOT inside it.
        ToolHelpers.IsPathWithinBoundary(Path.Combine(Sibling, "secret.txt"), App)
            .Should().BeFalse();
    }

    [Fact]
    public void IsPathWithinBoundary_TrailingSeparatorOnBoundary_IsHandled()
    {
        ToolHelpers.IsPathWithinBoundary(Path.Combine(App, "file.txt"), App + Path.DirectorySeparatorChar)
            .Should().BeTrue();
    }

    [Fact]
    public void IsPathWithinBoundary_CaseVariant_MatchesOsCaseSensitivity()
    {
        var expected = ToolHelpers.PathComparison == StringComparison.OrdinalIgnoreCase;
        ToolHelpers.IsPathWithinBoundary(Path.Combine(App.ToUpperInvariant(), "file.txt"), App)
            .Should().Be(expected);
    }

    [Fact]
    public void GetSafePath_SiblingPrefixEscape_Throws()
    {
        var escape = Path.Combine(Sibling, "secret.txt");
        Action act = () => ToolHelpers.GetSafePath(escape, App);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetSafePath_PathInsideWorkingDir_Succeeds()
    {
        var inside = Path.Combine(App, "ok.txt");
        ToolHelpers.GetSafePath(inside, App).Should().Be(Path.GetFullPath(inside));
    }

    [Fact]
    public void IsPathWithinAllowedPaths_SiblingPrefixEscape_ReturnsFalse()
    {
        var permissions = new ToolPermissions
        {
            FileSystemAccess = true,
            AllowedPaths = new HashSet<string> { App }
        };

        ToolHelpers.IsPathWithinAllowedPaths(Path.Combine(App, "f.txt"), permissions)
            .Should().BeTrue();
        ToolHelpers.IsPathWithinAllowedPaths(Path.Combine(Sibling, "f.txt"), permissions)
            .Should().BeFalse();
    }

    [Fact]
    public void SecurityManager_AllowedPath_SiblingPrefixEscape_IsDenied()
    {
        var sm = new SecurityManager(new Mock<ILogger<SecurityManager>>().Object);
        var permissions = new ToolPermissions
        {
            FileSystemAccess = true,
            AllowedPaths = new HashSet<string> { App },
            CustomPermissions = new Dictionary<string, object?>()
        };

        sm.IsFileAccessAllowed(Path.Combine(App, "f.txt"), permissions, FileAccessType.Read)
            .Should().BeTrue();
        sm.IsFileAccessAllowed(Path.Combine(Sibling, "f.txt"), permissions, FileAccessType.Read)
            .Should().BeFalse();
    }

    [Fact]
    public void SecurityManager_BlockedPath_SiblingPrefixIsNotOverBlocked()
    {
        var sm = new SecurityManager(new Mock<ILogger<SecurityManager>>().Object);
        var blocked = Path.Combine(Root, "etc");
        var permissions = new ToolPermissions
        {
            FileSystemAccess = true,
            BlockedPaths = new HashSet<string> { blocked },
            CustomPermissions = new Dictionary<string, object?>()
        };

        // Inside the blocked dir -> denied.
        sm.IsFileAccessAllowed(Path.Combine(blocked, "passwd"), permissions, FileAccessType.Read)
            .Should().BeFalse();
        // Sibling sharing the "etc" prefix (e.g. "etc-notes") must NOT be over-blocked.
        sm.IsFileAccessAllowed(Path.Combine(Root, "etc-notes", "x"), permissions, FileAccessType.Read)
            .Should().BeTrue();
    }
}
