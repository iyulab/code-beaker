using System.Text.RegularExpressions;
using CodeBeaker.Runtimes;
using FluentAssertions;
using Xunit;

namespace CodeBeaker.Runtimes.Tests;

/// <summary>
/// <see cref="LanguageCatalog"/> restates two facts that live somewhere else: the
/// generation each sandbox image is built from, and the image name CI builds and
/// publishes. Both are served to clients over <c>language.list</c> and
/// <c>GET /api/language</c>, and nothing made them move together — raising a base
/// image left the catalog advertising the old generation until somebody happened
/// to grep for it.
///
/// These tests read the Dockerfiles and the workflow themselves, so the restatements
/// cannot drift silently. No Docker daemon is involved — nothing is built here, which
/// is what keeps the guard running everywhere rather than skipping where Docker is absent.
/// </summary>
public sealed class RuntimeImageCatalogSyncTests
{
    /// <summary>
    /// Canonical language name → the directory under <c>docker/runtimes</c> that
    /// builds its image. The two differ (<c>csharp</c> builds
    /// <c>codebeaker-dotnet</c>), so the mapping cannot be derived from either name.
    /// <see cref="EveryRuntimeImageDirectory_IsCoveredByTheCatalog"/> and
    /// <see cref="EveryCatalogLanguage_IsMapped"/> are what keep this table honest.
    /// </summary>
    private static readonly (string Language, string Directory)[] ImageDirectories =
    [
        ("python", "python"),
        ("javascript", "nodejs"),
        ("go", "golang"),
        ("csharp", "csharp"),
    ];

    [Theory]
    [InlineData("python")]
    [InlineData("javascript")]
    [InlineData("go")]
    [InlineData("csharp")]
    public void CatalogVersion_MatchesTheGenerationItsDockerfileBuildsFrom(string language)
    {
        var directory = DirectoryFor(language);
        var baseImage = ReadBaseImage(directory);

        GenerationOf(baseImage).Should().Be(
            LanguageCatalog.Resolve(language).Version,
            "the catalog reports this value to clients as the language version, so it has to be "
            + "the generation '{0}/Dockerfile' actually builds from ({1})", directory, baseImage);
    }

    /// <summary>
    /// The image the catalog hands to the Docker runtime only exists if CI actually
    /// builds it, from the directory that defines it. A language added to the catalog
    /// alone would pull an image nobody builds, and a step pointed at the wrong
    /// directory would publish the wrong contents under the right name — both surface
    /// only at runtime.
    ///
    /// The fact is restated in three places outside the catalog, so each gets checked:
    /// the CI build steps, the tag-push job that puts the images on GHCR, and the
    /// integration workflow that builds them for its own run.
    /// </summary>
    [Theory]
    [InlineData("python")]
    [InlineData("javascript")]
    [InlineData("go")]
    [InlineData("csharp")]
    public void CatalogImage_IsBuiltAndPublishedFromItsOwnDirectory(string language)
    {
        var image = LanguageCatalog.Resolve(language).DockerImage;
        var path = $"docker/runtimes/{DirectoryFor(language)}";

        // `codebeaker-dotnet:latest` → `dotnet`; the publish job assembles the pushed
        // tag as `ghcr.io/<owner>/codebeaker-${{ matrix.name }}`.
        var suffix = image["codebeaker-".Length..image.IndexOf(':')];

        BuildStepPairs().Should().Contain((path, image),
            "CI has to build '{0}' for the integration tests", image);

        PublishMatrixPairs().Should().Contain((suffix, path),
            "the publish-runtime-images matrix is what puts '{0}' on GHCR", image);

        IntegrationWorkflowPairs().Should().Contain((image, path),
            "the integration workflow builds its own copy of '{0}'", image);
    }

    /// <summary>CI build steps — a <c>context:</c> line followed by its <c>tags:</c> line.</summary>
    private static IReadOnlyList<(string Path, string Image)> BuildStepPairs() =>
        Pairs(ReadWorkflow("ci.yml"), @"context:\s*(?<a>\S+)\s*\r?\n\s*tags:\s*(?<b>\S+)");

    /// <summary>The tag-push job's matrix — a <c>- name:</c> line followed by its <c>path:</c> line.</summary>
    private static IReadOnlyList<(string Name, string Path)> PublishMatrixPairs() =>
        Pairs(ReadWorkflow("ci.yml"), @"-\s*name:\s*(?<a>\S+)\s*\r?\n\s*path:\s*(?<b>\S+)");

    /// <summary>The integration workflow — <c>docker build -t &lt;image&gt; &lt;path&gt;</c>.</summary>
    private static IReadOnlyList<(string Image, string Path)> IntegrationWorkflowPairs() =>
        Pairs(ReadWorkflow("integration-tests.yml"), @"docker build -t (?<a>\S+) (?<b>\S+)");

    private static IReadOnlyList<(string, string)> Pairs(string workflow, string pattern)
    {
        var matches = Regex.Matches(workflow, pattern);

        matches.Should().NotBeEmpty(
            "'{0}' is the shape this guard reads; if the workflow stopped using it the guard "
            + "would pass vacuously", pattern);

        return matches.Select(m => (m.Groups["a"].Value, m.Groups["b"].Value)).ToArray();
    }

    [Fact]
    public void EveryRuntimeImageDirectory_IsCoveredByTheCatalog()
    {
        var onDisk = Directory
            .EnumerateDirectories(Path.Combine(RepositoryRoot().FullName, "docker", "runtimes"))
            .Select(d => Path.GetFileName(d)!);

        onDisk.Should().BeEquivalentTo(
            ImageDirectories.Select(e => e.Directory),
            "a runtime image nobody maps to a language would never be version-checked");
    }

    [Fact]
    public void EveryCatalogLanguage_IsMapped()
    {
        ImageDirectories.Select(e => e.Language)
            .Should().BeEquivalentTo(LanguageCatalog.Names);
    }

    private static string DirectoryFor(string language) =>
        ImageDirectories.Single(e => e.Language == language).Directory;

    /// <summary>
    /// The tag's leading component: <c>3.13-slim</c> → <c>3.13</c>, <c>10.0</c> →
    /// <c>10.0</c>. Everything after the first dash names a base-OS variant, not a
    /// language generation.
    /// </summary>
    private static string GenerationOf(string baseImage)
    {
        var tag = baseImage[(baseImage.LastIndexOf(':') + 1)..];
        return tag.Split('-')[0];
    }

    private static string ReadBaseImage(string directory)
    {
        var dockerfile = Path.Combine(RepositoryRoot().FullName, "docker", "runtimes", directory, "Dockerfile");
        File.Exists(dockerfile).Should().BeTrue("'{0}' is the image definition under test", dockerfile);

        var from = File.ReadLines(dockerfile)
            .FirstOrDefault(l => l.StartsWith("FROM ", StringComparison.Ordinal));

        from.Should().NotBeNull("'{0}' has to declare a base image", dockerfile);

        // `FROM <image>` — a build stage alias (`AS build`) would follow the image.
        return from!.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
    }

    private static string ReadWorkflow(string name) =>
        File.ReadAllText(Path.Combine(RepositoryRoot().FullName, ".github", "workflows", name));

    /// <summary>
    /// Walks up from the test binaries until the directory holding the runtime image
    /// definitions appears. Resolving from <see cref="AppContext.BaseDirectory"/> keeps
    /// this working wherever the suite runs from, unlike a path relative to the caller.
    /// </summary>
    private static DirectoryInfo RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "docker", "runtimes")))
            {
                return dir;
            }
        }

        throw new InvalidOperationException(
            $"No ancestor of '{AppContext.BaseDirectory}' contains 'docker/runtimes'.");
    }
}
