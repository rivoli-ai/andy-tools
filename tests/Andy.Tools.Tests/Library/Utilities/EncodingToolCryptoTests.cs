using System.Text.Json;
using Andy.Tools.Core;
using Andy.Tools.Library.Utilities;
using FluentAssertions;

namespace Andy.Tools.Tests.Library.Utilities;

/// <summary>
/// Regression tests for issue #13: insecure RNG for passwords, a fake single-round-SHA256 "bcrypt",
/// and a non-constant-time verify.
/// </summary>
public class EncodingToolCryptoTests
{
    private static async Task<string> RunAsync(EncodingTool tool, Dictionary<string, object?> p)
    {
        var result = await tool.ExecuteAsync(p, new ToolExecutionContext());
        result.IsSuccessful.Should().BeTrue();
        return JsonSerializer.Serialize(result.Data);
    }

    [Fact]
    public async Task BcryptHash_UsesPbkdf2_AndVerifiesRoundTrip()
    {
        var tool = new EncodingTool();
        await tool.InitializeAsync();

        var hashJson = await RunAsync(tool, new() { ["operation"] = "bcrypt_hash", ["input_text"] = "Correct horse" });
        var hash = JsonDocument.Parse(hashJson).RootElement.GetProperty("hash").GetString()!;
        hash.Should().StartWith("$pbkdf2-sha256$");

        var okJson = await RunAsync(tool, new()
        {
            ["operation"] = "bcrypt_verify",
            ["input_text"] = "Correct horse",
            ["compare_value"] = hash
        });
        okJson.Should().Contain("\"is_valid\":true");

        var badJson = await RunAsync(tool, new()
        {
            ["operation"] = "bcrypt_verify",
            ["input_text"] = "wrong",
            ["compare_value"] = hash
        });
        badJson.Should().Contain("\"is_valid\":false");
    }

    [Fact]
    public async Task BcryptHash_SameInput_ProducesDifferentSaltedHashes()
    {
        var tool = new EncodingTool();
        await tool.InitializeAsync();

        var a = JsonDocument.Parse(await RunAsync(tool, new() { ["operation"] = "bcrypt_hash", ["input_text"] = "same" }))
            .RootElement.GetProperty("hash").GetString();
        var b = JsonDocument.Parse(await RunAsync(tool, new() { ["operation"] = "bcrypt_hash", ["input_text"] = "same" }))
            .RootElement.GetProperty("hash").GetString();

        a.Should().NotBe(b, "each hash must use a fresh random salt");
    }

    [Fact]
    public async Task BcryptVerify_LegacyFakeBcryptFormat_IsRejected()
    {
        var tool = new EncodingTool();
        await tool.InitializeAsync();

        var json = await RunAsync(tool, new()
        {
            ["operation"] = "bcrypt_verify",
            ["input_text"] = "whatever",
            ["compare_value"] = "$2a$10$abc:hash:deadbeef"
        });
        json.Should().Contain("\"is_valid\":false");
    }

    [Fact]
    public async Task PasswordGenerate_ProducesDistinctValues()
    {
        var tool = new EncodingTool();
        await tool.InitializeAsync();

        var seen = new HashSet<string>();
        for (var i = 0; i < 25; i++)
        {
            var json = await RunAsync(tool, new()
            {
                ["operation"] = "password_generate",
                ["input_text"] = "",
                ["length"] = 20,
                ["include_symbols"] = true,
                ["include_numbers"] = true
            });
            var pwd = JsonDocument.Parse(json).RootElement.GetProperty("password").GetString()!;
            seen.Add(pwd);
        }

        // A secure RNG must not repeat 20-char passwords across 25 draws.
        seen.Count.Should().Be(25);
    }
}
