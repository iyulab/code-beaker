"""
Phase 2.1 통합 테스트: REST API 테스트 (다중 언어 지원)
"""

import pytest
from fastapi.testclient import TestClient

from src.api.main import app

# TestClient 생성
client = TestClient(app)


class TestHealthEndpoints:
    """헬스 체크 엔드포인트 테스트"""

    def test_root_endpoint(self):
        """루트 엔드포인트 테스트"""
        response = client.get("/")
        assert response.status_code == 200

        data = response.json()
        assert data["status"] == "healthy"
        assert "version" in data

    def test_health_endpoint(self):
        """헬스 체크 엔드포인트 테스트"""
        response = client.get("/health")
        assert response.status_code == 200

        data = response.json()
        assert data["status"] == "healthy"


class TestLanguagesEndpoint:
    """언어 목록 엔드포인트 테스트 (Phase 2.1)"""

    def test_list_languages(self):
        """지원 언어 목록 조회"""
        response = client.get("/languages")
        assert response.status_code == 200

        data = response.json()
        assert "languages" in data
        assert isinstance(data["languages"], list)
        assert "python" in data["languages"]
        assert "py" in data["languages"]


class TestExecuteEndpoint:
    """코드 실행 엔드포인트 테스트"""

    def test_execute_hello_world(self):
        """Hello World 실행 테스트"""
        response = client.post(
            "/execute", json={"code": 'print("Hello, CodeBeaker!")', "language": "python"}
        )

        assert response.status_code == 200

        data = response.json()
        assert data["success"] is True
        assert "Hello, CodeBeaker!" in data["stdout"]
        assert data["exit_code"] == 0
        assert data["timeout"] is False

    def test_execute_calculation(self):
        """계산 실행 테스트"""
        code = """
result = 2 + 2
print(result)
"""
        response = client.post("/execute", json={"code": code, "language": "python"})

        assert response.status_code == 200

        data = response.json()
        assert data["success"] is True
        assert "4" in data["stdout"]

    def test_execute_with_error(self):
        """에러가 발생하는 코드 실행 테스트"""
        code = "x = 1 / 0"
        response = client.post("/execute", json={"code": code, "language": "python"})

        assert response.status_code == 200

        data = response.json()
        assert data["success"] is False
        assert data["exit_code"] != 0
        assert "ZeroDivisionError" in data["stderr"]

    def test_execute_with_timeout(self):
        """타임아웃 테스트"""
        code = """
import time
time.sleep(10)
"""
        response = client.post(
            "/execute", json={"code": code, "language": "python", "timeout": 1}
        )

        assert response.status_code == 200

        data = response.json()
        assert data["success"] is False
        assert data["timeout"] is True
        assert "timeout" in data["stderr"].lower()

    def test_unsupported_language(self):
        """지원하지 않는 언어 테스트"""
        response = client.post(
            "/execute", json={"code": "puts 'test'", "language": "ruby"}
        )

        assert response.status_code == 400
        assert "Unsupported language" in response.json()["detail"]

    def test_missing_code_field(self):
        """필수 필드 누락 테스트"""
        response = client.post("/execute", json={"language": "python"})

        assert response.status_code == 422  # Validation error

    def test_invalid_timeout(self):
        """잘못된 타임아웃 값 테스트"""
        # 너무 큰 타임아웃
        response = client.post(
            "/execute", json={"code": "print('test')", "timeout": 100}
        )
        assert response.status_code == 422

        # 0 이하 타임아웃
        response = client.post("/execute", json={"code": "print('test')", "timeout": 0})
        assert response.status_code == 422

    def test_code_too_large(self):
        """코드 크기 제한 테스트"""
        # 100KB 초과 코드
        large_code = "x = 1\n" * 100000
        response = client.post("/execute", json={"code": large_code, "language": "python"})

        assert response.status_code == 422  # Validation error

    def test_default_values(self):
        """기본값 테스트"""
        response = client.post("/execute", json={"code": "print('test')"})

        assert response.status_code == 200

        data = response.json()
        assert data["success"] is True

    def test_response_structure(self):
        """응답 구조 검증"""
        response = client.post("/execute", json={"code": "print('test')"})

        assert response.status_code == 200

        data = response.json()
        # 필수 필드 확인
        assert "success" in data
        assert "stdout" in data
        assert "stderr" in data
        assert "exit_code" in data
        assert "duration_ms" in data
        assert "timeout" in data

    def test_multiple_executions(self):
        """여러 번 실행 테스트"""
        for i in range(3):
            response = client.post(
                "/execute", json={"code": f'print("Test {i}")', "language": "python"}
            )

            assert response.status_code == 200

            data = response.json()
            assert data["success"] is True
            assert f"Test {i}" in data["stdout"]


class TestAPIDocumentation:
    """API 문서 테스트"""

    def test_openapi_schema(self):
        """OpenAPI 스키마 접근 테스트"""
        response = client.get("/openapi.json")
        assert response.status_code == 200

        schema = response.json()
        assert schema["info"]["title"] == "CodeBeaker API"
        assert "paths" in schema

    def test_swagger_ui(self):
        """Swagger UI 접근 테스트"""
        response = client.get("/docs")
        assert response.status_code == 200

    def test_redoc(self):
        """ReDoc 접근 테스트"""
        response = client.get("/redoc")
        assert response.status_code == 200


class TestAsyncExecutionEndpoint:
    """비동기 실행 엔드포인트 테스트 (Phase 3.1)"""

    def test_async_execute_hello_world(self):
        """비동기 Hello World 실행 테스트"""
        response = client.post(
            "/execute/async",
            json={"code": 'print("Hello Async!")', "language": "python"},
        )

        # Redis가 없으면 503, 있으면 200
        if response.status_code == 503:
            import pytest

            pytest.skip("Redis not available")

        assert response.status_code == 200

        data = response.json()
        assert "execution_id" in data
        assert data["status"] == "queued"
        assert len(data["execution_id"]) == 36  # UUID 길이

    def test_async_execute_javascript(self):
        """비동기 JavaScript 실행 테스트"""
        response = client.post(
            "/execute/async",
            json={"code": 'console.log("Async JS");', "language": "javascript"},
        )

        if response.status_code == 503:
            import pytest

            pytest.skip("Redis not available")

        assert response.status_code == 200

        data = response.json()
        assert data["status"] == "queued"

    def test_async_execute_unsupported_language(self):
        """지원하지 않는 언어 테스트"""
        response = client.post(
            "/execute/async", json={"code": 'puts "test"', "language": "ruby"}
        )

        if response.status_code == 503:
            import pytest

            pytest.skip("Redis not available")

        assert response.status_code == 400
        assert "Unsupported language" in response.json()["detail"]

    def test_async_execute_with_timeout(self):
        """타임아웃 설정 테스트"""
        response = client.post(
            "/execute/async",
            json={
                "code": 'print("test")',
                "language": "python",
                "timeout": 15,
            },
        )

        if response.status_code == 503:
            import pytest

            pytest.skip("Redis not available")

        assert response.status_code == 200


class TestExecutionStatusEndpoint:
    """실행 상태 조회 엔드포인트 테스트 (Phase 3.1)"""

    def test_get_execution_status_not_found(self):
        """존재하지 않는 실행 ID 조회 테스트"""
        response = client.get("/execution/non-existent-id")

        if response.status_code == 503:
            import pytest

            pytest.skip("Redis not available")

        assert response.status_code == 404
        assert "not found" in response.json()["detail"].lower()

    def test_get_execution_status_queued(self):
        """queued 상태 조회 테스트"""
        # 작업 제출
        submit_response = client.post(
            "/execute/async",
            json={"code": 'print("status test")', "language": "python"},
        )

        if submit_response.status_code == 503:
            import pytest

            pytest.skip("Redis not available")

        execution_id = submit_response.json()["execution_id"]

        # 상태 조회
        response = client.get(f"/execution/{execution_id}")

        assert response.status_code == 200

        data = response.json()
        assert data["execution_id"] == execution_id
        assert data["status"] in ["queued", "running", "completed", "failed"]
        assert data["language"] == "python"
        assert "created_at" in data

    def test_async_execute_and_check_status_multiple_times(self):
        """여러 번 상태 조회 테스트"""
        # 작업 제출
        submit_response = client.post(
            "/execute/async",
            json={
                "code": 'import time; time.sleep(0.1); print("done")',
                "language": "python",
            },
        )

        if submit_response.status_code == 503:
            import pytest

            pytest.skip("Redis not available")

        execution_id = submit_response.json()["execution_id"]

        # 여러 번 상태 조회 (idempotent)
        for _ in range(3):
            response = client.get(f"/execution/{execution_id}")
            assert response.status_code == 200
            data = response.json()
            assert data["execution_id"] == execution_id
