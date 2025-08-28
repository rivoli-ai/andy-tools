using System.Text;
using Andy.Tools.Core;
using Andy.Tools.Library.Utilities;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Library.Utilities;

public class EncodingToolTests : IDisposable
{
    private readonly EncodingTool _tool;

    public EncodingToolTests()
    {
        _tool = new EncodingTool();
    }

    public void Dispose()
    {
        // Nothing to dispose
    }

    #region Metadata Tests

    [Fact]
    public void Metadata_ShouldHaveCorrectValues()
    {
        // Act
        var metadata = _tool.Metadata;

        // Assert
        metadata.Should().NotBeNull();
        metadata.Id.Should().Be("encoding_tool");
        metadata.Name.Should().Be("Encoding Tool");
        metadata.Description.Should().Contain("encoding, decoding, and hashing");
        metadata.Category.Should().Be(ToolCategory.Utility);
        metadata.RequiredPermissions.Should().Be(ToolPermissionFlags.None);
        metadata.Parameters.Should().NotBeEmpty();
    }

    [Fact]
    public void Metadata_ShouldHaveRequiredParameters()
    {
        // Act
        var parameters = _tool.Metadata.Parameters;

        // Assert
        parameters.Should().Contain(p => p.Name == "operation" && p.Required);
        parameters.Should().Contain(p => p.Name == "input_text" && p.Required);
        parameters.Should().Contain(p => p.Name == "encoding" && !p.Required);
        parameters.Should().Contain(p => p.Name == "compare_value" && !p.Required);
        parameters.Should().Contain(p => p.Name == "salt" && !p.Required);
        parameters.Should().Contain(p => p.Name == "iterations" && !p.Required);
        parameters.Should().Contain(p => p.Name == "length" && !p.Required);
        parameters.Should().Contain(p => p.Name == "include_symbols" && !p.Required);
        parameters.Should().Contain(p => p.Name == "include_numbers" && !p.Required);
    }

    #endregion

    #region Base64 Tests

    [Fact]
    public async Task ExecuteAsync_Base64Encode_ShouldEncodeText()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "base64_encode",
            ["input_text"] = "Hello, World!"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("\"encoded\":\"SGVsbG8sIFdvcmxkIQ==\"");
    }

    [Fact]
    public async Task ExecuteAsync_Base64Decode_ShouldDecodeText()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "base64_decode",
            ["input_text"] = "SGVsbG8sIFdvcmxkIQ=="
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("\"decoded\":\"Hello, World!\"");
    }

    [Fact]
    public async Task ExecuteAsync_Base64Decode_WithInvalidInput_ShouldReturnError()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "base64_decode",
            ["input_text"] = "Invalid Base64!"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid");
    }

    #endregion

    #region URL Encoding Tests

    [Fact]
    public async Task ExecuteAsync_UrlEncode_ShouldEncodeText()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "url_encode",
            ["input_text"] = "Hello World! Special chars: &=?"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var jsonData = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        var jsonObject = global::System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonData);
        jsonObject.Should().NotBeNull();
        var encoded = jsonObject?["encoded"]?.ToString();
        encoded.Should().NotBeNull();
        // Uri.EscapeDataString uses %20 for spaces, not +
        encoded.Should().Contain("Hello%20World");
        encoded.Should().Contain("%26");
        encoded.Should().Contain("%3D");
        encoded.Should().Contain("%3F");
    }

    [Fact]
    public async Task ExecuteAsync_UrlDecode_ShouldDecodeText()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "url_decode",
            ["input_text"] = "Hello+World%21+Special+chars%3A+%26%3D%3F"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var jsonData = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        var jsonObject = global::System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonData);
        jsonObject.Should().NotBeNull();
        var decoded = jsonObject?["decoded"]?.ToString();
        decoded.Should().NotBeNull();
        decoded.Should().Be("Hello World! Special chars: &=?");
    }

    #endregion

    #region HTML Encoding Tests

    [Fact]
    public async Task ExecuteAsync_HtmlEncode_ShouldEncodeText()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "html_encode",
            ["input_text"] = "<script>alert('XSS')</script>"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var jsonData = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        var jsonObject = global::System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonData);
        jsonObject.Should().NotBeNull();
        var encoded = jsonObject?["encoded"]?.ToString();
        encoded.Should().NotBeNull();
        encoded.Should().Contain("&lt;script&gt;");
        encoded.Should().Contain("&lt;/script&gt;");
    }

    [Fact]
    public async Task ExecuteAsync_HtmlDecode_ShouldDecodeText()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "html_decode",
            ["input_text"] = "&lt;div&gt;Hello &amp; Welcome&lt;/div&gt;"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var jsonData = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        var jsonObject = global::System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonData);
        jsonObject.Should().NotBeNull();
        var decoded = jsonObject?["decoded"]?.ToString();
        decoded.Should().NotBeNull();
        decoded.Should().Be("<div>Hello & Welcome</div>");
    }

    #endregion

    #region Hex Encoding Tests

    [Fact]
    public async Task ExecuteAsync_HexEncode_ShouldEncodeText()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "hex_encode",
            ["input_text"] = "Hello"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("48656C6C6F"); // "Hello" in hex
    }

    [Fact]
    public async Task ExecuteAsync_HexDecode_ShouldDecodeText()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "hex_decode",
            ["input_text"] = "48656C6C6F"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("\"decoded\":\"Hello\"");
    }

    #endregion

    #region Hash Tests

    [Fact]
    public async Task ExecuteAsync_Md5Hash_ShouldGenerateHash()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "md5_hash",
            ["input_text"] = "Hello"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("8b1a9953c4611296a827abf8c47804d7"); // MD5 hash of "Hello"
    }

    [Fact]
    public async Task ExecuteAsync_Sha256Hash_ShouldGenerateHash()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "sha256_hash",
            ["input_text"] = "Hello"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("185f8db32271fe25f561a6fc938b2e264306ec304eda518007d1764826381969"); // SHA256 hash of "Hello"
    }

    [Fact]
    public async Task ExecuteAsync_Sha1Hash_ShouldGenerateHash()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "sha1_hash",
            ["input_text"] = "Hello"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("f7ff9e8b7bb2e09b70935a5d785e0cc5d9d0abf0"); // SHA1 hash of "Hello"
    }

    [Fact]
    public async Task ExecuteAsync_Sha512Hash_ShouldGenerateHash()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "sha512_hash",
            ["input_text"] = "Hello"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        // SHA512 hash is 128 characters long
        json.Should().MatchRegex("\"hash\":\"[a-f0-9]{128}\"");
    }

    #endregion

    #region GUID/UUID Tests

    [Fact]
    public async Task ExecuteAsync_GuidGenerate_ShouldGenerateGuid()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "guid_generate",
            ["input_text"] = "" // Not used but required
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        // GUID format: 8-4-4-4-12 hex digits
        json.Should().MatchRegex("[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}");
    }

    [Fact]
    public async Task ExecuteAsync_UuidGenerate_ShouldGenerateUuid()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "uuid_generate",
            ["input_text"] = "" // Not used but required
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        // UUID format (uppercase)
        json.Should().MatchRegex("[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}");
    }

    #endregion

    #region Password Generation Tests

    [Fact]
    public async Task ExecuteAsync_PasswordGenerate_WithSymbolsAndNumbers_ShouldGeneratePassword()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "password_generate",
            ["input_text"] = "", // Not used but required
            ["length"] = 16,
            ["include_symbols"] = true,
            ["include_numbers"] = true
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("\"password\":");
        json.Should().Contain("\"length\":16");
        json.Should().Contain("\"has_uppercase\":true");
        json.Should().Contain("\"has_lowercase\":true");
        json.Should().Contain("\"has_numbers\":true");
        json.Should().Contain("\"has_symbols\":true");
    }

    [Fact]
    public async Task ExecuteAsync_PasswordGenerate_WithoutSymbols_ShouldGenerateAlphanumericPassword()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "password_generate",
            ["input_text"] = "", // Not used but required
            ["length"] = 12,
            ["include_symbols"] = false,
            ["include_numbers"] = true
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("\"length\":12");
        json.Should().Contain("\"has_symbols\":false");
    }

    #endregion

    #region BCrypt Tests

    [Fact]
    public async Task ExecuteAsync_BcryptHash_ShouldGenerateHash()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "bcrypt_hash",
            ["input_text"] = "MyPassword123"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("\"hash\":\"$2"); // BCrypt hashes start with $2
        json.Should().Contain("\"algorithm\":\"bcrypt\"");
    }

    [Fact]
    public async Task ExecuteAsync_BcryptVerify_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        // First generate a hash
        var hashParams = new Dictionary<string, object?>
        {
            ["operation"] = "bcrypt_hash",
            ["input_text"] = "MyPassword123"
        };

        var context = new ToolExecutionContext();
        await _tool.InitializeAsync();
        
        var hashResult = await _tool.ExecuteAsync(hashParams, context);
        var hashJson = global::System.Text.Json.JsonSerializer.Serialize(hashResult.Data);
        var hashMatch = global::System.Text.RegularExpressions.Regex.Match(hashJson, "\"hash\":\"([^\"]+)\"");
        var hash = hashMatch.Groups[1].Value;

        // Now verify
        var verifyParams = new Dictionary<string, object?>
        {
            ["operation"] = "bcrypt_verify",
            ["input_text"] = "MyPassword123",
            ["compare_value"] = hash
        };

        // Act
        var result = await _tool.ExecuteAsync(verifyParams, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("\"is_valid\":true");
    }

    [Fact]
    public async Task ExecuteAsync_BcryptVerify_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "bcrypt_verify",
            ["input_text"] = "WrongPassword",
            ["compare_value"] = "$2a$10$N/0v8Y8pgf.2.jMr8L1kluYFpAF7c8Mv0KPitilhSKGcNqDxJHXaW" // Hash of something else
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("\"is_valid\":false");
    }

    #endregion

    #region Hash Validation Tests

    [Fact]
    public async Task ExecuteAsync_ValidateHash_WithValidMd5_ShouldReturnValid()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "validate_hash",
            ["input_text"] = "8b1a9953c4611296a827abf8c47804d7" // Valid MD5
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("\"is_valid\":true");
        json.Should().Contain("\"hash_type\":\"MD5\"");
    }

    [Fact]
    public async Task ExecuteAsync_ValidateHash_WithInvalidHash_ShouldReturnInvalid()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "validate_hash",
            ["input_text"] = "not-a-valid-hash!"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("\"is_valid\":false");
    }

    #endregion

    #region Compare Hashes Tests

    [Fact]
    public async Task ExecuteAsync_CompareHashes_WithSameHashes_ShouldMatch()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "compare_hashes",
            ["input_text"] = "8b1a9953c4611296a827abf8c47804d7",
            ["compare_value"] = "8b1a9953c4611296a827abf8c47804d7"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("\"match\":true");
    }

    [Fact]
    public async Task ExecuteAsync_CompareHashes_WithDifferentHashes_ShouldNotMatch()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "compare_hashes",
            ["input_text"] = "8b1a9953c4611296a827abf8c47804d7",
            ["compare_value"] = "f7ff9e8b7bb2e09b70935a5d785e0cc5d9d0abf0"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("\"match\":false");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_UnknownOperation_ShouldReturnError()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "unknown_operation",
            ["input_text"] = "test"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("must be one of");
    }

    [Fact]
    public async Task ExecuteAsync_MissingRequiredParameters_ShouldReturnError()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "base64_encode"
            // Missing input_text
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("input_text");
    }

    #endregion

    #region Encoding Parameter Tests

    [Fact]
    public async Task ExecuteAsync_WithDifferentEncoding_ShouldRespectEncoding()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "base64_encode",
            ["input_text"] = "Hello 世界",
            ["encoding"] = "UTF-8"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Metadata.Should().ContainKey("encoding_used");
        result.Metadata!["encoding_used"].Should().Be("UTF-8");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidEncoding_ShouldReturnError()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "base64_encode",
            ["input_text"] = "Hello",
            ["encoding"] = "INVALID-ENCODING"
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("encoding");
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public async Task ExecuteAsync_EmptyInput_ShouldHandleGracefully()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "base64_encode",
            ["input_text"] = ""
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = global::System.Text.Json.JsonSerializer.Serialize(result.Data);
        json.Should().Contain("\"encoded\":\"\""); // Empty string encodes to empty string
    }

    [Fact]
    public async Task ExecuteAsync_VeryLongInput_ShouldHandleGracefully()
    {
        // Arrange
        var longInput = new string('A', 10000);
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "md5_hash",
            ["input_text"] = longInput
        };

        var context = new ToolExecutionContext();

        // Initialize the tool
        await _tool.InitializeAsync();

        // Act
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Metadata.Should().ContainKey("input_length");
        result.Metadata!["input_length"].Should().Be(10000);
    }

    #endregion
}