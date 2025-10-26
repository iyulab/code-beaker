"""
파일시스템 기반 아키텍처 시뮬레이션

FileQueue와 FileStorage를 사용한 코드 실행 시뮬레이션
"""

import sys
import time
from pathlib import Path

# 프로젝트 루트를 Python 경로에 추가
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

from src.common.file_queue import FileQueue
from src.common.file_storage import FileStorage
from src.common.models import ExecutionConfig
from src.runtime import RuntimeRegistry


def clean_test_data():
    """테스트 데이터 디렉토리 정리"""
    import shutil

    data_dir = Path("data")
    if data_dir.exists():
        shutil.rmtree(data_dir)
        print("✓ 기존 테스트 데이터 정리 완료")


def simulate_single_execution():
    """단일 코드 실행 시뮬레이션"""
    print("\n=== 단일 실행 시뮬레이션 ===\n")

    # 1. 큐에 작업 제출
    queue = FileQueue()
    code = """
package main
import "fmt"

func main() {
    fmt.Println("Hello from Go!")
    fmt.Println("Filesystem-based architecture works!")
}
"""
    exec_id = queue.submit_task(code, "go", ExecutionConfig(timeout=10))
    print(f"✓ 작업 제출 완료: {exec_id}")

    # 2. 큐에서 작업 가져오기
    task = queue.get_task(timeout=2)
    if not task:
        print("❌ 작업을 가져올 수 없습니다")
        return

    print(f"✓ 작업 가져오기 완료: {task['execution_id']}")

    # 3. 상태 업데이트: running
    storage = FileStorage()
    storage.update_status(exec_id, "running", started_at=time.time())
    print(f"✓ 상태 업데이트: running")

    # 4. 코드 실행
    try:
        runtime = RuntimeRegistry.get(task["language"])
        config = ExecutionConfig(
            timeout=task["timeout"],
            memory_limit=task["memory_limit"],
            cpu_limit=task["cpu_limit"],
            network_enabled=task["network_enabled"],
        )
        result = runtime.execute(task["code"], config)
        print(f"✓ 코드 실행 완료 (exit_code: {result.exit_code})")

        # 5. 결과 저장
        queue.save_result(
            exec_id,
            result.stdout,
            result.stderr,
            result.exit_code,
            result.duration_ms,
            result.timeout,
            result.error_type,
        )
        print(f"✓ 결과 저장 완료")

        # 6. 결과 조회
        saved_result = storage.get_result(exec_id)
        print(f"\n실행 결과:")
        print(f"  Status: {saved_result['status']}")
        print(f"  Exit Code: {saved_result['exit_code']}")
        print(f"  Duration: {saved_result['duration_ms']}ms")
        print(f"\nStdout:")
        print(saved_result["stdout"])

    except Exception as e:
        print(f"❌ 실행 실패: {e}")
        queue.save_result(exec_id, "", str(e), 1, 0, False, type(e).__name__)


def simulate_multiple_workers():
    """다중 워커 시뮬레이션"""
    print("\n=== 다중 워커 시뮬레이션 ===\n")

    # 1. 여러 작업 제출
    queue = FileQueue()
    tasks = [
        ('print("Python Task 1")', "python"),
        ('console.log("JavaScript Task 1")', "javascript"),
        (
            'package main\nimport "fmt"\nfunc main() { fmt.Println("Go Task 1") }',
            "go",
        ),
        ('print("Python Task 2")', "python"),
        ('console.log("JavaScript Task 2")', "javascript"),
    ]

    exec_ids = []
    for code, lang in tasks:
        exec_id = queue.submit_task(code, lang, ExecutionConfig(timeout=10))
        exec_ids.append(exec_id)
        print(f"✓ 작업 제출: {lang} - {exec_id[:8]}...")

    print(f"\n총 {len(exec_ids)}개 작업 제출 완료")
    print(f"큐 크기: {queue.get_queue_size()}")

    # 2. 워커 시뮬레이션 (2개 워커)
    def worker(worker_id: int, count: int):
        """워커 프로세스 시뮬레이션"""
        processed = 0
        storage = FileStorage()

        while processed < count:
            task = queue.get_task(timeout=1)
            if not task:
                break

            exec_id = task["execution_id"]
            print(f"  Worker {worker_id}: 처리 중 {exec_id[:8]}... ({task['language']})")

            try:
                runtime = RuntimeRegistry.get(task["language"])
                config = ExecutionConfig(timeout=task["timeout"])
                result = runtime.execute(task["code"], config)

                queue.save_result(
                    exec_id,
                    result.stdout,
                    result.stderr,
                    result.exit_code,
                    result.duration_ms,
                )
                print(
                    f"  Worker {worker_id}: ✓ 완료 {exec_id[:8]}... (exit: {result.exit_code})"
                )
                processed += 1

            except Exception as e:
                print(f"  Worker {worker_id}: ❌ 실패 {exec_id[:8]}... ({e})")
                queue.save_result(exec_id, "", str(e), 1, 0)

    # 워커 실행 (순차적으로)
    print("\n워커 시작...")
    worker(1, 3)
    worker(2, 2)

    # 3. 결과 확인
    print(f"\n최종 큐 크기: {queue.get_queue_size()}")

    storage = FileStorage()
    for i, exec_id in enumerate(exec_ids, 1):
        status = storage.get_status(exec_id)
        if status:
            print(f"{i}. {exec_id[:8]}... - {status.get('status', 'unknown')}")


def check_filesystem_structure():
    """파일시스템 구조 확인"""
    print("\n=== 파일시스템 구조 확인 ===\n")

    data_dir = Path("data")
    if not data_dir.exists():
        print("data/ 디렉토리가 없습니다. 시뮬레이션을 먼저 실행하세요.")
        return

    def print_tree(directory: Path, prefix: str = "", max_depth: int = 3, current_depth: int = 0):
        """디렉토리 트리 출력"""
        if current_depth >= max_depth:
            return

        try:
            entries = sorted(directory.iterdir(), key=lambda x: (not x.is_dir(), x.name))
            for i, entry in enumerate(entries):
                is_last = i == len(entries) - 1
                current_prefix = "└── " if is_last else "├── "
                print(f"{prefix}{current_prefix}{entry.name}")

                if entry.is_dir():
                    extension_prefix = "    " if is_last else "│   "
                    print_tree(entry, prefix + extension_prefix, max_depth, current_depth + 1)
        except PermissionError:
            pass

    print(f"data/")
    print_tree(data_dir, "", max_depth=4)


def main():
    """메인 실행"""
    import argparse

    parser = argparse.ArgumentParser(description="파일시스템 기반 아키텍처 시뮬레이션")
    parser.add_argument(
        "--clean", action="store_true", help="기존 테스트 데이터 정리"
    )
    parser.add_argument(
        "--single", action="store_true", help="단일 실행 시뮬레이션"
    )
    parser.add_argument(
        "--multi", action="store_true", help="다중 워커 시뮬레이션"
    )
    parser.add_argument(
        "--tree", action="store_true", help="파일시스템 구조 확인"
    )
    parser.add_argument(
        "--all", action="store_true", help="모든 시뮬레이션 실행"
    )

    args = parser.parse_args()

    if args.clean:
        clean_test_data()

    if args.single or args.all:
        simulate_single_execution()

    if args.multi or args.all:
        simulate_multiple_workers()

    if args.tree or args.all:
        check_filesystem_structure()

    if not any([args.single, args.multi, args.tree, args.all, args.clean]):
        parser.print_help()


if __name__ == "__main__":
    main()
