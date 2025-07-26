using Andy.Tools.Core;

namespace Andy.Tools.Examples;

/// <summary>
/// Helper methods for examples
/// </summary>
public static class ExampleHelpers
{
    /// <summary>
    /// Gets a value from a dictionary with a default if not found
    /// </summary>
    public static T? GetValueOrDefault<T>(this Dictionary<string, object?> dict, string key, T? defaultValue = default)
    {
        if (dict.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }
}

/// <summary>
/// Placeholder for missing ToolCategory values
/// </summary>
public static class ToolCategoryExtensions
{
    public const string Text = "Text";
    public const string Utilities = "Utilities";
}