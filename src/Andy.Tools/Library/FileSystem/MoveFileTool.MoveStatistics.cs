namespace Andy.Tools.Library.FileSystem;

public partial class MoveFileTool
{
    private class MoveStatistics
    {
        public string SourcePath { get; set; } = "";
        public string DestinationPath { get; set; } = "";
        public bool IsDirectory { get; set; }
        public int ItemsMoved { get; set; }
        public long BytesMoved { get; set; }
        public string? BackupPath { get; set; }
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public TimeSpan OperationTime => DateTime.UtcNow - StartTime;
    }
}
