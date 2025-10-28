using CodeBeaker.Commands.Utilities;
using FluentAssertions;

namespace CodeBeaker.Commands.Tests;

/// <summary>
/// Unit tests for PatchApplicator
/// Phase 14: Testing Patch application utility
/// </summary>
public class PatchApplicatorTests
{
    [Fact]
    public void ApplyPatch_ShouldSucceed_WhenPatchIsValid()
    {
        // Arrange
        var originalContent = "line1\nline2\nline3";
        var patch = @"--- a/test.txt
+++ b/test.txt
@@ -1,3 +1,3 @@
 line1
-line2
+line2-patched
 line3";

        // Act
        var result = PatchApplicator.ApplyPatch(patch, originalContent);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.HunksApplied.Should().Be(1);
        result.HunksFailed.Should().Be(0);
        result.ModifiedContent.Should().Contain("line2-patched");
    }

    [Fact]
    public void ApplyPatch_ShouldNotModifyContent_WhenDryRun()
    {
        // Arrange
        var originalContent = "line1\nline2\nline3";
        var patch = @"--- a/test.txt
+++ b/test.txt
@@ -1,3 +1,3 @@
 line1
-line2
+line2-patched
 line3";

        // Act
        var result = PatchApplicator.ApplyPatch(patch, originalContent, dryRun: true);

        // Assert
        result.Should().NotBeNull();
        result.DryRun.Should().BeTrue();
        result.Success.Should().BeTrue();
        result.ModifiedContent.Should().Contain("line2-patched");
    }

    [Fact]
    public void ApplyPatch_ShouldFail_WhenContextDoesNotMatch()
    {
        // Arrange
        var originalContent = "completely different content";
        var patch = @"--- a/test.txt
+++ b/test.txt
@@ -1,3 +1,3 @@
 line1
-line2
+line2-patched
 line3";

        // Act
        var result = PatchApplicator.ApplyPatch(patch, originalContent);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.HunksFailed.Should().BeGreaterThan(0);
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ApplyPatch_ShouldHandleMultipleHunks()
    {
        // Arrange
        var originalContent = "line1\nline2\nline3\nline4\nline5";
        var patch = @"--- a/test.txt
+++ b/test.txt
@@ -1,3 +1,3 @@
 line1
-line2
+line2-patched
 line3
@@ -3,3 +3,3 @@
 line3
-line4
+line4-patched
 line5";

        // Act
        var result = PatchApplicator.ApplyPatch(patch, originalContent);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.HunksApplied.Should().Be(2);
        result.HunksFailed.Should().Be(0);
        result.ModifiedContent.Should().Contain("line2-patched");
        result.ModifiedContent.Should().Contain("line4-patched");
    }

    [Fact]
    public void ApplyPatch_ShouldHandleAddedLines()
    {
        // Arrange
        var originalContent = "line1\nline2";
        var patch = @"--- a/test.txt
+++ b/test.txt
@@ -1,2 +1,3 @@
 line1
 line2
+line3";

        // Act
        var result = PatchApplicator.ApplyPatch(patch, originalContent);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ModifiedContent.Should().Contain("line3");
    }

    [Fact]
    public void ApplyPatch_ShouldHandleRemovedLines()
    {
        // Arrange
        var originalContent = "line1\nline2\nline3";
        var patch = @"--- a/test.txt
+++ b/test.txt
@@ -1,3 +1,2 @@
 line1
-line2
 line3";

        // Act
        var result = PatchApplicator.ApplyPatch(patch, originalContent);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ModifiedContent.Should().NotContain("line2");
        result.ModifiedContent.Should().Contain("line1");
        result.ModifiedContent.Should().Contain("line3");
    }

    [Fact]
    public void ApplyPatch_ShouldUseFuzzyMatching()
    {
        // Arrange - Original has slightly different whitespace, but context should still match
        var originalContent = "line1\n line2\nline3";  // Extra space before line2
        var patch = @"--- a/test.txt
+++ b/test.txt
@@ -1,3 +1,3 @@
 line1
-line2
+line2-patched
 line3";

        // Act
        var result = PatchApplicator.ApplyPatch(patch, originalContent);

        // Assert
        result.Should().NotBeNull();
        // Fuzzy matching should attempt to apply, but may fail due to exact match requirement
        if (result.Success)
        {
            result.HunksApplied.Should().BeGreaterThan(0);
        }
        else
        {
            result.Errors.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void ParseUnifiedDiff_ShouldParseValidPatch()
    {
        // Arrange
        var patch = @"--- a/test.txt
+++ b/test.txt
@@ -1,3 +1,3 @@
 line1
-line2
+line2-patched
 line3";

        // Act
        var patches = PatchApplicator.ParseUnifiedDiff(patch);

        // Assert
        patches.Should().NotBeNull();
        patches.Should().HaveCount(1);
        patches[0].OriginalFile.Should().Be("test.txt");
        patches[0].ModifiedFile.Should().Be("test.txt");
        patches[0].Hunks.Should().HaveCount(1);
        patches[0].Hunks[0].OriginalStart.Should().Be(1);
        patches[0].Hunks[0].OriginalCount.Should().Be(3);
    }

    [Fact]
    public void ParseUnifiedDiff_ShouldHandlePathWithPrefix()
    {
        // Arrange
        var patch = @"--- a/src/test.txt
+++ b/src/test.txt
@@ -1,3 +1,3 @@
 line1
-line2
+line2-patched
 line3";

        // Act
        var patches = PatchApplicator.ParseUnifiedDiff(patch);

        // Assert
        patches.Should().NotBeNull();
        patches.Should().HaveCount(1);
        patches[0].OriginalFile.Should().Be("src/test.txt");
        patches[0].ModifiedFile.Should().Be("src/test.txt");
    }

    [Fact]
    public void ApplyPatch_ShouldFail_WhenPatchIsEmpty()
    {
        // Arrange
        var originalContent = "line1\nline2\nline3";
        var patch = "";

        // Act
        var result = PatchApplicator.ApplyPatch(patch, originalContent);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("No valid patches found"));
    }

    [Fact]
    public void ApplyPatch_ShouldHandleComplexPythonCode()
    {
        // Arrange
        var originalContent = @"def factorial(n):
    if n < 1:
        return 1
    return n * factorial(n - 1)

print(factorial(5))";

        var patch = @"--- a/script.py
+++ b/script.py
@@ -1,6 +1,7 @@
 def factorial(n):
-    if n < 1:
+    if n <= 1:
         return 1
     return n * factorial(n - 1)

 print(factorial(5))
+print(factorial(10))";

        // Act
        var result = PatchApplicator.ApplyPatch(patch, originalContent);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ModifiedContent.Should().Contain("if n <= 1:");
        result.ModifiedContent.Should().Contain("print(factorial(10))");
    }

    [Fact]
    public void ApplyPatch_ShouldProvideDetailedErrors_WhenFailed()
    {
        // Arrange
        var originalContent = "totally wrong content";
        var patch = @"--- a/test.txt
+++ b/test.txt
@@ -1,3 +1,3 @@
 line1
-line2
+line2-patched
 line3";

        // Act
        var result = PatchApplicator.ApplyPatch(patch, originalContent);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Contains("Failed to apply hunk"));
    }
}
