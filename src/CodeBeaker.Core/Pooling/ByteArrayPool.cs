using System.Buffers;

namespace CodeBeaker.Core.Pooling;

/// <summary>
/// 바이트 배열 풀링 유틸리티 (Phase 7)
/// ArrayPool을 활용한 메모리 효율적 바이트 배열 관리
/// </summary>
public static class ByteArrayPool
{
    private static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared;

    /// <summary>
    /// 풀에서 바이트 배열 임대
    /// </summary>
    /// <param name="minimumLength">최소 길이</param>
    /// <returns>풀에서 임대한 배열</returns>
    public static byte[] Rent(int minimumLength)
    {
        return Pool.Rent(minimumLength);
    }

    /// <summary>
    /// 풀에 바이트 배열 반환
    /// </summary>
    /// <param name="array">반환할 배열</param>
    /// <param name="clearArray">배열 초기화 여부 (보안상 권장)</param>
    public static void Return(byte[] array, bool clearArray = true)
    {
        Pool.Return(array, clearArray);
    }

    /// <summary>
    /// 풀을 사용한 안전한 배열 작업 (using 패턴)
    /// </summary>
    public static PooledByteArray RentScoped(int minimumLength)
    {
        return new PooledByteArray(minimumLength);
    }
}

/// <summary>
/// IDisposable 패턴으로 자동 반환되는 풀 배열
/// </summary>
public readonly struct PooledByteArray : IDisposable
{
    private readonly byte[] _array;
    private readonly int _length;

    public PooledByteArray(int minimumLength)
    {
        _array = ByteArrayPool.Rent(minimumLength);
        _length = _array.Length;
    }

    /// <summary>
    /// 실제 배열 (읽기 전용)
    /// </summary>
    public byte[] Array => _array;

    /// <summary>
    /// 배열 길이
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// ArraySegment로 변환
    /// </summary>
    public ArraySegment<byte> AsSegment(int count)
    {
        return new ArraySegment<byte>(_array, 0, count);
    }

    /// <summary>
    /// Span으로 변환
    /// </summary>
    public Span<byte> AsSpan()
    {
        return _array.AsSpan(0, _length);
    }

    /// <summary>
    /// 풀에 자동 반환
    /// </summary>
    public void Dispose()
    {
        ByteArrayPool.Return(_array, clearArray: true);
    }
}
