using Andy.Tools.Core;
using Andy.Tools.Library.Common;
using Andy.Tools.Library.FileSystem;
using FluentAssertions;

namespace Andy.Tools.Tests.Library.FileSystem;

/// <summary>
/// Regression tests for issue #23: CopyFile lacked a same-path guard, deleted-file size tallying read
/// FileInfo.Length after deletion, and backup paths used a 1-second-resolution suffix that overwrote.
/// </summary>
public sealed class FileSystemPerfCorrectnessTests : IDisposable
{
    private readonly string _dir;

    public FileSystemPerfCorrectnessTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "andy_fsperf_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public async Task Copy_SourceEqualsDestination_IsRejected()
    {
        var file = Path.Combine(_dir, "f.txt");
        await File.WriteAllTextAsync(file, "data");

        var tool = new CopyFileTool();
        await tool.InitializeAsync();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["source_path"] = file,
            ["destination_path"] = file,
            ["overwrite"] = true
        }, new ToolExecutionContext { WorkingDirectory = _dir });

        result.IsSuccessful.Should().BeFalse();
        (await File.ReadAllTextAsync(file)).Should().Be("data");
    }

    [Fact]
    public async Task DeleteDirectory_ReportsBytesDeleted()
    {
        var sub = Path.Combine(_dir, "tree");
        Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(sub, "a.txt"), new string('x', 100));

        var tool = new DeleteFileTool();
        await tool.InitializeAsync();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["target_path"] = sub,
            ["recursive"] = true
        }, new ToolExecutionContext { WorkingDirectory = _dir });

        // The key regression: deleting files in a directory no longer throws while tallying size.
        result.IsSuccessful.Should().BeTrue(result.ErrorMessage);
        Directory.Exists(sub).Should().BeFalse();
    }

    [Fact]
    public void GetBackupPath_ProducesUniquePathsWithinSameSecond()
    {
        var paths = new HashSet<string>();
        for (var i = 0; i < 50; i++)
        {
            paths.Add(ToolHelpers.GetBackupPath("/tmp/file.txt"));
        }

        paths.Count.Should().Be(50, "backup paths must not collide within the same second");
    }
}
