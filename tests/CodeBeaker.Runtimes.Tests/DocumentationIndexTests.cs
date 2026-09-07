using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CodeBeaker.Runtimes.Tests;

/// <summary>
/// <c>DOCUMENTATION_INDEX.md</c> promises two things: that everything it links is
/// actually committed here, and that it is where a reader finds the documents. Both
/// were kept by hand. The first was broken once already — eight files carried links
/// to directories that were never tracked — and the second broke the moment a new
/// document was added without anyone remembering the index.
///
/// So both directions are checked here: a link that resolves to nothing fails, and a
/// document nobody listed fails. Only repository files are read — no network, no
/// Docker, no application — which is why this sits beside the other guards in this
/// project rather than in the one that needs a running server.
/// </summary>
public sealed class DocumentationIndexTests
{
    private const string IndexFile = "DOCUMENTATION_INDEX.md";

    /// <summary>Markdown links, minus the anchor and minus image embeds.</summary>
    private static readonly Regex Link = new(@"(?<!\!)\[[^\]]*\]\((?<target>[^)\s#]+)(?:#[^)]*)?\)");

    private static string IndexText() =>
        File.ReadAllText(Path.Combine(RuntimeImageFiles.RepositoryRoot().FullName, IndexFile));

    [Fact]
    public void EveryLink_ResolvesToACommittedFile()
    {
        var root = RuntimeImageFiles.RepositoryRoot().FullName;

        var targets = Link.Matches(IndexText())
            .Select(m => m.Groups["target"].Value)
            .Where(target => !Uri.IsWellFormedUriString(target, UriKind.Absolute))
            .Distinct()
            .ToList();

        targets.Should().NotBeEmpty("an index that links nothing would pass this test without "
            + "checking anything");

        var missing = targets
            .Where(target => !File.Exists(Path.Combine(root, target.Replace('/', Path.DirectorySeparatorChar))))
            .ToList();

        missing.Should().BeEmpty(
            "'{0}' says it lists only documents committed to this repository, so a link that "
            + "resolves to nothing is a promise it is not keeping", IndexFile);
    }

    /// <summary>
    /// The reverse direction. <c>docs/archive/</c> is deliberately outside this: the index
    /// says in its own words that those describe past development stages rather than how
    /// the project behaves now, which is why they are not listed.
    /// </summary>
    [Fact]
    public void EveryCurrentDocument_IsListed()
    {
        var root = RuntimeImageFiles.RepositoryRoot().FullName;
        var index = IndexText();

        var documents = Directory
            .EnumerateFiles(Path.Combine(root, "docs"), "*.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .ToList();

        documents.Should().NotBeEmpty("the document set is the input to this test");

        var unlisted = documents
            .Where(name => !index.Contains($"(docs/{name})", StringComparison.Ordinal))
            .ToList();

        unlisted.Should().BeEmpty(
            "a document nobody links from '{0}' is one a reader arriving at this repository "
            + "has no way to find", IndexFile);
    }
}
