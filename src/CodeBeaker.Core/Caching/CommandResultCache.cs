using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using CodeBeaker.Commands.Models;

namespace CodeBeaker.Core.Caching;

/// <summary>
/// 명령 실행 결과 캐싱 (Phase 7)
/// 동일한 코드 실행 시 캐시된 결과 반환으로 성능 향상
/// </summary>
public sealed class CommandResultCache
{
    private readonly ConcurrentDictionary<string, CachedResult> _cache = new();
    private readonly TimeSpan _defaultExpiration;
    private readonly int _maxCacheSize;
    private long _hitCount;
    private long _missCount;

    public CommandResultCache(TimeSpan? defaultExpiration = null, int maxCacheSize = 1000)
    {
        _defaultExpiration = defaultExpiration ?? TimeSpan.FromMinutes(5);
        _maxCacheSize = maxCacheSize;
    }

    /// <summary>
    /// 명령 결과 캐싱
    /// </summary>
    public void Set(Command command, CommandResult result, TimeSpan? expiration = null)
    {
        var key = GenerateCacheKey(command);
        var cachedResult = new CachedResult
        {
            Result = result,
            CachedAt = DateTime.UtcNow,
            Expiration = expiration ?? _defaultExpiration
        };

        _cache.AddOrUpdate(key, cachedResult, (_, _) => cachedResult);

        // 캐시 크기 제한
        if (_cache.Count > _maxCacheSize)
        {
            EvictOldest();
        }
    }

    /// <summary>
    /// 캐시된 결과 조회
    /// </summary>
    public CommandResult? TryGet(Command command)
    {
        var key = GenerateCacheKey(command);

        if (_cache.TryGetValue(key, out var cachedResult))
        {
            // 만료 확인
            if (DateTime.UtcNow - cachedResult.CachedAt <= cachedResult.Expiration)
            {
                Interlocked.Increment(ref _hitCount);
                return cachedResult.Result;
            }

            // 만료된 항목 제거
            _cache.TryRemove(key, out _);
        }

        Interlocked.Increment(ref _missCount);
        return null;
    }

    /// <summary>
    /// 캐시 통계
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            HitCount = Interlocked.Read(ref _hitCount),
            MissCount = Interlocked.Read(ref _missCount),
            CacheSize = _cache.Count,
            HitRate = CalculateHitRate()
        };
    }

    /// <summary>
    /// 캐시 초기화
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        Interlocked.Exchange(ref _hitCount, 0);
        Interlocked.Exchange(ref _missCount, 0);
    }

    /// <summary>
    /// 캐시 키 생성 (명령 타입 + 내용 해시)
    /// </summary>
    private static string GenerateCacheKey(Command command)
    {
        var builder = new StringBuilder();
        builder.Append(command.Type);
        builder.Append(':');

        switch (command)
        {
            case ExecuteCodeCommand code:
                builder.Append(ComputeHash(code.Code));
                break;
            case ExecuteShellCommand shell:
                builder.Append(shell.CommandName);
                builder.Append(':');
                builder.Append(ComputeHash(string.Join(",", shell.Args)));
                break;
            case ReadFileCommand read:
                builder.Append(read.Path);
                break;
            default:
                builder.Append(command.GetHashCode());
                break;
        }

        return builder.ToString();
    }

    /// <summary>
    /// SHA256 해시 계산
    /// </summary>
    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16]; // 앞 16자리만 사용
    }

    /// <summary>
    /// 가장 오래된 항목 제거
    /// </summary>
    private void EvictOldest()
    {
        var oldest = _cache
            .OrderBy(kvp => kvp.Value.CachedAt)
            .Take(_maxCacheSize / 10) // 10% 제거
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in oldest)
        {
            _cache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// 캐시 히트율 계산
    /// </summary>
    private double CalculateHitRate()
    {
        var totalRequests = _hitCount + _missCount;
        return totalRequests > 0 ? (double)_hitCount / totalRequests : 0;
    }

    private sealed class CachedResult
    {
        public required CommandResult Result { get; init; }
        public required DateTime CachedAt { get; init; }
        public required TimeSpan Expiration { get; init; }
    }
}

/// <summary>
/// 캐시 통계 정보
/// </summary>
public sealed class CacheStatistics
{
    public long HitCount { get; init; }
    public long MissCount { get; init; }
    public int CacheSize { get; init; }
    public double HitRate { get; init; }
}
