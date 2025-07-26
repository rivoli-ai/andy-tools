using System.Security.Cryptography;
using System.Text;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;
using SystemNet = System.Net;

namespace Andy.Tools.Library.Utilities;

/// <summary>
/// Tool for encoding, decoding, and hashing operations.
/// </summary>
public class EncodingTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "encoding_tool",
        Name = "Encoding Tool",
        Description = "Performs encoding, decoding, and hashing operations on text and data",
        Version = "1.0.0",
        Category = ToolCategory.Utility,
        RequiredPermissions = ToolPermissionFlags.None,
        Parameters =
        [
            new()
            {
                Name = "operation",
                Description = "The encoding/decoding operation to perform",
                Type = "string",
                Required = true,
                AllowedValues =
                [
                    "base64_encode", "base64_decode", "url_encode", "url_decode",
                    "html_encode", "html_decode", "hex_encode", "hex_decode",
                    "md5_hash", "sha1_hash", "sha256_hash", "sha512_hash",
                    "bcrypt_hash", "bcrypt_verify", "guid_generate", "uuid_generate",
                    "password_generate", "validate_hash", "compare_hashes"
                ]
            },
            new()
            {
                Name = "input_text",
                Description = "The text or data to process",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "encoding",
                Description = "Text encoding to use (default: UTF-8)",
                Type = "string",
                Required = false,
                DefaultValue = "UTF-8",
                AllowedValues = ["UTF-8", "ASCII", "Unicode", "UTF-32"]
            },
            new()
            {
                Name = "compare_value",
                Description = "Value to compare against (for hash verification)",
                Type = "string",
                Required = false
            },
            new()
            {
                Name = "salt",
                Description = "Salt value for hashing operations",
                Type = "string",
                Required = false
            },
            new()
            {
                Name = "iterations",
                Description = "Number of iterations for key derivation (default: 1000)",
                Type = "integer",
                Required = false,
                DefaultValue = 1000,
                MinValue = 1,
                MaxValue = 100000
            },
            new()
            {
                Name = "length",
                Description = "Length for generated values (passwords, GUIDs, etc.)",
                Type = "integer",
                Required = false,
                DefaultValue = 12,
                MinValue = 4,
                MaxValue = 128
            },
            new()
            {
                Name = "include_symbols",
                Description = "Whether to include symbols in password generation (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            },
            new()
            {
                Name = "include_numbers",
                Description = "Whether to include numbers in password generation (default: true)",
                Type = "boolean",
                Required = false,
                DefaultValue = true
            }
        ]
    };

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var operation = GetParameter<string>(parameters, "operation");
        var inputText = GetParameter<string>(parameters, "input_text");
        var encodingName = GetParameter(parameters, "encoding", "UTF-8");
        var compareValue = GetParameter<string>(parameters, "compare_value");
        var salt = GetParameter<string>(parameters, "salt");
        var iterations = GetParameter(parameters, "iterations", 1000);
        var length = GetParameter(parameters, "length", 12);
        var includeSymbols = GetParameter(parameters, "include_symbols", true);
        var includeNumbers = GetParameter(parameters, "include_numbers", true);

        try
        {
            ReportProgress(context, $"Performing {operation} operation...", 20);

            var encoding = GetTextEncoding(encodingName);
            var result = await PerformEncodingOperationAsync(
                operation, inputText, encoding, compareValue, salt, iterations,
                length, includeSymbols, includeNumbers, context);

            ReportProgress(context, "Encoding operation completed", 100);

            var metadata = new Dictionary<string, object?>
            {
                ["operation"] = operation,
                ["encoding_used"] = encodingName,
                ["input_length"] = inputText.Length,
                ["output_length"] = result.Output?.ToString()?.Length ?? 0,
                ["operation_time"] = result.OperationTime
            };

            // Add operation-specific metadata
            foreach (var kvp in result.Metadata)
            {
                metadata[kvp.Key] = kvp.Value;
            }

            return ToolResults.Success(
                result.Output,
                $"Successfully performed {operation} operation",
                metadata
            );
        }
        catch (ArgumentException ex)
        {
            return ToolResults.InvalidParameter("input_text or parameters", inputText, ex.Message);
        }
        catch (FormatException ex)
        {
            return ToolResults.InvalidParameter("input_text", inputText, $"Invalid format: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Encoding operation failed: {ex.Message}", "ENCODING_ERROR", details: ex);
        }
    }

    private static async Task<EncodingOperationResult> PerformEncodingOperationAsync(
        string operation,
        string inputText,
        Encoding encoding,
        string? compareValue,
        string? salt,
        int iterations,
        int length,
        bool includeSymbols,
        bool includeNumbers,
        ToolExecutionContext context)
    {
        var startTime = DateTime.UtcNow;
        var result = new EncodingOperationResult();

        try
        {
            result.Output = operation.ToLowerInvariant() switch
            {
                "base64_encode" => EncodeBase64(inputText, encoding, result),
                "base64_decode" => DecodeBase64(inputText, encoding, result),
                "url_encode" => EncodeUrl(inputText, result),
                "url_decode" => DecodeUrl(inputText, result),
                "html_encode" => EncodeHtml(inputText, result),
                "html_decode" => DecodeHtml(inputText, result),
                "hex_encode" => EncodeHex(inputText, encoding, result),
                "hex_decode" => DecodeHex(inputText, encoding, result),
                "md5_hash" => HashMd5(inputText, encoding, result),
                "sha1_hash" => HashSha1(inputText, encoding, result),
                "sha256_hash" => HashSha256(inputText, encoding, result),
                "sha512_hash" => HashSha512(inputText, encoding, result),
                "bcrypt_hash" => HashBcrypt(inputText, salt, result),
                "bcrypt_verify" => VerifyBcrypt(inputText, compareValue, result),
                "guid_generate" => GenerateGuid(result),
                "uuid_generate" => GenerateUuid(result),
                "password_generate" => GeneratePassword(length, includeSymbols, includeNumbers, result),
                "validate_hash" => ValidateHash(inputText, result),
                "compare_hashes" => CompareHashes(inputText, compareValue, result),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };
        }
        finally
        {
            result.OperationTime = DateTime.UtcNow - startTime;
        }

        await Task.CompletedTask;
        return result;
    }

    private static string EncodeBase64(string input, Encoding encoding, EncodingOperationResult result)
    {
        var bytes = encoding.GetBytes(input);
        var encoded = Convert.ToBase64String(bytes);

        result.Metadata["original_bytes"] = bytes.Length;
        result.Metadata["encoded_length"] = encoded.Length;

        return encoded;
    }

    private static string DecodeBase64(string input, Encoding encoding, EncodingOperationResult result)
    {
        try
        {
            var bytes = Convert.FromBase64String(input);
            var decoded = encoding.GetString(bytes);

            result.Metadata["decoded_bytes"] = bytes.Length;
            result.Metadata["decoded_length"] = decoded.Length;

            return decoded;
        }
        catch (FormatException)
        {
            throw new ArgumentException("Invalid Base64 string");
        }
    }

    private static string EncodeUrl(string input, EncodingOperationResult result)
    {
        var encoded = Uri.EscapeDataString(input);

        result.Metadata["original_length"] = input.Length;
        result.Metadata["encoded_length"] = encoded.Length;
        result.Metadata["encoding_ratio"] = (double)encoded.Length / input.Length;

        return encoded;
    }

    private static string DecodeUrl(string input, EncodingOperationResult result)
    {
        var decoded = Uri.UnescapeDataString(input);

        result.Metadata["original_length"] = input.Length;
        result.Metadata["decoded_length"] = decoded.Length;

        return decoded;
    }

    private static string EncodeHtml(string input, EncodingOperationResult result)
    {
        var encoded = SystemNet.WebUtility.HtmlEncode(input);

        result.Metadata["original_length"] = input.Length;
        result.Metadata["encoded_length"] = encoded.Length;

        return encoded;
    }

    private static string DecodeHtml(string input, EncodingOperationResult result)
    {
        var decoded = SystemNet.WebUtility.HtmlDecode(input);

        result.Metadata["original_length"] = input.Length;
        result.Metadata["decoded_length"] = decoded.Length;

        return decoded;
    }

    private static string EncodeHex(string input, Encoding encoding, EncodingOperationResult result)
    {
        var bytes = encoding.GetBytes(input);
        var hex = Convert.ToHexString(bytes).ToLowerInvariant();

        result.Metadata["original_bytes"] = bytes.Length;
        result.Metadata["hex_length"] = hex.Length;

        return hex;
    }

    private static string DecodeHex(string input, Encoding encoding, EncodingOperationResult result)
    {
        try
        {
            var bytes = Convert.FromHexString(input);
            var decoded = encoding.GetString(bytes);

            result.Metadata["hex_bytes"] = bytes.Length;
            result.Metadata["decoded_length"] = decoded.Length;

            return decoded;
        }
        catch (FormatException)
        {
            throw new ArgumentException("Invalid hexadecimal string");
        }
    }

    private static string HashMd5(string input, Encoding encoding, EncodingOperationResult result)
    {
        var bytes = encoding.GetBytes(input);
        var hash = MD5.HashData(bytes);
        var hashString = Convert.ToHexString(hash).ToLowerInvariant();

        result.Metadata["hash_algorithm"] = "MD5";
        result.Metadata["hash_length"] = hashString.Length;
        result.Metadata["input_bytes"] = bytes.Length;

        return hashString;
    }

    private static string HashSha1(string input, Encoding encoding, EncodingOperationResult result)
    {
        var bytes = encoding.GetBytes(input);
        var hash = SHA1.HashData(bytes);
        var hashString = Convert.ToHexString(hash).ToLowerInvariant();

        result.Metadata["hash_algorithm"] = "SHA1";
        result.Metadata["hash_length"] = hashString.Length;
        result.Metadata["input_bytes"] = bytes.Length;

        return hashString;
    }

    private static string HashSha256(string input, Encoding encoding, EncodingOperationResult result)
    {
        var bytes = encoding.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        var hashString = Convert.ToHexString(hash).ToLowerInvariant();

        result.Metadata["hash_algorithm"] = "SHA256";
        result.Metadata["hash_length"] = hashString.Length;
        result.Metadata["input_bytes"] = bytes.Length;

        return hashString;
    }

    private static string HashSha512(string input, Encoding encoding, EncodingOperationResult result)
    {
        var bytes = encoding.GetBytes(input);
        var hash = SHA512.HashData(bytes);
        var hashString = Convert.ToHexString(hash).ToLowerInvariant();

        result.Metadata["hash_algorithm"] = "SHA512";
        result.Metadata["hash_length"] = hashString.Length;
        result.Metadata["input_bytes"] = bytes.Length;

        return hashString;
    }

    private static string HashBcrypt(string input, string? salt, EncodingOperationResult result)
    {
        // Simple BCrypt implementation would require additional package
        // For now, use a combination of salt + SHA256
        var saltToUse = salt ?? GenerateRandomSalt();
        var combined = saltToUse + input;
        var bytes = Encoding.UTF8.GetBytes(combined);
        var hash = SHA256.HashData(bytes);
        var hashString = Convert.ToHexString(hash).ToLowerInvariant();

        result.Metadata["hash_algorithm"] = "SHA256+Salt";
        result.Metadata["salt_used"] = saltToUse;
        result.Metadata["hash_length"] = hashString.Length;

        return $"salt:{saltToUse}:hash:{hashString}";
    }

    private static bool VerifyBcrypt(string input, string? hashedValue, EncodingOperationResult result)
    {
        if (string.IsNullOrEmpty(hashedValue))
        {
            throw new ArgumentException("Hashed value is required for verification");
        }

        // Parse our custom format: salt:value:hash:value
        var parts = hashedValue.Split(':');
        if (parts.Length != 4 || parts[0] != "salt" || parts[2] != "hash")
        {
            result.Metadata["verification_result"] = false;
            result.Metadata["error"] = "Invalid hash format";
            return false;
        }

        var salt = parts[1];
        var originalHash = parts[3];

        // Recreate hash with same salt
        var combined = salt + input;
        var bytes = Encoding.UTF8.GetBytes(combined);
        var hash = SHA256.HashData(bytes);
        var hashString = Convert.ToHexString(hash).ToLowerInvariant();

        var matches = string.Equals(hashString, originalHash, StringComparison.OrdinalIgnoreCase);

        result.Metadata["verification_result"] = matches;
        result.Metadata["salt_used"] = salt;

        return matches;
    }

    private static string GenerateGuid(EncodingOperationResult result)
    {
        var guid = Guid.NewGuid();

        result.Metadata["guid_version"] = "Version 4";
        result.Metadata["guid_format"] = "Standard";

        return guid.ToString();
    }

    private static string GenerateUuid(EncodingOperationResult result)
    {
        var uuid = Guid.NewGuid();

        result.Metadata["uuid_version"] = "Version 4";
        result.Metadata["uuid_format"] = "RFC 4122";

        return uuid.ToString();
    }

    private static string GeneratePassword(int length, bool includeSymbols, bool includeNumbers, EncodingOperationResult result)
    {
        const string letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string numbers = "0123456789";
        const string symbols = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        var characters = letters;
        if (includeNumbers)
        {
            characters += numbers;
        }

        if (includeSymbols)
        {
            characters += symbols;
        }

        var random = new Random();
        var password = new StringBuilder(length);

        for (int i = 0; i < length; i++)
        {
            password.Append(characters[random.Next(characters.Length)]);
        }

        result.Metadata["password_length"] = length;
        result.Metadata["character_set_size"] = characters.Length;
        result.Metadata["includes_numbers"] = includeNumbers;
        result.Metadata["includes_symbols"] = includeSymbols;
        result.Metadata["entropy_bits"] = Math.Log2(Math.Pow(characters.Length, length));

        return password.ToString();
    }

    private static object ValidateHash(string hash, EncodingOperationResult result)
    {
        var validationResults = new Dictionary<string, object?>
        {
            ["is_valid_md5"] = IsValidHash(hash, 32),
            ["is_valid_sha1"] = IsValidHash(hash, 40),
            ["is_valid_sha256"] = IsValidHash(hash, 64),
            ["is_valid_sha512"] = IsValidHash(hash, 128),
            ["is_valid_bcrypt"] = hash.StartsWith("salt:") && hash.Contains(":hash:"),
            ["hash_length"] = hash.Length,
            ["contains_only_hex"] = IsHexString(hash)
        };

        var possibleTypes = new List<string>();
        if ((bool)validationResults["is_valid_md5"]!)
        {
            possibleTypes.Add("MD5");
        }

        if ((bool)validationResults["is_valid_sha1"]!)
        {
            possibleTypes.Add("SHA1");
        }

        if ((bool)validationResults["is_valid_sha256"]!)
        {
            possibleTypes.Add("SHA256");
        }

        if ((bool)validationResults["is_valid_sha512"]!)
        {
            possibleTypes.Add("SHA512");
        }

        if ((bool)validationResults["is_valid_bcrypt"]!)
        {
            possibleTypes.Add("Custom BCrypt");
        }

        validationResults["possible_types"] = possibleTypes;
        validationResults["is_likely_hash"] = possibleTypes.Count > 0;

        result.Metadata["validation_performed"] = true;

        return validationResults;
    }

    private static object CompareHashes(string hash1, string? hash2, EncodingOperationResult result)
    {
        if (string.IsNullOrEmpty(hash2))
        {
            throw new ArgumentException("Second hash is required for comparison");
        }

        var areEqual = string.Equals(hash1, hash2, StringComparison.OrdinalIgnoreCase);
        var hash1Info = ValidateHashInternal(hash1);
        var hash2Info = ValidateHashInternal(hash2);

        var comparison = new Dictionary<string, object?>
        {
            ["hashes_equal"] = areEqual,
            ["case_sensitive_equal"] = string.Equals(hash1, hash2, StringComparison.Ordinal),
            ["hash1_length"] = hash1.Length,
            ["hash2_length"] = hash2.Length,
            ["hash1_type"] = hash1Info.MostLikelyType,
            ["hash2_type"] = hash2Info.MostLikelyType,
            ["same_type"] = hash1Info.MostLikelyType == hash2Info.MostLikelyType,
            ["both_valid_hashes"] = hash1Info.IsLikelyHash && hash2Info.IsLikelyHash
        };

        result.Metadata["comparison_performed"] = true;

        return comparison;
    }

    // Helper methods
    private static Encoding GetTextEncoding(string encodingName)
    {
        return encodingName.ToUpperInvariant() switch
        {
            "UTF-8" => Encoding.UTF8,
            "ASCII" => Encoding.ASCII,
            "UNICODE" => Encoding.Unicode,
            "UTF-32" => Encoding.UTF32,
            _ => throw new ArgumentException($"Unsupported encoding: {encodingName}")
        };
    }

    private static string GenerateRandomSalt(int length = 16)
    {
        var random = new Random();
        var chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var salt = new StringBuilder(length);

        for (int i = 0; i < length; i++)
        {
            salt.Append(chars[random.Next(chars.Length)]);
        }

        return salt.ToString();
    }

    private static bool IsValidHash(string hash, int expectedLength)
    {
        return hash.Length == expectedLength && IsHexString(hash);
    }

    private static bool IsHexString(string str)
    {
        return str.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }

    private static HashValidationInfo ValidateHashInternal(string hash)
    {
        var info = new HashValidationInfo
        {
            Length = hash.Length,
            IsHexString = IsHexString(hash)
        };

        if (info.IsHexString)
        {
            info.MostLikelyType = hash.Length switch
            {
                32 => "MD5",
                40 => "SHA1",
                64 => "SHA256",
                128 => "SHA512",
                _ => "Unknown"
            };
            info.IsLikelyHash = info.MostLikelyType != "Unknown";
        }
        else if (hash.StartsWith("salt:") && hash.Contains(":hash:"))
        {
            info.MostLikelyType = "Custom BCrypt";
            info.IsLikelyHash = true;
        }
        else
        {
            info.MostLikelyType = "Unknown";
            info.IsLikelyHash = false;
        }

        return info;
    }

    private class EncodingOperationResult
    {
        public object? Output { get; set; }
        public TimeSpan OperationTime { get; set; }
        public Dictionary<string, object?> Metadata { get; set; } = [];
    }

    private class HashValidationInfo
    {
        public int Length { get; set; }
        public bool IsHexString { get; set; }
        public string MostLikelyType { get; set; } = "";
        public bool IsLikelyHash { get; set; }
    }
}
