using Andy.Tools.Core;
using Andy.Tools.Library.System;
using Xunit;

namespace Andy.Tools.Tests.Library.System;

public class SystemInfoToolTests
{
    private readonly SystemInfoTool _tool;
    private readonly ToolExecutionContext _context;

    public SystemInfoToolTests()
    {
        _tool = new SystemInfoTool();
        _tool.InitializeAsync().GetAwaiter().GetResult();
        _context = new ToolExecutionContext();
    }

    [Fact]
    public async Task ExecuteAsync_BasicInfo_ReturnsSystemInformation()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>();

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful, $"Tool execution failed: {result.ErrorMessage}");
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);

        var os = data["os"] as Dictionary<string, object?>;
        Assert.NotNull(os);

        // Check OS properties exist
        Assert.Contains("platform", os.Keys);
        Assert.Contains("version", os.Keys);
        Assert.Contains("version_string", os.Keys);
        Assert.Contains("is_64_bit", os.Keys);
        Assert.Contains("machine_name", os.Keys);
        Assert.Contains("os_family", os.Keys);
        Assert.Contains("architecture", os.Keys);

        // Validate some values
        Assert.NotNull(os["platform"]);
        Assert.NotNull(os["version"]);
        Assert.NotNull(os["machine_name"]);
    }

    [Fact]
    public async Task ExecuteAsync_MemoryInfo_ReturnsMemoryStatistics()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>();

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful, $"Tool execution failed: {result.ErrorMessage}");
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);

        var memory = data["memory"] as Dictionary<string, object?>;
        Assert.NotNull(memory);

        // Check memory properties
        Assert.Contains("working_set", memory.Keys);
        Assert.Contains("gc_total_memory", memory.Keys);
        Assert.Contains("working_set_formatted", memory.Keys);
        Assert.Contains("gc_total_memory_formatted", memory.Keys);

        // Validate values are positive
        Assert.True((long)memory["working_set"]! > 0);
        Assert.True((long)memory["gc_total_memory"]! >= 0);
    }

    [Fact]
    public async Task ExecuteAsync_ProcessInfo_ReturnsProcessDetails()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>();

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful, $"Tool execution failed: {result.ErrorMessage}");
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);

        var cpu = data["cpu"] as Dictionary<string, object?>;
        Assert.NotNull(cpu);

        // Check CPU properties
        Assert.Contains("processor_count", cpu.Keys);
        Assert.Contains("architecture", cpu.Keys);

        // Validate values
        Assert.True((int)cpu["processor_count"]! > 0);
        Assert.NotNull(cpu["architecture"]);
    }

    [Fact]
    public async Task ExecuteAsync_AllSections_ReturnsComprehensiveInfo()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>(); // Default is all sections

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful);
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);

        // Check all categories are present
        Assert.Contains("os", data.Keys);
        Assert.Contains("hardware", data.Keys);
        Assert.Contains("runtime", data.Keys);
        Assert.Contains("environment", data.Keys);
        Assert.Contains("network", data.Keys);
        Assert.Contains("storage", data.Keys);
        Assert.Contains("memory", data.Keys);
        Assert.Contains("cpu", data.Keys);
    }

    [Fact]
    public async Task ExecuteAsync_EnvironmentInfo_IncludesVariables()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["include_sensitive"] = true
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful, $"Tool execution failed: {result.ErrorMessage}");
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);

        var environment = data["environment"] as Dictionary<string, object?>;
        Assert.NotNull(environment);

        Assert.Contains("command_line", environment.Keys);
        Assert.Contains("current_directory", environment.Keys);
        Assert.Contains("system_directory", environment.Keys);
        Assert.Contains("environment_variables", environment.Keys);

        var envVars = environment["environment_variables"] as Dictionary<string, object?>;
        Assert.NotNull(envVars);
        Assert.NotEmpty(envVars);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidSection_ReturnsError()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["categories"] = new List<string> { "invalid_category" }
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.False(result.IsSuccessful);
        Assert.Contains("Parameter 'categories' must be one of", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_FilterSensitive_ExcludesSensitiveData()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["include_sensitive"] = false
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters, _context);

        // Assert
        Assert.True(result.IsSuccessful, $"Tool execution failed: {result.ErrorMessage}");
        var data = result.Data as Dictionary<string, object?>;
        Assert.NotNull(data);

        var environment = data["environment"] as Dictionary<string, object?>;
        Assert.NotNull(environment);

        Assert.Contains("safe_environment_variables", environment.Keys);
        var safeVars = environment["safe_environment_variables"] as Dictionary<string, object?>;
        Assert.NotNull(safeVars);
        // When include_sensitive is false, it returns safe_environment_variables instead
    }

    [Fact]
    public void Metadata_HasCorrectConfiguration()
    {
        // Assert
        Assert.Equal("system_info", _tool.Metadata.Id);
        Assert.Equal("System Information", _tool.Metadata.Name);
        Assert.Equal(ToolCategory.System, _tool.Metadata.Category);
        Assert.Equal(ToolPermissionFlags.SystemInformation, _tool.Metadata.RequiredPermissions);

        var categoriesParam = _tool.Metadata.Parameters.First(p => p.Name == "categories");
        Assert.False(categoriesParam.Required);
    }
}
