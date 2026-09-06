using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Core.Sessions;
using CodeBeaker.Core.Storage;
using CodeBeaker.Integration.Tests.TestHelpers;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// 세션을 만든 인스턴스가 아닌 다른 인스턴스가 같은 컨테이너에 다시 붙을 수 있는지
/// 살아있는 데몬에 대고 확인한다.
///
/// 두 <see cref="SessionManager"/> 가 하나의 세션 저장소를 공유하는 구성은
/// 다중 인스턴스 배포에서 요청이 다른 호스트로 라우팅되는 상황과 같다.
/// 재연결은 저장된 세션의 식별자만으로 이뤄지므로, 그 식별자가 실제 컨테이너를
/// 가리키지 않으면 이 시나리오는 조용히 실패한다.
/// </summary>
public sealed class SessionReconnectionLiveTests : IDisposable
{
    private readonly InMemorySessionStore _sharedStore = new();
    private readonly SessionManager _creator;
    private readonly SessionManager _other;

    public SessionReconnectionLiveTests()
    {
        _creator = new SessionManager(
            _sharedStore, new List<IExecutionRuntime> { new CodeBeaker.Runtimes.Docker.DockerRuntime() });
        _other = new SessionManager(
            _sharedStore, new List<IExecutionRuntime> { new CodeBeaker.Runtimes.Docker.DockerRuntime() });
    }

    private static async Task SkipIfDockerUnavailableAsync()
    {
        var reason = await DockerTestHelper.GetSkipReasonAsync();
        Skip.If(reason is not null, reason);
    }

    [SkippableFact]
    public async Task AnotherInstance_ReattachesToTheLiveContainerAndExecutes()
    {
        await SkipIfDockerUnavailableAsync();

        var session = await _creator.CreateSessionAsync(new SessionConfig
        {
            Language = "python",
            RuntimeType = RuntimeType.Docker
        });

        try
        {
            // 두 번째 인스턴스는 이 세션의 환경을 캐시하고 있지 않다 —
            // 저장된 식별자로 살아있는 컨테이너에 재연결해야만 한다.
            var revived = await _other.GetSessionAsync(session.SessionId);

            Assert.NotNull(revived);

            // 재연결이 실제로 일어났는지는 이 인스턴스로 명령이 실행되는지로만 확인할 수 있다
            // (Session.Environment 는 internal 이고, 재구성 실패 시 조용히 null 이 된다).
            var result = await _other.ExecuteInSessionAsync(
                session.SessionId,
                new WriteFileCommand
                {
                    Path = "/workspace/reconnected.txt",
                    Content = "written after reattaching",
                    Mode = FileWriteMode.Create
                });

            Assert.True(result.Success, result.Error);
        }
        finally
        {
            await _creator.CloseSessionAsync(session.SessionId);
        }
    }

    public void Dispose()
    {
        _creator.Dispose();
        _other.Dispose();
    }
}
