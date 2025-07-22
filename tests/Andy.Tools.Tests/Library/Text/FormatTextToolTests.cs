using System.Text;
using System.Text.Json;
using Andy.Tools.Core;
using Andy.Tools.Library.Text;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Library.Text;

public class FormatTextToolTests : IDisposable
{
    private readonly FormatTextTool _tool;

    public FormatTextToolTests()
    {
        _tool = new FormatTextTool();
    }

    public void Dispose()
    {
        // No cleanup needed for this tool
    }

    #region Metadata Tests

    [Fact]
    public void Metadata_ShouldHaveCorrectValues()
    {
        // Act
        var metadata = _tool.Metadata;

        // Assert
        metadata.Should().NotBeNull();
        metadata.Id.Should().Be("format_text");
        metadata.Name.Should().Be("Format Text");
        metadata.Description.Should().Contain("format");
        metadata.Category.Should().Be(ToolCategory.TextProcessing);
        metadata.Parameters.Should().NotBeEmpty();
    }

    [Fact]
    public void Metadata_ShouldHaveRequiredParameters()
    {
        // Act
        var parameters = _tool.Metadata.Parameters;

        // Assert
        parameters.Should().Contain(p => p.Name == "input_text" && p.Required);
        parameters.Should().Contain(p => p.Name == "operation" && p.Required);
        parameters.Should().Contain(p => p.Name == "options" && !p.Required);

        var operationParam = parameters.First(p => p.Name == "operation");
        operationParam.AllowedValues.Should().NotBeNull();
        operationParam.AllowedValues!.Should().Contain("trim");
        operationParam.AllowedValues!.Should().Contain("upper");
        operationParam.AllowedValues!.Should().Contain("format_json");
    }

    #endregion

    #region Basic Text Operations

    [Fact]
    public async Task ExecuteAsync_TrimOperation_ShouldRemoveWhitespace()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "  Hello World  ",
            ["operation"] = "trim"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("Hello World");

        data.Should().ContainKey("operation");
        data!["operation"].Should().Be("trim");
    }

    [Fact]
    public async Task ExecuteAsync_UpperOperation_ShouldConvertToUppercase()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "Hello World",
            ["operation"] = "upper"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("HELLO WORLD");
    }

    [Fact]
    public async Task ExecuteAsync_LowerOperation_ShouldConvertToLowercase()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "Hello World",
            ["operation"] = "lower"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("hello world");
    }

    [Fact]
    public async Task ExecuteAsync_TitleOperation_ShouldConvertToTitleCase()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "hello world test",
            ["operation"] = "title"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("Hello World Test");
    }

    [Fact]
    public async Task ExecuteAsync_ReverseOperation_ShouldReverseText()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "Hello",
            ["operation"] = "reverse"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("olleH");
    }

    #endregion

    #region Case Conversion Tests

    [Fact]
    public async Task ExecuteAsync_CamelCaseOperation_ShouldConvertToCamelCase()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "hello world test",
            ["operation"] = "camel"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("helloWorldTest");
    }

    [Fact]
    public async Task ExecuteAsync_PascalCaseOperation_ShouldConvertToPascalCase()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "hello world test",
            ["operation"] = "pascal"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("HelloWorldTest");
    }

    [Fact]
    public async Task ExecuteAsync_SnakeCaseOperation_ShouldConvertToSnakeCase()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "Hello World Test",
            ["operation"] = "snake"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("hello_world_test");
    }

    [Fact]
    public async Task ExecuteAsync_KebabCaseOperation_ShouldConvertToKebabCase()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "Hello World Test",
            ["operation"] = "kebab"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("hello-world-test");
    }

    #endregion

    #region Line Operations

    [Fact]
    public async Task ExecuteAsync_SortLinesOperation_ShouldSortLinesAlphabetically()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "zebra\napple\nbanana",
            ["operation"] = "sort_lines"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("apple\nbanana\nzebra");
    }

    [Fact]
    public async Task ExecuteAsync_SortLinesDescending_ShouldSortInDescendingOrder()
    {
        // Arrange
        var options = new Dictionary<string, object?>
        {
            ["descending"] = true
        };

        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "apple\nbanana\nzebra",
            ["operation"] = "sort_lines",
            ["options"] = options
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("zebra\nbanana\napple");
    }

    [Fact]
    public async Task ExecuteAsync_RemoveDuplicateLines_ShouldRemoveDuplicates()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "apple\nbanana\napple\ncherry\nbanana",
            ["operation"] = "remove_duplicates"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        var lines = ((string)data!["content"]).Split('\n');
        lines.Should().Contain("apple");
        lines.Should().Contain("banana");
        lines.Should().Contain("cherry");
        lines.Where(l => l == "apple").Should().HaveCount(1);
        lines.Where(l => l == "banana").Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_RemoveEmptyLines_ShouldRemoveEmptyLines()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "apple\n\nbanana\n   \ncherry",
            ["operation"] = "remove_empty_lines"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("apple\nbanana\ncherry");
    }

    #endregion

    #region Text Formatting

    [Fact]
    public async Task ExecuteAsync_NormalizeWhitespace_ShouldNormalizeSpaces()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "  Hello    world   test  ",
            ["operation"] = "normalize_whitespace"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("Hello world test");
    }

    [Fact]
    public async Task ExecuteAsync_WordWrap_ShouldWrapText()
    {
        // Arrange
        var options = new Dictionary<string, object?>
        {
            ["width"] = 10
        };

        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "This is a long line that should be wrapped",
            ["operation"] = "word_wrap",
            ["options"] = options
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        var content = (string)data!["content"];
        content.Should().Contain("\n"); // Should contain line breaks

        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            (line.Length <= 10 || !line.Contains(' ')).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ExecuteAsync_IndentText_ShouldIndentLines()
    {
        // Arrange
        var options = new Dictionary<string, object?>
        {
            ["size"] = 2,
            ["use_spaces"] = true
        };

        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "line1\nline2\nline3",
            ["operation"] = "indent",
            ["options"] = options
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("  line1\n  line2\n  line3");
    }

    [Fact]
    public async Task ExecuteAsync_UnindentText_ShouldRemoveIndentation()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "    line1\n    line2\n        line3",
            ["operation"] = "unindent"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("line1\nline2\n    line3");
    }

    #endregion

    #region Encoding Operations

    [Fact]
    public async Task ExecuteAsync_EncodeBase64_ShouldEncodeText()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "Hello World",
            ["operation"] = "encode_base64"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be(Convert.ToBase64String(Encoding.UTF8.GetBytes("Hello World")));
    }

    [Fact]
    public async Task ExecuteAsync_DecodeBase64_ShouldDecodeText()
    {
        // Arrange
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("Hello World"));
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = base64,
            ["operation"] = "decode_base64"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("Hello World");
    }

    [Fact]
    public async Task ExecuteAsync_EncodeUrl_ShouldEncodeUrl()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "Hello World & Test",
            ["operation"] = "encode_url"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("Hello%20World%20%26%20Test");
    }

    [Fact]
    public async Task ExecuteAsync_DecodeUrl_ShouldDecodeUrl()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "Hello%20World%20%26%20Test",
            ["operation"] = "decode_url"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("Hello World & Test");
    }

    #endregion

    #region JSON Operations

    [Fact]
    public async Task ExecuteAsync_FormatJson_ShouldFormatJson()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "{\"name\":\"test\",\"value\":123}",
            ["operation"] = "format_json"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        var content = (string)data!["content"];
        content.Should().Contain("\"name\": \"test\"");
        content.Should().Contain("\"value\": 123");
    }

    [Fact]
    public async Task ExecuteAsync_MinifyJson_ShouldMinifyJson()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "{\n  \"name\": \"test\",\n  \"value\": 123\n}",
            ["operation"] = "minify_json"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("{\"name\":\"test\",\"value\":123}");
    }

    [Fact]
    public async Task ExecuteAsync_FormatXml_ShouldFormatXml()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "<root><item>test</item></root>",
            ["operation"] = "format_xml"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        var content = (string)data!["content"];
        content.Should().Contain("<root>");
        content.Should().Contain("  <item>test</item>");
        content.Should().Contain("</root>");
    }

    #endregion

    #region Extraction Operations

    [Fact]
    public async Task ExecuteAsync_ExtractNumbers_ShouldExtractNumbers()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "Price is $25.99 and quantity is 3 items. Total: 77.97",
            ["operation"] = "extract_numbers"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        var numbers = ((string)data!["content"]).Split('\n');
        numbers.Should().Contain("25.99");
        numbers.Should().Contain("3");
        numbers.Should().Contain("77.97");

        data.Should().ContainKey("numbers_found");
        data!["numbers_found"].Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_ExtractEmails_ShouldExtractEmails()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "Contact us at support@example.com or sales@test.org for help.",
            ["operation"] = "extract_emails"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        var emails = ((string)data!["content"]).Split('\n');
        emails.Should().Contain("support@example.com");
        emails.Should().Contain("sales@test.org");

        data.Should().ContainKey("emails_found");
        data!["emails_found"].Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ExtractUrls_ShouldExtractUrls()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "Visit https://example.com or http://test.org for more info.",
            ["operation"] = "extract_urls"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        var urls = ((string)data!["content"]).Split('\n');
        urls.Should().Contain("https://example.com");
        urls.Should().Contain("http://test.org");

        data.Should().ContainKey("urls_found");
        data!["urls_found"].Should().Be(2);
    }

    #endregion

    #region Counting Operations

    [Fact]
    public async Task ExecuteAsync_CountWords_ShouldCountWords()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "Hello world test hello",
            ["operation"] = "count_words"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        var content = (string)data!["content"];
        content.Should().Contain("Words: 4");
        content.Should().Contain("Unique words: 3");

        data.Should().ContainKey("word_count");
        data.Should().ContainKey("unique_words");
        data!["word_count"].Should().Be(4);
        data["unique_words"].Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_CountCharacters_ShouldCountCharacters()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "Hello, World! 123",
            ["operation"] = "count_chars"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        var content = (string)data!["content"];
        content.Should().Contain("Total characters: 17");
        content.Should().Contain("Letters: 10");
        content.Should().Contain("Digits: 3");

        data.Should().ContainKey("total_characters");
        data.Should().ContainKey("letters");
        data.Should().ContainKey("digits");
        data!["total_characters"].Should().Be(17);
        data["letters"].Should().Be(10);
        data["digits"].Should().Be(3);
    }

    #endregion

    #region Options Parsing Tests

    [Fact]
    public async Task ExecuteAsync_WithJsonStringOptions_ShouldParseOptions()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "apple\nbanana\nzebra",
            ["operation"] = "sort_lines",
            ["options"] = "{\"descending\": true}"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("zebra\nbanana\napple");
    }

    [Fact]
    public async Task ExecuteAsync_WithDictionaryOptions_ShouldUseOptions()
    {
        // Arrange
        var options = new Dictionary<string, object?>
        {
            ["width"] = 5
        };

        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "Hello World Test",
            ["operation"] = "word_wrap",
            ["options"] = options
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        var content = (string)data!["content"];
        content.Should().Contain("\n");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_InvalidOperation_ShouldReturnFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "test",
            ["operation"] = "invalid_operation"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Unknown operation");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidBase64_ShouldReturnFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "invalid base64!!!",
            ["operation"] = "decode_base64"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid base64");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJson_ShouldReturnFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "{invalid json",
            ["operation"] = "format_json"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("JSON parsing error");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidXml_ShouldReturnFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "<invalid><xml>",
            ["operation"] = "format_xml"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid XML");
    }

    [Fact]
    public async Task ExecuteAsync_MissingInputText_ShouldReturnFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["operation"] = "trim"
            // Missing input_text
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Match(msg => msg.Contains("input_text") || msg.Contains("required"));
    }

    [Fact]
    public async Task ExecuteAsync_MissingOperation_ShouldReturnFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "test"
            // Missing operation
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Match(msg => msg.Contains("operation") || msg.Contains("required"));
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldHandleCancellation()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "test",
            ["operation"] = "upper"
        };

        var cts = new CancellationTokenSource();
        var context = new ToolExecutionContext { CancellationToken = cts.Token };

        // Cancel immediately
        cts.Cancel();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        // Operation may complete before cancellation or be cancelled
        // Just ensure we don't throw an exception
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ExecuteAsync_EmptyString_ShouldHandleGracefully()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "",
            ["operation"] = "trim"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        data!["content"].Should().Be("");
    }

    [Fact]
    public async Task ExecuteAsync_VeryLongText_ShouldHandleGracefully()
    {
        // Arrange
        var longText = new string('A', 100000);
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = longText,
            ["operation"] = "upper"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        var content = (string)data!["content"];
        content.Should().HaveLength(100000);
        data!["content"].Should().Be(longText.ToUpperInvariant());
    }

    [Fact]
    public async Task ExecuteAsync_SpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "Hello 世界 🌍 测试",
            ["operation"] = "upper"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        var content = (string)data!["content"];
        content.Should().Contain("HELLO");
        content.Should().Contain("世界"); // Chinese characters don't have uppercase
        content.Should().Contain("🌍"); // Emoji unchanged
    }

    #endregion

    #region Metadata Verification

    [Fact]
    public async Task ExecuteAsync_ShouldIncludeOperationMetadata()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["input_text"] = "Hello World",
            ["operation"] = "upper"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var metadata = result.Data as Dictionary<string, object?>;
        metadata.Should().NotBeNull();
        metadata.Should().ContainKey("operation");
        metadata.Should().ContainKey("input_length");
        metadata.Should().ContainKey("output_length");
        metadata.Should().ContainKey("operation_time");

        metadata!["operation"].Should().Be("upper");
        metadata["input_length"].Should().Be(11);
        metadata["output_length"].Should().Be(11);
    }

    #endregion
}
