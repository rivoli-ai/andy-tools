using Andy.Tools.Core;
using Andy.Tools.Library;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Tools.Examples;

public static class CustomToolExamples
{
    public static async Task RunAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== Custom Tool Examples ===\n");
        Console.WriteLine("This demonstrates how to create your own tools.\n");

        // Use the existing service provider which already has tools registered
        var toolExecutor = serviceProvider.GetRequiredService<IToolExecutor>();

        // Example 1: Word count tool
        Console.WriteLine("1. Word Count Tool:");
        await UseWordCountTool(toolExecutor);

        // Example 2: CSV processor
        Console.WriteLine("\n2. CSV Processor Tool:");
        await UseCsvProcessor(toolExecutor);

        // Example 3: Password generator
        Console.WriteLine("\n3. Password Generator Tool:");
        await UsePasswordGenerator(toolExecutor);
    }

    private static async Task UseWordCountTool(IToolExecutor toolExecutor)
    {
        var sampleText = """
            The Andy Tools framework provides a comprehensive solution for building
            extensible tool systems. With its modular architecture, developers can
            easily create custom tools that integrate seamlessly with the existing
            infrastructure. The framework handles security, permissions, and resource
            management automatically.
            """;

        var parameters = new Dictionary<string, object?>
        {
            ["text"] = sampleText,
            ["count_type"] = "all" // words, lines, characters, or all
        };

        var result = await toolExecutor.ExecuteAsync("word_count", parameters);
        
        if (result.IsSuccessful && result.Data is Dictionary<string, object?> counts)
        {
            Console.WriteLine("Text statistics:");
            Console.WriteLine($"- Words: {counts.GetValueOrDefault("words")}");
            Console.WriteLine($"- Lines: {counts.GetValueOrDefault("lines")}");
            Console.WriteLine($"- Characters: {counts.GetValueOrDefault("characters")}");
            Console.WriteLine($"- Characters (no spaces): {counts.GetValueOrDefault("characters_no_spaces")}");
        }
    }

    private static async Task UseCsvProcessor(IToolExecutor toolExecutor)
    {
        var csvData = """
            Product,Price,Quantity
            Apple,1.50,100
            Banana,0.75,150
            Orange,2.00,80
            Grape,3.50,50
            """;

        var parameters = new Dictionary<string, object?>
        {
            ["csv_data"] = csvData,
            ["operation"] = "calculate_total",
            ["column"] = "Price",
            ["multiplier_column"] = "Quantity"
        };

        var result = await toolExecutor.ExecuteAsync("csv_processor", parameters);
        
        if (result.IsSuccessful && result.Data is Dictionary<string, object?> data)
        {
            Console.WriteLine($"Total revenue: ${data.GetValueOrDefault("total"):F2}");
            Console.WriteLine($"Items processed: {data.GetValueOrDefault("row_count")}");
        }
    }

    private static async Task UsePasswordGenerator(IToolExecutor toolExecutor)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["length"] = 16,
            ["include_uppercase"] = true,
            ["include_lowercase"] = true,
            ["include_numbers"] = true,
            ["include_symbols"] = true,
            ["exclude_ambiguous"] = true // Exclude 0, O, l, 1, etc.
        };

        var result = await toolExecutor.ExecuteAsync("password_generator", parameters);
        
        if (result.IsSuccessful && result.Data is Dictionary<string, object?> data)
        {
            Console.WriteLine($"Generated password: {data.GetValueOrDefault("password")}");
            Console.WriteLine($"Strength: {data.GetValueOrDefault("strength")}");
            Console.WriteLine($"Entropy bits: {data.GetValueOrDefault("entropy_bits")}");
        }
    }
}

// Custom tool implementations

public class WordCountTool : ToolBase
{
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "word_count",
        Name = "Word Count",
        Description = "Counts words, lines, and characters in text",
        Version = "1.0.0",
        Category = ToolCategory.TextProcessing,
        Parameters = new[]
        {
            new ToolParameter
            {
                Name = "text",
                Description = "The text to analyze",
                Type = "string",
                Required = true
            },
            new ToolParameter
            {
                Name = "count_type",
                Description = "What to count: words, lines, characters, or all",
                Type = "string",
                Required = false,
                DefaultValue = "all",
                AllowedValues = new object[] { "words", "lines", "characters", "all" }
            }
        }
    };

    protected override Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var text = GetParameter<string>(parameters, "text", "");
        var countType = GetParameter<string>(parameters, "count_type", "all");

        if (string.IsNullOrEmpty(text))
        {
            return Task.FromResult(ToolResult.Failure("Text cannot be empty"));
        }

        var result = new Dictionary<string, object?>();

        if (countType == "all" || countType == "words")
        {
            var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            result["words"] = words.Length;
        }

        if (countType == "all" || countType == "lines")
        {
            var lines = text.Split('\n');
            result["lines"] = lines.Length;
        }

        if (countType == "all" || countType == "characters")
        {
            result["characters"] = text.Length;
            result["characters_no_spaces"] = text.Count(c => !char.IsWhiteSpace(c));
        }

        return Task.FromResult(ToolResult.Success(result));
    }
}

public class CsvProcessorTool : ToolBase
{
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "csv_processor",
        Name = "CSV Processor",
        Description = "Processes CSV data with various operations",
        Version = "1.0.0",
        Category = ToolCategory.TextProcessing,
        Parameters = new[]
        {
            new ToolParameter
            {
                Name = "csv_data",
                Description = "The CSV data to process",
                Type = "string",
                Required = true
            },
            new ToolParameter
            {
                Name = "operation",
                Description = "Operation to perform",
                Type = "string",
                Required = true,
                AllowedValues = new object[] { "sum", "average", "count", "calculate_total" }
            },
            new ToolParameter
            {
                Name = "column",
                Description = "Column to operate on",
                Type = "string",
                Required = true
            },
            new ToolParameter
            {
                Name = "multiplier_column",
                Description = "Column to multiply by (for calculate_total)",
                Type = "string",
                Required = false
            }
        }
    };

    protected override Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var csvData = GetParameter<string>(parameters, "csv_data", "");
        var operation = GetParameter<string>(parameters, "operation", "");
        var column = GetParameter<string>(parameters, "column", "");
        var multiplierColumn = GetParameter<string>(parameters, "multiplier_column", "");

        try
        {
            var lines = csvData.Trim().Split('\n');
            if (lines.Length < 2)
            {
                return Task.FromResult(ToolResult.Failure("CSV must have headers and at least one data row"));
            }

            var headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
            var columnIndex = Array.IndexOf(headers, column);
            if (columnIndex == -1)
            {
                return Task.FromResult(ToolResult.Failure($"Column '{column}' not found"));
            }

            var multiplierIndex = -1;
            if (!string.IsNullOrEmpty(multiplierColumn))
            {
                multiplierIndex = Array.IndexOf(headers, multiplierColumn);
                if (multiplierIndex == -1)
                {
                    return Task.FromResult(ToolResult.Failure($"Multiplier column '{multiplierColumn}' not found"));
                }
            }

            double total = 0;
            int count = 0;

            for (int i = 1; i < lines.Length; i++)
            {
                var values = lines[i].Split(',').Select(v => v.Trim()).ToArray();
                if (values.Length > columnIndex)
                {
                    if (double.TryParse(values[columnIndex], out var value))
                    {
                        if (operation == "calculate_total" && multiplierIndex >= 0 && values.Length > multiplierIndex)
                        {
                            if (double.TryParse(values[multiplierIndex], out var multiplier))
                            {
                                total += value * multiplier;
                            }
                        }
                        else
                        {
                            total += value;
                        }
                        count++;
                    }
                }
            }

            var result = new Dictionary<string, object?>
            {
                ["row_count"] = count
            };

            switch (operation)
            {
                case "sum":
                case "calculate_total":
                    result["total"] = total;
                    break;
                case "average":
                    result["average"] = count > 0 ? total / count : 0;
                    break;
                case "count":
                    result["count"] = count;
                    break;
            }

            return Task.FromResult(ToolResult.Success(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Failure($"Error processing CSV: {ex.Message}"));
        }
    }
}

public class PasswordGeneratorTool : ToolBase
{
    private static readonly Random _random = new();
    
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "password_generator",
        Name = "Password Generator",
        Description = "Generates secure passwords with customizable options",
        Version = "1.0.0",
        Category = ToolCategory.Utility,
        Parameters = new[]
        {
            new ToolParameter
            {
                Name = "length",
                Description = "Password length",
                Type = "integer",
                Required = false,
                DefaultValue = 16,
                MinValue = 8,
                MaxValue = 128
            },
            new ToolParameter
            {
                Name = "include_uppercase",
                Description = "Include uppercase letters",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new ToolParameter
            {
                Name = "include_lowercase",
                Description = "Include lowercase letters",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new ToolParameter
            {
                Name = "include_numbers",
                Description = "Include numbers",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new ToolParameter
            {
                Name = "include_symbols",
                Description = "Include symbols",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new ToolParameter
            {
                Name = "exclude_ambiguous",
                Description = "Exclude ambiguous characters (0, O, l, 1, etc.)",
                Type = "boolean",
                Required = false,
                DefaultValue = false
            }
        }
    };

    protected override Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var length = GetParameter<int>(parameters, "length", 16);
        var includeUppercase = GetParameter<bool>(parameters, "include_uppercase", true);
        var includeLowercase = GetParameter<bool>(parameters, "include_lowercase", true);
        var includeNumbers = GetParameter<bool>(parameters, "include_numbers", true);
        var includeSymbols = GetParameter<bool>(parameters, "include_symbols", true);
        var excludeAmbiguous = GetParameter<bool>(parameters, "exclude_ambiguous", false);

        var charset = "";
        if (includeLowercase) charset += "abcdefghijklmnopqrstuvwxyz";
        if (includeUppercase) charset += "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        if (includeNumbers) charset += "0123456789";
        if (includeSymbols) charset += "!@#$%^&*()_+-=[]{}|;:,.<>?";

        if (excludeAmbiguous)
        {
            charset = charset.Replace("0", "").Replace("O", "").Replace("o", "")
                            .Replace("l", "").Replace("1", "").Replace("I", "");
        }

        if (string.IsNullOrEmpty(charset))
        {
            return Task.FromResult(ToolResult.Failure("At least one character type must be selected"));
        }

        var password = new char[length];
        for (int i = 0; i < length; i++)
        {
            password[i] = charset[_random.Next(charset.Length)];
        }

        // Calculate entropy
        var entropyBits = Math.Log2(Math.Pow(charset.Length, length));
        
        // Determine strength
        var strength = entropyBits switch
        {
            < 30 => "Very Weak",
            < 50 => "Weak",
            < 70 => "Fair",
            < 90 => "Strong",
            _ => "Very Strong"
        };

        var result = new Dictionary<string, object?>
        {
            ["password"] = new string(password),
            ["strength"] = strength,
            ["entropy_bits"] = Math.Round(entropyBits, 2),
            ["charset_size"] = charset.Length
        };

        return Task.FromResult(ToolResult.Success(result));
    }
}