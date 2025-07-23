namespace Andy.Tools.Library.FileSystem;

public partial class CopyFileTool
{
    private class CopyStatistics
    {
        public int FilesCopied { get; set; }
        public int DirectoriesCreated { get; set; }
        public long BytesCopied { get; set; }
        public int FilesSkipped { get; set; }
        public List<string> Errors { get; set; } = [];
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public TimeSpan OperationTime => DateTime.UtcNow - StartTime;
    }
}
