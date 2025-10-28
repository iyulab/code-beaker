using Prometheus;

namespace CodeBeaker.API.Metrics;

/// <summary>
/// CodeBeaker 인프라 메트릭 정의
/// </summary>
public static class CodeBeakerMetrics
{
    // Execution Metrics
    public static readonly Counter ExecutionsTotal = Prometheus.Metrics.CreateCounter(
        "codebeaker_executions_total",
        "Total number of code executions",
        new CounterConfiguration
        {
            LabelNames = new[] { "language", "runtime_type", "status" }
        });

    public static readonly Histogram ExecutionDuration = Prometheus.Metrics.CreateHistogram(
        "codebeaker_execution_duration_seconds",
        "Execution duration in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "language", "runtime_type" },
            Buckets = new[] { 0.01, 0.05, 0.1, 0.5, 1, 2, 5, 10, 30 }
        });

    // Session Metrics
    public static readonly Gauge ActiveSessions = Prometheus.Metrics.CreateGauge(
        "codebeaker_active_sessions",
        "Number of currently active sessions",
        new GaugeConfiguration
        {
            LabelNames = new[] { "runtime_type" }
        });

    public static readonly Counter SessionsCreated = Prometheus.Metrics.CreateCounter(
        "codebeaker_sessions_created_total",
        "Total number of sessions created",
        new CounterConfiguration
        {
            LabelNames = new[] { "runtime_type", "language" }
        });

    public static readonly Counter SessionsClosed = Prometheus.Metrics.CreateCounter(
        "codebeaker_sessions_closed_total",
        "Total number of sessions closed",
        new CounterConfiguration
        {
            LabelNames = new[] { "runtime_type", "reason" }
        });

    public static readonly Histogram SessionLifetime = Prometheus.Metrics.CreateHistogram(
        "codebeaker_session_lifetime_seconds",
        "Session lifetime in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "runtime_type" },
            Buckets = new[] { 60.0, 300.0, 600.0, 1800.0, 3600.0, 7200.0 } // 1min ~ 2hours
        });

    // Runtime Metrics
    public static readonly Gauge RuntimesAvailable = Prometheus.Metrics.CreateGauge(
        "codebeaker_runtimes_available",
        "Number of available runtimes",
        new GaugeConfiguration
        {
            LabelNames = new[] { "runtime_type" }
        });

    public static readonly Histogram EnvironmentCreationDuration = Prometheus.Metrics.CreateHistogram(
        "codebeaker_environment_creation_duration_seconds",
        "Environment creation duration in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "runtime_type" },
            Buckets = new[] { 0.05, 0.1, 0.5, 1, 2, 5, 10 }
        });

    // Queue Metrics
    public static readonly Gauge QueueDepth = Prometheus.Metrics.CreateGauge(
        "codebeaker_queue_depth",
        "Number of tasks in queue");

    public static readonly Counter TasksSubmitted = Prometheus.Metrics.CreateCounter(
        "codebeaker_tasks_submitted_total",
        "Total number of tasks submitted to queue",
        new CounterConfiguration
        {
            LabelNames = new[] { "language" }
        });

    public static readonly Counter TasksCompleted = Prometheus.Metrics.CreateCounter(
        "codebeaker_tasks_completed_total",
        "Total number of tasks completed",
        new CounterConfiguration
        {
            LabelNames = new[] { "language", "status" }
        });

    // Storage Metrics
    public static readonly Counter StorageOperations = Prometheus.Metrics.CreateCounter(
        "codebeaker_storage_operations_total",
        "Total number of storage operations",
        new CounterConfiguration
        {
            LabelNames = new[] { "operation" } // save, get, update
        });

    public static readonly Histogram StorageOperationDuration = Prometheus.Metrics.CreateHistogram(
        "codebeaker_storage_operation_duration_seconds",
        "Storage operation duration in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "operation" },
            Buckets = new[] { 0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1 }
        });

    // Error Metrics
    public static readonly Counter ErrorsTotal = Prometheus.Metrics.CreateCounter(
        "codebeaker_errors_total",
        "Total number of errors",
        new CounterConfiguration
        {
            LabelNames = new[] { "error_type", "component" }
        });

    // Resource Metrics (인프라 레벨)
    public static readonly Gauge MemoryUsage = Prometheus.Metrics.CreateGauge(
        "codebeaker_memory_usage_bytes",
        "Memory usage in bytes");

    public static readonly Counter RestartCount = Prometheus.Metrics.CreateCounter(
        "codebeaker_restart_total",
        "Total number of application restarts");

    // Phase 8: Resource Monitoring Metrics
    public static readonly Gauge SessionMemoryUsage = Prometheus.Metrics.CreateGauge(
        "codebeaker_session_memory_usage_bytes",
        "Session memory usage in bytes",
        new GaugeConfiguration
        {
            LabelNames = new[] { "session_id", "runtime_type" }
        });

    public static readonly Gauge SessionCpuUsage = Prometheus.Metrics.CreateGauge(
        "codebeaker_session_cpu_usage_percent",
        "Session CPU usage percentage",
        new GaugeConfiguration
        {
            LabelNames = new[] { "session_id", "runtime_type" }
        });

    public static readonly Gauge SessionDiskUsage = Prometheus.Metrics.CreateGauge(
        "codebeaker_session_disk_usage_bytes",
        "Session disk usage in bytes",
        new GaugeConfiguration
        {
            LabelNames = new[] { "session_id", "runtime_type" }
        });

    public static readonly Gauge SessionNetworkRx = Prometheus.Metrics.CreateGauge(
        "codebeaker_session_network_rx_bytes",
        "Session network received bytes",
        new GaugeConfiguration
        {
            LabelNames = new[] { "session_id", "runtime_type" }
        });

    public static readonly Gauge SessionNetworkTx = Prometheus.Metrics.CreateGauge(
        "codebeaker_session_network_tx_bytes",
        "Session network transmitted bytes",
        new GaugeConfiguration
        {
            LabelNames = new[] { "session_id", "runtime_type" }
        });

    public static readonly Counter ResourceViolations = Prometheus.Metrics.CreateCounter(
        "codebeaker_resource_violations_total",
        "Total number of resource limit violations",
        new CounterConfiguration
        {
            LabelNames = new[] { "session_id", "resource_type", "severity" }
        });

    // Phase 8: Cache Metrics
    public static readonly Counter CacheHits = Prometheus.Metrics.CreateCounter(
        "codebeaker_cache_hits_total",
        "Total number of cache hits",
        new CounterConfiguration
        {
            LabelNames = new[] { "cache_type" }
        });

    public static readonly Counter CacheMisses = Prometheus.Metrics.CreateCounter(
        "codebeaker_cache_misses_total",
        "Total number of cache misses",
        new CounterConfiguration
        {
            LabelNames = new[] { "cache_type" }
        });

    public static readonly Gauge CacheSize = Prometheus.Metrics.CreateGauge(
        "codebeaker_cache_size",
        "Current number of items in cache",
        new GaugeConfiguration
        {
            LabelNames = new[] { "cache_type" }
        });

    public static readonly Gauge CacheHitRate = Prometheus.Metrics.CreateGauge(
        "codebeaker_cache_hit_rate",
        "Cache hit rate (0.0 to 1.0)",
        new GaugeConfiguration
        {
            LabelNames = new[] { "cache_type" }
        });

    // Phase 8: Background Service Health
    public static readonly Gauge BackgroundServiceHealth = Prometheus.Metrics.CreateGauge(
        "codebeaker_background_service_health",
        "Background service health status (1=healthy, 0=unhealthy)",
        new GaugeConfiguration
        {
            LabelNames = new[] { "service_name" }
        });

    public static readonly Counter BackgroundServiceErrors = Prometheus.Metrics.CreateCounter(
        "codebeaker_background_service_errors_total",
        "Total number of background service errors",
        new CounterConfiguration
        {
            LabelNames = new[] { "service_name", "error_type" }
        });

    // Phase 8: Docker Connection Monitoring
    public static readonly Gauge DockerConnectionHealth = Prometheus.Metrics.CreateGauge(
        "codebeaker_docker_connection_health",
        "Docker daemon connection health (1=connected, 0=disconnected)");

    public static readonly Counter DockerReconnections = Prometheus.Metrics.CreateCounter(
        "codebeaker_docker_reconnections_total",
        "Total number of Docker daemon reconnections");

    public static readonly Counter DockerZombieContainersCleanup = Prometheus.Metrics.CreateCounter(
        "codebeaker_docker_zombie_containers_cleaned_total",
        "Total number of zombie containers cleaned up");

    /// <summary>
    /// 초기화 시 RestartCount 증가
    /// </summary>
    public static void RecordRestart()
    {
        RestartCount.Inc();
    }

    /// <summary>
    /// 주기적인 메트릭 업데이트
    /// </summary>
    public static void UpdateMemoryMetrics()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        MemoryUsage.Set(process.WorkingSet64);
    }
}
