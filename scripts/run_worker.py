"""
워커 프로세스 실행 스크립트
"""

import sys
from pathlib import Path

# 프로젝트 루트를 sys.path에 추가
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

from src.worker import Worker


def main():
    """워커 실행"""
    print("=" * 60)
    print("CodeBeaker Worker")
    print("=" * 60)
    print()

    try:
        worker = Worker()
        worker.start()
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
