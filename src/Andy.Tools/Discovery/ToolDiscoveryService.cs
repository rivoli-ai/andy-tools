using System.Reflection;
using System.Text.RegularExpressions;
using Andy.Tools.Core;
using Andy.Tools.Validation;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Discovery;

/// <summary>
/// Service for discovering tools from assemblies and directories.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ToolDiscoveryService"/> class.
/// </remarks>
/// <param name="validator">The tool validator.</param>
/// <param name="logger">The logger.</param>
public class ToolDiscoveryService(IToolValidator validator, ILogger<ToolDiscoveryService> logger) : IToolDiscovery
{
    private readonly IToolValidator _validator = validator;
    private readonly ILogger<ToolDiscoveryService> _logger = logger;
    private ToolDiscoveryStatistics? _lastStats;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveredTool>> DiscoverToolsAsync(ToolDiscoveryOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ToolDiscoveryOptions();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var discoveredTools = new List<DiscoveredTool>();
        var stats = new ToolDiscoveryStatistics();

        try
        {
            _logger.LogInformation("Starting tool discovery with options: ScanCurrentAssembly={ScanCurrentAssembly}, ScanLoadedAssemblies={ScanLoadedAssemblies}, PluginDirectories={PluginDirectoryCount}",
                options.ScanCurrentAssembly, options.ScanLoadedAssemblies, options.PluginDirectories.Count);

            // Discover from current assembly
            if (options.ScanCurrentAssembly)
            {
                var currentAssembly = Assembly.GetExecutingAssembly();
                var currentAssemblyTools = DiscoverToolsFromAssembly(currentAssembly, options.ValidateTools);
                discoveredTools.AddRange(currentAssemblyTools);
                stats.AssembliesScanned++;
                _logger.LogDebug("Discovered {ToolCount} tools from current assembly {AssemblyName}",
                    currentAssemblyTools.Count, currentAssembly.GetName().Name);
            }

            // Discover from loaded assemblies
            if (options.ScanLoadedAssemblies)
            {
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !ShouldExcludeAssembly(a, options.ExcludeAssemblyPatterns))
                    .ToList();

                foreach (var assembly in loadedAssemblies)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var assemblyTools = DiscoverToolsFromAssembly(assembly, options.ValidateTools);
                        discoveredTools.AddRange(assemblyTools);
                        stats.AssembliesScanned++;

                        if (assemblyTools.Count > 0)
                        {
                            _logger.LogDebug("Discovered {ToolCount} tools from loaded assembly {AssemblyName}",
                                assemblyTools.Count, assembly.GetName().Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        var error = $"Error scanning assembly {assembly.GetName().Name}: {ex.Message}";
                        stats.Errors.Add(error);
                        _logger.LogWarning(ex, "Failed to scan assembly {AssemblyName}", assembly.GetName().Name);
                    }
                }
            }

            // Discover from additional assemblies
            foreach (var assembly in options.AdditionalAssemblies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var assemblyTools = DiscoverToolsFromAssembly(assembly, options.ValidateTools);
                    discoveredTools.AddRange(assemblyTools);
                    stats.AssembliesScanned++;

                    if (assemblyTools.Count > 0)
                    {
                        _logger.LogDebug("Discovered {ToolCount} tools from additional assembly {AssemblyName}",
                            assemblyTools.Count, assembly.GetName().Name);
                    }
                }
                catch (Exception ex)
                {
                    var error = $"Error scanning additional assembly {assembly.GetName().Name}: {ex.Message}";
                    stats.Errors.Add(error);
                    _logger.LogWarning(ex, "Failed to scan additional assembly {AssemblyName}", assembly.GetName().Name);
                }
            }

            // Discover from plugin directories
            foreach (var directory in options.PluginDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var directoryTools = await DiscoverToolsFromDirectoryAsync(
                        directory,
                        options.PluginFilePatterns,
                        options.MaxDirectoryDepth > 1,
                        cancellationToken);
                    discoveredTools.AddRange(directoryTools);

                    if (directoryTools.Count > 0)
                    {
                        _logger.LogDebug("Discovered {ToolCount} tools from plugin directory {Directory}",
                            directoryTools.Count, directory);
                    }
                }
                catch (Exception ex)
                {
                    var error = $"Error scanning plugin directory {directory}: {ex.Message}";
                    stats.Errors.Add(error);
                    _logger.LogWarning(ex, "Failed to scan plugin directory {Directory}", directory);
                }
            }

            // Update statistics
            stats.ValidToolsDiscovered = discoveredTools.Count(t => t.IsValid);
            stats.InvalidToolsFound = discoveredTools.Count(t => !t.IsValid);
            stats.TypesExamined = discoveredTools.Count;
            stopwatch.Stop();
            stats.DiscoveryDuration = stopwatch.Elapsed;

            _lastStats = stats;

            _logger.LogInformation("Tool discovery completed in {Duration}ms. Found {ValidTools} valid tools and {InvalidTools} invalid tools from {AssembliesScanned} assemblies",
                stats.DiscoveryDuration.TotalMilliseconds, stats.ValidToolsDiscovered, stats.InvalidToolsFound, stats.AssembliesScanned);

            return discoveredTools.AsReadOnly();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            stats.DiscoveryDuration = stopwatch.Elapsed;
            stats.Errors.Add($"Discovery failed: {ex.Message}");
            _lastStats = stats;

            _logger.LogError(ex, "Tool discovery failed after {Duration}ms", stats.DiscoveryDuration.TotalMilliseconds);
            throw;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<DiscoveredTool> DiscoverToolsFromAssembly(Assembly assembly, bool validate = true)
    {
        var discoveredTools = new List<DiscoveredTool>();

        try
        {
            var types = assembly.GetTypes()
                .Where(t => typeof(ITool).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            foreach (var type in types)
            {
                try
                {
                    var discoveredTool = new DiscoveredTool
                    {
                        ToolType = type,
                        SourceAssembly = assembly,
                        SourcePath = assembly.Location,
                        DiscoverySource = "assembly",
                        Metadata = new Dictionary<string, object?>
                        {
                            ["assembly_name"] = assembly.GetName().Name,
                            ["assembly_version"] = assembly.GetName().Version?.ToString(),
                            ["type_full_name"] = type.FullName
                        }
                    };

                    if (validate)
                    {
                        var validationResult = _validator.ValidateToolType(type);
                        discoveredTool.IsValid = validationResult.IsValid;
                        if (!validationResult.IsValid)
                        {
                            discoveredTool.ValidationErrors = [.. validationResult.Errors.Select(e => e.Message)];
                        }
                    }

                    discoveredTools.Add(discoveredTool);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process tool type {TypeName} from assembly {AssemblyName}",
                        type.FullName, assembly.GetName().Name);
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            _logger.LogWarning("Failed to load some types from assembly {AssemblyName}: {LoaderExceptions}",
                assembly.GetName().Name, string.Join(", ", ex.LoaderExceptions?.Select(e => e?.Message) ?? Array.Empty<string>()));

            // Try to process the types that did load successfully
            var loadedTypes = ex.Types?.Where(t => t != null).Cast<Type>().ToList() ?? [];
            var toolTypes = loadedTypes
                .Where(t => typeof(ITool).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            foreach (var type in toolTypes)
            {
                try
                {
                    var discoveredTool = new DiscoveredTool
                    {
                        ToolType = type,
                        SourceAssembly = assembly,
                        SourcePath = assembly.Location,
                        DiscoverySource = "assembly"
                    };

                    if (validate)
                    {
                        var validationResult = _validator.ValidateToolType(type);
                        discoveredTool.IsValid = validationResult.IsValid;
                        if (!validationResult.IsValid)
                        {
                            discoveredTool.ValidationErrors = [.. validationResult.Errors.Select(e => e.Message)];
                        }
                    }

                    discoveredTools.Add(discoveredTool);
                }
                catch (Exception typeEx)
                {
                    _logger.LogWarning(typeEx, "Failed to process partial-loaded tool type {TypeName}",
                        type.FullName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover tools from assembly {AssemblyName}",
                assembly.GetName().Name);
        }

        return discoveredTools.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveredTool>> DiscoverToolsFromDirectoryAsync(string directoryPath, IList<string>? filePatterns = null, bool recursive = true, CancellationToken cancellationToken = default)
    {
        filePatterns ??= ["*.dll"];
        var discoveredTools = new List<DiscoveredTool>();

        if (!Directory.Exists(directoryPath))
        {
            _logger.LogWarning("Plugin directory does not exist: {DirectoryPath}", directoryPath);
            return discoveredTools.AsReadOnly();
        }

        try
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = filePatterns
                .SelectMany(pattern => Directory.GetFiles(directoryPath, pattern, searchOption))
                .Distinct()
                .ToList();

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var fileTools = await DiscoverToolsFromFileAsync(file, true);
                    discoveredTools.AddRange(fileTools);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to discover tools from file {FilePath}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover tools from directory {DirectoryPath}", directoryPath);
        }

        return discoveredTools.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveredTool>> DiscoverToolsFromFileAsync(string filePath, bool validate = true)
    {
        var discoveredTools = new List<DiscoveredTool>();

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Plugin file does not exist: {FilePath}", filePath);
            return discoveredTools.AsReadOnly();
        }

        try
        {
            // Load the assembly
            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load assembly from file {FilePath}", filePath);
                return discoveredTools.AsReadOnly();
            }

            // Discover tools from the loaded assembly
            var assemblyTools = DiscoverToolsFromAssembly(assembly, validate);

            // Update the source information to indicate these came from a file
            foreach (var tool in assemblyTools)
            {
                tool.SourcePath = filePath;
                tool.DiscoverySource = "file";
                tool.Metadata["file_path"] = filePath;
                tool.Metadata["file_size"] = new FileInfo(filePath).Length;
                tool.Metadata["file_modified"] = File.GetLastWriteTime(filePath);
                discoveredTools.Add(tool);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover tools from file {FilePath}", filePath);
        }

        await Task.CompletedTask; // Make this async for future enhancements
        return discoveredTools.AsReadOnly();
    }

    /// <inheritdoc />
    public ToolDiscoveryInfo GetDiscoveryInfo()
    {
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !ShouldExcludeAssembly(a, ["System.*", "Microsoft.*"]))
            .ToList();

        return new ToolDiscoveryInfo
        {
            LoadedAssembliesCount = loadedAssemblies.Count,
            LoadedAssemblyNames = [.. loadedAssemblies.Select(a => a.GetName().Name ?? "Unknown")],
            AvailablePluginDirectories = GetAvailablePluginDirectories(),
            SupportedFileExtensions = [".dll", ".exe"],
            CurrentOptions = new ToolDiscoveryOptions(),
            LastDiscoveryStats = _lastStats
        };
    }

    private static bool ShouldExcludeAssembly(Assembly assembly, IList<string> excludePatterns)
    {
        var assemblyName = assembly.GetName().Name;
        return string.IsNullOrEmpty(assemblyName)
            ? true
            : excludePatterns.Any(pattern =>
        {
            var regex = new Regex(pattern.Replace("*", ".*"), RegexOptions.IgnoreCase);
            return regex.IsMatch(assemblyName);
        });
    }

    private static IList<string> GetAvailablePluginDirectories()
    {
        var directories = new List<string>();

        // Add common plugin directories
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var pluginDirectories = new[]
        {
            Path.Combine(baseDirectory, "plugins"),
            Path.Combine(baseDirectory, "tools"),
            Path.Combine(baseDirectory, "extensions"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Andy", "plugins"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Andy", "plugins")
        };

        foreach (var dir in pluginDirectories)
        {
            if (Directory.Exists(dir))
            {
                directories.Add(dir);
            }
        }

        return directories;
    }
}
