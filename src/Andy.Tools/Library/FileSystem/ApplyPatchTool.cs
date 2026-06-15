using System.Text;
using Andy.Tools.Core;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Library.FileSystem;

/// <summary>
/// Tool that applies a structured, multi-hunk patch across one or more files atomically.
/// </summary>
/// <remarks>
/// <para>The patch uses an OpenAI-style envelope that is easy to parse and LLM-friendly:</para>
/// <code>
/// *** Begin Patch
/// *** Update File: relative/path.cs
/// @@
///  context line
/// -removed line
/// +added line
/// *** Add File: new/file.txt
/// +line 1
/// +line 2
/// *** Delete File: old/path.txt
/// *** End Patch
/// </code>
/// <para>
/// Operations: <c>Update File</c> applies one or more hunks (each introduced by a <c>@@</c> marker).
/// A hunk is made of context lines (leading space), removed lines (<c>-</c>) and added lines (<c>+</c>).
/// Each hunk is located by matching its context+removed lines exactly against the current file content;
/// if it does not match, that file fails. <c>Add File</c> creates a new file from the <c>+</c> lines and
/// fails if it already exists. <c>Delete File</c> removes a file and fails if it is missing.
/// </para>
/// <para>
/// The operation is atomic: every operation is validated against the current on-disk state first, and
/// if ANY operation would fail nothing is written and the failing file/hunk is reported.
/// </para>
/// </remarks>
public class ApplyPatchTool : ToolBase
{
    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "apply_patch",
        Name = "Apply Patch",
        Description =
            "Applies a structured multi-hunk patch across one or more files atomically. "
            + "If any operation does not apply cleanly, nothing is changed. "
            + "Patch format (OpenAI-style envelope):\n"
            + "*** Begin Patch\n"
            + "*** Update File: relative/path.cs\n"
            + "@@\n"
            + " context line (leading space)\n"
            + "-removed line\n"
            + "+added line\n"
            + "*** Add File: new/file.txt\n"
            + "+line 1\n"
            + "+line 2\n"
            + "*** Delete File: old/path.txt\n"
            + "*** End Patch\n"
            + "Update File applies hunks (each introduced by '@@'); a hunk's context and removed lines "
            + "must match the current file exactly or that file fails. Add File creates a file (fails if it "
            + "exists). Delete File removes a file (fails if missing). All paths are relative to the working "
            + "directory.",
        Version = "1.0.0",
        Category = ToolCategory.FileSystem,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead | ToolPermissionFlags.FileSystemWrite,
        Parameters =
        [
            new()
            {
                Name = "patch",
                Description = "The patch text in the OpenAI-style envelope (between '*** Begin Patch' and '*** End Patch').",
                Type = "string",
                Required = true
            }
        ]
    };

    /// <inheritdoc />
    protected override Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        var patch = GetParameter<string>(parameters, "patch");

        if (string.IsNullOrWhiteSpace(patch))
        {
            return Task.FromResult(ToolResults.InvalidParameter("patch", patch, "Patch text cannot be empty"));
        }

        List<PatchOperation> operations;
        try
        {
            operations = PatchParser.Parse(patch);
        }
        catch (PatchFormatException ex)
        {
            return Task.FromResult(ToolResults.Failure(ex.Message, "PATCH_PARSE_ERROR"));
        }

        if (operations.Count == 0)
        {
            return Task.FromResult(ToolResults.Failure("Patch contains no operations", "PATCH_EMPTY"));
        }

        ReportProgress(context, "Validating patch operations...", 10);

        // Phase 1: validate every operation against the current state. Build the plan but write nothing.
        var plan = new List<PlannedWrite>();
        foreach (var op in operations)
        {
            string safePath;
            try
            {
                safePath = ToolHelpers.GetSafePath(op.Path, context.WorkingDirectory);
            }
            catch (ArgumentException ex)
            {
                return Task.FromResult(ToolResults.Failure(
                    $"Invalid path '{op.Path}': {ex.Message}", "INVALID_PATH"));
            }

            if (!ToolHelpers.IsPathWithinAllowedPaths(safePath, context.Permissions))
            {
                return Task.FromResult(ToolResults.Failure(
                    $"Path '{op.Path}' is not within allowed paths", "PATH_NOT_ALLOWED"));
            }

            switch (op.Kind)
            {
                case PatchOperationKind.Add:
                    if (File.Exists(safePath))
                    {
                        return Task.FromResult(ToolResults.Failure(
                            $"Cannot add file '{op.Path}': it already exists", "FILE_EXISTS"));
                    }

                    plan.Add(new PlannedWrite(PlannedWriteKind.Create, safePath, op.Path, op.AddedContent!, ToolHelpers.Utf8NoBom));
                    break;

                case PatchOperationKind.Delete:
                    if (!File.Exists(safePath))
                    {
                        return Task.FromResult(ToolResults.Failure(
                            $"Cannot delete file '{op.Path}': it does not exist", "FILE_NOT_FOUND"));
                    }

                    plan.Add(new PlannedWrite(PlannedWriteKind.Delete, safePath, op.Path, Content: null, Encoding: null));
                    break;

                case PatchOperationKind.Update:
                    if (!File.Exists(safePath))
                    {
                        return Task.FromResult(ToolResults.Failure(
                            $"Cannot update file '{op.Path}': it does not exist", "FILE_NOT_FOUND"));
                    }

                    var encoding = ToolHelpers.DetectEncoding(safePath);
                    string original;
                    try
                    {
                        original = File.ReadAllText(safePath, encoding);
                    }
                    catch (Exception ex)
                    {
                        return Task.FromResult(ToolResults.Failure(
                            $"Failed to read file '{op.Path}': {ex.Message}", "READ_ERROR", details: ex));
                    }

                    if (!HunkApplier.TryApply(original, op.Hunks, out var updated, out var hunkError))
                    {
                        return Task.FromResult(ToolResults.Failure(
                            $"Patch does not apply to '{op.Path}': {hunkError}", "HUNK_MISMATCH"));
                    }

                    plan.Add(new PlannedWrite(PlannedWriteKind.Update, safePath, op.Path, updated, encoding));
                    break;

                default:
                    return Task.FromResult(ToolResults.Failure(
                        $"Unknown operation for '{op.Path}'", "PATCH_PARSE_ERROR"));
            }
        }

        ReportProgress(context, "Applying patch...", 60);

        // Phase 2: apply. Back up files we modify/delete; roll back on any unexpected IO failure.
        var changed = new List<string>();
        var added = new List<string>();
        var deleted = new List<string>();
        var backups = new List<(string Backup, string Original)>();
        var createdFiles = new List<string>();

        try
        {
            foreach (var write in plan)
            {
                switch (write.Kind)
                {
                    case PlannedWriteKind.Create:
                        var dir = Path.GetDirectoryName(write.SafePath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        File.WriteAllText(write.SafePath, write.Content!, write.Encoding!);
                        createdFiles.Add(write.SafePath);
                        added.Add(write.RelativePath);
                        break;

                    case PlannedWriteKind.Update:
                        var updateBackup = ToolHelpers.GetBackupPath(write.SafePath);
                        File.Copy(write.SafePath, updateBackup, true);
                        backups.Add((updateBackup, write.SafePath));
                        File.WriteAllText(write.SafePath, write.Content!, write.Encoding!);
                        changed.Add(write.RelativePath);
                        break;

                    case PlannedWriteKind.Delete:
                        var deleteBackup = ToolHelpers.GetBackupPath(write.SafePath);
                        File.Copy(write.SafePath, deleteBackup, true);
                        backups.Add((deleteBackup, write.SafePath));
                        File.Delete(write.SafePath);
                        deleted.Add(write.RelativePath);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            // Best-effort rollback so the operation stays atomic even on mid-apply IO failure.
            foreach (var created in createdFiles)
            {
                TryDelete(created);
            }

            foreach (var (backup, originalPath) in backups)
            {
                TryRestore(backup, originalPath);
            }

            return Task.FromResult(ToolResults.Failure(
                $"Failed to apply patch: {ex.Message}", "APPLY_ERROR", details: ex));
        }

        ReportProgress(context, "Patch applied", 100);

        var metadata = new Dictionary<string, object?>
        {
            ["files_changed"] = changed,
            ["files_added"] = added,
            ["files_deleted"] = deleted,
            ["operation_count"] = plan.Count
        };

        var summary = $"Applied patch: {changed.Count} changed, {added.Count} added, {deleted.Count} deleted";
        return Task.FromResult(ToolResults.Success(metadata, summary, metadata));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort during rollback.
        }
    }

    private static void TryRestore(string backupPath, string originalPath)
    {
        try
        {
            File.Copy(backupPath, originalPath, true);
        }
        catch
        {
            // Best effort during rollback.
        }
    }

    private enum PlannedWriteKind
    {
        Create,
        Update,
        Delete
    }

    private sealed record PlannedWrite(
        PlannedWriteKind Kind,
        string SafePath,
        string RelativePath,
        string? Content,
        Encoding? Encoding);

    private enum PatchOperationKind
    {
        Add,
        Update,
        Delete
    }

    /// <summary>A single hunk: context/removed lines to match, and the resulting lines.</summary>
    private sealed class Hunk
    {
        /// <summary>Lines to match in the source (context + removed), in order.</summary>
        public List<string> Before { get; } = [];

        /// <summary>Lines that replace the matched region (context + added), in order.</summary>
        public List<string> After { get; } = [];
    }

    private sealed class PatchOperation
    {
        public PatchOperationKind Kind { get; init; }

        public string Path { get; init; } = "";

        public List<Hunk> Hunks { get; } = [];

        /// <summary>Full content for an Add operation (already newline-joined).</summary>
        public string? AddedContent { get; set; }
    }

    private sealed class PatchFormatException(string message) : Exception(message);

    private static class PatchParser
    {
        private const string BeginPatch = "*** Begin Patch";
        private const string EndPatch = "*** End Patch";
        private const string UpdatePrefix = "*** Update File:";
        private const string AddPrefix = "*** Add File:";
        private const string DeletePrefix = "*** Delete File:";

        public static List<PatchOperation> Parse(string patch)
        {
            // Normalize newlines but remember nothing else; matching is done line-by-line.
            var lines = patch.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            var index = 0;

            // Skip leading blank lines up to the envelope.
            while (index < lines.Length && lines[index].Trim().Length == 0)
            {
                index++;
            }

            if (index >= lines.Length || lines[index].Trim() != BeginPatch)
            {
                throw new PatchFormatException($"Patch must start with '{BeginPatch}'");
            }

            index++; // consume Begin Patch

            var operations = new List<PatchOperation>();
            var sawEnd = false;

            while (index < lines.Length)
            {
                var line = lines[index];

                if (line.Trim() == EndPatch)
                {
                    sawEnd = true;
                    index++;
                    break;
                }

                if (line.StartsWith(AddPrefix, StringComparison.Ordinal))
                {
                    var path = line[AddPrefix.Length..].Trim();
                    if (path.Length == 0)
                    {
                        throw new PatchFormatException("Add File header is missing a path");
                    }

                    index++;
                    var addedLines = new List<string>();
                    while (index < lines.Length && !IsHeader(lines[index]))
                    {
                        var content = lines[index];
                        if (content.Length == 0)
                        {
                            // Allow a trailing blank line inside the file body.
                            addedLines.Add("");
                        }
                        else if (content[0] == '+')
                        {
                            addedLines.Add(content[1..]);
                        }
                        else
                        {
                            throw new PatchFormatException(
                                $"Add File '{path}' body lines must start with '+': '{content}'");
                        }

                        index++;
                    }

                    operations.Add(new PatchOperation
                    {
                        Kind = PatchOperationKind.Add,
                        Path = path,
                        AddedContent = string.Join("\n", addedLines)
                    });
                }
                else if (line.StartsWith(DeletePrefix, StringComparison.Ordinal))
                {
                    var path = line[DeletePrefix.Length..].Trim();
                    if (path.Length == 0)
                    {
                        throw new PatchFormatException("Delete File header is missing a path");
                    }

                    operations.Add(new PatchOperation
                    {
                        Kind = PatchOperationKind.Delete,
                        Path = path
                    });
                    index++;
                }
                else if (line.StartsWith(UpdatePrefix, StringComparison.Ordinal))
                {
                    var path = line[UpdatePrefix.Length..].Trim();
                    if (path.Length == 0)
                    {
                        throw new PatchFormatException("Update File header is missing a path");
                    }

                    index++;
                    var op = new PatchOperation
                    {
                        Kind = PatchOperationKind.Update,
                        Path = path
                    };

                    ParseUpdateHunks(lines, ref index, op, path);

                    if (op.Hunks.Count == 0)
                    {
                        throw new PatchFormatException($"Update File '{path}' has no hunks");
                    }

                    operations.Add(op);
                }
                else if (line.Trim().Length == 0)
                {
                    index++; // tolerate blank lines between operations
                }
                else
                {
                    throw new PatchFormatException($"Unexpected line in patch: '{line}'");
                }
            }

            if (!sawEnd)
            {
                throw new PatchFormatException($"Patch must end with '{EndPatch}'");
            }

            return operations;
        }

        private static void ParseUpdateHunks(string[] lines, ref int index, PatchOperation op, string path)
        {
            while (index < lines.Length && !IsHeader(lines[index]))
            {
                var line = lines[index];

                if (line.StartsWith("@@", StringComparison.Ordinal))
                {
                    index++;
                    var hunk = new Hunk();

                    while (index < lines.Length
                        && !IsHeader(lines[index])
                        && !lines[index].StartsWith("@@", StringComparison.Ordinal))
                    {
                        var hl = lines[index];

                        if (hl.Length == 0)
                        {
                            // A bare empty line is treated as an empty context line.
                            hunk.Before.Add("");
                            hunk.After.Add("");
                        }
                        else
                        {
                            var marker = hl[0];
                            var rest = hl[1..];
                            switch (marker)
                            {
                                case ' ':
                                    hunk.Before.Add(rest);
                                    hunk.After.Add(rest);
                                    break;
                                case '-':
                                    hunk.Before.Add(rest);
                                    break;
                                case '+':
                                    hunk.After.Add(rest);
                                    break;
                                default:
                                    throw new PatchFormatException(
                                        $"Update File '{path}' hunk line must start with ' ', '-' or '+': '{hl}'");
                            }
                        }

                        index++;
                    }

                    if (hunk.Before.Count == 0 && hunk.After.Count == 0)
                    {
                        throw new PatchFormatException($"Update File '{path}' has an empty hunk");
                    }

                    op.Hunks.Add(hunk);
                }
                else if (line.Trim().Length == 0)
                {
                    index++; // tolerate blank separator lines between hunks
                }
                else
                {
                    throw new PatchFormatException(
                        $"Update File '{path}' expected a '@@' hunk header but found: '{line}'");
                }
            }
        }

        private static bool IsHeader(string line)
        {
            return line.StartsWith(UpdatePrefix, StringComparison.Ordinal)
                || line.StartsWith(AddPrefix, StringComparison.Ordinal)
                || line.StartsWith(DeletePrefix, StringComparison.Ordinal)
                || line.Trim() == EndPatch
                || line.Trim() == BeginPatch;
        }
    }

    private static class HunkApplier
    {
        public static bool TryApply(string original, List<Hunk> hunks, out string result, out string error)
        {
            // Preserve the original trailing-newline state.
            var hadTrailingNewline = original.EndsWith('\n');
            var normalized = original.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = new List<string>(normalized.Split('\n'));

            // Splitting "a\nb\n" yields ["a","b",""]; drop that synthetic trailing empty entry so
            // line indices line up with real lines, and re-add the newline at the end.
            if (hadTrailingNewline && lines.Count > 0 && lines[^1].Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }

            // Search position advances so multiple hunks are applied in document order.
            var searchFrom = 0;

            foreach (var hunk in hunks)
            {
                var matchIndex = FindMatch(lines, hunk.Before, searchFrom);
                if (matchIndex < 0)
                {
                    error = hunk.Before.Count == 0
                        ? "could not locate hunk (no context lines)"
                        : $"context not found near: '{hunk.Before[0]}'";
                    result = original;
                    return false;
                }

                lines.RemoveRange(matchIndex, hunk.Before.Count);
                lines.InsertRange(matchIndex, hunk.After);
                searchFrom = matchIndex + hunk.After.Count;
            }

            var joined = string.Join("\n", lines);
            result = hadTrailingNewline && joined.Length > 0 ? joined + "\n" : joined;
            error = "";
            return true;
        }

        private static int FindMatch(List<string> lines, List<string> before, int searchFrom)
        {
            if (before.Count == 0)
            {
                return -1;
            }

            for (var i = searchFrom; i + before.Count <= lines.Count; i++)
            {
                var match = true;
                for (var j = 0; j < before.Count; j++)
                {
                    if (!string.Equals(lines[i + j], before[j], StringComparison.Ordinal))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
