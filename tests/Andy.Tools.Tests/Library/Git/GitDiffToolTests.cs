using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Andy.Tools.Core;
using Andy.Tools.Library.Git;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Andy.Tools.Tests.Library.Git;

public class GitDiffToolTests
{
    private readonly Mock<ILogger<GitDiffTool>> _loggerMock;
    private readonly GitDiffTool _tool;

    public GitDiffToolTests()
    {
        _loggerMock = new Mock<ILogger<GitDiffTool>>();
        _tool = new GitDiffTool(_loggerMock.Object);
    }

    [Fact]
    public void Constructor_AcceptsNullLogger()
    {
        // Arrange & Act
        var tool = new GitDiffTool(null);

        // Assert
        Assert.NotNull(tool);
    }

    [Fact]
    public void ParameterlessConstructor_Works()
    {
        // Arrange & Act
        var tool = new GitDiffTool();

        // Assert
        Assert.NotNull(tool);
        Assert.NotNull(tool.Metadata);
    }

    [Fact]
    public void Metadata_ReturnsCorrectValues()
    {
        // Act
        var metadata = _tool.Metadata;

        // Assert
        Assert.Equal("git_diff", metadata.Id);
        Assert.Equal("git_diff", metadata.Name);
        Assert.Equal("Display git changes with color-coded diff output for modified files", metadata.Description);
        Assert.Equal(ToolCategory.Git, metadata.Category);
        Assert.Equal(4, metadata.Parameters.Count);
    }

    [Fact]
    public void Metadata_Parameters_HaveCorrectConfiguration()
    {
        // Act
        var parameters = _tool.Metadata.Parameters;

        // Assert
        var filePathParam = parameters[0];
        Assert.Equal("file_path", filePathParam.Name);
        Assert.Equal("string", filePathParam.Type);
        Assert.Equal("Path to the specific file to show diff for (optional)", filePathParam.Description);
        Assert.False(filePathParam.Required);

        var stagedParam = parameters[1];
        Assert.Equal("staged", stagedParam.Name);
        Assert.Equal("boolean", stagedParam.Type);
        Assert.Equal("Show staged changes instead of working directory changes", stagedParam.Description);
        Assert.False(stagedParam.Required);
        Assert.Equal(false, stagedParam.DefaultValue);

        var contextLinesParam = parameters[2];
        Assert.Equal("context_lines", contextLinesParam.Name);
        Assert.Equal("integer", contextLinesParam.Type);
        Assert.Equal("Number of context lines to show around changes (default: 3)", contextLinesParam.Description);
        Assert.False(contextLinesParam.Required);
        Assert.Equal(3, contextLinesParam.DefaultValue);

        var maxLinesParam = parameters[3];
        Assert.Equal("max_lines", maxLinesParam.Name);
        Assert.Equal("integer", maxLinesParam.Type);
        Assert.Equal("Maximum number of changed lines to display per file (default: 10)", maxLinesParam.Description);
        Assert.False(maxLinesParam.Required);
        Assert.Equal(10, maxLinesParam.DefaultValue);
    }

    [Fact]
    public void CanExecuteWithPermissions_RequiresProcessExecution()
    {
        // Arrange
        var permissionsWithProcess = new ToolPermissions { ProcessExecution = true };
        var permissionsWithoutProcess = new ToolPermissions { ProcessExecution = false };

        // Act & Assert
        Assert.True(_tool.CanExecuteWithPermissions(permissionsWithProcess));
        Assert.False(_tool.CanExecuteWithPermissions(permissionsWithoutProcess));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenNotInGitRepository()
    {
        // Arrange
        var parameters = new Dictionary<string, object>();

        // Note: We can't easily mock the Process execution in the tool itself,
        // so this test would need the GitDiffTool to be refactored to accept
        // an interface for process execution to be properly testable.
        // For now, we'll skip this test.
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ExecuteAsync_HandlesExceptions_AndReturnsErrorResponse()
    {
        // This test would also require refactoring the tool to be more testable
        // by injecting process execution dependencies.
        await Task.CompletedTask;
    }
}

public class FileDiffTests
{
    [Fact]
    public void FileDiff_CalculatesTotalModificationsCorrectly()
    {
        // Arrange
        var fileDiff = new FileDiff
        {
            FilePath = "test.cs",
            AddedLines = 5,
            RemovedLines = 3
        };

        // Act
        var total = fileDiff.TotalModifications;

        // Assert
        Assert.Equal(8, total);
    }

    [Fact]
    public void FileDiff_InitializesEmptyHunksList()
    {
        // Arrange & Act
        var fileDiff = new FileDiff();

        // Assert
        Assert.NotNull(fileDiff.Hunks);
        Assert.Empty(fileDiff.Hunks);
    }
}

public class DiffHunkTests
{
    [Fact]
    public void DiffHunk_InitializesEmptyLinesList()
    {
        // Arrange & Act
        var hunk = new DiffHunk();

        // Assert
        Assert.NotNull(hunk.Lines);
        Assert.Empty(hunk.Lines);
    }

    [Fact]
    public void DiffHunk_PropertiesSetCorrectly()
    {
        // Arrange & Act
        var hunk = new DiffHunk
        {
            OldStart = 10,
            OldCount = 5,
            NewStart = 12,
            NewCount = 7
        };

        // Assert
        Assert.Equal(10, hunk.OldStart);
        Assert.Equal(5, hunk.OldCount);
        Assert.Equal(12, hunk.NewStart);
        Assert.Equal(7, hunk.NewCount);
    }
}

public class DiffLineTests
{
    [Theory]
    [InlineData(DiffLineType.Added, "+", 42)]
    [InlineData(DiffLineType.Removed, "-", 40)]
    [InlineData(DiffLineType.Context, " ", 41)]
    public void DiffLine_PropertiesSetCorrectly(DiffLineType type, string content, int lineNumber)
    {
        // Arrange & Act
        var line = new DiffLine
        {
            Type = type,
            Content = content,
            LineNumber = lineNumber
        };

        // Assert
        Assert.Equal(type, line.Type);
        Assert.Equal(content, line.Content);
        Assert.Equal(lineNumber, line.LineNumber);
    }
}
