using System.Collections.Concurrent;
using CodeBeaker.Core.Interfaces;

namespace CodeBeaker.Core.Storage;

/// <summary>
/// 메모리 기반 세션 스토리지 (단일 인스턴스용)
/// 기존 ConcurrentDictionary 로직 분리
/// </summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, SessionData> _sessions = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public Task SaveSessionAsync(SessionData session, CancellationToken cancellationToken = default)
    {
        _sessions[session.SessionId] = session;
        return Task.CompletedTask;
    }

    public Task<SessionData?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<bool> RemoveSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var removed = _sessions.TryRemove(sessionId, out _);
        return Task.FromResult(removed);
    }

    public Task<List<SessionData>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        var sessions = _sessions.Values.ToList();
        return Task.FromResult(sessions);
    }

    public Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var exists = _sessions.ContainsKey(sessionId);
        return Task.FromResult(exists);
    }

    public Task UpdateActivityAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.LastActivity = DateTime.UtcNow;
            session.ExecutionCount++;
            if (session.State == "Idle")
            {
                session.State = "Active";
            }
        }
        return Task.CompletedTask;
    }

    public async Task<IAsyncDisposable?> AcquireLockAsync(
        string lockKey,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        // InMemory는 단일 프로세스이므로 SemaphoreSlim 사용
        var acquired = await _lock.WaitAsync(timeout, cancellationToken);
        if (!acquired)
        {
            return null;
        }

        return new SemaphoreLock(_lock);
    }

    private sealed class SemaphoreLock : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public SemaphoreLock(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _semaphore.Release();
                _disposed = true;
            }
            return ValueTask.CompletedTask;
        }
    }
}
