using CodeBeaker.Core.Interfaces;

namespace CodeBeaker.Core.Monitoring;

/// <summary>
/// 리소스 사용량 스냅샷의 고정 용량 이력.
/// 용량을 넘으면 가장 오래된 항목부터 밀려난다.
/// 백그라운드 모니터링(기록)과 조회가 서로 다른 스레드에서 일어나므로 잠금으로 보호한다.
/// </summary>
public sealed class ResourceUsageHistory
{
    public const int DefaultCapacity = 100;

    private readonly int _capacity;
    private readonly Queue<ResourceUsage> _items;

    public ResourceUsageHistory(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                "History capacity must be positive.");
        }

        _capacity = capacity;
        _items = new Queue<ResourceUsage>(capacity);
    }

    /// <summary>
    /// 스냅샷 한 건을 기록한다.
    /// </summary>
    public void Record(ResourceUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);

        lock (_items)
        {
            _items.Enqueue(usage);

            while (_items.Count > _capacity)
            {
                _items.Dequeue();
            }
        }
    }

    /// <summary>
    /// 최근 <paramref name="count"/>건을 오래된 것부터 반환한다.
    /// </summary>
    public List<ResourceUsage> Recent(int count)
    {
        if (count <= 0)
        {
            return new List<ResourceUsage>();
        }

        lock (_items)
        {
            var skip = Math.Max(0, _items.Count - count);
            return _items.Skip(skip).ToList();
        }
    }
}
