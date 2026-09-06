namespace CodeBeaker.Core.Interfaces;

/// <summary>
/// 이미 살아 있는 실행 환경에 다시 붙을 수 있는 런타임.
/// 컨테이너처럼 호스트 프로세스보다 오래 사는 환경만 구현한다 —
/// 프로세스 기반 런타임은 API 프로세스와 수명을 같이 하므로 재연결이 원리적으로 불가능하다.
/// (<see cref="IResourceMonitor"/>와 같은 선택적 능력 인터페이스 패턴)
/// </summary>
public interface IReconnectableRuntime
{
    /// <summary>
    /// 기존 환경 식별자(컨테이너 id 등)로 실행 환경을 되살린다.
    /// 대상이 이미 사라졌거나 재연결에 실패하면 null.
    /// </summary>
    Task<IExecutionEnvironment?> ReconnectEnvironmentAsync(
        string environmentId,
        RuntimeConfig config,
        CancellationToken cancellationToken = default);
}
