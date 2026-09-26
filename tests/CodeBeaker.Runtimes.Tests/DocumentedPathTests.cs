using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CodeBeaker.Runtimes.Tests;

/// <summary>
/// The current documents tell a developer which scripts to run and which projects to open.
/// Nothing tied those names to the tree: a developer guide went on sending readers to a
/// Worker project and five setup scripts written for an architecture that had been removed,
/// and each one failed only when someone followed it.
///
/// So every <c>scripts/…</c> file and <c>src/CodeBeaker.*</c> project a current document names
/// has to exist. <c>CHANGELOG.md</c> and <c>docs/archive/</c> are records of what used to be,
/// and are left out on purpose.
/// </summary>
public sealed class DocumentedPathTests
{
    private static readonly Regex ScriptPath = new(@"scripts[/\\](?<name>[A-Za-z0-9._-]+\.(?:ps1|sh))");

    private static readonly Regex ProjectPath = new(@"src[/\\](?<name>CodeBeaker\.[A-Za-z]+(?:\.[A-Za-z]+)*)");

    [Fact]
    public void EveryScriptACurrentDocumentNames_Exists()
    {
        var root = RuntimeImageFiles.RepositoryRoot().FullName;

        var missing = References(ScriptPath)
            .Where(r => !File.Exists(Path.Combine(root, "scripts", r.Name)))
            .Select(r => $"scripts/{r.Name} (in {r.Document})")
            .ToList();

        missing.Should().BeEmpty("a script a document tells a developer to run has to exist");
    }

    [Fact]
    public void EveryProjectACurrentDocumentNames_Exists()
    {
        var root = RuntimeImageFiles.RepositoryRoot().FullName;

        var missing = References(ProjectPath)
            .Where(r => !Directory.Exists(Path.Combine(root, "src", r.Name)))
            .Select(r => $"src/{r.Name} (in {r.Document})")
            .ToList();

        missing.Should().BeEmpty("a project a document points a developer at has to exist");
    }

    private static IEnumerable<(string Name, string Document)> References(Regex pattern) =>
        CurrentDocuments()
            .SelectMany(document => pattern.Matches(File.ReadAllText(document))
                .Select(m => (m.Groups["name"].Value, Path.GetRelativePath(RuntimeImageFiles.RepositoryRoot().FullName, document))))
            .Distinct();

    private static IEnumerable<string> CurrentDocuments()
    {
        var root = RuntimeImageFiles.RepositoryRoot().FullName;

        var rootDocuments = Directory.EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path) != "CHANGELOG.md");
        var docs = Directory.EnumerateFiles(Path.Combine(root, "docs"), "*.md", SearchOption.TopDirectoryOnly);

        // docs-site/docs is generated from these at build time and not committed, so it is
        // covered through its sources and absent from a fresh clone.
        return rootDocuments.Concat(docs);
    }
}
