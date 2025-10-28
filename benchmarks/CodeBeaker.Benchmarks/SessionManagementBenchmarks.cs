using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Core.Sessions;
using CodeBeaker.Core.Storage;
using CodeBeaker.Runtimes.Deno;

namespace CodeBeaker.Benchmarks;

/// <summary>
/// 세션 관리 성능 벤치마크
/// 세션 생성, 조회, 실행, 정리 성능 측정
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class SessionManagementBenchmarks
{
    private SessionManager? _sessionManager;
    private readonly List<string> _createdSessionIds = new();

    [GlobalSetup]
    public void Setup()
    {
        var sessionStore = new InMemorySessionStore();
        var runtimes = new IExecutionRuntime[] { new DenoRuntime() };
        _sessionManager = new SessionManager(sessionStore, runtimes);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _createdSessionIds.Clear();
    }

    [Benchmark(Description = "Create Session")]
    public async Task<Session> CreateSession()
    {
        if (_sessionManager == null)
        {
            return null!;
        }

        var config = new SessionConfig
        {
            Language = "deno",
            RuntimeType = RuntimeType.Deno
        };

        var session = await _sessionManager.CreateSessionAsync(config);
        _createdSessionIds.Add(session.SessionId);

        return session;
    }

    [Benchmark(Description = "Get Session (Cache Hit)")]
    public async Task<Session?> GetSession()
    {
        if (_sessionManager == null || _createdSessionIds.Count == 0)
        {
            return null;
        }

        var sessionId = _createdSessionIds[0];
        return await _sessionManager.GetSessionAsync(sessionId);
    }

    [Benchmark(Description = "List Sessions")]
    public async Task<List<Session>> ListSessions()
    {
        if (_sessionManager == null)
        {
            return new List<Session>();
        }

        return await _sessionManager.ListSessionsAsync();
    }

    [Benchmark(Description = "Parallel Session Creation (10 sessions)")]
    public async Task ParallelSessionCreation()
    {
        if (_sessionManager == null)
        {
            return;
        }

        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var config = new SessionConfig
            {
                Language = "deno",
                RuntimeType = RuntimeType.Deno
            };

            var session = await _sessionManager.CreateSessionAsync(config);
            _createdSessionIds.Add(session.SessionId);
            return session;
        });

        await Task.WhenAll(tasks);
    }
}
