using System.Reflection;
using Andy.Tools.Discovery;
using Andy.Tools.Validation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Andy.Tools.Tests.Discovery;

/// <summary>
/// Regression tests for issue #15: MaxDirectoryDepth was collapsed to a boolean (any depth &gt; 1 became
/// an unbounded recursive scan), and plugin assemblies were loaded with no opportunity to vet them.
/// </summary>
public sealed class ToolDiscoveryHardeningTests : IDisposable
{
    private readonly string _root;
    private readonly ToolDiscoveryService _service;

    public ToolDiscoveryHardeningTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "andy_disc_hard_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        var validator = new Mock<IToolValidator>();
        validator.Setup(v => v.ValidateToolType(It.IsAny<Type>())).Returns(ValidationResult.Success());
        _service = new ToolDiscoveryService(validator.Object, NullLogger<ToolDiscoveryService>.Instance);
    }

    [Theory]
    [InlineData(1, false)] // root only: a file two levels down is NOT enumerated
    [InlineData(2, true)]  // depth 2 reaches the file
    [InlineData(5, true)]
    public void EnumerateFilesUpToDepth_HonorsConfiguredDepth(int maxDepth, bool shouldFind)
    {
        var sub = Path.Combine(_root, "a");
        Directory.CreateDirectory(sub);
        var file = Path.Combine(sub, "plugin.dll");
        File.WriteAllBytes(file, new byte[] { 0x4D, 0x5A }); // not a real assembly; we only test enumeration

        var method = typeof(ToolDiscoveryService).GetMethod(
            "EnumerateFilesUpToDepth", BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (IReadOnlyList<string>)method.Invoke(
            null, [_root, new List<string> { "*.dll" }, maxDepth])!;

        result.Any(f => string.Equals(Path.GetFullPath(f), Path.GetFullPath(file))).Should().Be(shouldFind);
    }

    [Fact]
    public async Task PluginAssemblyValidator_RejectingFile_PreventsItFromLoading()
    {
        // Copy a real, loadable assembly into the plugin directory under a different file name.
        var realAssembly = typeof(ToolDiscoveryService).Assembly.Location;
        var pluginPath = Path.Combine(_root, "MyPlugin.dll");
        File.Copy(realAssembly, pluginPath, overwrite: true);

        var baseOptions = new ToolDiscoveryOptions
        {
            ScanCurrentAssembly = false,
            ScanLoadedAssemblies = false,
            ValidateTools = false,
            PluginDirectories = [_root],
            PluginFilePatterns = ["MyPlugin.dll"]
        };

        var withReject = await _service.DiscoverToolsAsync(new ToolDiscoveryOptions
        {
            ScanCurrentAssembly = false,
            ScanLoadedAssemblies = false,
            ValidateTools = false,
            PluginDirectories = [_root],
            PluginFilePatterns = ["MyPlugin.dll"],
            PluginAssemblyValidator = _ => false
        });
        withReject.Should().BeEmpty("a rejected assembly must not be loaded or scanned");

        // Sanity: without the validator, the same loadable assembly does yield tools.
        var withoutValidator = await _service.DiscoverToolsAsync(baseOptions);
        withoutValidator.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }
}
