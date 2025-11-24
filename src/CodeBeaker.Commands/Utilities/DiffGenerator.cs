using System.Text;

namespace CodeBeaker.Commands.Utilities;

/// <summary>
/// Generates unified diff output between two text contents
/// Phase 12: AI Agent Integration
/// </summary>
public static class DiffGenerator
{
    /// <summary>
    /// Generate unified diff between two text contents
    /// </summary>
    /// <param name="originalContent">Original text</param>
    /// <param name="modifiedContent">Modified text</param>
    /// <param name="originalFileName">Original file name for diff header</param>
    /// <param name="modifiedFileName">Modified file name for diff header</param>
    /// <param name="contextLines">Number of context lines</param>
    /// <returns>Unified diff text</returns>
    public static string GenerateUnifiedDiff(
        string originalContent,
        string modifiedContent,
        string originalFileName = "original",
        string modifiedFileName = "modified",
        int contextLines = 3)
    {
        var originalLines = SplitLines(originalContent);
        var modifiedLines = SplitLines(modifiedContent);

        // Check if files are identical
        if (originalContent == modifiedContent)
        {
            return string.Empty; // No differences
        }

        var diff = new StringBuilder();

        // Unified diff header
        diff.AppendLine($"--- {originalFileName}");
        diff.AppendLine($"+++ {modifiedFileName}");

        // Calculate diff using simple LCS-based algorithm
        var hunks = CalculateDiffHunks(originalLines, modifiedLines, contextLines);

        foreach (var hunk in hunks)
        {
            diff.AppendLine(hunk);
        }

        return diff.ToString();
    }

    /// <summary>
    /// Calculate diff statistics
    /// </summary>
    public static (int added, int removed, int modified) CalculateStats(
        string originalContent,
        string modifiedContent)
    {
        var originalLines = SplitLines(originalContent);
        var modifiedLines = SplitLines(modifiedContent);

        var lcs = CalculateLCS(originalLines, modifiedLines);

        int added = modifiedLines.Length - lcs.Count;
        int removed = originalLines.Length - lcs.Count;
        int modified = Math.Min(added, removed);

        return (added, removed, modified);
    }

    private static string[] SplitLines(string content)
    {
        if (string.IsNullOrEmpty(content))
            return Array.Empty<string>();

        return content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    }

    private static List<string> CalculateDiffHunks(
        string[] originalLines,
        string[] modifiedLines,
        int contextLines)
    {
        var hunks = new List<string>();
        var operations = CalculateDiffOperations(originalLines, modifiedLines);

        if (operations.Count == 0)
            return hunks;

        // Group operations into hunks with context
        var currentHunk = new List<DiffOperation>();
        int lastLineProcessed = -1;

        foreach (var op in operations)
        {
            // Check if we need to start a new hunk
            if (currentHunk.Count > 0 &&
                op.OriginalLineNumber > lastLineProcessed + contextLines * 2 + 1)
            {
                // Output current hunk
                hunks.Add(FormatHunk(currentHunk, originalLines, modifiedLines, contextLines));
                currentHunk.Clear();
            }

            currentHunk.Add(op);
            lastLineProcessed = Math.Max(lastLineProcessed,
                op.Type == DiffOperationType.Add ? op.ModifiedLineNumber : op.OriginalLineNumber);
        }

        // Output final hunk
        if (currentHunk.Count > 0)
        {
            hunks.Add(FormatHunk(currentHunk, originalLines, modifiedLines, contextLines));
        }

        return hunks;
    }

    private static string FormatHunk(
        List<DiffOperation> operations,
        string[] originalLines,
        string[] modifiedLines,
        int contextLines)
    {
        var hunk = new StringBuilder();

        // Determine hunk range
        int originalStart = operations[0].OriginalLineNumber;
        int modifiedStart = operations[0].ModifiedLineNumber;

        // Add context before
        int contextStart = Math.Max(0, originalStart - contextLines);
        originalStart = contextStart;
        modifiedStart = Math.Max(0, modifiedStart - contextLines);

        // Calculate hunk size
        int originalCount = 0;
        int modifiedCount = 0;

        foreach (var op in operations)
        {
            if (op.Type == DiffOperationType.Delete || op.Type == DiffOperationType.Equal)
                originalCount++;
            if (op.Type == DiffOperationType.Add || op.Type == DiffOperationType.Equal)
                modifiedCount++;
        }

        // Add context after
        int contextEnd = Math.Min(originalLines.Length, operations[^1].OriginalLineNumber + contextLines + 1);
        originalCount += contextEnd - operations[^1].OriginalLineNumber - 1;
        modifiedCount += contextEnd - operations[^1].OriginalLineNumber - 1;

        // Hunk header
        hunk.AppendLine($"@@ -{originalStart + 1},{originalCount} +{modifiedStart + 1},{modifiedCount} @@");

        // Add context before
        for (int i = contextStart; i < operations[0].OriginalLineNumber; i++)
        {
            if (i < originalLines.Length)
                hunk.AppendLine($" {originalLines[i]}");
        }

        // Add operations
        foreach (var op in operations)
        {
            switch (op.Type)
            {
                case DiffOperationType.Delete:
                    if (op.OriginalLineNumber < originalLines.Length)
                        hunk.AppendLine($"-{originalLines[op.OriginalLineNumber]}");
                    break;
                case DiffOperationType.Add:
                    if (op.ModifiedLineNumber < modifiedLines.Length)
                        hunk.AppendLine($"+{modifiedLines[op.ModifiedLineNumber]}");
                    break;
                case DiffOperationType.Equal:
                    if (op.OriginalLineNumber < originalLines.Length)
                        hunk.AppendLine($" {originalLines[op.OriginalLineNumber]}");
                    break;
            }
        }

        // Add context after
        int lastLine = operations[^1].OriginalLineNumber + 1;
        for (int i = lastLine; i < contextEnd; i++)
        {
            if (i < originalLines.Length)
                hunk.AppendLine($" {originalLines[i]}");
        }

        return hunk.ToString();
    }

    private static List<DiffOperation> CalculateDiffOperations(
        string[] originalLines,
        string[] modifiedLines)
    {
        var operations = new List<DiffOperation>();
        var lcs = CalculateLCS(originalLines, modifiedLines);

        int origIndex = 0;
        int modIndex = 0;
        int lcsIndex = 0;

        while (origIndex < originalLines.Length || modIndex < modifiedLines.Length)
        {
            if (lcsIndex < lcs.Count &&
                origIndex < originalLines.Length &&
                modIndex < modifiedLines.Length &&
                originalLines[origIndex] == lcs[lcsIndex] &&
                modifiedLines[modIndex] == lcs[lcsIndex])
            {
                // Equal line
                operations.Add(new DiffOperation
                {
                    Type = DiffOperationType.Equal,
                    OriginalLineNumber = origIndex,
                    ModifiedLineNumber = modIndex
                });
                origIndex++;
                modIndex++;
                lcsIndex++;
            }
            else if (origIndex < originalLines.Length &&
                     (lcsIndex >= lcs.Count || originalLines[origIndex] != lcs[lcsIndex]))
            {
                // Delete from original
                operations.Add(new DiffOperation
                {
                    Type = DiffOperationType.Delete,
                    OriginalLineNumber = origIndex,
                    ModifiedLineNumber = modIndex
                });
                origIndex++;
            }
            else if (modIndex < modifiedLines.Length)
            {
                // Add to modified
                operations.Add(new DiffOperation
                {
                    Type = DiffOperationType.Add,
                    OriginalLineNumber = origIndex,
                    ModifiedLineNumber = modIndex
                });
                modIndex++;
            }
        }

        return operations;
    }

    private static List<string> CalculateLCS(string[] original, string[] modified)
    {
        int m = original.Length;
        int n = modified.Length;

        // Dynamic programming table
        var dp = new int[m + 1, n + 1];

        // Fill DP table
        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (original[i - 1] == modified[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                }
                else
                {
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }
        }

        // Backtrack to find LCS
        var lcs = new List<string>();
        int oi = m, mi = n;

        while (oi > 0 && mi > 0)
        {
            if (original[oi - 1] == modified[mi - 1])
            {
                lcs.Insert(0, original[oi - 1]);
                oi--;
                mi--;
            }
            else if (dp[oi - 1, mi] > dp[oi, mi - 1])
            {
                oi--;
            }
            else
            {
                mi--;
            }
        }

        return lcs;
    }

    private class DiffOperation
    {
        public DiffOperationType Type { get; set; }
        public int OriginalLineNumber { get; set; }
        public int ModifiedLineNumber { get; set; }
    }

    private enum DiffOperationType
    {
        Equal,
        Delete,
        Add
    }
}
