namespace CodeBeaker.Core.Interfaces;

/// <summary>
/// 리소스 모니터링 인터페이스
/// 실행 환경의 실시간 리소스 사용량 추적
/// </summary>
public interface IResourceMonitor
{
    /// <summary>
    /// 현재 리소스 사용량 조회
    /// </summary>
    Task<ResourceUsage> GetCurrentUsageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 리소스 제한 위반 여부 확인
    /// </summary>
    Task<ResourceViolation?> CheckViolationsAsync(
        ResourceLimits limits,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 리소스 사용 이력 (최근 N개)
    /// </summary>
    Task<List<ResourceUsage>> GetUsageHistoryAsync(
        int count = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 리소스 사용량 스냅샷
/// </summary>
public sealed class ResourceUsage
{
    /// <summary>
    /// 측정 시각
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // === Memory Usage ===

    /// <summary>
    /// 현재 메모리 사용량 (bytes)
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// 최대 메모리 사용량 (bytes)
    /// </summary>
    public long MemoryPeakBytes { get; set; }

    /// <summary>
    /// 메모리 사용률 (0.0 ~ 1.0)
    /// </summary>
    public double MemoryUsagePercent { get; set; }

    // === CPU Usage ===

    /// <summary>
    /// CPU 사용 시간 (nanoseconds)
    /// </summary>
    public long CpuUsageNanoseconds { get; set; }

    /// <summary>
    /// CPU 사용률 (0.0 ~ cores 개수)
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Throttled time (microseconds) - CPU 제한으로 인한 대기 시간
    /// </summary>
    public long CpuThrottledMicroseconds { get; set; }

    // === Disk I/O ===

    /// <summary>
    /// 디스크 읽기 바이트
    /// </summary>
    public long DiskReadBytes { get; set; }

    /// <summary>
    /// 디스크 쓰기 바이트
    /// </summary>
    public long DiskWriteBytes { get; set; }

    /// <summary>
    /// 디스크 사용량 (bytes)
    /// </summary>
    public long DiskUsageBytes { get; set; }

    // === Network I/O ===

    /// <summary>
    /// 네트워크 수신 바이트
    /// </summary>
    public long NetworkRxBytes { get; set; }

    /// <summary>
    /// 네트워크 송신 바이트
    /// </summary>
    public long NetworkTxBytes { get; set; }

    // === Process Info ===

    /// <summary>
    /// 현재 프로세스/스레드 개수
    /// </summary>
    public int ProcessCount { get; set; }

    /// <summary>
    /// 파일 디스크립터 개수
    /// </summary>
    public int FileDescriptorCount { get; set; }
}

/// <summary>
/// 리소스 제한 위반 정보
/// </summary>
public sealed class ResourceViolation
{
    /// <summary>
    /// 위반 발생 시각
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 위반 유형
    /// </summary>
    public ResourceViolationType Type { get; set; }

    /// <summary>
    /// 현재 사용량
    /// </summary>
    public long CurrentValue { get; set; }

    /// <summary>
    /// 제한값
    /// </summary>
    public long LimitValue { get; set; }

    /// <summary>
    /// 심각도 (0.0 ~ 1.0)
    /// </summary>
    public double Severity { get; set; }

    /// <summary>
    /// 설명 메시지
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 자동 종료 여부
    /// </summary>
    public bool ShouldTerminate { get; set; }
}

/// <summary>
/// 리소스 위반 유형
/// </summary>
public enum ResourceViolationType
{
    /// <summary>
    /// 메모리 제한 초과
    /// </summary>
    MemoryLimit,

    /// <summary>
    /// 메모리 경고 임계값
    /// </summary>
    MemoryWarning,

    /// <summary>
    /// CPU 할당량 초과
    /// </summary>
    CpuQuota,

    /// <summary>
    /// 디스크 할당량 초과
    /// </summary>
    DiskQuota,

    /// <summary>
    /// 디스크 I/O 속도 초과
    /// </summary>
    DiskIo,

    /// <summary>
    /// 네트워크 대역폭 초과
    /// </summary>
    NetworkBandwidth,

    /// <summary>
    /// 프로세스 개수 초과
    /// </summary>
    ProcessCount,

    /// <summary>
    /// 실행 시간 초과
    /// </summary>
    ExecutionTimeout,

    /// <summary>
    /// Idle 시간 초과
    /// </summary>
    IdleTimeout
}
