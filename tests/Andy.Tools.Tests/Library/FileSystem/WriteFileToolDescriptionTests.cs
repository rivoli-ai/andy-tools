using Andy.Tools.Library.FileSystem;
using Xunit;

namespace Andy.Tools.Tests.Library.FileSystem;

public class WriteFileToolDescriptionTests
{
    [Fact]
    public void WriteFileTool_Description_ClarifiesUsageIntent()
    {
        // Arrange
        var tool = new WriteFileTool();

        // Act
        var description = tool.Metadata.Description;

        // Assert
        Assert.Contains("ONLY when explicitly asked", description);
        Assert.Contains("save a file", description);
        Assert.Contains("display", description);
        Assert.Contains("output it directly", description);
    }
}
