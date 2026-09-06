using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Storage;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace CodeBeaker.Core.Tests.Storage;

/// <summary>
/// 두 <see cref="ISessionStore"/> 구현이 같은 계약을 지키는지 나란히 확인한다.
///
/// 분산 배포에서 실제로 쓰이는 것은 Redis 구현인데 검증은 전부 인메모리 구현으로만
/// 이뤄져 있었다. 두 구현이 조용히 갈라지면 단일 인스턴스에서는 보이지 않고
/// 다중 인스턴스에서만 드러난다.
/// </summary>
public class SessionStoreContractTests
{
    private static SessionData NewSession(string id = "s-1") => new()
    {
        SessionId = id,
        ContainerId = "container-1",
        EnvironmentId = "container-1",
        RuntimeType = RuntimeType.Docker,
        Language = "python",
        CreatedAt = DateTime.UtcNow.AddMinutes(-5),
        LastActivity = DateTime.UtcNow.AddMinutes(-5),
        State = "Idle",
        ExecutionCount = 7,
        Config = new SessionConfigData
        {
            Language = "python",
            IdleTimeoutMinutes = 30,
            MaxLifetimeMinutes = 120
        }
    };

    /// <summary>
    /// 실제 Redis 없이 <see cref="RedisSessionStore"/>를 세운다. 이 클래스가 쓰는
    /// <see cref="IDatabase"/> 표면만 흉내내고, 문자열 키/값을 사전에 담는다.
    /// </summary>
    private sealed class FakeRedis
    {
        public readonly Dictionary<string, string> Values = new();
        public readonly Dictionary<string, TimeSpan?> Expiries = new();
        public readonly IConnectionMultiplexer Multiplexer;

        public FakeRedis()
        {
            var db = new Mock<IDatabase>(MockBehavior.Strict);

            db.Setup(d => d.StringSetAsync(
                    It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                    It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .Returns((RedisKey k, RedisValue v, TimeSpan? e, bool _, When when, CommandFlags __) =>
                {
                    if (when == When.NotExists && Values.ContainsKey(k!))
                    {
                        return Task.FromResult(false);
                    }

                    Values[k!] = v!;
                    Expiries[k!] = e;
                    return Task.FromResult(true);
                });

            // The lock path calls the four-parameter overload (no CommandFlags), which
            // is a different method than the one the save path resolves to — a strict
            // mock is what makes that visible instead of silently answering `false`.
            db.Setup(d => d.StringSetAsync(
                    It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                    It.IsAny<When>()))
                .Returns((RedisKey k, RedisValue v, TimeSpan? e, When when) =>
                {
                    if (when == When.NotExists && Values.ContainsKey(k!))
                    {
                        return Task.FromResult(false);
                    }

                    Values[k!] = v!;
                    Expiries[k!] = e;
                    return Task.FromResult(true);
                });

            // Lock release runs a compare-and-delete script.
            db.Setup(d => d.ScriptEvaluateAsync(
                    It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .Returns((string _, RedisKey[] keys, RedisValue[] values, CommandFlags __) =>
                {
                    var key = keys[0]!;
                    if (Values.TryGetValue(key, out var held) && held == values[0]!)
                    {
                        Values.Remove(key);
                        return Task.FromResult(RedisResult.Create(1));
                    }

                    return Task.FromResult(RedisResult.Create(0));
                });

            db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns((RedisKey k, CommandFlags _) => Task.FromResult(
                    Values.TryGetValue(k!, out var v) ? (RedisValue)v : RedisValue.Null));

            db.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns((RedisKey k, CommandFlags _) => Task.FromResult(Values.Remove(k!)));

            db.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns((RedisKey k, CommandFlags _) => Task.FromResult(Values.ContainsKey(k!)));

            var mux = new Mock<IConnectionMultiplexer>();
            mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);
            Multiplexer = mux.Object;
        }
    }

    public static TheoryData<string> StoreKinds => new() { "memory", "redis" };

    private static (ISessionStore store, FakeRedis? redis) CreateStore(string kind)
    {
        if (kind == "memory")
        {
            return (new InMemorySessionStore(), null);
        }

        var fake = new FakeRedis();
        return (new RedisSessionStore(fake.Multiplexer), fake);
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public async Task SaveThenGet_RoundTripsTheSession(string kind)
    {
        var (store, _) = CreateStore(kind);
        var session = NewSession();

        await store.SaveSessionAsync(session);
        var loaded = await store.GetSessionAsync(session.SessionId);

        Assert.NotNull(loaded);
        Assert.Equal(session.SessionId, loaded!.SessionId);
        Assert.Equal(session.ContainerId, loaded.ContainerId);
        Assert.Equal(session.ExecutionCount, loaded.ExecutionCount);
        Assert.Equal(session.State, loaded.State);
        Assert.Equal(session.Config.IdleTimeoutMinutes, loaded.Config.IdleTimeoutMinutes);
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public async Task GetSession_ReturnsNull_ForUnknownId(string kind)
    {
        var (store, _) = CreateStore(kind);

        Assert.Null(await store.GetSessionAsync("no-such-session"));
        Assert.False(await store.ExistsAsync("no-such-session"));
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public async Task RemoveSession_MakesItGone(string kind)
    {
        var (store, _) = CreateStore(kind);
        var session = NewSession();
        await store.SaveSessionAsync(session);

        Assert.True(await store.RemoveSessionAsync(session.SessionId));
        Assert.Null(await store.GetSessionAsync(session.SessionId));
        Assert.False(await store.ExistsAsync(session.SessionId));
    }

    /// <summary>
    /// 계약: 이 메서드는 이름 그대로 활동 시각만 갱신한다. 실행 횟수와 상태는
    /// 호출자가 소유하며, 저장소가 함께 건드리면 호출자가 뒤이어 저장하는
    /// 스냅샷에 덮어써져 두 소유자 중 하나가 조용히 진다.
    /// </summary>
    [Theory]
    [MemberData(nameof(StoreKinds))]
    public async Task UpdateActivity_TouchesOnlyTheActivityTimestamp(string kind)
    {
        var (store, _) = CreateStore(kind);
        var session = NewSession();
        var before = session.LastActivity;
        await store.SaveSessionAsync(session);

        await store.UpdateActivityAsync(session.SessionId);

        var loaded = await store.GetSessionAsync(session.SessionId);
        Assert.NotNull(loaded);
        Assert.True(loaded!.LastActivity > before);
        Assert.Equal(7, loaded.ExecutionCount);
        Assert.Equal("Idle", loaded.State);
    }

    [Fact]
    public async Task RedisStore_PrefixesKeysAndExpiresOnTheShorterLimit()
    {
        var fake = new FakeRedis();
        var store = new RedisSessionStore(fake.Multiplexer);
        var session = NewSession("prefixed");

        await store.SaveSessionAsync(session);

        var key = Assert.Single(fake.Values.Keys);
        Assert.Equal("codebeaker:session:prefixed", key);

        // Config: idle 30m, max lifetime 120m — the shorter one wins.
        Assert.Equal(TimeSpan.FromMinutes(30), fake.Expiries[key]);
    }

    [Fact]
    public async Task RedisLock_IsHeldExclusivelyAndReleasedOnDispose()
    {
        var fake = new FakeRedis();
        var store = new RedisSessionStore(fake.Multiplexer);

        var first = await store.AcquireLockAsync("resource", TimeSpan.FromMilliseconds(200));
        Assert.NotNull(first);
        Assert.Contains("codebeaker:session:lock:resource", fake.Values.Keys);

        // The lock key is taken, so a second acquisition gives up when the timeout elapses.
        var second = await store.AcquireLockAsync("resource", TimeSpan.FromMilliseconds(100));
        Assert.Null(second);

        // Releasing it hands the lock to the next caller.
        await first!.DisposeAsync();
        Assert.DoesNotContain("codebeaker:session:lock:resource", fake.Values.Keys);

        var third = await store.AcquireLockAsync("resource", TimeSpan.FromMilliseconds(200));
        Assert.NotNull(third);
    }
}
