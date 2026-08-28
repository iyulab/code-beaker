using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Runtimes.Native;
using Xunit;

namespace CodeBeaker.Runtimes.Tests;

/// <summary>
/// Native Process Runtime 통합 테스트 — Deno/Bun/Node/Python과 달리 별도 인터프리터
/// 설치가 필요 없어(순수 OS 프로세스 스폰) 전부 실제로 실행한다(Skip 없음).
/// </summary>
public sealed class NativeProcessRuntimeTests
{
    private readonly NativeProcessRuntime _runtime = new();

    [Fact]
    public void Runtime_ShouldHaveCorrectProperties()
    {
        Assert.Equal("native", _runtime.Name);
        Assert.Equal(RuntimeType.NativeProcess, _runtime.Type);
        Assert.Contains("dotnet", _runtime.SupportedEnvironments);
        Assert.Contains("go", _runtime.SupportedEnvironments);
    }

    [Fact]
    public void Runtime_ShouldReportLowestIsolationOfAllRuntimes()
    {
        // 다른 언어 런타임들과 달리 자체 샌드박싱이 전혀 없음 — 그 사실을 정직하게 신고해야
        // RuntimeSelector의 Security/Balanced 선호도가 가능하면 더 격리된 런타임을 우선한다.
        var capabilities = _runtime.GetCapabilities();

        Assert.Equal(1, capabilities.IsolationLevel);
    }

    [Fact]
    public async Task IsAvailableAsync_AlwaysReturnsTrue()
    {
        // 자체 인터프리터가 없으므로 항상 사용 가능 — 명령어 자체의 존재 여부는
        // ExecuteAsync 실행 시점에 판가름 난다.
        Assert.True(await _runtime.IsAvailableAsync());
    }

    [Fact]
    public async Task ExecuteShellCommand_RunsAndCapturesStdout()
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"native-test-{Guid.NewGuid():N}");
        try
        {
            var environment = await _runtime.CreateEnvironmentAsync(new RuntimeConfig
            {
                Environment = "dotnet",
                WorkspaceDirectory = workspace,
            });

            var result = await environment.ExecuteAsync(new ExecuteShellCommand
            {
                CommandName = "dotnet",
                Args = ["--version"],
            });

            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.Result);

            await environment.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, true);
            }
        }
    }

    [Fact]
    public async Task ExecuteShellCommand_WithNonZeroExit_ReturnsFailureWithoutThrowing()
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"native-test-{Guid.NewGuid():N}");
        try
        {
            var environment = await _runtime.CreateEnvironmentAsync(new RuntimeConfig
            {
                Environment = "dotnet",
                WorkspaceDirectory = workspace,
            });

            var result = await environment.ExecuteAsync(new ExecuteShellCommand
            {
                CommandName = "dotnet",
                Args = ["nonexistent-verb-xyz"],
            });

            Assert.False(result.Success);

            await environment.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithUnsupportedCommandType_ReturnsFailureNotSupportedException()
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"native-test-{Guid.NewGuid():N}");
        var environment = await _runtime.CreateEnvironmentAsync(new RuntimeConfig
        {
            Environment = "dotnet",
            WorkspaceDirectory = workspace,
        });

        var result = await environment.ExecuteAsync(new ReadFileCommand { Path = "whatever.txt" });

        Assert.False(result.Success);
        Assert.Contains("not supported", result.Error, StringComparison.OrdinalIgnoreCase);

        await environment.DisposeAsync();
        Directory.Delete(workspace, true);
    }
}
