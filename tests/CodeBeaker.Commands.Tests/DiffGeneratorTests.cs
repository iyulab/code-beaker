using CodeBeaker.Commands.Utilities;
using FluentAssertions;

namespace CodeBeaker.Commands.Tests;

/// <summary>
/// Unit tests for DiffGenerator
/// Phase 14: Testing Diff generation utility
/// </summary>
public class DiffGeneratorTests
{
    [Fact]
    public void GenerateUnifiedDiff_ShouldReturnEmptyString_WhenContentsIdentical()
    {
        // Arrange
        var content = "line1\nline2\nline3";

        // Act
        var diff = DiffGenerator.GenerateUnifiedDiff(content, content);

        // Assert
        diff.Should().BeEmpty();
    }

    [Fact]
    public void GenerateUnifiedDiff_ShouldIncludeAddedLines()
    {
        // Arrange
        var original = "line1\nline2";
        var modified = "line1\nline2\nline3";

        // Act
        var diff = DiffGenerator.GenerateUnifiedDiff(original, modified);

        // Assert
        diff.Should().Contain("--- original");
        diff.Should().Contain("+++ modified");
        diff.Should().Contain("+line3");
    }

    [Fact]
    public void GenerateUnifiedDiff_ShouldIncludeRemovedLines()
    {
        // Arrange
        var original = "line1\nline2\nline3";
        var modified = "line1\nline3";

        // Act
        var diff = DiffGenerator.GenerateUnifiedDiff(original, modified);

        // Assert
        diff.Should().Contain("-line2");
    }

    [Fact]
    public void GenerateUnifiedDiff_ShouldIncludeModifiedLines()
    {
        // Arrange
        var original = "line1\nline2\nline3";
        var modified = "line1\nline2-modified\nline3";

        // Act
        var diff = DiffGenerator.GenerateUnifiedDiff(original, modified);

        // Assert
        diff.Should().Contain("-line2");
        diff.Should().Contain("+line2-modified");
    }

    [Fact]
    public void GenerateUnifiedDiff_ShouldIncludeContextLines()
    {
        // Arrange
        var original = "line1\nline2\nline3\nline4\nline5";
        var modified = "line1\nline2\nline3-modified\nline4\nline5";

        // Act
        var diff = DiffGenerator.GenerateUnifiedDiff(original, modified, "original", "modified", contextLines: 2);

        // Assert
        diff.Should().Contain(" line2");  // Context line
        diff.Should().Contain(" line4");  // Context line
        diff.Should().Contain("-line3");
        diff.Should().Contain("+line3-modified");
    }

    [Fact]
    public void GenerateUnifiedDiff_ShouldHandleCustomFilenames()
    {
        // Arrange
        var original = "old";
        var modified = "new";

        // Act
        var diff = DiffGenerator.GenerateUnifiedDiff(original, modified, "file1.txt", "file2.txt");

        // Assert
        diff.Should().Contain("--- file1.txt");
        diff.Should().Contain("+++ file2.txt");
    }

    [Fact]
    public void CalculateStats_ShouldReturnZero_WhenContentsIdentical()
    {
        // Arrange
        var content = "line1\nline2\nline3";

        // Act
        var (added, removed, modified) = DiffGenerator.CalculateStats(content, content);

        // Assert
        added.Should().Be(0);
        removed.Should().Be(0);
        modified.Should().Be(0);
    }

    [Fact]
    public void CalculateStats_ShouldCountAddedLines()
    {
        // Arrange
        var original = "line1\nline2";
        var modified = "line1\nline2\nline3\nline4";

        // Act
        var (added, removed, modifiedCount) = DiffGenerator.CalculateStats(original, modified);

        // Assert
        added.Should().Be(2);
        removed.Should().Be(0);
    }

    [Fact]
    public void CalculateStats_ShouldCountRemovedLines()
    {
        // Arrange
        var original = "line1\nline2\nline3\nline4";
        var modified = "line1\nline4";

        // Act
        var (added, removed, modifiedCount) = DiffGenerator.CalculateStats(original, modified);

        // Assert
        removed.Should().Be(2);
        added.Should().Be(0);
    }

    [Fact]
    public void CalculateStats_ShouldCountModifiedLines()
    {
        // Arrange
        var original = "line1\nline2\nline3";
        var modified = "line1\nline2-changed\nline3";

        // Act
        var (added, removed, modifiedCount) = DiffGenerator.CalculateStats(original, modified);

        // Assert
        modifiedCount.Should().Be(1);
    }

    [Fact]
    public void GenerateUnifiedDiff_ShouldHandleEmptyStrings()
    {
        // Arrange
        var original = "";
        var modified = "new content";

        // Act
        var diff = DiffGenerator.GenerateUnifiedDiff(original, modified);

        // Assert
        diff.Should().Contain("+new content");
    }

    [Fact]
    public void GenerateUnifiedDiff_ShouldHandleComplexChanges()
    {
        // Arrange
        var original = @"def factorial(n):
    if n < 1:
        return 1
    return n * factorial(n - 1)

print(factorial(5))";

        var modified = @"def factorial(n):
    if n <= 1:
        return 1
    return n * factorial(n - 1)

print(factorial(5))
print(factorial(10))";

        // Act
        var diff = DiffGenerator.GenerateUnifiedDiff(original, modified, "old.py", "new.py");

        // Assert
        diff.Should().Contain("--- old.py");
        diff.Should().Contain("+++ new.py");
        diff.Should().Contain("-    if n < 1:");
        diff.Should().Contain("+    if n <= 1:");
        diff.Should().Contain("+print(factorial(10))");
    }
}
