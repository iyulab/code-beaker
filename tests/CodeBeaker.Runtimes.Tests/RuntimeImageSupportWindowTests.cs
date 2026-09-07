using FluentAssertions;
using Xunit;

namespace CodeBeaker.Runtimes.Tests;

/// <summary>
/// The sandbox images run user code on a language runtime, and a <c>v*</c> tag
/// publishes them for anyone to pull. A generation whose upstream support has ended
/// keeps working, so nothing fails and nobody notices — one of these images was built
/// from a generation that had been out of support for two years before anyone looked.
///
/// Dependency updates answer a different question: they report that a newer tag
/// exists, never that the pinned one stopped receiving fixes. These tests answer that
/// one, from a table recorded in the repository, so the check needs no network and no
/// Docker daemon and runs wherever the suite runs.
///
/// A failure here is not a flake. It means the recorded review date for a pinned
/// generation has arrived: confirm upstream support, then either move the Dockerfile
/// (and the catalog version the existing sync guard ties to it) to a supported
/// generation, or extend the window with a fresh upstream date.
/// </summary>
public sealed class RuntimeImageSupportWindowTests
{
    /// <summary>
    /// The date each pinned generation stops being known-supported.
    ///
    /// For runtimes that publish an end-of-life date, that date is the value. Go does
    /// not: it supports the two most recent majors, so a release ends support when the
    /// second one after it arrives, on no announced schedule. Its entry is therefore a
    /// review deadline derived from the roughly six-month release cadence, not a known
    /// end-of-life — deliberately conservative, since the cost of reviewing early is a
    /// glance at the release notes.
    /// </summary>
    private static readonly (string Language, string Generation, DateOnly ReviewBy, string Basis)[] SupportWindows =
    [
        ("python", "3.13", new DateOnly(2029, 10, 31),
            "Python 3.13 end-of-life, per endoflife.date as recorded on 2026-09-07"),
        ("javascript", "24", new DateOnly(2028, 4, 30),
            "Node.js 24 end-of-life, per endoflife.date as recorded on 2026-09-07"),
        ("go", "1.27", new DateOnly(2027, 8, 31),
            "estimate, not an announced date: Go supports the two most recent majors and "
            + "releases roughly every six months, so 1.27 loses support when 1.29 ships"),
        ("csharp", "10.0", new DateOnly(2028, 11, 14),
            ".NET 10 end-of-life, per endoflife.date as recorded on 2026-09-07"),
    ];

    /// <summary>
    /// Raising a base image without recording what its support window is would leave
    /// the new generation unchecked, which is the state this suite exists to prevent.
    /// </summary>
    [Theory]
    [InlineData("python")]
    [InlineData("javascript")]
    [InlineData("go")]
    [InlineData("csharp")]
    public void EveryPinnedGeneration_HasARecordedSupportWindow(string language)
    {
        var directory = RuntimeImageFiles.DirectoryFor(language);
        var generation = RuntimeImageFiles.GenerationOf(RuntimeImageFiles.ReadBaseImage(directory));

        SupportWindows.Select(w => (w.Language, w.Generation)).Should().Contain((language, generation),
            "'docker/runtimes/{0}/Dockerfile' now builds from generation '{1}', so this suite needs "
            + "that generation's support window before it can vouch for it — add it to SupportWindows "
            + "with the upstream date it came from", directory, generation);
    }

    /// <summary>
    /// The check itself. Failing on a date with no commit behind it is the intent:
    /// support ends on a calendar, not on a code change, and nothing else in the
    /// repository would say so.
    /// </summary>
    [Theory]
    [InlineData("python")]
    [InlineData("javascript")]
    [InlineData("go")]
    [InlineData("csharp")]
    public void NoPinnedGeneration_IsPastItsSupportWindow(string language)
    {
        var directory = RuntimeImageFiles.DirectoryFor(language);
        var generation = RuntimeImageFiles.GenerationOf(RuntimeImageFiles.ReadBaseImage(directory));
        var window = SupportWindows.SingleOrDefault(w => w.Language == language && w.Generation == generation);

        // EveryPinnedGeneration_HasARecordedSupportWindow is what reports a missing entry.
        if (window.Basis is null)
        {
            return;
        }

        DateOnly.FromDateTime(DateTime.UtcNow).Should().BeBefore(window.ReviewBy,
            "'docker/runtimes/{0}/Dockerfile' pins generation '{1}', and that date is where its support "
            + "was established from — {2}. Confirm upstream support now: either move the image and the "
            + "catalog version to a supported generation, or extend this window with a fresh upstream date",
            directory, generation, window.Basis);
    }

    /// <summary>
    /// A window recorded for a generation nothing builds from is dead weight that
    /// reads as coverage — the same failure mode as the stale restatements the sync
    /// guard removes.
    /// </summary>
    [Fact]
    public void EveryRecordedSupportWindow_DescribesAGenerationInUse()
    {
        var pinned = RuntimeImageFiles.Directories
            .Select(e => (e.Language, Generation: RuntimeImageFiles.GenerationOf(RuntimeImageFiles.ReadBaseImage(e.Directory))));

        SupportWindows.Select(w => (w.Language, w.Generation)).Should().BeEquivalentTo(pinned,
            "a support window for a generation no Dockerfile builds from looks like coverage without being it");
    }
}
