"""
Phase 5.2: Prometheus 메트릭 수집

코드 실행, API 요청, 워커 상태 등의 메트릭 수집
"""

from prometheus_client import Counter, Gauge, Histogram, Info

# 애플리케이션 정보
APP_INFO = Info("codebeaker_app", "Application information")

# 코드 실행 메트릭
CODE_EXECUTIONS_TOTAL = Counter(
    "codebeaker_executions_total",
    "Total number of code executions",
    ["language", "status"],  # labels: language (python, javascript, csharp), status (success, failure, timeout)
)

CODE_EXECUTION_DURATION = Histogram(
    "codebeaker_execution_duration_seconds",
    "Code execution duration in seconds",
    ["language"],
    buckets=[0.1, 0.5, 1.0, 2.0, 5.0, 10.0, 30.0, 60.0],  # 100ms ~ 60s
)

# API 요청 메트릭
HTTP_REQUESTS_TOTAL = Counter(
    "codebeaker_http_requests_total",
    "Total HTTP requests",
    ["method", "endpoint", "status_code"],
)

HTTP_REQUEST_DURATION = Histogram(
    "codebeaker_http_request_duration_seconds",
    "HTTP request duration in seconds",
    ["method", "endpoint"],
    buckets=[0.01, 0.05, 0.1, 0.5, 1.0, 2.0, 5.0],  # 10ms ~ 5s
)

# 활성 요청 게이지
ACTIVE_REQUESTS = Gauge(
    "codebeaker_active_requests",
    "Number of active HTTP requests",
)

# 큐 메트릭
QUEUE_SIZE = Gauge(
    "codebeaker_queue_size",
    "Number of tasks in queue",
    ["queue_type"],  # labels: pending, processing
)

QUEUE_TASKS_TOTAL = Counter(
    "codebeaker_queue_tasks_total",
    "Total number of queued tasks",
    ["status"],  # labels: submitted, completed, failed
)

# 워커 메트릭
WORKER_ACTIVE = Gauge(
    "codebeaker_worker_active",
    "Number of active workers",
)

WORKER_TASKS_TOTAL = Counter(
    "codebeaker_worker_tasks_total",
    "Total number of tasks processed by workers",
    ["worker_id", "status"],
)

# 리소스 사용량 메트릭
MEMORY_USAGE_BYTES = Gauge(
    "codebeaker_memory_usage_bytes",
    "Memory usage in bytes",
    ["container_id"],
)

CPU_USAGE_PERCENT = Gauge(
    "codebeaker_cpu_usage_percent",
    "CPU usage percentage",
    ["container_id"],
)


def init_metrics(version: str = "unknown") -> None:
    """
    메트릭 초기화

    Args:
        version: 애플리케이션 버전
    """
    APP_INFO.info({"version": version})


def record_execution(language: str, duration_seconds: float, status: str) -> None:
    """
    코드 실행 메트릭 기록

    Args:
        language: 실행 언어 (python, javascript, csharp)
        duration_seconds: 실행 시간 (초)
        status: 실행 상태 (success, failure, timeout)
    """
    CODE_EXECUTIONS_TOTAL.labels(language=language, status=status).inc()
    CODE_EXECUTION_DURATION.labels(language=language).observe(duration_seconds)


def record_http_request(
    method: str, endpoint: str, status_code: int, duration_seconds: float
) -> None:
    """
    HTTP 요청 메트릭 기록

    Args:
        method: HTTP 메서드 (GET, POST, etc.)
        endpoint: 엔드포인트 경로
        status_code: HTTP 상태 코드
        duration_seconds: 요청 처리 시간 (초)
    """
    HTTP_REQUESTS_TOTAL.labels(
        method=method, endpoint=endpoint, status_code=status_code
    ).inc()
    HTTP_REQUEST_DURATION.labels(method=method, endpoint=endpoint).observe(
        duration_seconds
    )
