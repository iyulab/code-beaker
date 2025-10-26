"""
Phase 3.1: REST API 서버 (비동기 실행 지원)

FastAPI를 사용한 코드 실행 API with RuntimeRegistry & TaskQueue
Phase 5.1: 구조화된 로깅 및 요청 추적 추가
"""

from fastapi import FastAPI, HTTPException, Response
from fastapi.middleware.cors import CORSMiddleware
from prometheus_client import CONTENT_TYPE_LATEST, generate_latest

from src import __version__
from src.api.middleware import LoggingMiddleware, RequestIDMiddleware
from src.api.models import (
    AsyncExecuteResponse,
    ExecuteRequest,
    ExecuteResponse,
    ExecutionStatusResponse,
    HealthResponse,
)
from src.common.logging_config import configure_logging, get_logger
from src.common.metrics import init_metrics, record_execution
from src.common.models import ExecutionConfig
from src.common.queue import TaskQueue
from src.runtime import RuntimeRegistry

# Phase 5.1: 구조화된 로깅 설정
configure_logging(log_level="INFO", json_logs=False)  # 개발 환경에서는 컬러 출력
logger = get_logger(__name__)

# Phase 5.2: Prometheus 메트릭 초기화
init_metrics(version=__version__)

# FastAPI 앱 생성
app = FastAPI(
    title="CodeBeaker API",
    description="다중 언어 코드 실행 API",
    version=__version__,
    docs_url="/docs",
    redoc_url="/redoc",
)

# Phase 5.1: 요청 ID 추적 미들웨어
app.add_middleware(RequestIDMiddleware)

# Phase 5.1: 로깅 미들웨어
app.add_middleware(LoggingMiddleware)

# CORS 미들웨어 추가 (개발 환경)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # 프로덕션에서는 제한 필요
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

logger.info("api_server_initialized", version=__version__)

# TaskQueue 인스턴스 (Phase 3.1)
try:
    task_queue = TaskQueue()
except Exception as e:
    import warnings

    warnings.warn(f"Failed to initialize TaskQueue: {e}. Async execution will not be available.")
    task_queue = None


@app.get("/", response_model=HealthResponse)
async def root():
    """루트 엔드포인트 - 헬스 체크"""
    return HealthResponse(status="healthy", version=__version__)


@app.get("/health", response_model=HealthResponse)
async def health_check():
    """헬스 체크 엔드포인트"""
    return HealthResponse(status="healthy", version=__version__)


@app.get("/languages")
async def list_languages():
    """지원하는 언어 목록 반환"""
    return {"languages": RuntimeRegistry.list_languages()}


@app.get("/metrics")
async def metrics():
    """
    Prometheus 메트릭 엔드포인트

    Returns:
        Prometheus 형식의 메트릭 데이터
    """
    return Response(content=generate_latest(), media_type=CONTENT_TYPE_LATEST)


@app.post("/execute", response_model=ExecuteResponse)
async def execute_code(request: ExecuteRequest):
    """
    코드 실행 엔드포인트

    지원 언어: python (py), 향후 확장 예정
    """
    # 언어 검증
    if not RuntimeRegistry.is_supported(request.language):
        supported = ", ".join(RuntimeRegistry.list_languages())
        raise HTTPException(
            status_code=400,
            detail=f"Unsupported language: {request.language}. Supported: {supported}",
        )

    # 실행 설정
    config = ExecutionConfig(timeout=request.timeout)

    try:
        # 런타임 가져오기
        runtime = RuntimeRegistry.get(request.language)

        # 코드 실행
        result = runtime.execute(request.code, config)

        # Phase 5.2: 메트릭 기록
        status = "success" if result.exit_code == 0 and not result.timeout else (
            "timeout" if result.timeout else "failure"
        )
        record_execution(
            language=runtime.get_language_name(),
            duration_seconds=result.duration_ms / 1000.0,
            status=status,
        )

        # 응답 생성
        return ExecuteResponse(
            success=(result.exit_code == 0 and not result.timeout),
            stdout=result.stdout,
            stderr=result.stderr,
            exit_code=result.exit_code,
            duration_ms=result.duration_ms,
            timeout=result.timeout,
            error_type=result.error_type,
        )

    except ValueError as e:
        # 지원하지 않는 언어
        raise HTTPException(status_code=400, detail=str(e)) from e

    except Exception as e:
        # 예상치 못한 에러
        raise HTTPException(
            status_code=500, detail=f"Internal server error: {str(e)}"
        ) from e


@app.post("/execute/async", response_model=AsyncExecuteResponse)
async def execute_code_async(request: ExecuteRequest):
    """
    비동기 코드 실행 엔드포인트 (Phase 3.1)

    작업을 큐에 제출하고 즉시 실행 ID를 반환합니다.
    """
    if task_queue is None:
        raise HTTPException(
            status_code=503,
            detail="Task queue not available. Please check Redis connection.",
        )

    # 언어 검증
    if not RuntimeRegistry.is_supported(request.language):
        supported = ", ".join(RuntimeRegistry.list_languages())
        raise HTTPException(
            status_code=400,
            detail=f"Unsupported language: {request.language}. Supported: {supported}",
        )

    # 실행 설정
    config = ExecutionConfig(timeout=request.timeout)

    try:
        # 작업 큐에 제출
        execution_id = task_queue.submit_task(request.code, request.language, config)

        return AsyncExecuteResponse(execution_id=execution_id, status="queued")

    except Exception as e:
        # 예상치 못한 에러
        raise HTTPException(
            status_code=500, detail=f"Failed to submit task: {str(e)}"
        ) from e


@app.get("/execution/{execution_id}", response_model=ExecutionStatusResponse)
async def get_execution_status(execution_id: str):
    """
    실행 상태 조회 엔드포인트 (Phase 3.1)

    실행 ID로 작업의 현재 상태를 조회합니다.
    """
    if task_queue is None:
        raise HTTPException(
            status_code=503,
            detail="Task queue not available. Please check Redis connection.",
        )

    try:
        # 상태 조회
        status_data = task_queue.get_status(execution_id)

        if status_data is None:
            raise HTTPException(
                status_code=404, detail=f"Execution not found: {execution_id}"
            )

        # 응답 생성
        response_data = {
            "execution_id": execution_id,
            "status": status_data.get("status", "unknown"),
            "language": status_data.get("language", ""),
            "created_at": status_data.get("created_at"),
            "updated_at": status_data.get("updated_at"),
        }

        # 완료된 작업의 경우 결과 포함
        if status_data.get("status") in ["completed", "failed"]:
            response_data.update(
                {
                    "stdout": status_data.get("stdout", ""),
                    "stderr": status_data.get("stderr", ""),
                    "exit_code": int(status_data.get("exit_code", -1)),
                    "duration_ms": int(status_data.get("duration_ms", 0)),
                    "timeout": status_data.get("timeout", "False") == "True",
                    "error_type": status_data.get("error_type") or None,
                    "completed_at": status_data.get("completed_at"),
                }
            )

        return ExecutionStatusResponse(**response_data)

    except HTTPException:
        raise

    except Exception as e:
        # 예상치 못한 에러
        raise HTTPException(
            status_code=500, detail=f"Failed to get status: {str(e)}"
        ) from e


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=8000, log_level="info")
