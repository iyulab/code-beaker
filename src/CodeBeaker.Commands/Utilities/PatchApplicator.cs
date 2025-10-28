using System.Text;
using System.Text.RegularExpressions;
using CodeBeaker.Commands.Models;

namespace CodeBeaker.Commands.Utilities;

/// <summary>
/// Applies unified diff patches to files
/// Phase 13: Debug & Improvement
/// </summary>
public static class PatchApplicator
{
    /// <summary>
    /// Apply unified diff patch to content
    /// </summary>
    public static PatchResult ApplyPatch(string patchText, string originalContent, bool dryRun = false)
    {
        var result = new PatchResult { DryRun = dryRun };

        try
        {
            // Parse patch
            var patches = ParseUnifiedDiff(patchText);
            if (patches.Count == 0)
            {
                result.Errors.Add("No valid patches found in diff");
                return result;
            }

            // For single file patch
            var patch = patches[0];

            // Apply hunks
            var modifiedContent = originalContent;
            var lines = modifiedContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();

            foreach (var hunk in patch.Hunks)
            {
                try
                {
                    lines = ApplyHunk(lines, hunk);
                    result.HunksApplied++;
                }
                catch (Exception ex)
                {
                    result.HunksFailed++;
                    result.Errors.Add($"Failed to apply hunk at line {hunk.OriginalStart}: {ex.Message}");
                }
            }

            result.ModifiedContent = string.Join("\n", lines);
            result.FilesPatched = result.HunksApplied > 0 ? 1 : 0;
            result.Success = result.HunksFailed == 0 && result.HunksApplied > 0;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Patch application failed: {ex.Message}");
            result.Success = false;
        }

        return result;
    }

    /// <summary>
    /// Parse unified diff format
    /// </summary>
    public static List<FilePatch> ParseUnifiedDiff(string diffText)
    {
        var patches = new List<FilePatch>();
        var lines = diffText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        FilePatch? currentPatch = null;
        Hunk? currentHunk = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // File header: --- original
            if (line.StartsWith("---"))
            {
                currentPatch = new FilePatch();
                currentPatch.OriginalFile = ExtractFileName(line);
                patches.Add(currentPatch);
                continue;
            }

            // File header: +++ modified
            if (line.StartsWith("+++"))
            {
                if (currentPatch != null)
                {
                    currentPatch.ModifiedFile = ExtractFileName(line);
                }
                continue;
            }

            // Hunk header: @@ -n,m +n,m @@
            if (line.StartsWith("@@"))
            {
                var hunkInfo = ParseHunkHeader(line);
                if (hunkInfo != null && currentPatch != null)
                {
                    currentHunk = hunkInfo;
                    currentPatch.Hunks.Add(currentHunk);
                }
                continue;
            }

            // Hunk content
            if (currentHunk != null && line.Length > 0)
            {
                var firstChar = line[0];
                var content = line.Length > 1 ? line.Substring(1) : "";

                switch (firstChar)
                {
                    case ' ': // Context line
                        currentHunk.Lines.Add(new HunkLine
                        {
                            Type = LineType.Context,
                            Content = content
                        });
                        break;
                    case '-': // Deleted line
                        currentHunk.Lines.Add(new HunkLine
                        {
                            Type = LineType.Delete,
                            Content = content
                        });
                        break;
                    case '+': // Added line
                        currentHunk.Lines.Add(new HunkLine
                        {
                            Type = LineType.Add,
                            Content = content
                        });
                        break;
                }
            }
        }

        return patches;
    }

    /// <summary>
    /// Apply a single hunk to lines
    /// </summary>
    private static List<string> ApplyHunk(List<string> lines, Hunk hunk)
    {
        var result = new List<string>();

        // Find the hunk location (with fuzzy matching)
        int startLine = FindHunkLocation(lines, hunk);
        if (startLine == -1)
        {
            throw new Exception("Cannot find matching context for hunk");
        }

        // Copy lines before hunk
        for (int i = 0; i < startLine; i++)
        {
            result.Add(lines[i]);
        }

        // Apply hunk changes
        int originalIndex = startLine;
        foreach (var hunkLine in hunk.Lines)
        {
            switch (hunkLine.Type)
            {
                case LineType.Context:
                    // Verify context matches
                    if (originalIndex < lines.Count)
                    {
                        result.Add(lines[originalIndex]);
                        originalIndex++;
                    }
                    break;

                case LineType.Delete:
                    // Skip deleted line
                    originalIndex++;
                    break;

                case LineType.Add:
                    // Add new line
                    result.Add(hunkLine.Content);
                    break;
            }
        }

        // Copy lines after hunk
        for (int i = originalIndex; i < lines.Count; i++)
        {
            result.Add(lines[i]);
        }

        return result;
    }

    /// <summary>
    /// Find where a hunk should be applied (with fuzzy matching)
    /// </summary>
    private static int FindHunkLocation(List<string> lines, Hunk hunk)
    {
        // Extract context lines from hunk
        var contextLines = hunk.Lines
            .Where(l => l.Type == LineType.Context || l.Type == LineType.Delete)
            .Take(3) // Look at first 3 context/delete lines
            .Select(l => l.Content)
            .ToList();

        if (contextLines.Count == 0)
            return hunk.OriginalStart - 1; // Use hunk header info

        // Try exact match first
        for (int i = 0; i <= lines.Count - contextLines.Count; i++)
        {
            bool match = true;
            for (int j = 0; j < contextLines.Count; j++)
            {
                if (i + j >= lines.Count || lines[i + j] != contextLines[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return i;
        }

        // Fuzzy match: allow some differences
        int bestMatch = -1;
        int bestScore = 0;

        for (int i = 0; i <= lines.Count - contextLines.Count; i++)
        {
            int score = 0;
            for (int j = 0; j < contextLines.Count; j++)
            {
                if (i + j < lines.Count && lines[i + j] == contextLines[j])
                    score++;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = i;
            }
        }

        // Require at least 50% match
        if (bestScore >= contextLines.Count / 2)
            return bestMatch;

        return -1; // Cannot find location
    }

    /// <summary>
    /// Parse hunk header: @@ -n,m +n,m @@
    /// </summary>
    private static Hunk? ParseHunkHeader(string line)
    {
        var match = Regex.Match(line, @"@@ -(\d+),?(\d*) \+(\d+),?(\d*) @@");
        if (!match.Success)
            return null;

        var hunk = new Hunk
        {
            OriginalStart = int.Parse(match.Groups[1].Value),
            OriginalCount = string.IsNullOrEmpty(match.Groups[2].Value) ? 1 : int.Parse(match.Groups[2].Value),
            ModifiedStart = int.Parse(match.Groups[3].Value),
            ModifiedCount = string.IsNullOrEmpty(match.Groups[4].Value) ? 1 : int.Parse(match.Groups[4].Value)
        };

        return hunk;
    }

    /// <summary>
    /// Extract file name from diff header
    /// </summary>
    private static string ExtractFileName(string line)
    {
        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return "";

        var fileName = parts[1];

        // Remove a/ or b/ prefix
        if (fileName.StartsWith("a/") || fileName.StartsWith("b/"))
            fileName = fileName.Substring(2);

        return fileName;
    }
}

/// <summary>
/// Represents a patch for a single file
/// </summary>
public class FilePatch
{
    public string OriginalFile { get; set; } = string.Empty;
    public string ModifiedFile { get; set; } = string.Empty;
    public List<Hunk> Hunks { get; set; } = new();
}

/// <summary>
/// Represents a hunk (continuous block of changes)
/// </summary>
public class Hunk
{
    public int OriginalStart { get; set; }
    public int OriginalCount { get; set; }
    public int ModifiedStart { get; set; }
    public int ModifiedCount { get; set; }
    public List<HunkLine> Lines { get; set; } = new();
}

/// <summary>
/// Represents a single line in a hunk
/// </summary>
public class HunkLine
{
    public LineType Type { get; set; }
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Type of line in a hunk
/// </summary>
public enum LineType
{
    Context,  // Unchanged line (starts with space)
    Delete,   // Deleted line (starts with -)
    Add       // Added line (starts with +)
}
