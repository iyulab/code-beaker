using CodeBeaker.Runtimes;
using CodeBeaker.Runtimes.Docker;
using FluentAssertions;
using Xunit;

namespace CodeBeaker.Runtimes.Tests;

public sealed class LanguageCatalogTests
{
    [Theory]
    [InlineData("python", "python")]
    [InlineData("PYTHON", "python")]
    [InlineData("py", "python")]
    [InlineData("python3", "python")]
    [InlineData("javascript", "javascript")]
    [InlineData("js", "javascript")]
    [InlineData("node", "javascript")]
    [InlineData("nodejs", "javascript")]
    [InlineData("go", "go")]
    [InlineData("golang", "go")]
    [InlineData("csharp", "csharp")]
    [InlineData("cs", "csharp")]
    [InlineData("dotnet", "csharp")]
    [InlineData("CSharp", "csharp")]
    public void Resolve_NameOrAlias_ReturnsCanonicalLanguage(string input, string expectedName)
    {
        var language = LanguageCatalog.Resolve(input);

        language.Name.Should().Be(expectedName);
    }

    [Fact]
    public void Resolve_UnsupportedLanguage_ThrowsNotSupportedException()
    {
        var act = () => LanguageCatalog.Resolve("ruby");

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*ruby*not supported*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_EmptyName_ThrowsArgumentException(string? input)
    {
        var act = () => LanguageCatalog.Resolve(input!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("python", true)]
    [InlineData("js", true)]
    [InlineData("ruby", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSupported_AcceptsNullAndAliases(string? input, bool expected)
    {
        LanguageCatalog.IsSupported(input).Should().Be(expected);
    }

    [Fact]
    public void Names_ListsEveryCanonicalNameOnce()
    {
        LanguageCatalog.Names.Should().BeEquivalentTo("python", "javascript", "go", "csharp");
        LanguageCatalog.Names.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void EveryLanguage_ListsItsOwnNameAmongAliases()
    {
        foreach (var language in LanguageCatalog.All)
        {
            language.Aliases.Should().Contain(language.Name, because: "a canonical name must resolve to itself");
        }
    }

    [Fact]
    public void Aliases_AreUniqueAcrossLanguages()
    {
        var all = LanguageCatalog.All.SelectMany(l => l.Aliases).Select(a => a.ToLowerInvariant());

        all.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void DockerRuntime_AcceptsExactlyTheCatalogNames()
    {
        var runtime = new DockerRuntime();

        runtime.SupportedEnvironments.Should().BeEquivalentTo(LanguageCatalog.AllNames);
    }
}
