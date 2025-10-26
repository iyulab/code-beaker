"""
Phase 2.4: 패키지 화이트리스트 관리

보안을 위해 허용된 패키지만 설치 가능하도록 제한
"""

from typing import Set

# Python 패키지 화이트리스트
PYTHON_WHITELIST: Set[str] = {
    "numpy",
    "pandas",
    "requests",
    "scipy",
    "matplotlib",
    "pillow",
    "pytest",
    "flask",
    "django",
    "beautifulsoup4",
}

# JavaScript 패키지 화이트리스트
JAVASCRIPT_WHITELIST: Set[str] = {
    "lodash",
    "axios",
    "moment",
    "express",
    "react",
    "vue",
    "jest",
    "mocha",
    "chalk",
    "commander",
}


def validate_packages(packages: list[str], language: str) -> tuple[bool, list[str]]:
    """
    패키지 목록이 화이트리스트에 있는지 검증

    Args:
        packages: 검증할 패키지 목록
        language: 언어 (python, javascript)

    Returns:
        (모두 허용 여부, 허용되지 않은 패키지 목록)
    """
    if not packages:
        return True, []

    # 언어별 화이트리스트 선택
    if language.lower() in ("python", "py"):
        whitelist = PYTHON_WHITELIST
    elif language.lower() in ("javascript", "js", "nodejs", "node"):
        whitelist = JAVASCRIPT_WHITELIST
    else:
        # 지원하지 않는 언어는 패키지 설치 불가
        return False, packages

    # 허용되지 않은 패키지 찾기
    invalid_packages = [pkg for pkg in packages if pkg.lower() not in whitelist]

    return len(invalid_packages) == 0, invalid_packages
