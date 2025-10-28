using System.Text.Json;
using CodeBeaker.Core.Interfaces;
using StackExchange.Redis;

namespace CodeBeaker.Core.Storage;

/// <summary>
/// Redis 기반 분산 세션 스토리지 (수평 확장 지원)
/// </summary>
public sealed class RedisSessionStore : ISessionStore, IAsyncDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly string _keyPrefix;
    private readonly TimeSpan _defaultExpiry;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisSessionStore(
        IConnectionMultiplexer redis,
        string keyPrefix = "codebeaker:session:",
        TimeSpan? defaultExpiry = null)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _db = _redis.GetDatabase();
        _keyPrefix = keyPrefix;
        _defaultExpiry = defaultExpiry ?? TimeSpan.FromHours(2);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// 세션 키 생성
    /// </summary>
    private string GetKey(string sessionId) => $"{_keyPrefix}{sessionId}";

    public async Task SaveSessionAsync(SessionData session, CancellationToken cancellationToken = default)
    {
        var key = GetKey(session.SessionId);
        var json = JsonSerializer.Serialize(session, _jsonOptions);

        // TTL 계산 (IdleTimeout과 MaxLifetime 중 짧은 것)
        var idleTimeout = TimeSpan.FromMinutes(session.Config.IdleTimeoutMinutes);
        var maxLifetime = TimeSpan.FromMinutes(session.Config.MaxLifetimeMinutes);
        var expiry = idleTimeout < maxLifetime ? idleTimeout : maxLifetime;

        await _db.StringSetAsync(key, json, expiry);
    }

    public async Task<SessionData?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var key = GetKey(sessionId);
        var json = await _db.StringGetAsync(key);

        if (json.IsNullOrEmpty)
        {
            return null;
        }

        return JsonSerializer.Deserialize<SessionData>(json!, _jsonOptions);
    }

    public async Task<bool> RemoveSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var key = GetKey(sessionId);
        return await _db.KeyDeleteAsync(key);
    }

    public async Task<List<SessionData>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        var sessions = new List<SessionData>();
        var server = _redis.GetServer(_redis.GetEndPoints().First());

        // SCAN을 사용하여 모든 세션 키 조회
        await foreach (var key in server.KeysAsync(pattern: $"{_keyPrefix}*"))
        {
            var json = await _db.StringGetAsync(key);
            if (!json.IsNullOrEmpty)
            {
                var session = JsonSerializer.Deserialize<SessionData>(json!, _jsonOptions);
                if (session != null)
                {
                    sessions.Add(session);
                }
            }
        }

        return sessions;
    }

    public async Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var key = GetKey(sessionId);
        return await _db.KeyExistsAsync(key);
    }

    public async Task UpdateActivityAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(sessionId, cancellationToken);
        if (session != null)
        {
            session.LastActivity = DateTime.UtcNow;
            session.ExecutionCount++;
            if (session.State == "Idle")
            {
                session.State = "Active";
            }
            await SaveSessionAsync(session, cancellationToken);
        }
    }

    public async Task<IAsyncDisposable?> AcquireLockAsync(
        string lockKey,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var key = $"{_keyPrefix}lock:{lockKey}";
        var token = Guid.NewGuid().ToString();
        var expiry = timeout;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            // Redis SET NX (Set if Not eXists) 사용
            var acquired = await _db.StringSetAsync(key, token, expiry, When.NotExists);

            if (acquired)
            {
                return new RedisLock(_db, key, token);
            }

            // 짧은 대기 후 재시도
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }

        return null; // 락 획득 실패
    }

    public async ValueTask DisposeAsync()
    {
        await _redis.CloseAsync();
        _redis.Dispose();
    }

    /// <summary>
    /// Redis 분산 락
    /// </summary>
    private sealed class RedisLock : IAsyncDisposable
    {
        private readonly IDatabase _db;
        private readonly string _key;
        private readonly string _token;
        private bool _disposed;

        public RedisLock(IDatabase db, string key, string token)
        {
            _db = db;
            _key = key;
            _token = token;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                // 토큰 검증 후 삭제 (Lua 스크립트로 원자적 실행)
                var script = @"
                    if redis.call('get', KEYS[1]) == ARGV[1] then
                        return redis.call('del', KEYS[1])
                    else
                        return 0
                    end";

                await _db.ScriptEvaluateAsync(script, new RedisKey[] { _key }, new RedisValue[] { _token });
                _disposed = true;
            }
        }
    }
}
