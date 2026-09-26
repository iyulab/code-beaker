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
///
/// Whether a pinned generation is still supported upstream is a different question,
/// answered by <see cref="RuntimeImageSupportWindowTests"/>.
/// </summary>
public sealed class RuntimeImageCatalogSyncTests
{
    [Theory]
    [InlineData("python")]
    [InlineData("javascript")]
    [InlineData("go")]
    [InlineData("csharp")]
    public void CatalogVersion_MatchesTheGenerationItsDockerfileBuildsFrom(string language)
    {
        var directory = RuntimeImageFiles.DirectoryFor(language);
        var baseImage = RuntimeImageFiles.ReadBaseImage(directory);

        RuntimeImageFiles.GenerationOf(baseImage).Should().Be(
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
    /// The fact is restated in two places outside the catalog, so each gets checked:
    /// the CI build steps (which the integration-tests job runs against) and the
    /// tag-push job that puts the images on GHCR.
    /// </summary>
    [Theory]
    [InlineData("python")]
    [InlineData("javascript")]
    [InlineData("go")]
    [InlineData("csharp")]
    public void CatalogImage_IsBuiltAndPublishedFromItsOwnDirectory(string language)
    {
        var image = LanguageCatalog.Resolve(language).DockerImage;
        var path = $"docker/runtimes/{RuntimeImageFiles.DirectoryFor(language)}";

        // `codebeaker-dotnet:latest` → `dotnet`; the publish job assembles the pushed
        // tag as `ghcr.io/<owner>/codebeaker-${{ matrix.name }}`.
        var suffix = image["codebeaker-".Length..image.IndexOf(':')];

        BuildStepPairs().Should().Contain((path, image),
            "CI has to build '{0}' for the integration tests", image);

        PublishMatrixPairs().Should().Contain((suffix, path),
            "the publish-runtime-images matrix is what puts '{0}' on GHCR", image);
    }

    /// <summary>CI build steps — a <c>context:</c> line followed by its <c>tags:</c> line.</summary>
    private static IReadOnlyList<(string Path, string Image)> BuildStepPairs() =>
        Pairs(RuntimeImageFiles.ReadWorkflow("ci.yml"), @"context:\s*(?<a>\S+)\s*\r?\n\s*tags:\s*(?<b>\S+)");

    /// <summary>The tag-push job's matrix — a <c>- name:</c> line followed by its <c>path:</c> line.</summary>
    private static IReadOnlyList<(string Name, string Path)> PublishMatrixPairs() =>
        Pairs(RuntimeImageFiles.ReadWorkflow("ci.yml"), @"-\s*name:\s*(?<a>\S+)\s*\r?\n\s*path:\s*(?<b>\S+)");

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
            .EnumerateDirectories(Path.Combine(RuntimeImageFiles.RepositoryRoot().FullName, "docker", "runtimes"))
            .Select(d => Path.GetFileName(d)!);

        onDisk.Should().BeEquivalentTo(
            RuntimeImageFiles.Directories.Select(e => e.Directory),
            "a runtime image nobody maps to a language would never be version-checked");
    }

    [Fact]
    public void EveryCatalogLanguage_IsMapped()
    {
        RuntimeImageFiles.Directories.Select(e => e.Language)
            .Should().BeEquivalentTo(LanguageCatalog.Names);
    }
}
