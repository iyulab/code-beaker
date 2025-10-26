"""API 요청/응답 모델"""

from typing import Optional

from pydantic import BaseModel, ConfigDict, Field


class ExecuteRequest(BaseModel):
    """코드 실행 요청"""

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "code": 'print("Hello, CodeBeaker!")',
                "language": "python",
                "timeout": 5,
            }
        }
    )

    code: str = Field(..., max_length=100000, description="실행할 코드 (최대 100KB)")
    language: str = Field(default="python", description="프로그래밍 언어")
    timeout: int = Field(default=5, ge=1, le=30, description="타임아웃 (초, 1-30)")


class ExecuteResponse(BaseModel):
    """코드 실행 응답"""

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "success": True,
                "stdout": "Hello, CodeBeaker!\n",
                "stderr": "",
                "exit_code": 0,
                "duration_ms": 45,
                "timeout": False,
                "error_type": None,
            }
        }
    )

    success: bool = Field(..., description="실행 성공 여부")
    stdout: str = Field(default="", description="표준 출력")
    stderr: str = Field(default="", description="표준 에러")
    exit_code: int = Field(..., description="종료 코드")
    duration_ms: Optional[int] = Field(None, description="실행 시간 (밀리초)")
    timeout: bool = Field(default=False, description="타임아웃 발생 여부")
    error_type: Optional[str] = Field(None, description="에러 타입")


class HealthResponse(BaseModel):
    """헬스 체크 응답"""

    status: str = Field(..., description="서비스 상태")
    version: str = Field(..., description="버전")


class AsyncExecuteResponse(BaseModel):
    """비동기 코드 실행 응답 (Phase 3.1)"""

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "execution_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                "status": "queued",
            }
        }
    )

    execution_id: str = Field(..., description="실행 ID")
    status: str = Field(..., description="실행 상태 (queued)")


class ExecutionStatusResponse(BaseModel):
    """실행 상태 조회 응답 (Phase 3.1)"""

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "execution_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                "status": "completed",
                "language": "python",
                "stdout": "Hello, World!\n",
                "stderr": "",
                "exit_code": 0,
                "duration_ms": 125,
                "timeout": False,
                "created_at": "2025-01-15T10:30:00Z",
                "completed_at": "2025-01-15T10:30:01Z",
            }
        }
    )

    execution_id: str = Field(..., description="실행 ID")
    status: str = Field(..., description="실행 상태 (queued, running, completed, failed)")
    language: str = Field(..., description="프로그래밍 언어")
    stdout: Optional[str] = Field(None, description="표준 출력")
    stderr: Optional[str] = Field(None, description="표준 에러")
    exit_code: Optional[int] = Field(None, description="종료 코드")
    duration_ms: Optional[int] = Field(None, description="실행 시간 (밀리초)")
    timeout: Optional[bool] = Field(None, description="타임아웃 발생 여부")
    error_type: Optional[str] = Field(None, description="에러 타입")
    created_at: Optional[str] = Field(None, description="생성 시간")
    updated_at: Optional[str] = Field(None, description="업데이트 시간")
    completed_at: Optional[str] = Field(None, description="완료 시간")
