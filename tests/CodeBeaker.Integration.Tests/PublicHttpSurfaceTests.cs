using System.Net;
using System.Text.RegularExpressions;
using CodeBeaker.Integration.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// <c>docs/API.md</c> is the published description of this server's HTTP surface, and
/// the documentation site serves it verbatim. It replaced a list of endpoints written
/// by hand in the site's sync script, which advertised <c>POST /api/execute</c> and
/// <c>GET /api/execute/:id/status</c> — neither has ever existed — alongside
/// <c>GET /api/languages</c>, which is not the name the route is served under.
/// Nothing compared any of it to the application, so it stayed on the public site.
///
/// These tests read the table in that document and compare it, in both directions,
/// with the routes the application actually registers: a documented path the server
/// does not serve fails, and a route nobody documented fails too.
/// </summary>
public sealed class PublicHttpSurfaceTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public PublicHttpSurfaceTests(ApiTestFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Rows shaped <c>| `METHOD` | `/path` | … |</c>. Prose in the document is free to
    /// mention a path without being read as a claim about the routing table.
    /// </summary>
    private static readonly Regex DocumentedRow =
        new(@"^\|\s*`(?<method>[A-Z]+)`\s*\|\s*`(?<path>/[^`]*)`\s*\|", RegexOptions.Multiline);

    private static IReadOnlyList<(string Method, string Path)> Documented()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot().FullName, "docs", "API.md"));
        var rows = DocumentedRow.Matches(text)
            .Select(m => (Method: m.Groups["method"].Value, Path: m.Groups["path"].Value))
            .ToList();

        rows.Should().NotBeEmpty("the document is the input to this test — an empty table would "
            + "let every check below pass without comparing anything");

        return rows;
    }

    /// <summary>
    /// Walks up from the test binaries until the repository's documents appear, so the
    /// path holds wherever the suite is run from.
    /// </summary>
    private static DirectoryInfo RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs", "API.md")))
            {
                return dir;
            }
        }

        throw new InvalidOperationException(
            $"No ancestor of '{AppContext.BaseDirectory}' contains 'docs/API.md'.");
    }

    private IReadOnlyList<(string Method, string Path)> RoutedEndpoints()
    {
        _fixture.CreateClient();

        return _fixture.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint =>
            {
                var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
                return (
                    Method: methods is { Count: 1 } ? methods[0] : "ANY",
                    Path: "/" + endpoint.RoutePattern.RawText!.TrimStart('/'));
            })
            .ToList();
    }

    [Fact]
    public void EveryRegisteredRoute_IsDocumented()
    {
        RoutedEndpoints().Should().BeSubsetOf(
            Documented(),
            "a route the server serves but nobody wrote down is a surface consumers can only "
            + "find by reading the source — record it in the table in 'docs/API.md'");
    }

    /// <summary>
    /// The reverse direction. Not every documented path is a route — <c>/metrics</c> is
    /// served by middleware and never reaches the routing table — so anything absent
    /// from it is asked for over HTTP instead, which is the stronger check of the two:
    /// it fails on a path that is merely unreachable, not only on one that is unrouted.
    /// </summary>
    [Fact]
    public async Task EveryDocumentedPath_IsServed()
    {
        var routed = RoutedEndpoints().ToHashSet();
        var client = _fixture.CreateClient();

        foreach (var row in Documented())
        {
            if (routed.Contains(row))
            {
                continue;
            }

            if (row.Path.Contains('{'))
            {
                throw new Xunit.Sdk.XunitException(
                    $"'{row.Method} {row.Path}' is documented with a route parameter but no route "
                    + "matches it, and a templated path cannot be requested directly");
            }

            var response = await client.SendAsync(
                new HttpRequestMessage(new HttpMethod(row.Method == "ANY" ? "GET" : row.Method), row.Path));

            response.StatusCode.Should().NotBe(
                HttpStatusCode.NotFound,
                "'{0} {1}' is documented but the server does not serve it", row.Method, row.Path);
        }
    }
}
