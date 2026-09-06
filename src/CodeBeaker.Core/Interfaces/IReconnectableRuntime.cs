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
    ///
    /// null 은 <b>되살릴 대상이 확실히 없다</b>는 뜻이다 — 호출자는 이 답을 근거로 세션을
    /// 종료 처리해도 된다. 되살릴 수 없는 잔해(멈춘 컨테이너 등)가 남아 있었다면 구현이
    /// 그것까지 걷어낸 뒤 null 을 돌려준다. 호출자가 기록을 지우고 나면 그 잔해를 가리키는
    /// 것이 아무것도 남지 않기 때문이다. 반면 지금 확인할 수 없는 사정(데몬 미기동, 연결 끊김 등)은
    /// null 이 아니라 <b>예외</b>로 알린다. 둘을 같은 값으로 뭉치면 일시적 장애 한 번이
    /// 살아 있는 환경을 사라진 것으로 굳혀 버린다.
    /// </summary>
    Task<IExecutionEnvironment?> ReconnectEnvironmentAsync(
        string environmentId,
        RuntimeConfig config,
        CancellationToken cancellationToken = default);
}
