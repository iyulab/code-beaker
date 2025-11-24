using CodeBeaker.Core.Interfaces;

namespace CodeBeaker.Core.Runtime;

/// <summary>
/// 개발환경에 따라 최적의 런타임을 선택하는 전략 클래스
/// </summary>
public sealed class RuntimeSelector
{
    private readonly Dictionary<string, List<IExecutionRuntime>> _runtimeMapping;
    private readonly RuntimePreference _defaultPreference;

    public RuntimeSelector(
        IEnumerable<IExecutionRuntime> availableRuntimes,
        RuntimePreference defaultPreference = RuntimePreference.Balanced)
    {
        _defaultPreference = defaultPreference;
        _runtimeMapping = new Dictionary<string, List<IExecutionRuntime>>(
            StringComparer.OrdinalIgnoreCase);

        // 런타임을 지원 환경별로 그룹화
        foreach (var runtime in availableRuntimes)
        {
            foreach (var env in runtime.SupportedEnvironments)
            {
                if (!_runtimeMapping.ContainsKey(env))
                {
                    _runtimeMapping[env] = new List<IExecutionRuntime>();
                }
                _runtimeMapping[env].Add(runtime);
            }
        }
    }

    /// <summary>
    /// 개발환경에 맞는 최적의 런타임 선택
    /// </summary>
    /// <param name="environment">개발환경 (python, nodejs, deno 등)</param>
    /// <param name="preference">선호 기준 (속도, 보안, 메모리 등)</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>선택된 런타임, 없으면 null</returns>
    public async Task<IExecutionRuntime?> SelectBestRuntimeAsync(
        string environment,
        RuntimePreference? preference = null,
        CancellationToken cancellationToken = default)
    {
        preference ??= _defaultPreference;

        if (!_runtimeMapping.TryGetValue(environment, out var runtimes))
        {
            return null;
        }

        // 사용 가능한 런타임만 필터링
        var availableRuntimes = new List<IExecutionRuntime>();
        foreach (var runtime in runtimes)
        {
            if (await runtime.IsAvailableAsync(cancellationToken))
            {
                availableRuntimes.Add(runtime);
            }
        }

        if (availableRuntimes.Count == 0)
        {
            return null;
        }

        // 선호도에 따라 정렬
        return preference.Value switch
        {
            RuntimePreference.Speed => SelectBySpeed(availableRuntimes),
            RuntimePreference.Security => SelectBySecurity(availableRuntimes),
            RuntimePreference.Memory => SelectByMemory(availableRuntimes),
            RuntimePreference.Balanced => SelectBalanced(availableRuntimes),
            _ => availableRuntimes[0]
        };
    }

    /// <summary>
    /// 특정 런타임 타입 강제 선택
    /// </summary>
    public async Task<IExecutionRuntime?> SelectByTypeAsync(
        RuntimeType runtimeType,
        string environment,
        CancellationToken cancellationToken = default)
    {
        if (!_runtimeMapping.TryGetValue(environment, out var runtimes))
        {
            return null;
        }

        var runtime = runtimes.FirstOrDefault(r => r.Type == runtimeType);
        if (runtime == null)
        {
            return null;
        }

        return await runtime.IsAvailableAsync(cancellationToken) ? runtime : null;
    }

    /// <summary>
    /// 사용 가능한 모든 런타임 조회
    /// </summary>
    public async Task<List<IExecutionRuntime>> GetAvailableRuntimesAsync(
        string environment,
        CancellationToken cancellationToken = default)
    {
        if (!_runtimeMapping.TryGetValue(environment, out var runtimes))
        {
            return new List<IExecutionRuntime>();
        }

        var available = new List<IExecutionRuntime>();
        foreach (var runtime in runtimes)
        {
            if (await runtime.IsAvailableAsync(cancellationToken))
            {
                available.Add(runtime);
            }
        }

        return available;
    }

    private IExecutionRuntime SelectBySpeed(List<IExecutionRuntime> runtimes)
    {
        return runtimes
            .OrderBy(r => r.GetCapabilities().StartupTimeMs)
            .First();
    }

    private IExecutionRuntime SelectBySecurity(List<IExecutionRuntime> runtimes)
    {
        return runtimes
            .OrderByDescending(r => r.GetCapabilities().IsolationLevel)
            .First();
    }

    private IExecutionRuntime SelectByMemory(List<IExecutionRuntime> runtimes)
    {
        return runtimes
            .OrderBy(r => r.GetCapabilities().MemoryOverheadMB)
            .First();
    }

    private IExecutionRuntime SelectBalanced(List<IExecutionRuntime> runtimes)
    {
        // 균형 점수 = (정규화된 속도) + (정규화된 메모리) + (격리 수준 / 2)
        var scored = runtimes.Select(r =>
        {
            var caps = r.GetCapabilities();
            var speedScore = 1000.0 / (caps.StartupTimeMs + 1); // 빠를수록 높음
            var memoryScore = 1000.0 / (caps.MemoryOverheadMB + 1); // 적을수록 높음
            var securityScore = caps.IsolationLevel / 2.0; // 격리 수준

            return new
            {
                Runtime = r,
                Score = speedScore + memoryScore + securityScore
            };
        }).ToList();

        return scored
            .OrderByDescending(x => x.Score)
            .First()
            .Runtime;
    }
}

/// <summary>
/// 런타임 선택 기준
/// </summary>
public enum RuntimePreference
{
    /// <summary>
    /// 시작 속도 우선
    /// </summary>
    Speed,

    /// <summary>
    /// 보안/격리 우선
    /// </summary>
    Security,

    /// <summary>
    /// 메모리 사용량 최소화
    /// </summary>
    Memory,

    /// <summary>
    /// 균형잡힌 선택 (기본값)
    /// </summary>
    Balanced
}
