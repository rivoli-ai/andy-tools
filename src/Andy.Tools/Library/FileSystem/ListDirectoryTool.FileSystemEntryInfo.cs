namespace Andy.Tools.Library.FileSystem;

public partial class ListDirectoryTool
{
    private class FileSystemEntryInfo
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsFile { get; set; }
        public long Size { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime ModifiedTime { get; set; }
        public FileAttributes Attributes { get; set; }
        public string Extension { get; set; } = "";
        public int Depth { get; set; }
    }
}
