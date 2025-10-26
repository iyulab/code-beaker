"""
Phase 2.4 테스트: 의존성 설치 지원 테스트
"""

import pytest

from src.common.models import ExecutionConfig
from src.runtime import RuntimeRegistry


class TestPythonPackages:
    """Python 패키지 설치 테스트"""

    def test_numpy_package_installation(self):
        """numpy 패키지 설치 및 사용 테스트"""
        runtime = RuntimeRegistry.get("python")

        code = """
import numpy as np
arr = np.array([1, 2, 3, 4, 5])
print(f"Sum: {arr.sum()}")
print(f"Mean: {arr.mean()}")
"""
        config = ExecutionConfig(packages=["numpy"], timeout=30, network_enabled=True)
        result = runtime.execute(code, config)

        assert result.exit_code == 0
        assert "Sum: 15" in result.stdout
        assert "Mean: 3.0" in result.stdout

    def test_requests_package_installation(self):
        """requests 패키지 설치 테스트 (네트워크 사용 없이)"""
        runtime = RuntimeRegistry.get("python")

        code = """
import requests
print(f"requests version: {requests.__version__}")
"""
        config = ExecutionConfig(packages=["requests"], timeout=30, network_enabled=True)
        result = runtime.execute(code, config)

        assert result.exit_code == 0
        assert "requests version:" in result.stdout

    def test_multiple_packages_installation(self):
        """여러 패키지 동시 설치 테스트"""
        runtime = RuntimeRegistry.get("python")

        code = """
import numpy as np
import requests
print(f"numpy: {np.__version__}")
print(f"requests: {requests.__version__}")
"""
        config = ExecutionConfig(packages=["numpy", "requests"], timeout=30, network_enabled=True)
        result = runtime.execute(code, config)

        assert result.exit_code == 0
        assert "numpy:" in result.stdout
        assert "requests:" in result.stdout

    def test_unauthorized_package_blocked(self):
        """화이트리스트에 없는 패키지 설치 차단 테스트"""
        runtime = RuntimeRegistry.get("python")

        code = 'print("test")'
        config = ExecutionConfig(packages=["malicious-package"])

        # 허용되지 않은 패키지는 ValueError 발생
        with pytest.raises(ValueError) as exc_info:
            runtime.execute(code, config)

        assert "not allowed" in str(exc_info.value).lower()

    def test_no_packages_default_behavior(self):
        """패키지 설치 없이 기본 동작 테스트"""
        runtime = RuntimeRegistry.get("python")

        code = """
import sys
print(f"Python version: {sys.version}")
"""
        config = ExecutionConfig()  # packages=[] (기본값)
        result = runtime.execute(code, config)

        assert result.exit_code == 0
        assert "Python version:" in result.stdout


class TestJavaScriptPackages:
    """JavaScript 패키지 설치 테스트"""

    def test_lodash_package_installation(self):
        """lodash 패키지 설치 및 사용 테스트"""
        runtime = RuntimeRegistry.get("javascript")

        code = """
const _ = require('lodash');
const arr = [1, 2, 3, 4, 5];
console.log('Sum:', _.sum(arr));
console.log('Mean:', _.mean(arr));
"""
        config = ExecutionConfig(packages=["lodash"], timeout=30, network_enabled=True)
        result = runtime.execute(code, config)

        assert result.exit_code == 0
        assert "Sum: 15" in result.stdout
        assert "Mean: 3" in result.stdout

    def test_axios_package_installation(self):
        """axios 패키지 설치 테스트"""
        runtime = RuntimeRegistry.get("javascript")

        code = """
const axios = require('axios');
console.log('axios version:', axios.VERSION);
"""
        config = ExecutionConfig(packages=["axios"], timeout=30, network_enabled=True)
        result = runtime.execute(code, config)

        assert result.exit_code == 0
        assert "axios version:" in result.stdout

    def test_multiple_js_packages_installation(self):
        """여러 JavaScript 패키지 동시 설치 테스트"""
        runtime = RuntimeRegistry.get("javascript")

        code = """
const _ = require('lodash');
const axios = require('axios');
console.log('lodash loaded:', typeof _);
console.log('axios loaded:', typeof axios);
"""
        config = ExecutionConfig(packages=["lodash", "axios"], timeout=30, network_enabled=True)
        result = runtime.execute(code, config)

        assert result.exit_code == 0
        assert "lodash loaded: function" in result.stdout
        assert "axios loaded: function" in result.stdout

    def test_unauthorized_js_package_blocked(self):
        """화이트리스트에 없는 JavaScript 패키지 차단 테스트"""
        runtime = RuntimeRegistry.get("javascript")

        code = 'console.log("test");'
        config = ExecutionConfig(packages=["malicious-js-package"])

        # 허용되지 않은 패키지는 ValueError 발생
        with pytest.raises(ValueError) as exc_info:
            runtime.execute(code, config)

        assert "not allowed" in str(exc_info.value).lower()

    def test_no_js_packages_default_behavior(self):
        """패키지 설치 없이 기본 JavaScript 동작 테스트"""
        runtime = RuntimeRegistry.get("javascript")

        code = """
console.log('Node version:', process.version);
"""
        config = ExecutionConfig()  # packages=[] (기본값)
        result = runtime.execute(code, config)

        assert result.exit_code == 0
        assert "Node version:" in result.stdout


class TestPackageWhitelist:
    """패키지 화이트리스트 관리 테스트"""

    def test_python_allowed_packages(self):
        """Python 허용된 패키지 목록 테스트"""
        from src.common.packages import PYTHON_WHITELIST

        # 허용된 패키지들
        allowed = ["numpy", "pandas", "requests", "scipy", "matplotlib"]

        for package in allowed:
            # 화이트리스트에 있어야 함
            assert package in PYTHON_WHITELIST

    def test_javascript_allowed_packages(self):
        """JavaScript 허용된 패키지 목록 테스트"""
        from src.common.packages import JAVASCRIPT_WHITELIST

        # 허용된 패키지들
        allowed = ["lodash", "axios", "moment", "express"]

        for package in allowed:
            # 화이트리스트에 있어야 함
            assert package in JAVASCRIPT_WHITELIST

    def test_validate_packages_python(self):
        """Python 패키지 검증 테스트"""
        from src.common.packages import validate_packages

        # 허용된 패키지는 검증 통과
        valid_packages = ["numpy", "requests"]
        is_valid, invalid = validate_packages(valid_packages, "python")
        assert is_valid is True
        assert len(invalid) == 0

        # 허용되지 않은 패키지는 검증 실패
        invalid_packages = ["malicious-package"]
        is_valid, invalid = validate_packages(invalid_packages, "python")
        assert is_valid is False
        assert "malicious-package" in invalid

    def test_validate_packages_javascript(self):
        """JavaScript 패키지 검증 테스트"""
        from src.common.packages import validate_packages

        # 허용된 패키지는 검증 통과
        valid_packages = ["lodash", "axios"]
        is_valid, invalid = validate_packages(valid_packages, "javascript")
        assert is_valid is True
        assert len(invalid) == 0

        # 허용되지 않은 패키지는 검증 실패
        invalid_packages = ["malicious-js-package"]
        is_valid, invalid = validate_packages(invalid_packages, "javascript")
        assert is_valid is False
        assert "malicious-js-package" in invalid
