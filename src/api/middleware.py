"""
Phase 5.1: API 미들웨어

요청 ID 추적 및 성능 메트릭 로깅
Phase 5.2: Prometheus 메트릭 수집 추가
"""

import time
import uuid
from typing import Callable

import structlog
from fastapi import Request, Response
from starlette.middleware.base import BaseHTTPMiddleware

from src.common.metrics import (
    ACTIVE_REQUESTS,
    record_http_request,
)


class RequestIDMiddleware(BaseHTTPMiddleware):
    """
    요청 ID 추적 미들웨어

    각 HTTP 요청에 고유 ID를 부여하고 로그 컨텍스트에 추가
    """

    async def dispatch(self, request: Request, call_next: Callable) -> Response:
        """
        요청 처리 및 ID 추적

        Args:
            request: HTTP 요청
            call_next: 다음 미들웨어/핸들러

        Returns:
            HTTP 응답
        """
        # 요청 ID 생성 (클라이언트가 제공하거나 새로 생성)
        request_id = request.headers.get("X-Request-ID", str(uuid.uuid4()))

        # structlog 컨텍스트에 요청 ID 추가
        structlog.contextvars.clear_contextvars()
        structlog.contextvars.bind_contextvars(
            request_id=request_id,
            method=request.method,
            path=request.url.path,
            client_ip=request.client.host if request.client else None,
        )

        # 응답 헤더에 요청 ID 추가
        response = await call_next(request)
        response.headers["X-Request-ID"] = request_id

        return response


class LoggingMiddleware(BaseHTTPMiddleware):
    """
    로깅 및 성능 메트릭 미들웨어

    요청/응답 로그 및 실행 시간 측정
    """

    def __init__(self, app):
        super().__init__(app)
        self.logger = structlog.get_logger(__name__)

    async def dispatch(self, request: Request, call_next: Callable) -> Response:
        """
        요청 처리 및 로깅

        Args:
            request: HTTP 요청
            call_next: 다음 미들웨어/핸들러

        Returns:
            HTTP 응답
        """
        start_time = time.time()

        # Phase 5.2: 활성 요청 카운터 증가
        ACTIVE_REQUESTS.inc()

        # 요청 로그
        self.logger.info(
            "request_started",
            method=request.method,
            path=request.url.path,
            query_params=dict(request.query_params),
        )

        try:
            # 요청 처리
            response = await call_next(request)
            duration_seconds = time.time() - start_time
            duration_ms = int(duration_seconds * 1000)

            # Phase 5.2: Prometheus 메트릭 기록
            record_http_request(
                method=request.method,
                endpoint=request.url.path,
                status_code=response.status_code,
                duration_seconds=duration_seconds,
            )

            # 성공 로그
            self.logger.info(
                "request_completed",
                status_code=response.status_code,
                duration_ms=duration_ms,
            )

            # 성능 메트릭을 응답 헤더에 추가
            response.headers["X-Response-Time"] = f"{duration_ms}ms"

            return response

        except Exception as e:
            duration_seconds = time.time() - start_time
            duration_ms = int(duration_seconds * 1000)

            # Phase 5.2: 에러도 메트릭으로 기록 (5xx 상태)
            record_http_request(
                method=request.method,
                endpoint=request.url.path,
                status_code=500,
                duration_seconds=duration_seconds,
            )

            # 에러 로그
            self.logger.error(
                "request_failed",
                error_type=type(e).__name__,
                error_message=str(e),
                duration_ms=duration_ms,
                exc_info=True,
            )

            raise

        finally:
            # Phase 5.2: 활성 요청 카운터 감소
            ACTIVE_REQUESTS.dec()
