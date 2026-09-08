using System.Collections.Generic;
using Andy.Tools.Library.Git;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Library.Git;

public class GitWorktreePorcelainParserTests
{
    [Fact]
    public void Parse_EmptyOutput_ReturnsEmptyList()
    {
        GitWorktreePorcelainParser.Parse(string.Empty).Should().BeEmpty();
        GitWorktreePorcelainParser.Parse("   \n  ").Should().BeEmpty();
    }

    [Fact]
    public void Parse_MainAndLinkedWorktrees_MarksOnlyFirstAsMain()
    {
        var output =
            "worktree /repo\n" +
            "HEAD 1111111111111111111111111111111111111111\n" +
            "branch refs/heads/main\n" +
            "\n" +
            "worktree /repo-lanes/feature\n" +
            "HEAD 2222222222222222222222222222222222222222\n" +
            "branch refs/heads/feature/x\n";

        var worktrees = GitWorktreePorcelainParser.Parse(output);

        worktrees.Should().HaveCount(2);
        worktrees[0]["path"].Should().Be("/repo");
        worktrees[0]["is_main"].Should().Be(true);
        worktrees[0]["branch"].Should().Be("main");
        worktrees[0]["head"].Should().Be("1111111111111111111111111111111111111111");
        worktrees[1]["is_main"].Should().Be(false);
        worktrees[1]["branch"].Should().Be("feature/x");
    }

    [Fact]
    public void Parse_DetachedWorktree_HasNoBranch()
    {
        var output =
            "worktree /repo\n" +
            "HEAD 1111111111111111111111111111111111111111\n" +
            "branch refs/heads/main\n" +
            "\n" +
            "worktree /wt-detached\n" +
            "HEAD 3333333333333333333333333333333333333333\n" +
            "detached\n";

        var worktrees = GitWorktreePorcelainParser.Parse(output);

        worktrees[1]["is_detached"].Should().Be(true);
        worktrees[1]["branch"].Should().BeNull();
    }

    [Fact]
    public void Parse_BareRepository_IsFlagged()
    {
        var output = "worktree /bare-repo\nbare\n";

        var worktrees = GitWorktreePorcelainParser.Parse(output);

        worktrees.Should().HaveCount(1);
        worktrees[0]["is_bare"].Should().Be(true);
        worktrees[0]["head"].Should().BeNull();
    }

    [Fact]
    public void Parse_LockedWorktree_WithAndWithoutReason()
    {
        var output =
            "worktree /wt-locked-reason\n" +
            "HEAD 4444444444444444444444444444444444444444\n" +
            "detached\n" +
            "locked being used by CI\n" +
            "\n" +
            "worktree /wt-locked-bare\n" +
            "HEAD 5555555555555555555555555555555555555555\n" +
            "detached\n" +
            "locked\n";

        var worktrees = GitWorktreePorcelainParser.Parse(output);

        worktrees[0]["is_locked"].Should().Be(true);
        worktrees[0]["lock_reason"].Should().Be("being used by CI");
        worktrees[1]["is_locked"].Should().Be(true);
        worktrees[1]["lock_reason"].Should().BeNull();
    }

    [Fact]
    public void Parse_PrunableWorktree_CapturesReason()
    {
        var output =
            "worktree /wt-gone\n" +
            "HEAD 6666666666666666666666666666666666666666\n" +
            "detached\n" +
            "prunable gitdir file points to non-existent location\n";

        var worktrees = GitWorktreePorcelainParser.Parse(output);

        worktrees[0]["is_prunable"].Should().Be(true);
        worktrees[0]["prune_reason"].Should().Be("gitdir file points to non-existent location");
    }

    [Fact]
    public void Parse_UnknownAttributeLines_AreIgnored()
    {
        var output =
            "worktree /repo\n" +
            "HEAD 7777777777777777777777777777777777777777\n" +
            "branch refs/heads/main\n" +
            "some-future-attribute value\n";

        var worktrees = GitWorktreePorcelainParser.Parse(output);

        worktrees.Should().HaveCount(1);
        worktrees[0]["branch"].Should().Be("main");
    }

    [Fact]
    public void Parse_WindowsLineEndings_AreHandled()
    {
        var output = "worktree /repo\r\nHEAD 8888888888888888888888888888888888888888\r\nbranch refs/heads/main\r\n";

        var worktrees = GitWorktreePorcelainParser.Parse(output);

        worktrees.Should().HaveCount(1);
        worktrees[0]["path"].Should().Be("/repo");
        worktrees[0]["branch"].Should().Be("main");
    }
}
