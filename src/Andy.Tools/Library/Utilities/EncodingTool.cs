using System.Security.Cryptography;
using System.Text;
using System.Linq;
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

    private static object EncodeBase64(string input, Encoding encoding, EncodingOperationResult result)
    {
        var bytes = encoding.GetBytes(input);
        var encoded = Convert.ToBase64String(bytes);

        result.Metadata["original_bytes"] = bytes.Length;
        result.Metadata["encoded_length"] = encoded.Length;

        return new { encoded = encoded };
    }

    private static object DecodeBase64(string input, Encoding encoding, EncodingOperationResult result)
    {
        try
        {
            var bytes = Convert.FromBase64String(input);
            var decoded = encoding.GetString(bytes);

            result.Metadata["decoded_bytes"] = bytes.Length;
            result.Metadata["decoded_length"] = decoded.Length;

            return new { decoded = decoded };
        }
        catch (FormatException)
        {
            throw new ArgumentException("Invalid Base64 string");
        }
    }

    private static object EncodeUrl(string input, EncodingOperationResult result)
    {
        var encoded = Uri.EscapeDataString(input);

        result.Metadata["original_length"] = input.Length;
        result.Metadata["encoded_length"] = encoded.Length;
        result.Metadata["encoding_ratio"] = (double)encoded.Length / input.Length;

        return new { encoded = encoded };
    }

    private static object DecodeUrl(string input, EncodingOperationResult result)
    {
        // Symmetric with EncodeUrl, which uses Uri.EscapeDataString (RFC 3986): spaces become %20 and a
        // literal '+' stays '+'. So we must NOT translate '+' to space here, otherwise a round-trip of
        // "a+b" would corrupt to "a b".
        var decoded = Uri.UnescapeDataString(input);

        result.Metadata["original_length"] = input.Length;
        result.Metadata["decoded_length"] = decoded.Length;

        return new { decoded = decoded };
    }

    private static object EncodeHtml(string input, EncodingOperationResult result)
    {
        var encoded = SystemNet.WebUtility.HtmlEncode(input);

        result.Metadata["original_length"] = input.Length;
        result.Metadata["encoded_length"] = encoded.Length;

        return new { encoded = encoded };
    }

    private static object DecodeHtml(string input, EncodingOperationResult result)
    {
        var decoded = SystemNet.WebUtility.HtmlDecode(input);

        result.Metadata["original_length"] = input.Length;
        result.Metadata["decoded_length"] = decoded.Length;

        return new { decoded = decoded };
    }

    private static object EncodeHex(string input, Encoding encoding, EncodingOperationResult result)
    {
        var bytes = encoding.GetBytes(input);
        var hex = Convert.ToHexString(bytes); // Keep uppercase for test expectation

        result.Metadata["original_bytes"] = bytes.Length;
        result.Metadata["hex_length"] = hex.Length;

        return new { encoded = hex };
    }

    private static object DecodeHex(string input, Encoding encoding, EncodingOperationResult result)
    {
        try
        {
            var bytes = Convert.FromHexString(input);
            var decoded = encoding.GetString(bytes);

            result.Metadata["hex_bytes"] = bytes.Length;
            result.Metadata["decoded_length"] = decoded.Length;

            return new { decoded = decoded };
        }
        catch (FormatException)
        {
            throw new ArgumentException("Invalid hexadecimal string");
        }
    }

    private static object HashMd5(string input, Encoding encoding, EncodingOperationResult result)
    {
        var bytes = encoding.GetBytes(input);
        var hash = MD5.HashData(bytes);
        var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        result.Metadata["hash_algorithm"] = "MD5";
        result.Metadata["hash_length"] = hashString.Length;
        result.Metadata["input_bytes"] = bytes.Length;

        return new { hash = hashString };
    }

    private static object HashSha1(string input, Encoding encoding, EncodingOperationResult result)
    {
        var bytes = encoding.GetBytes(input);
        var hash = SHA1.HashData(bytes);
        var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        result.Metadata["hash_algorithm"] = "SHA1";
        result.Metadata["hash_length"] = hashString.Length;
        result.Metadata["input_bytes"] = bytes.Length;

        return new { hash = hashString };
    }

    private static object HashSha256(string input, Encoding encoding, EncodingOperationResult result)
    {
        var bytes = encoding.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        result.Metadata["hash_algorithm"] = "SHA256";
        result.Metadata["hash_length"] = hashString.Length;
        result.Metadata["input_bytes"] = bytes.Length;

        return new { hash = hashString };
    }

    private static object HashSha512(string input, Encoding encoding, EncodingOperationResult result)
    {
        var bytes = encoding.GetBytes(input);
        var hash = SHA512.HashData(bytes);
        var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        result.Metadata["hash_algorithm"] = "SHA512";
        result.Metadata["hash_length"] = hashString.Length;
        result.Metadata["input_bytes"] = bytes.Length;

        return new { hash = hashString };
    }

    // Password hashing parameters. The operation is named "bcrypt_*" for API compatibility, but a real
    // bcrypt requires a third-party package; PBKDF2-HMAC-SHA256 is built in and provides proper key
    // stretching (the previous implementation was a single unsalted-iteration SHA256 masquerading as bcrypt).
    private const string Pbkdf2Prefix = "pbkdf2-sha256";
    private const int Pbkdf2Iterations = 100_000;
    private const int Pbkdf2SaltBytes = 16;
    private const int Pbkdf2HashBytes = 32;

    private static object HashBcrypt(string input, string? salt, EncodingOperationResult result)
    {
        var saltBytes = salt != null
            ? Encoding.UTF8.GetBytes(salt)
            : RandomNumberGenerator.GetBytes(Pbkdf2SaltBytes);

        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(input), saltBytes, Pbkdf2Iterations, HashAlgorithmName.SHA256, Pbkdf2HashBytes);

        // Self-describing PHC-style format: $pbkdf2-sha256$<iterations>$<hex salt>$<hex hash>.
        // Hex (not base64) keeps the encoded value free of JSON-escaped characters like '+'/'/'.
        var encoded = $"${Pbkdf2Prefix}${Pbkdf2Iterations}${Convert.ToHexString(saltBytes)}${Convert.ToHexString(hashBytes)}";

        result.Metadata["hash_algorithm"] = "PBKDF2-SHA256";
        result.Metadata["iterations"] = Pbkdf2Iterations;
        result.Metadata["hash_length"] = encoded.Length;

        return new
        {
            hash = encoded,
            algorithm = Pbkdf2Prefix
        };
    }

    private static object VerifyBcrypt(string input, string? hashedValue, EncodingOperationResult result)
    {
        if (string.IsNullOrEmpty(hashedValue))
        {
            throw new ArgumentException("Hashed value is required for verification");
        }

        var matches = false;

        // Expected format: $pbkdf2-sha256$<iterations>$<b64 salt>$<b64 hash>
        var parts = hashedValue.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 4 && parts[0] == Pbkdf2Prefix && int.TryParse(parts[1], out var iterations) && iterations > 0)
        {
            try
            {
                var saltBytes = Convert.FromHexString(parts[2]);
                var expected = Convert.FromHexString(parts[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(
                    Encoding.UTF8.GetBytes(input), saltBytes, iterations, HashAlgorithmName.SHA256, expected.Length);

                // Constant-time comparison to avoid leaking the hash via timing.
                matches = CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                matches = false;
            }
        }
        else
        {
            result.Metadata["error"] = $"Unrecognized hash format (expected ${Pbkdf2Prefix}$...)";
        }

        result.Metadata["verification_result"] = matches;
        return new { is_valid = matches };
    }

    private static object GenerateGuid(EncodingOperationResult result)
    {
        var guid = Guid.NewGuid();

        result.Metadata["guid_version"] = "Version 4";
        result.Metadata["guid_format"] = "Standard";

        return new { guid = guid.ToString() };
    }

    private static object GenerateUuid(EncodingOperationResult result)
    {
        var uuid = Guid.NewGuid();

        result.Metadata["uuid_version"] = "Version 4";
        result.Metadata["uuid_format"] = "RFC 4122";

        return new { uuid = uuid.ToString().ToUpperInvariant() };
    }

    private static object GeneratePassword(int length, bool includeSymbols, bool includeNumbers, EncodingOperationResult result)
    {
        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string numbers = "0123456789";
        const string symbols = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        // Use a cryptographically secure RNG: generated passwords must not be predictable.
        var password = new StringBuilder(length);
        var requiredChars = new List<char>();

        // Ensure at least one character from each required category
        requiredChars.Add(lowercase[RandomNumberGenerator.GetInt32(lowercase.Length)]);
        requiredChars.Add(uppercase[RandomNumberGenerator.GetInt32(uppercase.Length)]);

        if (includeNumbers)
        {
            requiredChars.Add(numbers[RandomNumberGenerator.GetInt32(numbers.Length)]);
        }

        if (includeSymbols)
        {
            requiredChars.Add(symbols[RandomNumberGenerator.GetInt32(symbols.Length)]);
        }

        // Build the full character set
        var characters = lowercase + uppercase;
        if (includeNumbers)
        {
            characters += numbers;
        }

        if (includeSymbols)
        {
            characters += symbols;
        }

        // Fill remaining positions with random characters
        for (int i = requiredChars.Count; i < length; i++)
        {
            requiredChars.Add(characters[RandomNumberGenerator.GetInt32(characters.Length)]);
        }

        // Cryptographically secure Fisher-Yates shuffle so the guaranteed-category characters are not
        // always at predictable positions.
        for (int i = requiredChars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (requiredChars[i], requiredChars[j]) = (requiredChars[j], requiredChars[i]);
        }

        foreach (var c in requiredChars)
        {
            password.Append(c);
        }

        result.Metadata["password_length"] = length;
        result.Metadata["character_set_size"] = characters.Length;
        result.Metadata["includes_numbers"] = includeNumbers;
        result.Metadata["includes_symbols"] = includeSymbols;
        result.Metadata["entropy_bits"] = Math.Log2(Math.Pow(characters.Length, length));

        var passwordStr = password.ToString();
        
        return new 
        { 
            password = passwordStr,
            length = length,
            has_uppercase = passwordStr.Any(char.IsUpper),
            has_lowercase = passwordStr.Any(char.IsLower),
            has_numbers = passwordStr.Any(char.IsDigit),
            has_symbols = includeSymbols && passwordStr.Any(c => symbols.Contains(c))
        };
    }

    private static object ValidateHash(string hash, EncodingOperationResult result)
    {
        var validationResults = new Dictionary<string, object?>
        {
            ["is_valid_md5"] = IsValidHash(hash, 32),
            ["is_valid_sha1"] = IsValidHash(hash, 40),
            ["is_valid_sha256"] = IsValidHash(hash, 64),
            ["is_valid_sha512"] = IsValidHash(hash, 128),
            ["is_valid_pbkdf2"] = hash.StartsWith($"${Pbkdf2Prefix}$", StringComparison.Ordinal),
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

        if ((bool)validationResults["is_valid_pbkdf2"]!)
        {
            possibleTypes.Add("PBKDF2-SHA256");
        }

        validationResults["possible_types"] = possibleTypes;
        validationResults["is_likely_hash"] = possibleTypes.Count > 0;

        result.Metadata["validation_performed"] = true;

        // Add expected properties for tests
        validationResults["is_valid"] = possibleTypes.Count > 0;
        if (possibleTypes.Count > 0 && possibleTypes[0] != null)
        {
            validationResults["hash_type"] = possibleTypes[0];
        }
        
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
            ["both_valid_hashes"] = hash1Info.IsLikelyHash && hash2Info.IsLikelyHash,
            ["match"] = areEqual  // Add the expected property for tests
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
        else if (hash.StartsWith($"${Pbkdf2Prefix}$", StringComparison.Ordinal))
        {
            info.MostLikelyType = "PBKDF2-SHA256";
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
