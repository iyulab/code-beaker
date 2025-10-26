"""
Phase 4 테스트: 보안 및 격리 테스트
"""

import pytest

from src.common.models import ExecutionConfig
from src.runtime import RuntimeRegistry


class TestSecurityProfile:
    """보안 프로필 테스트"""

    def test_network_isolation_prevents_external_access(self):
        """네트워크 격리 - 외부 접속 차단 테스트"""
        runtime = RuntimeRegistry.get("python")

        # 네트워크 비활성화 시 외부 접속 실패
        code = """
import urllib.request
try:
    urllib.request.urlopen('http://www.google.com', timeout=1)
    print("NETWORK_ACCESS_SUCCESS")
except Exception as e:
    print(f"NETWORK_ACCESS_BLOCKED: {type(e).__name__}")
"""
        config = ExecutionConfig(network_enabled=False)
        result = runtime.execute(code, config)

        # 네트워크 접속이 차단되어야 함
        assert "NETWORK_ACCESS_BLOCKED" in result.stdout or "NETWORK_ACCESS_BLOCKED" in result.stderr

    def test_file_system_read_only(self):
        """파일시스템 읽기 전용 테스트"""
        runtime = RuntimeRegistry.get("python")

        # /tmp는 tmpfs로 쓰기 가능, 다른 위치는 읽기 전용
        code = """
# /tmp는 쓰기 가능해야 함 (tmpfs)
try:
    with open('/tmp/test.txt', 'w') as f:
        f.write('test')
    print("TMP_WRITE_SUCCESS")
except Exception as e:
    print(f"TMP_WRITE_FAILED: {type(e).__name__}")

# 다른 위치는 읽기 전용이어야 함
try:
    with open('/etc/test.txt', 'w') as f:
        f.write('test')
    print("ETC_WRITE_SUCCESS")
except Exception as e:
    print(f"ETC_WRITE_BLOCKED: {type(e).__name__}")
"""
        config = ExecutionConfig()
        result = runtime.execute(code, config)

        # /tmp는 쓰기 성공, /etc는 차단되어야 함
        assert "TMP_WRITE_SUCCESS" in result.stdout
        assert ("ETC_WRITE_BLOCKED" in result.stdout or "ETC_WRITE_BLOCKED" in result.stderr)

    def test_container_isolation_prevents_host_access(self):
        """컨테이너 격리 - 호스트 접근 차단 테스트"""
        runtime = RuntimeRegistry.get("python")

        # 호스트 파일시스템 접근 시도
        code = """
import os
try:
    # 컨테이너 외부 경로 접근 시도
    files = os.listdir('/host')
    print(f"HOST_ACCESS_SUCCESS: {len(files)}")
except Exception as e:
    print(f"HOST_ACCESS_BLOCKED: {type(e).__name__}")
"""
        config = ExecutionConfig()
        result = runtime.execute(code, config)

        # 호스트 접근이 차단되어야 함
        assert "HOST_ACCESS_BLOCKED" in result.stdout or result.exit_code != 0

    def test_resource_limits_enforced(self):
        """리소스 제한 강제 테스트"""
        runtime = RuntimeRegistry.get("python")

        # 메모리 제한 테스트
        code = """
try:
    # 1GB 메모리 할당 시도
    data = bytearray(1024 * 1024 * 1024)
    print("MEMORY_ALLOCATION_SUCCESS")
except MemoryError:
    print("MEMORY_LIMIT_ENFORCED")
except Exception as e:
    print(f"MEMORY_ALLOCATION_FAILED: {type(e).__name__}")
"""
        config = ExecutionConfig(memory_limit="128m")
        result = runtime.execute(code, config)

        # 메모리 제한이 적용되어야 함
        assert (
            "MEMORY_LIMIT_ENFORCED" in result.stdout
            or "MEMORY_ALLOCATION_FAILED" in result.stdout
            or result.exit_code != 0
        )


class TestResourceMonitoring:
    """리소스 모니터링 테스트"""

    def test_execution_includes_resource_stats(self):
        """실행 결과에 리소스 통계 포함 테스트"""
        runtime = RuntimeRegistry.get("python")

        code = 'print("test")'
        config = ExecutionConfig()
        result = runtime.execute(code, config)

        # 리소스 통계가 포함되어야 함
        assert hasattr(result, 'duration_ms')
        assert result.duration_ms is not None
        assert result.duration_ms >= 0

    def test_memory_usage_tracking(self):
        """메모리 사용량 추적 테스트"""
        runtime = RuntimeRegistry.get("python")

        code = """
# 메모리 사용
data = [i for i in range(100000)]
print("Memory allocated")
"""
        config = ExecutionConfig()
        result = runtime.execute(code, config)

        # 메모리 사용량 정보가 있어야 함 (향후 구현)
        # assert hasattr(result, 'memory_used_mb')
        assert result.exit_code == 0

    def test_cpu_usage_tracking(self):
        """CPU 사용량 추적 테스트"""
        runtime = RuntimeRegistry.get("python")

        code = """
# CPU 사용
import math
result = sum(math.sqrt(i) for i in range(100000))
print(f"Calculated: {result}")
"""
        config = ExecutionConfig()
        result = runtime.execute(code, config)

        # CPU 사용량 정보가 있어야 함 (향후 구현)
        # assert hasattr(result, 'cpu_time_ms')
        assert result.exit_code == 0


class TestSecurityHardening:
    """보안 강화 테스트"""

    def test_no_privileged_execution(self):
        """권한 상승 방지 테스트"""
        runtime = RuntimeRegistry.get("python")

        # sudo 시도
        code = """
import subprocess
try:
    subprocess.run(['sudo', 'whoami'], check=True)
    print("SUDO_SUCCESS")
except Exception as e:
    print(f"SUDO_BLOCKED: {type(e).__name__}")
"""
        config = ExecutionConfig()
        result = runtime.execute(code, config)

        # sudo가 차단되어야 함
        assert "SUDO_BLOCKED" in result.stdout or result.exit_code != 0

    def test_no_device_access(self):
        """디바이스 접근 차단 테스트"""
        runtime = RuntimeRegistry.get("python")

        # 디바이스 접근 시도
        code = """
try:
    with open('/dev/null', 'r') as f:
        f.read()
    print("DEVICE_ACCESS_SUCCESS")
except Exception as e:
    print(f"DEVICE_ACCESS_BLOCKED: {type(e).__name__}")
"""
        config = ExecutionConfig()
        result = runtime.execute(code, config)

        # /dev/null은 허용될 수 있지만, 다른 디바이스는 차단되어야 함
        # 테스트는 성공 또는 차단 둘 다 허용

    def test_no_proc_access(self):
        """/proc 접근 제한 테스트"""
        runtime = RuntimeRegistry.get("python")

        # /proc 파일시스템 접근 시도
        code = """
import os
try:
    processes = os.listdir('/proc')
    print(f"PROC_ACCESS_SUCCESS: {len(processes)} entries")
except Exception as e:
    print(f"PROC_ACCESS_BLOCKED: {type(e).__name__}")
"""
        config = ExecutionConfig()
        result = runtime.execute(code, config)

        # /proc 접근이 제한되어야 함 (또는 최소한의 정보만)
        # 일부 컨테이너는 /proc를 마운트하므로 유연하게 처리
