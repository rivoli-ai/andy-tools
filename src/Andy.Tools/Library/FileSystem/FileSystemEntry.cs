namespace Andy.Tools.Library.FileSystem;

/// <summary>
/// Represents a file system entry returned by ListDirectoryTool.
/// </summary>
public class FileSystemEntry
{
    public string Name { get; set; } = "";
    public string? FullPath { get; set; }
    public string Type { get; set; } = "";
    public long? Size { get; set; }
    public string? SizeFormatted { get; set; }
    public string? Extension { get; set; }
    public DateTime? Created { get; set; }
    public DateTime? Modified { get; set; }
    public string? Attributes { get; set; }
    public bool? IsHidden { get; set; }
    public bool? IsReadonly { get; set; }
    public int? Depth { get; set; }
}
