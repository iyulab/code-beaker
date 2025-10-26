"""
Phase 5.1: 구조화된 로깅 설정

structlog을 사용한 JSON 로깅 및 컨텍스트 추적
"""

import logging
import sys
from typing import Any

import structlog
from structlog.types import EventDict, Processor


def add_app_context(logger: Any, method_name: str, event_dict: EventDict) -> EventDict:
    """
    애플리케이션 컨텍스트 정보 추가

    Args:
        logger: 로거 인스턴스
        method_name: 로깅 메서드 이름
        event_dict: 이벤트 딕셔너리

    Returns:
        업데이트된 이벤트 딕셔너리
    """
    event_dict["app"] = "codebeaker"
    event_dict["environment"] = "development"  # TODO: 환경 변수에서 가져오기
    return event_dict


def configure_logging(log_level: str = "INFO", json_logs: bool = True) -> None:
    """
    구조화된 로깅 설정

    Args:
        log_level: 로그 레벨 (DEBUG, INFO, WARNING, ERROR, CRITICAL)
        json_logs: JSON 포맷 사용 여부
    """
    # 표준 logging 설정
    logging.basicConfig(
        format="%(message)s",
        stream=sys.stdout,
        level=getattr(logging, log_level.upper()),
    )

    # structlog 프로세서 체인
    processors: list[Processor] = [
        # 컨텍스트 변수 병합 (요청 ID 등)
        structlog.contextvars.merge_contextvars,
        # 타임스탬프 추가
        structlog.processors.TimeStamper(fmt="iso"),
        # 로그 레벨 추가
        structlog.stdlib.add_log_level,
        # 로거 이름 추가
        structlog.stdlib.add_logger_name,
        # 애플리케이션 컨텍스트 추가
        add_app_context,
        # 스택 정보 추가 (에러 시)
        structlog.processors.StackInfoRenderer(),
        # 예외 정보 포맷팅
        structlog.processors.format_exc_info,
    ]

    if json_logs:
        # JSON 렌더러 (프로덕션)
        processors.append(structlog.processors.JSONRenderer())
    else:
        # 개발 환경용 컬러 출력
        processors.append(
            structlog.dev.ConsoleRenderer(
                colors=True,
                exception_formatter=structlog.dev.plain_traceback,
            )
        )

    # structlog 설정
    structlog.configure(
        processors=processors,
        wrapper_class=structlog.stdlib.BoundLogger,
        context_class=dict,
        logger_factory=structlog.stdlib.LoggerFactory(),
        cache_logger_on_first_use=True,
    )


def get_logger(name: str) -> structlog.stdlib.BoundLogger:
    """
    구조화된 로거 가져오기

    Args:
        name: 로거 이름 (보통 __name__ 사용)

    Returns:
        structlog 로거
    """
    return structlog.get_logger(name)
