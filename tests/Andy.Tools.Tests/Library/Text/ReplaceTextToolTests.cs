using System.IO;
using System.Collections;
using System.Linq;
using System.Text;
using Andy.Tools.Core;
using Andy.Tools.Library.Text;
using FluentAssertions;
using Xunit;

namespace Andy.Tools.Tests.Library.Text;

public class ReplaceTextToolTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFile;
    private readonly string _testFile2;
    private readonly string _subDirectory;
    private readonly ReplaceTextTool _tool;

    public ReplaceTextToolTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "ReplaceTextToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        _testFile = Path.Combine(_testDirectory, "test.txt");
        _testFile2 = Path.Combine(_testDirectory, "test2.txt");
        _subDirectory = Path.Combine(_testDirectory, "subdir");

        _tool = new ReplaceTextTool();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }


    private static int GetFilesModified(IList items)
    {
        int count = 0;
        foreach (var item in items)
        {
            // FileReplaceResult is a private class, so we need to use reflection
            var type = item?.GetType();
            if (type != null)
            {
                var modifiedProp = type.GetProperty("Modified");
                if (modifiedProp != null)
                {
                    var value = modifiedProp.GetValue(item);
                    if (value is bool isModified && isModified)
                    {
                        count++;
                    }
                }
            }
        }
        return count;
    }
    
    private static int GetTotalReplacements(IList items)
    {
        int total = 0;
        foreach (var item in items)
        {
            // FileReplaceResult is a private class, so we need to use reflection
            var type = item?.GetType();
            if (type != null)
            {
                var replacementCountProp = type.GetProperty("ReplacementCount");
                if (replacementCountProp != null)
                {
                    var value = replacementCountProp.GetValue(item);
                    if (value is int replacementCount)
                    {
                        total += replacementCount;
                    }
                }
            }
        }
        return total;
    }
    
    private static IList? GetSampleMatches(object fileResult)
    {
        // FileReplaceResult is a private class, so we need to use reflection
        var type = fileResult?.GetType();
        if (type != null)
        {
            var sampleMatchesProp = type.GetProperty("SampleMatches");
            if (sampleMatchesProp != null)
            {
                var value = sampleMatchesProp.GetValue(fileResult);
                return value as IList;
            }
        }
        return null;
    }

    #region Metadata Tests

    [Fact]
    public void Metadata_ShouldHaveCorrectValues()
    {
        // Act
        var metadata = _tool.Metadata;

        // Assert
        metadata.Should().NotBeNull();
        metadata.Id.Should().Be("replace_text");
        metadata.Name.Should().Be("Replace Text");
        metadata.Description.Should().ContainAny("replace", "Replace");
        metadata.Category.Should().Be(ToolCategory.TextProcessing);
        metadata.RequiredPermissions.Should().HaveFlag(ToolPermissionFlags.FileSystemRead);
        metadata.RequiredPermissions.Should().HaveFlag(ToolPermissionFlags.FileSystemWrite);
        metadata.Parameters.Should().NotBeEmpty();
    }

    [Fact]
    public void Metadata_ShouldHaveRequiredParameters()
    {
        // Act
        var parameters = _tool.Metadata.Parameters;

        // Assert
        parameters.Should().Contain(p => p.Name == "search_pattern" && p.Required);
        parameters.Should().Contain(p => p.Name == "replacement_text" && p.Required);
        parameters.Should().Contain(p => p.Name == "target_path" && !p.Required);
        parameters.Should().Contain(p => p.Name == "search_type" && !p.Required);

        var searchTypeParam = parameters.First(p => p.Name == "search_type");
        searchTypeParam.AllowedValues.Should().NotBeNull();
        searchTypeParam.AllowedValues!.Should().Contain("contains");
        searchTypeParam.AllowedValues!.Should().Contain("regex");
        searchTypeParam.AllowedValues!.Should().Contain("exact");
    }

    #endregion

    #region Single File Tests

    [Fact]
    public async Task ExecuteAsync_ReplaceInSingleFile_ShouldReplaceText()
    {
        // Arrange
        const string content = "Hello world. This is a test world.";
        await File.WriteAllTextAsync(_testFile, content);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "universe",
            ["target_path"] = _testFile
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var newContent = await File.ReadAllTextAsync(_testFile);
        newContent.Should().Be("Hello universe. This is a test universe.");

        var data = result.Data as Dictionary<string, object?>;
        // data.Should().ContainKey("files_modified"); - Now calculated from items
        // data.Should().ContainKey("total_replacements"); - Now calculated from items
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        var filesModified = GetFilesModified(items!);
        filesModified.Should().Be(1);
        var totalReplacements = GetTotalReplacements(items!);
        totalReplacements.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ExactMatch_ShouldReplaceExactMatches()
    {
        // Arrange
        const string content = "Hello world. worldly affairs.";
        await File.WriteAllTextAsync(_testFile, content);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "universe",
            ["target_path"] = _testFile,
            ["search_type"] = "exact",
            ["whole_words_only"] = true
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var newContent = await File.ReadAllTextAsync(_testFile);
        newContent.Should().Be("Hello universe. worldly affairs."); // Only exact "world", not "worldly"

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        
        // When processing empty file or no matches, items list should exist but be empty
        data.Should().ContainKey("items");
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        var totalReplacements = GetTotalReplacements(items!);
        totalReplacements.Should().Be(1); // Only exact "world", not "worldly"
    }

    [Fact]
    public async Task ExecuteAsync_CaseSensitive_ShouldRespectCase()
    {
        // Arrange
        const string content = "Hello World. hello world.";
        await File.WriteAllTextAsync(_testFile, content);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "hello",
            ["replacement_text"] = "hi",
            ["target_path"] = _testFile,
            ["case_sensitive"] = true
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var newContent = await File.ReadAllTextAsync(_testFile);
        newContent.Should().Be("Hello World. hi world."); // Only lowercase "hello" replaced

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        
        // When processing empty file or no matches, items list should exist but be empty
        data.Should().ContainKey("items");
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        var totalReplacements = GetTotalReplacements(items!);
        totalReplacements.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WholeWordsOnly_ShouldReplaceWholeWords()
    {
        // Arrange
        const string content = "cat catch category catastrophe";
        await File.WriteAllTextAsync(_testFile, content);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "cat",
            ["replacement_text"] = "dog",
            ["target_path"] = _testFile,
            ["whole_words_only"] = true
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var newContent = await File.ReadAllTextAsync(_testFile);
        newContent.Should().Be("dog catch category catastrophe"); // Only standalone "cat" replaced

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        
        // When processing empty file or no matches, items list should exist but be empty
        data.Should().ContainKey("items");
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        var totalReplacements = GetTotalReplacements(items!);
        totalReplacements.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_RegexPattern_ShouldUseRegex()
    {
        // Arrange
        const string content = "Phone: 123-456-7890 and 987-654-3210";
        await File.WriteAllTextAsync(_testFile, content);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = @"\d{3}-\d{3}-\d{4}",
            ["replacement_text"] = "[PHONE]",
            ["target_path"] = _testFile,
            ["search_type"] = "regex"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var newContent = await File.ReadAllTextAsync(_testFile);
        newContent.Should().Be("Phone: [PHONE] and [PHONE]");

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        
        // When processing empty file or no matches, items list should exist but be empty
        data.Should().ContainKey("items");
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        var totalReplacements = GetTotalReplacements(items!);
        totalReplacements.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_StartsWithPattern_ShouldReplaceAtStart()
    {
        // Arrange
        const string content = "Hello world.\nGoodbye world.";
        await File.WriteAllTextAsync(_testFile, content);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "Hello",
            ["replacement_text"] = "Hi",
            ["target_path"] = _testFile,
            ["search_type"] = "starts_with"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var newContent = await File.ReadAllTextAsync(_testFile);
        newContent.Should().Be("Hi world.\nGoodbye world.");

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        
        // When processing empty file or no matches, items list should exist but be empty
        data.Should().ContainKey("items");
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        var totalReplacements = GetTotalReplacements(items!);
        totalReplacements.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_EndsWithPattern_ShouldReplaceAtEnd()
    {
        // Arrange
        const string content = "Hello world.";
        await File.WriteAllTextAsync(_testFile, content);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world.",
            ["replacement_text"] = "universe!",
            ["target_path"] = _testFile,
            ["search_type"] = "ends_with"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var newContent = await File.ReadAllTextAsync(_testFile);
        newContent.Should().Be("Hello universe!");

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        
        // When processing empty file or no matches, items list should exist but be empty
        data.Should().ContainKey("items");
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        var totalReplacements = GetTotalReplacements(items!);
        totalReplacements.Should().Be(1);
    }

    #endregion

    #region Backup Tests

    [Fact]
    public async Task ExecuteAsync_WithBackup_ShouldCreateBackupFile()
    {
        // Arrange
        const string content = "Hello world";
        await File.WriteAllTextAsync(_testFile, content);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "universe",
            ["target_path"] = _testFile,
            ["create_backup"] = true
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        // Check that backup was created
        var backupFiles = Directory.GetFiles(_testDirectory, "*.backup.*");
        backupFiles.Should().HaveCount(1);

        var backupContent = await File.ReadAllTextAsync(backupFiles[0]);
        backupContent.Should().Be(content); // Original content in backup

        var newContent = await File.ReadAllTextAsync(_testFile);
        newContent.Should().Be("Hello universe"); // Modified content in original
    }

    [Fact]
    public async Task ExecuteAsync_WithoutBackup_ShouldNotCreateBackupFile()
    {
        // Arrange
        const string content = "Hello world";
        await File.WriteAllTextAsync(_testFile, content);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "universe",
            ["target_path"] = _testFile,
            ["create_backup"] = false
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        // Check that no backup was created
        var backupFiles = Directory.GetFiles(_testDirectory, "*.backup.*");
        backupFiles.Should().BeEmpty();
    }

    #endregion

    #region Dry Run Tests

    [Fact]
    public async Task ExecuteAsync_DryRun_ShouldNotModifyFiles()
    {
        // Arrange
        const string content = "Hello world";
        await File.WriteAllTextAsync(_testFile, content);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "universe",
            ["target_path"] = _testFile,
            ["dry_run"] = true
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.Message.Should().Contain("Dry run: Would replace");

        // File should remain unchanged
        var fileContent = await File.ReadAllTextAsync(_testFile);
        fileContent.Should().Be(content);

        var data = result.Data as Dictionary<string, object?>;
        data.Should().NotBeNull();
        
        // When processing empty file or no matches, items list should exist but be empty
        data.Should().ContainKey("items");
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        var totalReplacements = GetTotalReplacements(items!);
        totalReplacements.Should().Be(1); // Still reports what would be replaced
        var filesModified = GetFilesModified(items!);
        filesModified.Should().Be(0); // In dry run, Modified is false
    }

    #endregion

    #region Directory Processing Tests

    [Fact]
    public async Task ExecuteAsync_ProcessDirectory_ShouldProcessAllFiles()
    {
        // Arrange
        Directory.CreateDirectory(_subDirectory);

        await File.WriteAllTextAsync(_testFile, "Hello world 1");
        await File.WriteAllTextAsync(_testFile2, "Hello world 2");
        await File.WriteAllTextAsync(Path.Combine(_subDirectory, "test3.txt"), "Hello world 3");

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "universe",
            ["target_path"] = _testDirectory,
            ["recursive"] = true
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
        
        // When processing empty file or no matches, items list should exist but be empty
        data.Should().ContainKey("items");
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        var filesModified = GetFilesModified(items!);
        filesModified.Should().Be(3);
        var totalReplacements = GetTotalReplacements(items!);
        totalReplacements.Should().Be(3);

        // Check all files were modified
        (await File.ReadAllTextAsync(_testFile)).Should().Be("Hello universe 1");
        (await File.ReadAllTextAsync(_testFile2)).Should().Be("Hello universe 2");
        (await File.ReadAllTextAsync(Path.Combine(_subDirectory, "test3.txt"))).Should().Be("Hello universe 3");
    }

    [Fact]
    public async Task ExecuteAsync_NonRecursive_ShouldProcessOnlyTopLevel()
    {
        // Arrange
        Directory.CreateDirectory(_subDirectory);

        await File.WriteAllTextAsync(_testFile, "Hello world 1");
        await File.WriteAllTextAsync(Path.Combine(_subDirectory, "test3.txt"), "Hello world 3");

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "universe",
            ["target_path"] = _testDirectory,
            ["recursive"] = false
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
        
        // When processing empty file or no matches, items list should exist but be empty
        data.Should().ContainKey("items");
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        var filesModified = GetFilesModified(items!);
        filesModified.Should().Be(1); // Only top-level file

        // Check only top-level file was modified
        (await File.ReadAllTextAsync(_testFile)).Should().Be("Hello universe 1");
        (await File.ReadAllTextAsync(Path.Combine(_subDirectory, "test3.txt"))).Should().Be("Hello world 3");
    }

    [Fact]
    public async Task ExecuteAsync_WithFilePatterns_ShouldFilterByPattern()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFile, "Hello world"); // .txt file
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "test.log"), "Hello world"); // .log file

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "universe",
            ["target_path"] = _testDirectory,
            ["file_patterns"] = new List<string> { "*.txt" }
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
        
        // When processing empty file or no matches, items list should exist but be empty
        data.Should().ContainKey("items");
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        var filesModified = GetFilesModified(items!);
        filesModified.Should().Be(1); // Only .txt file

        // Check only .txt file was modified
        (await File.ReadAllTextAsync(_testFile)).Should().Be("Hello universe");
        (await File.ReadAllTextAsync(Path.Combine(_testDirectory, "test.log"))).Should().Be("Hello world");
    }

    [Fact]
    public async Task ExecuteAsync_WithExcludePatterns_ShouldExcludeFiles()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFile, "Hello world"); // .txt file
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "test.log"), "Hello world"); // .log file

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "universe",
            ["target_path"] = _testDirectory,
            ["exclude_patterns"] = new List<string> { "*.log" }
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
        
        // When processing empty file or no matches, items list should exist but be empty
        data.Should().ContainKey("items");
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        var filesModified = GetFilesModified(items!);
        filesModified.Should().Be(1); // Only .txt file (log excluded)

        // Check only .txt file was modified
        (await File.ReadAllTextAsync(_testFile)).Should().Be("Hello universe");
        (await File.ReadAllTextAsync(Path.Combine(_testDirectory, "test.log"))).Should().Be("Hello world");
    }

    #endregion

    #region Encoding Tests

    [Fact]
    public async Task ExecuteAsync_WithSpecificEncoding_ShouldUseEncoding()
    {
        // Arrange
        const string content = "Hello wörld with ümlauts";
        await File.WriteAllTextAsync(_testFile, content, Encoding.UTF8);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "wörld",
            ["replacement_text"] = "unïverse",
            ["target_path"] = _testFile,
            ["encoding"] = "utf-8"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        var newContent = await File.ReadAllTextAsync(_testFile, Encoding.UTF8);
        newContent.Should().Be("Hello unïverse with ümlauts");
    }

    #endregion

    #region Size Limits Tests

    [Fact]
    public async Task ExecuteAsync_FileTooLarge_ShouldSkipFile()
    {
        // Arrange
        // Create a file larger than 0.1 MB (need more than 104,857 bytes)
        var largeContent = new string('A', 53000) + " world " + new string('B', 53000);
        await File.WriteAllTextAsync(_testFile, largeContent);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "universe",
            ["target_path"] = _testFile,
            ["max_file_size_mb"] = 0.1 // Minimum allowed limit
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
        
        // When processing empty file or no matches, items list should exist but be empty
        data.Should().ContainKey("items");
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        var filesModified = GetFilesModified(items!);
        filesModified.Should().Be(0); // File skipped due to size
        // Errors are now part of individual file results

        // File should remain unchanged
        var fileContent = await File.ReadAllTextAsync(_testFile);
        fileContent.Should().Be(largeContent);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ShouldReturnFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "universe",
            ["target_path"] = Path.Combine(_testDirectory, "nonexistent.txt")
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not exist");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRegex_ShouldReturnFailure()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFile, "Hello world");

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "[invalid regex",
            ["replacement_text"] = "universe",
            ["target_path"] = _testFile,
            ["search_type"] = "regex"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid pattern");
    }

    [Fact]
    public async Task ExecuteAsync_MissingSearchPattern_ShouldReturnFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["replacement_text"] = "universe",
            ["target_path"] = _testFile
            // Missing search_pattern
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Match(msg => msg.Contains("search_pattern") || msg.Contains("required"));
    }

    [Fact]
    public async Task ExecuteAsync_MissingReplacementText_ShouldReturnFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["target_path"] = _testFile
            // Missing replacement_text
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Match(msg => msg.Contains("replacement_text") || msg.Contains("required"));
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedEncoding_ShouldReturnFailure()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFile, "Hello world");

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "universe",
            ["target_path"] = _testFile,
            ["encoding"] = "unsupported-encoding"
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Parameter 'encoding' must be one of");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldHandleCancellation()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFile, "Hello world");

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "universe",
            ["target_path"] = _testFile
        };

        var cts = new CancellationTokenSource();
        var context = new ToolExecutionContext { CancellationToken = cts.Token };

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();
        
        cts.Cancel();
        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        // Operation may complete before cancellation or be cancelled
        // Just ensure we don't throw an exception
    }

    #endregion

    #region Binary File Tests

    [Fact]
    public async Task ExecuteAsync_BinaryFile_ShouldSkipBinaryFile()
    {
        // Arrange
        var binaryContent = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x00, 0xFF }; // Contains null bytes
        var binaryFile = Path.Combine(_testDirectory, "binary.bin");
        await File.WriteAllBytesAsync(binaryFile, binaryContent);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "test",
            ["replacement_text"] = "replaced",
            ["target_path"] = _testDirectory
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
        
        // When processing empty file or no matches, items list should exist but be empty
        data.Should().ContainKey("items");
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        var filesModified = GetFilesModified(items!);
        filesModified.Should().Be(0); // Binary file skipped
        // Errors are now part of individual file results

        // Binary file should remain unchanged
        var fileContent = await File.ReadAllBytesAsync(binaryFile);
        fileContent.Should().BeEquivalentTo(binaryContent);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ExecuteAsync_EmptyFile_ShouldHandleGracefully()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFile, "");

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "universe",
            ["target_path"] = _testFile
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
        
        // When processing empty file or no matches, items list should exist but be empty
        data.Should().ContainKey("items");
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        var filesModified = GetFilesModified(items!);
        filesModified.Should().Be(0); // No matches in empty file
        var totalReplacements = GetTotalReplacements(items!);
        totalReplacements.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_NoMatches_ShouldReportZeroReplacements()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFile, "Hello universe");

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "planet",
            ["target_path"] = _testFile
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
        
        // When processing empty file or no matches, items list should exist but be empty
        data.Should().ContainKey("items");
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        var filesModified = GetFilesModified(items!);
        filesModified.Should().Be(0);
        var totalReplacements = GetTotalReplacements(items!);
        totalReplacements.Should().Be(0);

        // File should remain unchanged
        var fileContent = await File.ReadAllTextAsync(_testFile);
        fileContent.Should().Be("Hello universe");
    }

    [Fact]
    public async Task ExecuteAsync_EmptySearchPattern_ShouldReturnFailure()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFile, "Hello world");

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "",
            ["replacement_text"] = "universe",
            ["target_path"] = _testFile
        };

        var context = new ToolExecutionContext();

        // Act
        // Initialize the tool
        await _tool.InitializeAsync();

        var result = await _tool.ExecuteAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Match(msg => msg.Contains("search_pattern") || msg.Contains("required"));
    }

    #endregion

    #region Sample Matches Tests

    [Fact]
    public async Task ExecuteAsync_ShouldProvideSampleMatches()
    {
        // Arrange
        const string content = "world world world world world world"; // Many matches
        await File.WriteAllTextAsync(_testFile, content);

        var parameters = new Dictionary<string, object?>
        {
            ["search_pattern"] = "world",
            ["replacement_text"] = "universe",
            ["target_path"] = _testFile
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
        
        // When processing empty file or no matches, items list should exist but be empty
        data.Should().ContainKey("items");
        var items = data!["items"] as IList;
        items.Should().NotBeNull();
        items!.Count.Should().Be(1); // One file processed
        
        var fileResult = items![0];
        fileResult.Should().NotBeNull();
        var sampleMatches = GetSampleMatches(fileResult!);
        sampleMatches.Should().NotBeNull();

        // Sample matches should be limited (typically to 5)
        sampleMatches!.Count.Should().BeLessOrEqualTo(5);
    }

    #endregion
}
