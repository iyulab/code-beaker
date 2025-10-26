"""공통 데이터 모델"""

from dataclasses import dataclass, field
from typing import Optional


@dataclass
class ExecutionResult:
    """코드 실행 결과"""

    stdout: str
    stderr: str
    exit_code: int
    duration_ms: Optional[int] = None
    memory_mb: Optional[int] = None
    timeout: bool = False
    error_type: Optional[str] = None


@dataclass
class ExecutionConfig:
    """실행 설정"""

    timeout: int = 5  # 초
    memory_limit: str = "256m"
    cpu_limit: float = 0.5
    network_enabled: bool = False
    packages: list[str] = field(default_factory=list)  # 설치할 패키지 목록
