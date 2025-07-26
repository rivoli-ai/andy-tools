using System;
using System.Collections.Generic;
using System.Linq;
using Andy.Tools.Library.Git;
using Xunit;

namespace Andy.Tools.Tests.Library.Git;

public class GitDiffFormatterTests
{
    private readonly GitDiffFormatter _formatter;

    public GitDiffFormatterTests()
    {
        _formatter = new GitDiffFormatter();
    }

    [Fact]
    public void FormatStatsSummary_HandlesSimpleStats()
    {
        // Arrange
        var statsContent = @"file1.cs | 5 +++++
file2.cs | 3 ++-
3 files changed, 7 insertions(+), 2 deletions(-)";

        // Act
        var result = _formatter.FormatStatsSummary(statsContent);

        // Assert
        Assert.Contains("📊 **Change Summary**", result);
        Assert.Contains("`file1.cs` (5 changes: **+5**)", result);
        Assert.Contains("`file2.cs` (3 changes: **+2**, **-1**)", result);
        Assert.Contains("**Total**: 3 files changed, 7 insertions(+), 2 deletions(-)", result);
    }

    [Fact]
    public void FormatStatsSummary_HandlesEmptyStats()
    {
        // Arrange
        var statsContent = "";

        // Act
        var result = _formatter.FormatStatsSummary(statsContent);

        // Assert
        Assert.Equal("📊 **Change Summary**", result);
    }

    [Fact]
    public void FormatStatsSummary_HandlesOnlyTotalLine()
    {
        // Arrange
        var statsContent = "1 file changed, 10 insertions(+)";

        // Act
        var result = _formatter.FormatStatsSummary(statsContent);

        // Assert
        Assert.Contains("**Total**: 1 file changed, 10 insertions(+)", result);
    }

    [Fact]
    public void FormatFileDiff_FormatsSimpleDiff()
    {
        // Arrange
        var fileDiff = new FileDiff
        {
            FilePath = "test.cs",
            AddedLines = 2,
            RemovedLines = 1,
            Hunks = new List<DiffHunk>
            {
                new DiffHunk
                {
                    OldStart = 10,
                    OldCount = 3,
                    NewStart = 10,
                    NewCount = 4,
                    Lines = new List<DiffLine>
                    {
                        new DiffLine { Type = DiffLineType.Context, Content = "context line", LineNumber = 10 },
                        new DiffLine { Type = DiffLineType.Removed, Content = "removed line", LineNumber = 11 },
                        new DiffLine { Type = DiffLineType.Added, Content = "added line 1", LineNumber = 11 },
                        new DiffLine { Type = DiffLineType.Added, Content = "added line 2", LineNumber = 12 },
                        new DiffLine { Type = DiffLineType.Context, Content = "context line", LineNumber = 13 }
                    }
                }
            }
        };

        // Act
        var result = _formatter.FormatFileDiff(fileDiff, 10);

        // Assert
        Assert.Contains("📄 **test.cs** (3 modifications)", result);
        Assert.Contains("**+2** additions, **-1** deletions", result);
        Assert.Contains("Lines 10-13:", result);
        Assert.Contains("```diff", result);
        Assert.Contains("+   11: added line 1", result);
        Assert.Contains("+   12: added line 2", result);
        Assert.Contains("-   11: removed line", result);
        Assert.Contains("    10: context line", result);
    }

    [Fact]
    public void FormatFileDiff_TruncatesLongDiffs()
    {
        // Arrange
        var lines = new List<DiffLine>();
        for (int i = 0; i < 20; i++)
        {
            lines.Add(new DiffLine
            {
                Type = DiffLineType.Added,
                Content = $"added line {i}",
                LineNumber = 10 + i
            });
        }

        var fileDiff = new FileDiff
        {
            FilePath = "big.cs",
            AddedLines = 20,
            RemovedLines = 0,
            Hunks = new List<DiffHunk>
            {
                new DiffHunk
                {
                    OldStart = 10,
                    OldCount = 0,
                    NewStart = 10,
                    NewCount = 20,
                    Lines = lines
                }
            }
        };

        // Act
        var result = _formatter.FormatFileDiff(fileDiff, 5);

        // Assert
        Assert.Contains("📄 **big.cs** (20 modifications)", result);
        Assert.Contains("**+20** additions", result);
        Assert.Contains("*... and 15 more changes (total: 20 lines modified)*", result);

        // Should only show 5 changed lines
        var addedLineCount = result.Split('\n').Count(line => line.TrimStart().StartsWith("+"));
        Assert.Equal(5, addedLineCount);
    }

    [Fact]
    public void FormatFileDiff_HandlesMultipleHunks()
    {
        // Arrange
        var fileDiff = new FileDiff
        {
            FilePath = "multi.cs",
            AddedLines = 3,
            RemovedLines = 2,
            Hunks = new List<DiffHunk>
            {
                new DiffHunk
                {
                    OldStart = 10,
                    OldCount = 2,
                    NewStart = 10,
                    NewCount = 2,
                    Lines = new List<DiffLine>
                    {
                        new DiffLine { Type = DiffLineType.Removed, Content = "old 1", LineNumber = 10 },
                        new DiffLine { Type = DiffLineType.Added, Content = "new 1", LineNumber = 10 }
                    }
                },
                new DiffHunk
                {
                    OldStart = 20,
                    OldCount = 2,
                    NewStart = 20,
                    NewCount = 3,
                    Lines = new List<DiffLine>
                    {
                        new DiffLine { Type = DiffLineType.Removed, Content = "old 2", LineNumber = 20 },
                        new DiffLine { Type = DiffLineType.Added, Content = "new 2", LineNumber = 20 },
                        new DiffLine { Type = DiffLineType.Added, Content = "new 3", LineNumber = 21 }
                    }
                }
            }
        };

        // Act
        var result = _formatter.FormatFileDiff(fileDiff, 10);

        // Assert
        Assert.Contains("Lines 10-11:", result);
        Assert.Contains("Lines 20-22:", result);
        Assert.Equal(2, result.Split("```diff").Length - 1); // Should have 2 diff blocks
    }

    [Fact]
    public void FormatFileDiff_HandlesEmptyHunks()
    {
        // Arrange
        var fileDiff = new FileDiff
        {
            FilePath = "empty.cs",
            AddedLines = 0,
            RemovedLines = 0,
            Hunks = new List<DiffHunk>()
        };

        // Act
        var result = _formatter.FormatFileDiff(fileDiff, 10);

        // Assert
        Assert.Contains("📄 **empty.cs** (0 modifications)", result);
        Assert.DoesNotContain("additions", result);
        Assert.DoesNotContain("deletions", result);
        Assert.DoesNotContain("```diff", result);
    }

    [Fact]
    public void FormatFileDiff_ShowsOnlyAdditions_WhenNoRemovals()
    {
        // Arrange
        var fileDiff = new FileDiff
        {
            FilePath = "adds.cs",
            AddedLines = 5,
            RemovedLines = 0,
            Hunks = new List<DiffHunk> { new DiffHunk() }
        };

        // Act
        var result = _formatter.FormatFileDiff(fileDiff, 10);

        // Assert
        Assert.Contains("**+5** additions", result);
        Assert.DoesNotContain("deletions", result);
    }

    [Fact]
    public void FormatFileDiff_ShowsOnlyDeletions_WhenNoAdditions()
    {
        // Arrange
        var fileDiff = new FileDiff
        {
            FilePath = "dels.cs",
            AddedLines = 0,
            RemovedLines = 3,
            Hunks = new List<DiffHunk> { new DiffHunk() }
        };

        // Act
        var result = _formatter.FormatFileDiff(fileDiff, 10);

        // Assert
        Assert.Contains("**-3** deletions", result);
        Assert.DoesNotContain("additions", result);
    }
}
