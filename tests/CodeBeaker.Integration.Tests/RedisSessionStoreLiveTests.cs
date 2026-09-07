using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Storage;
using CodeBeaker.Integration.Tests.TestHelpers;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// <see cref="RedisSessionStore"/> 를 살아있는 Redis 서버에 대고 확인한다.
///
/// 단위 계약 테스트(<c>SessionStoreContractTests</c>)는 <c>IDatabase</c> 를 mock 으로 세우므로
/// 이 클래스가 서버에게 무엇을 요청하는지까지만 본다. 서버가 그 요청을 실제로 어떻게
/// 처리하는지 — TTL 이 걸리는지, SCAN 이 접두사에 맞는 키만 주는지, 락 해제 Lua 스크립트가
/// 남의 락을 건드리지 않는지 — 는 mock 이 답할 수 없는 사실이고, 분산 배포는 정확히 그것에
/// 의존한다. `StackExchange.Redis` 메이저 상승이 이 동작을 깨뜨려도 mock 테스트는 전부 통과한다.
/// </summary>
public sealed class RedisSessionStoreLiveTests : IClassFixture<RedisContainerFixture>
{
    private readonly RedisContainerFixture _redis;

    public RedisSessionStoreLiveTests(RedisContainerFixture redis)
    {
        _redis = redis;
    }

    private RedisSessionStore NewStore(string keyPrefix) =>
        new(_redis.Connection, keyPrefix);

    private void SkipIfUnavailable() => Skip.If(_redis.SkipReason is not null, _redis.SkipReason);

    private static SessionData NewSession(string id, int idleMinutes = 30, int maxMinutes = 120) => new()
    {
        SessionId = id,
        EnvironmentId = $"container-{id}",
        RuntimeType = RuntimeType.Docker,
        Language = "python",
        CreatedAt = DateTime.UtcNow.AddMinutes(-5),
        LastActivity = DateTime.UtcNow.AddMinutes(-5),
        State = "Idle",
        ExecutionCount = 7,
        Config = new SessionConfigData
        {
            Language = "python",
            IdleTimeoutMinutes = idleMinutes,
            MaxLifetimeMinutes = maxMinutes
        }
    };

    [SkippableFact]
    public async Task SaveThenGet_RoundTripsThroughTheServer()
    {
        SkipIfUnavailable();
        var store = NewStore($"live:{Guid.NewGuid():N}:");

        var saved = NewSession("s-round-trip");
        await store.SaveSessionAsync(saved);

        var loaded = await store.GetSessionAsync("s-round-trip");

        Assert.NotNull(loaded);
        Assert.Equal(saved.SessionId, loaded.SessionId);
        Assert.Equal(saved.EnvironmentId, loaded.EnvironmentId);
        Assert.Equal(saved.RuntimeType, loaded.RuntimeType);
        Assert.Equal(saved.ExecutionCount, loaded.ExecutionCount);
        Assert.Equal(saved.Config.IdleTimeoutMinutes, loaded.Config.IdleTimeoutMinutes);
        // 시각은 왕복하며 정밀도를 잃을 수 있으므로 초 단위로 본다.
        Assert.Equal(saved.CreatedAt, loaded.CreatedAt, TimeSpan.FromSeconds(1));
    }

    [SkippableFact]
    public async Task SaveSession_LetsTheServerExpireItOnTheShorterLimit()
    {
        SkipIfUnavailable();
        var prefix = $"live:{Guid.NewGuid():N}:";
        var store = NewStore(prefix);

        // idle 이 max 보다 짧으므로 TTL 은 idle 이어야 한다.
        await store.SaveSessionAsync(NewSession("s-ttl", idleMinutes: 12, maxMinutes: 240));

        var ttl = await _redis.Connection.GetDatabase().KeyTimeToLiveAsync($"{prefix}s-ttl");

        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, TimeSpan.FromMinutes(11), TimeSpan.FromMinutes(12));
    }

    [SkippableFact]
    public async Task ListSessions_ScansOnlyItsOwnPrefix()
    {
        SkipIfUnavailable();
        var mine = $"live:{Guid.NewGuid():N}:";
        var theirs = $"live:{Guid.NewGuid():N}:";
        var store = NewStore(mine);
        var neighbour = NewStore(theirs);

        await store.SaveSessionAsync(NewSession("a"));
        await store.SaveSessionAsync(NewSession("b"));
        await neighbour.SaveSessionAsync(NewSession("c"));

        var listed = await store.ListSessionsAsync();

        Assert.Equal(2, listed.Count);
        Assert.Equal(["a", "b"], listed.Select(s => s.SessionId).OrderBy(id => id));
    }

    [SkippableFact]
    public async Task RemoveAndExists_AgreeWithTheServer()
    {
        SkipIfUnavailable();
        var store = NewStore($"live:{Guid.NewGuid():N}:");
        await store.SaveSessionAsync(NewSession("s-remove"));

        Assert.True(await store.ExistsAsync("s-remove"));
        Assert.True(await store.RemoveSessionAsync("s-remove"));

        Assert.False(await store.ExistsAsync("s-remove"));
        Assert.Null(await store.GetSessionAsync("s-remove"));
        // 없는 키를 지우는 것은 실패가 아니라 "지운 것이 없음"이다.
        Assert.False(await store.RemoveSessionAsync("s-remove"));
    }

    [SkippableFact]
    public async Task AcquireLock_IsExclusiveWhileHeldAndFreeAfterRelease()
    {
        SkipIfUnavailable();
        var store = NewStore($"live:{Guid.NewGuid():N}:");

        var held = await store.AcquireLockAsync("session-a", TimeSpan.FromSeconds(2));
        Assert.NotNull(held);

        // 같은 키를 두 번째로 잡으려 하면 타임아웃까지 기다렸다가 실패해야 한다.
        var blocked = await store.AcquireLockAsync("session-a", TimeSpan.FromMilliseconds(300));
        Assert.Null(blocked);

        // 다른 키는 서로 막지 않는다.
        var other = await store.AcquireLockAsync("session-b", TimeSpan.FromSeconds(2));
        Assert.NotNull(other);
        await other.DisposeAsync();

        await held.DisposeAsync();

        var reacquired = await store.AcquireLockAsync("session-a", TimeSpan.FromSeconds(2));
        Assert.NotNull(reacquired);
        await reacquired.DisposeAsync();
    }

    [SkippableFact]
    public async Task ReleasingLock_LeavesALockTakenByAnotherHolderAlone()
    {
        SkipIfUnavailable();
        var prefix = $"live:{Guid.NewGuid():N}:";
        var store = NewStore(prefix);
        var key = $"{prefix}lock:session-c";

        var held = await store.AcquireLockAsync("session-c", TimeSpan.FromSeconds(2));
        Assert.NotNull(held);

        // 락이 만료된 뒤 다른 인스턴스가 같은 키를 잡은 상황을 만든다. 원래 소유자의
        // 해제가 토큰을 확인하지 않으면 남의 락을 지우게 되고, 그때부터 두 인스턴스가
        // 같은 세션을 동시에 만진다 — 해제 스크립트가 막아야 하는 것이 정확히 이것이다.
        var db = _redis.Connection.GetDatabase();
        await db.StringSetAsync(key, "another-holder");

        await held.DisposeAsync();

        Assert.Equal("another-holder", await db.StringGetAsync(key));
    }
}
