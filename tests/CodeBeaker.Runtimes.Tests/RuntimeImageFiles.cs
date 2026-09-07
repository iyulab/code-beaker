namespace CodeBeaker.Runtimes.Tests;

/// <summary>
/// Reads the files that define the sandbox runtime images, so tests can assert
/// against what is actually built rather than against a value restated in a test.
/// Two guards share this: one checks that the catalog and the workflows still agree
/// with the Dockerfiles, the other checks that the generations they pin are still
/// supported upstream.
/// </summary>
internal static class RuntimeImageFiles
{
    /// <summary>
    /// Canonical language name → the directory under <c>docker/runtimes</c> that
    /// builds its image. The two differ (<c>csharp</c> builds
    /// <c>codebeaker-dotnet</c>), so the mapping cannot be derived from either name.
    /// <c>RuntimeImageCatalogSyncTests</c> asserts both directions of this table
    /// against the catalog and the directories on disk, which is what keeps it honest.
    /// </summary>
    internal static readonly (string Language, string Directory)[] Directories =
    [
        ("python", "python"),
        ("javascript", "nodejs"),
        ("go", "golang"),
        ("csharp", "csharp"),
    ];

    internal static string DirectoryFor(string language) =>
        Directories.Single(e => e.Language == language).Directory;

    /// <summary>
    /// The tag's leading component: <c>3.13-slim</c> → <c>3.13</c>, <c>10.0</c> →
    /// <c>10.0</c>. Everything after the first dash names a base-OS variant, not a
    /// language generation.
    /// </summary>
    internal static string GenerationOf(string baseImage)
    {
        var tag = baseImage[(baseImage.LastIndexOf(':') + 1)..];
        return tag.Split('-')[0];
    }

    /// <summary>The image the given runtime directory's Dockerfile is built from.</summary>
    internal static string ReadBaseImage(string directory)
    {
        var dockerfile = Path.Combine(RepositoryRoot().FullName, "docker", "runtimes", directory, "Dockerfile");

        if (!File.Exists(dockerfile))
        {
            throw new InvalidOperationException($"'{dockerfile}' is the image definition under test, and it is missing.");
        }

        var from = File.ReadLines(dockerfile)
            .FirstOrDefault(l => l.StartsWith("FROM ", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"'{dockerfile}' has to declare a base image.");

        // `FROM <image>` — a build stage alias (`AS build`) would follow the image.
        return from.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
    }

    internal static string ReadWorkflow(string name) =>
        File.ReadAllText(Path.Combine(RepositoryRoot().FullName, ".github", "workflows", name));

    /// <summary>
    /// Walks up from the test binaries until the directory holding the runtime image
    /// definitions appears. Resolving from <see cref="AppContext.BaseDirectory"/> keeps
    /// this working wherever the suite runs from, unlike a path relative to the caller.
    /// </summary>
    internal static DirectoryInfo RepositoryRoot()
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
