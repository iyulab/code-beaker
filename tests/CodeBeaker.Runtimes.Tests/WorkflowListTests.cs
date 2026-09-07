using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CodeBeaker.Runtimes.Tests;

/// <summary>
/// Two lists in the workflows are kept by hand, and both fail silently. CI names each
/// test project it runs, so a project added to the solution but not to the workflow
/// passes locally and is never run on a runner — the workflow says as much in a comment,
/// which is a note rather than a check. The documentation workflow states its path filter
/// twice, once per trigger; if the two drift apart the site is validated on one event and
/// not the other, and nothing reports it.
///
/// Both are read out of the workflow files here. Like the other guards in this project,
/// nothing is built and nothing is fetched.
/// </summary>
public sealed class WorkflowListTests
{
    [Fact]
    public void EveryTestProject_IsRunByCi()
    {
        var workflow = RuntimeImageFiles.ReadWorkflow("ci.yml");

        var projects = Directory
            .EnumerateDirectories(Path.Combine(RuntimeImageFiles.RepositoryRoot().FullName, "tests"))
            .Where(directory => Directory.EnumerateFiles(directory, "*.csproj").Any())
            .Select(Path.GetFileName)
            .ToList();

        projects.Should().NotBeEmpty("the project set is the input to this test");

        var unrun = projects
            .Where(name => !workflow.Contains($"dotnet test tests/{name}/", StringComparison.Ordinal))
            .ToList();

        unrun.Should().BeEmpty(
            "'ci.yml' names each test project it runs, so one it does not name never runs on a "
            + "runner — it passes locally and reports nothing, which is worse than failing");
    }

    /// <summary>
    /// Every path filter in the documentation workflow has to describe the same set of
    /// files: the point of building the site on a pull request is that it is the same
    /// build the push would do.
    /// </summary>
    [Fact]
    public void DocumentationWorkflow_FiltersBothTriggersOnTheSamePaths()
    {
        var workflow = RuntimeImageFiles.ReadWorkflow("docs-deploy.yml");

        var filters = Regex
            .Matches(workflow, @"paths:\s*\r?\n(?<items>(?:[ \t]*-[ \t]*'[^']*'[ \t]*\r?\n)+)")
            .Select(match => Regex
                .Matches(match.Groups["items"].Value, @"'(?<path>[^']*)'")
                .Select(item => item.Groups["path"].Value)
                .ToList())
            .ToList();

        filters.Should().HaveCountGreaterThanOrEqualTo(2,
            "the workflow is triggered by both a push and a pull request and each states its "
            + "own filter — finding fewer than two means this test is no longer reading them");

        filters.Skip(1).Should().AllSatisfy(filter => filter.Should().Equal(filters[0]),
            "a filter that drifts from the others leaves the site validated on one event and "
            + "not the other, with nothing to report it");
    }
}
