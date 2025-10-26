using CodeBeaker.Runtimes;
using FluentAssertions;
using Xunit;

namespace CodeBeaker.Runtimes.Tests;

public sealed class RuntimeRegistryTests
{
    [Theory]
    [InlineData("python")]
    [InlineData("PYTHON")]
    [InlineData("Python")]
    public void Get_Python_ReturnsPythonRuntime(string language)
    {
        // Act
        var runtime = RuntimeRegistry.Get(language);

        // Assert
        runtime.Should().NotBeNull();
        runtime.Should().BeOfType<PythonRuntime>();
        runtime.LanguageName.Should().Be("python");
    }

    [Theory]
    [InlineData("javascript")]
    [InlineData("js")]
    [InlineData("node")]
    [InlineData("JavaScript")]
    public void Get_JavaScript_ReturnsJavaScriptRuntime(string language)
    {
        // Act
        var runtime = RuntimeRegistry.Get(language);

        // Assert
        runtime.Should().NotBeNull();
        runtime.Should().BeOfType<JavaScriptRuntime>();
        runtime.LanguageName.Should().Be("javascript");
    }

    [Theory]
    [InlineData("go")]
    [InlineData("golang")]
    [InlineData("Go")]
    public void Get_Go_ReturnsGoRuntime(string language)
    {
        // Act
        var runtime = RuntimeRegistry.Get(language);

        // Assert
        runtime.Should().NotBeNull();
        runtime.Should().BeOfType<GoRuntime>();
        runtime.LanguageName.Should().Be("go");
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("cs")]
    [InlineData("dotnet")]
    [InlineData("CSharp")]
    public void Get_CSharp_ReturnsCSharpRuntime(string language)
    {
        // Act
        var runtime = RuntimeRegistry.Get(language);

        // Assert
        runtime.Should().NotBeNull();
        runtime.Should().BeOfType<CSharpRuntime>();
        runtime.LanguageName.Should().Be("csharp");
    }

    [Fact]
    public void Get_UnsupportedLanguage_ThrowsNotSupportedException()
    {
        // Act & Assert
        var act = () => RuntimeRegistry.Get("ruby");

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*ruby*not supported*");
    }

    [Fact]
    public void Get_NullLanguage_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => RuntimeRegistry.Get(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetSupportedLanguages_ReturnsAllLanguages()
    {
        // Act
        var languages = RuntimeRegistry.GetSupportedLanguages();

        // Assert
        languages.Should().Contain("python");
        languages.Should().Contain("javascript");
        languages.Should().Contain("go");
        languages.Should().Contain("csharp");
        languages.Should().HaveCount(4);
    }

    [Theory]
    [InlineData("python", true)]
    [InlineData("javascript", true)]
    [InlineData("ruby", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSupported_ChecksLanguageSupport(string language, bool expected)
    {
        // Act
        var result = RuntimeRegistry.IsSupported(language);

        // Assert
        result.Should().Be(expected);
    }
}
