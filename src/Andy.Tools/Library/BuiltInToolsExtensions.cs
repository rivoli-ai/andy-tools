using Andy.Tools;
using Andy.Tools.Core;
using Andy.Tools.Library.FileSystem;
using Andy.Tools.Library.Git;
using Andy.Tools.Library.System;
using Andy.Tools.Library.Text;
using Andy.Tools.Library.Utilities;
using Andy.Tools.Library.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Andy.Tools.Library;

/// <summary>
/// Extensions for registering built-in tools with the service collection.
/// </summary>
public static class BuiltInToolsExtensions
{
    /// <summary>
    /// Registers all built-in tools with the tool registry.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBuiltInTools(this IServiceCollection services)
    {
        // Register all built-in tools
        services.AddFileSystemTools();
        services.AddTextProcessingTools();
        services.AddSystemTools();
        services.AddWebTools();
        services.AddUtilityTools();
        services.AddProductivityTools();
        services.AddGitTools();
        services.AddDevelopmentTools();

        return services;
    }

    /// <summary>
    /// Registers file system tools.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFileSystemTools(this IServiceCollection services)
    {
        services.AddTool<ReadFileTool>();
        services.AddTool<WriteFileTool>();
        services.AddTool<ListDirectoryTool>();
        services.AddTool<CopyFileTool>();
        services.AddTool<MoveFileTool>();
        services.AddTool<DeleteFileTool>();

        return services;
    }

    /// <summary>
    /// Registers text processing tools.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTextProcessingTools(this IServiceCollection services)
    {
        services.AddTool<SearchTextTool>();
        services.AddTool<ReplaceTextTool>();
        services.AddTool<FormatTextTool>();

        return services;
    }

    /// <summary>
    /// Registers system information tools.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSystemTools(this IServiceCollection services)
    {
        services.AddTool<SystemInfoTool>();
        services.AddTool<ProcessInfoTool>();

        return services;
    }

    /// <summary>
    /// Registers web and API tools.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWebTools(this IServiceCollection services)
    {
        services.AddTool<HttpRequestTool>();
        services.AddTool<JsonProcessorTool>();

        return services;
    }

    /// <summary>
    /// Registers utility tools.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddUtilityTools(this IServiceCollection services)
    {
        services.AddTool<DateTimeTool>();
        services.AddTool<EncodingTool>();

        return services;
    }

    /// <summary>
    /// Registers productivity tools.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProductivityTools(this IServiceCollection services)
    {
        services.AddTool<TodoManagementTool>();
        services.AddTool<TodoExecutor>();

        return services;
    }

    /// <summary>
    /// Registers Git tools.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGitTools(this IServiceCollection services)
    {
        // Register the Git diff tool
        services.AddTool<GitDiffTool>();

        return services;
    }

    /// <summary>
    /// Registers development and code analysis tools.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDevelopmentTools(this IServiceCollection services)
    {
        // PythonAnalyzerTool removed due to external dependency

        return services;
    }

    /// <summary>
    /// Registers a specific set of tools by category.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="categories">The tool categories to register.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddToolsByCategory(this IServiceCollection services, params ToolCategory[] categories)
    {
        foreach (var category in categories)
        {
            switch (category)
            {
                case ToolCategory.FileSystem:
                    services.AddFileSystemTools();
                    break;
                case ToolCategory.TextProcessing:
                    services.AddTextProcessingTools();
                    break;
                case ToolCategory.System:
                    services.AddSystemTools();
                    break;
                case ToolCategory.Web:
                    services.AddWebTools();
                    break;
                case ToolCategory.Utility:
                    services.AddUtilityTools();
                    break;
                case ToolCategory.Productivity:
                    services.AddProductivityTools();
                    break;
                case ToolCategory.Git:
                    services.AddGitTools();
                    break;
                case ToolCategory.Development:
                    services.AddDevelopmentTools();
                    break;
                default:
                    // Unknown category, skip
                    break;
            }
        }

        return services;
    }

    /// <summary>
    /// Registers specific tools by their IDs.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="toolIds">The tool IDs to register.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSpecificTools(this IServiceCollection services, params string[] toolIds)
    {
        var toolMappings = GetBuiltInToolMappings();

        foreach (var toolId in toolIds)
        {
            if (toolMappings.TryGetValue(toolId, out var toolType))
            {
                services.TryAddTransient(toolType);
                services.AddSingleton(new ToolRegistrationInfo
                {
                    ToolType = toolType,
                    Configuration = []
                });
            }
        }

        return services;
    }

    /// <summary>
    /// Gets a dictionary mapping tool IDs to their implementation types.
    /// </summary>
    /// <returns>A dictionary of tool ID to tool type mappings.</returns>
    public static Dictionary<string, Type> GetBuiltInToolMappings()
    {
        return new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            // File System Tools
            ["read_file"] = typeof(ReadFileTool),
            ["write_file"] = typeof(WriteFileTool),
            ["list_directory"] = typeof(ListDirectoryTool),
            ["copy_file"] = typeof(CopyFileTool),
            ["move_file"] = typeof(MoveFileTool),
            ["delete_file"] = typeof(DeleteFileTool),

            // Text Processing Tools
            ["search_text"] = typeof(SearchTextTool),
            ["replace_text"] = typeof(ReplaceTextTool),
            ["format_text"] = typeof(FormatTextTool),

            // System Tools
            ["system_info"] = typeof(SystemInfoTool),
            ["process_info"] = typeof(ProcessInfoTool),

            // Web Tools
            ["http_request"] = typeof(HttpRequestTool),
            ["json_processor"] = typeof(JsonProcessorTool),

            // Utility Tools
            ["datetime_tool"] = typeof(DateTimeTool),
            ["encoding_tool"] = typeof(EncodingTool),

            // Productivity Tools
            ["todo_management"] = typeof(TodoManagementTool),
            ["todo_executor"] = typeof(TodoExecutor),

            // Git Tools
            ["git_diff"] = typeof(GitDiffTool),

            // Development Tools
            // PythonAnalyzerTool removed due to external dependency
        };
    }

    /// <summary>
    /// Gets metadata for all built-in tools.
    /// </summary>
    /// <returns>A list of tool metadata for all built-in tools.</returns>
    public static List<ToolMetadata> GetBuiltInToolMetadata()
    {
        var toolTypes = GetBuiltInToolMappings().Values;
        var metadataList = new List<ToolMetadata>();

        foreach (var toolType in toolTypes)
        {
            try
            {
                // Create a temporary instance to get metadata
                var instance = (ITool)Activator.CreateInstance(toolType)!;
                metadataList.Add(instance.Metadata);
            }
            catch
            {
                // Skip tools that can't be instantiated
            }
        }

        return metadataList;
    }

    /// <summary>
    /// Gets built-in tools filtered by category.
    /// </summary>
    /// <param name="category">The tool category to filter by.</param>
    /// <returns>A list of tool metadata for the specified category.</returns>
    public static List<ToolMetadata> GetBuiltInToolsByCategory(ToolCategory category)
    {
        return [.. GetBuiltInToolMetadata().Where(metadata => metadata.Category == category)];
    }

    /// <summary>
    /// Gets built-in tools filtered by required permissions.
    /// </summary>
    /// <param name="availablePermissions">The available permissions.</param>
    /// <returns>A list of tool metadata for tools that can run with the available permissions.</returns>
    public static List<ToolMetadata> GetBuiltInToolsByPermissions(ToolPermissionFlags availablePermissions)
    {
        return [.. GetBuiltInToolMetadata().Where(metadata => (metadata.RequiredPermissions & availablePermissions) == metadata.RequiredPermissions)];
    }

    /// <summary>
    /// Validates that all built-in tools can be instantiated and have valid metadata.
    /// </summary>
    /// <returns>A validation result with any errors found.</returns>
    public static ToolLibraryValidationResult ValidateBuiltInTools()
    {
        var result = new ToolLibraryValidationResult();
        var toolMappings = GetBuiltInToolMappings();

        foreach (var kvp in toolMappings)
        {
            var toolId = kvp.Key;
            var toolType = kvp.Value;

            try
            {
                // Try to instantiate the tool
                var instance = (ITool)Activator.CreateInstance(toolType)!;

                // Validate metadata
                var metadata = instance.Metadata;
                var metadataErrors = ValidateToolMetadata(metadata);

                if (metadataErrors.Count > 0)
                {
                    result.Errors.Add($"Tool '{toolId}' has invalid metadata: {string.Join(", ", metadataErrors)}");
                }
                else
                {
                    result.ValidatedTools.Add(toolId);
                }

                // Verify the tool ID matches
                if (!string.Equals(metadata.Id, toolId, StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add($"Tool '{toolId}' metadata ID mismatch: expected '{toolId}', got '{metadata.Id}'");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Tool '{toolId}' instantiation failed: {ex.Message}");
            }
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    private static List<string> ValidateToolMetadata(ToolMetadata metadata)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(metadata.Id))
        {
            errors.Add("Tool ID cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(metadata.Name))
        {
            errors.Add("Tool name cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(metadata.Description))
        {
            errors.Add("Tool description cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(metadata.Version))
        {
            errors.Add("Tool version cannot be empty");
        }

        // Validate parameters
        var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in metadata.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                errors.Add("Parameter name cannot be empty");
                continue;
            }

            if (parameterNames.Contains(parameter.Name))
            {
                errors.Add($"Duplicate parameter name: {parameter.Name}");
            }
            else
            {
                parameterNames.Add(parameter.Name);
            }

            if (string.IsNullOrWhiteSpace(parameter.Description))
            {
                errors.Add($"Parameter '{parameter.Name}' description cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(parameter.Type))
            {
                errors.Add($"Parameter '{parameter.Name}' type cannot be empty");
            }
        }

        return errors;
    }

    /// <summary>
    /// Result of built-in tool library validation.
    /// </summary>
    public class ToolLibraryValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the validation passed.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets the list of validation errors.
        /// </summary>
        public List<string> Errors { get; } = [];

        /// <summary>
        /// Gets the list of successfully validated tool IDs.
        /// </summary>
        public List<string> ValidatedTools { get; } = [];

        /// <summary>
        /// Gets the total number of tools validated.
        /// </summary>
        public int TotalToolsValidated => ValidatedTools.Count + Errors.Count;
    }
}
