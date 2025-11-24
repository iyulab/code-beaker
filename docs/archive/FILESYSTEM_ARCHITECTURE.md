# 파일시스템 기반 아키텍처 설계

## 개요

CodeBeaker를 파일시스템 기반으로 재설계하여 외부 의존성(Redis, PostgreSQL) 없이 동작하도록 합니다.

### 설계 원칙

1. **Zero External Dependencies**: Redis, PostgreSQL 제거
2. **Filesystem-First**: 모든 상태를 파일시스템에 저장
3. **Simplicity**: 개발 환경에서 즉시 실행 가능
4. **Testability**: 파일 기반으로 테스트 및 시뮬레이션 용이

---

## 아키텍처 변경 사항

### 기존 아키텍처
```
API Server → Redis Queue → Worker Pool → Redis Storage
                ↓
            Task Status
            Results
            Metrics
```

### 새로운 아키텍처
```
API Server → File Queue → Worker Pool → File Storage
                ↓
            data/
            ├── queue/          # 작업 큐
            │   ├── pending/    # 대기 작업
            │   └── processing/ # 처리 중 작업
            ├── executions/     # 실행 결과
            │   └── {exec_id}/
            │       ├── status.json
            │       ├── stdout.txt
            │       └── stderr.txt
            └── metrics/        # 메트릭 데이터
                └── counters.json
```

---

## 파일 구조

### 1. 작업 큐 (`data/queue/`)

**pending/**: 대기 중인 작업
```
data/queue/pending/
├── {timestamp}_{uuid}.json  # 작업 파일
├── {timestamp}_{uuid}.json
└── ...
```

**작업 파일 형식** (`{timestamp}_{uuid}.json`):
```json
{
  "execution_id": "a1b2c3d4-...",
  "code": "print('Hello')",
  "language": "python",
  "timeout": 5,
  "memory_limit": "256m",
  "cpu_limit": 0.5,
  "network_enabled": false,
  "created_at": "2025-01-15T10:30:00Z"
}
```

**processing/**: 처리 중인 작업
- 워커가 작업을 가져가면 `pending/`에서 `processing/`으로 이동
- 완료/실패 시 삭제

### 2. 실행 결과 (`data/executions/`)

```
data/executions/{execution_id}/
├── status.json      # 상태 및 메타데이터
├── stdout.txt       # 표준 출력
├── stderr.txt       # 표준 에러
└── metadata.json    # 추가 메타데이터
```

**status.json**:
```json
{
  "execution_id": "a1b2c3d4-...",
  "status": "completed",
  "language": "python",
  "created_at": "2025-01-15T10:30:00Z",
  "started_at": "2025-01-15T10:30:01Z",
  "completed_at": "2025-01-15T10:30:02Z",
  "exit_code": 0,
  "duration_ms": 1234,
  "timeout": false,
  "error_type": null
}
```

### 3. 메트릭 (`data/metrics/`)

```
data/metrics/
├── counters.json           # 카운터 메트릭
├── histograms/            # 히스토그램 데이터
│   └── execution_duration.json
└── gauges/                # 게이지 메트릭
    └── queue_size.json
```

**counters.json**:
```json
{
  "executions_total": {
    "python_success": 42,
    "python_failure": 3,
    "javascript_success": 15
  },
  "total_processed": 60,
  "total_failed": 3
}
```

---

## 구현 계획

### Phase 1: 파일 기반 큐 구현

**파일**: `src/common/file_queue.py`

```python
class FileQueue:
    """파일시스템 기반 작업 큐"""

    def __init__(self, base_dir: str = "data/queue"):
        self.pending_dir = Path(base_dir) / "pending"
        self.processing_dir = Path(base_dir) / "processing"

    def submit_task(self, code, language, config) -> str:
        """작업을 큐에 제출"""
        # 1. 고유 ID 생성
        # 2. 작업 파일 생성 (pending/)
        # 3. 원자적 파일 쓰기 (임시 파일 → rename)

    def get_task(self, timeout: int = 1) -> Optional[dict]:
        """큐에서 작업 가져오기"""
        # 1. pending/ 디렉토리 스캔
        # 2. 가장 오래된 작업 선택 (timestamp 기준)
        # 3. processing/으로 이동 (원자적 rename)
        # 4. 잠금 파일 생성 (.lock)

    def complete_task(self, execution_id: str):
        """작업 완료 처리"""
        # processing/에서 파일 삭제
```

### Phase 2: 파일 기반 저장소 구현

**파일**: `src/common/file_storage.py`

```python
class FileStorage:
    """파일시스템 기반 상태 저장소"""

    def __init__(self, base_dir: str = "data/executions"):
        self.base_dir = Path(base_dir)

    def save_status(self, execution_id: str, status: dict):
        """상태 저장"""
        # 1. 디렉토리 생성
        # 2. status.json 원자적 쓰기

    def get_status(self, execution_id: str) -> Optional[dict]:
        """상태 조회"""
        # status.json 읽기

    def save_result(self, execution_id, stdout, stderr, ...):
        """실행 결과 저장"""
        # stdout.txt, stderr.txt 저장
```

### Phase 3: 메트릭 파일 저장

**파일**: `src/common/file_metrics.py`

```python
class FileMetrics:
    """파일시스템 기반 메트릭"""

    def __init__(self, base_dir: str = "data/metrics"):
        self.base_dir = Path(base_dir)
        self.lock = threading.Lock()

    def increment_counter(self, name: str, labels: dict):
        """카운터 증가"""
        # 1. 잠금 획득
        # 2. counters.json 읽기
        # 3. 값 증가
        # 4. 원자적 쓰기
```

---

## 동시성 처리

### 파일 잠금 전략

1. **작업 큐**: 파일 rename의 원자성 활용
   - `pending/task.json` → `processing/task.json` (원자적 이동)

2. **상태 저장**: Write-Once 패턴
   - 각 실행은 독립된 디렉토리 사용
   - 충돌 없음

3. **메트릭**: 파일 잠금 사용
   ```python
   import fcntl
   with open("counters.json", "r+") as f:
       fcntl.flock(f.fileno(), fcntl.LOCK_EX)
       # 읽기/쓰기
       fcntl.flock(f.fileno(), fcntl.LOCK_UN)
   ```

---

## 마이그레이션 계획

### 1단계: 인터페이스 추상화
```python
# src/common/queue_interface.py
class QueueInterface(ABC):
    @abstractmethod
    def submit_task(...): pass

    @abstractmethod
    def get_task(...): pass
```

### 2단계: 파일 기반 구현
```python
class FileQueue(QueueInterface):
    # 파일시스템 구현

class RedisQueue(QueueInterface):
    # 기존 Redis 구현 (호환성 유지)
```

### 3단계: 설정 기반 전환
```python
# config.py
QUEUE_TYPE = "file"  # or "redis"

if QUEUE_TYPE == "file":
    queue = FileQueue()
else:
    queue = RedisQueue()
```

---

## 성능 고려사항

### 파일 I/O 최적화

1. **Batch Write**: 여러 메트릭을 모아서 한번에 쓰기
2. **In-Memory Cache**: 자주 읽는 데이터 캐싱
3. **SSD 권장**: 빠른 I/O를 위해 SSD 사용

### 스케일링 제한

- **단일 노드**: 파일시스템은 단일 서버에서만 동작
- **처리량**: 초당 100-1000 작업 (SSD 기준)
- **워커 수**: 10-50개 워커 권장

### 프로덕션 전환

프로덕션 환경에서는 Redis/PostgreSQL 사용 권장:
```python
# production config
QUEUE_TYPE = "redis"
STORAGE_TYPE = "postgresql"
```

---

## 테스트 전략

### 1. 유닛 테스트
```python
def test_file_queue_submit_task():
    queue = FileQueue("test_data/queue")
    exec_id = queue.submit_task("print(1)", "python", config)

    # 파일 존재 확인
    assert (queue.pending_dir / f"{exec_id}.json").exists()
```

### 2. 동시성 테스트
```python
def test_concurrent_workers():
    # 10개 워커가 동시에 작업 가져가기
    # 중복 없이 처리되는지 확인
```

### 3. 시뮬레이션
```bash
# scripts/simulate_load.py
# 100개 작업 제출, 5개 워커 실행, 결과 검증
```

---

## 디렉토리 구조 예시

```
data/
├── queue/
│   ├── pending/
│   │   ├── 20250115_103000_a1b2c3d4.json
│   │   ├── 20250115_103001_e5f6g7h8.json
│   │   └── ...
│   └── processing/
│       └── 20250115_103002_i9j0k1l2.json
├── executions/
│   ├── a1b2c3d4-e5f6-g7h8-i9j0-k1l2m3n4o5p6/
│   │   ├── status.json
│   │   ├── stdout.txt
│   │   └── stderr.txt
│   └── ...
└── metrics/
    ├── counters.json
    ├── histograms/
    │   └── execution_duration.json
    └── gauges/
        └── queue_size.json
```

---

## 장단점

### 장점
✅ 외부 의존성 제거 (Redis, PostgreSQL 불필요)
✅ 개발 환경 단순화 (Docker Compose 불필요)
✅ 디버깅 용이 (파일로 직접 확인 가능)
✅ 백업 간단 (디렉토리 복사)
✅ 테스트 작성 용이

### 단점
❌ 단일 노드 제한 (분산 환경 불가)
❌ 처리량 제한 (파일 I/O 병목)
❌ 트랜잭션 부족 (원자성 제한적)
❌ 쿼리 기능 제한 (SQL 대비)

---

## 결론

파일시스템 기반 아키텍처는 **개발 환경 및 소규모 배포**에 적합합니다.

- **개발**: 빠른 설정, 쉬운 디버깅
- **테스트**: 파일 기반 시뮬레이션
- **소규모**: 단일 서버, 중간 처리량

**프로덕션**에서는 Redis/PostgreSQL 사용을 권장하며, 동일한 인터페이스로 전환 가능하도록 설계합니다.
