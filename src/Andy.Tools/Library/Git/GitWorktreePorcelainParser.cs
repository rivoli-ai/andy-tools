using System;
using System.Collections.Generic;

namespace Andy.Tools.Library.Git;

/// <summary>
/// Parses the output of <c>git worktree list --porcelain</c>.
/// </summary>
public static class GitWorktreePorcelainParser
{
    /// <summary>
    /// Parses porcelain worktree output into one dictionary per worktree.
    /// </summary>
    /// <param name="output">The raw stdout of <c>git worktree list --porcelain</c>.</param>
    /// <returns>A list of worktree entries with path, head, branch and state flags.</returns>
    public static List<Dictionary<string, object?>> Parse(string output)
    {
        var worktrees = new List<Dictionary<string, object?>>();
        if (string.IsNullOrWhiteSpace(output))
        {
            return worktrees;
        }

        Dictionary<string, object?>? current = null;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (line.Length == 0)
            {
                current = null;
                continue;
            }

            if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                current = NewEntry(line["worktree ".Length..]);
                // The first entry git prints is always the main worktree.
                current["is_main"] = worktrees.Count == 0;
                worktrees.Add(current);
                continue;
            }

            if (current == null)
            {
                continue;
            }

            if (line.StartsWith("HEAD ", StringComparison.Ordinal))
            {
                current["head"] = line["HEAD ".Length..];
            }
            else if (line.StartsWith("branch ", StringComparison.Ordinal))
            {
                var branchRef = line["branch ".Length..];
                const string headsPrefix = "refs/heads/";
                current["branch"] = branchRef.StartsWith(headsPrefix, StringComparison.Ordinal)
                    ? branchRef[headsPrefix.Length..]
                    : branchRef;
            }
            else if (line == "bare")
            {
                current["is_bare"] = true;
            }
            else if (line == "detached")
            {
                current["is_detached"] = true;
            }
            else if (line == "locked" || line.StartsWith("locked ", StringComparison.Ordinal))
            {
                current["is_locked"] = true;
                current["lock_reason"] = line.Length > "locked".Length ? line["locked ".Length..] : null;
            }
            else if (line == "prunable" || line.StartsWith("prunable ", StringComparison.Ordinal))
            {
                current["is_prunable"] = true;
                current["prune_reason"] = line.Length > "prunable".Length ? line["prunable ".Length..] : null;
            }
        }

        return worktrees;
    }

    private static Dictionary<string, object?> NewEntry(string path) => new()
    {
        ["path"] = path,
        ["head"] = null,
        ["branch"] = null,
        ["is_main"] = false,
        ["is_bare"] = false,
        ["is_detached"] = false,
        ["is_locked"] = false,
        ["lock_reason"] = null,
        ["is_prunable"] = false,
        ["prune_reason"] = null
    };
}
