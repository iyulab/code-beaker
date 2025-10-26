# CodeBeaker 개발 가이드

## 개발 환경 설정

### 1. 사전 요구사항

- Python 3.11+
- Docker Desktop
- Git

### 2. 저장소 클론

```bash
git clone https://github.com/iyulab/codebeaker.git
cd codebeaker
```

### 3. Python 가상환경 생성

```bash
# Windows
python -m venv venv
venv\Scripts\activate

# Linux/Mac
python -m venv venv
source venv/bin/activate
```

### 4. 의존성 설치

```bash
# 개발 의존성 포함 설치
make dev-install

# 또는 pip 직접 사용
pip install -r requirements-dev.txt
```

### 5. 환경 변수 설정

```bash
# .env.example을 .env로 복사
cp .env.example .env

# .env 파일 편집 (필요시)
```

### 6. Docker 서비스 시작

```bash
make docker-up

# 또는 docker-compose 직접 사용
docker-compose up -d
```

서비스 확인:
- PostgreSQL: `localhost:5432`
- Redis: `localhost:6379`
- Prometheus: `http://localhost:9090`
- Grafana: `http://localhost:3000` (admin/admin)

### 7. 런타임 Docker 이미지 빌드

코드 실행을 위한 언어별 Docker 이미지를 빌드합니다:

```bash
# Windows
scripts\build_runtime_images.bat

# Linux/Mac
bash scripts/build_runtime_images.sh

# 또는 개별 빌드
docker build -t codebeaker-python:latest docker/runtimes/python/
docker build -t codebeaker-nodejs:latest docker/runtimes/nodejs/
docker build -t codebeaker-csharp:latest docker/runtimes/csharp/
```

빌드 완료 후 이미지 확인:
```bash
docker images | grep codebeaker
```

## 개발 워크플로우

### 코드 작성

1. 기능 브랜치 생성
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. 코드 작성

3. 포맷팅
   ```bash
   make format
   ```

4. 린트 검사
   ```bash
   make lint
   ```

5. 테스트 실행
   ```bash
   make test
   ```

### 테스트

```bash
# 모든 테스트 실행
pytest tests/

# 커버리지 포함
pytest tests/ --cov=src --cov-report=html

# 특정 테스트만 실행
pytest tests/unit/test_runtime.py

# 마커로 필터링
pytest -m unit          # 유닛 테스트만
pytest -m integration   # 통합 테스트만
pytest -m "not slow"    # 느린 테스트 제외
```

### 코드 품질 도구

```bash
# Black (코드 포맷팅)
black src/ tests/

# isort (import 정렬)
isort src/ tests/

# mypy (타입 체크)
mypy src/

# pylint (린팅)
pylint src/

# flake8 (스타일 가이드)
flake8 src/ tests/
```

## 프로젝트 구조

```
codebeaker/
├── src/
│   ├── api/          # API 서버 (FastAPI)
│   ├── worker/       # 워커 프로세스
│   ├── runtime/      # 언어별 런타임 어댑터
│   └── common/       # 공통 유틸리티
├── tests/
│   ├── unit/         # 유닛 테스트
│   └── integration/  # 통합 테스트
├── docker/
│   └── runtimes/     # 언어별 Docker 이미지
├── docs/             # 문서
└── scripts/          # 유틸리티 스크립트
```

## 현재 진행 상황

### ✅ Phase 0: 프로젝트 설정 (완료)
- 프로젝트 구조 생성
- 개발 도구 설정 (pytest, black, mypy)
- Docker 환경 설정

### ✅ Phase 1.1: Hello World 실행기 (완료)
- SimpleExecutor 클래스 구현
- 타임아웃 처리
- 테스트 10개 작성 (100% 통과)

### ✅ Phase 1.3: REST API (완료)
- FastAPI 서버 구현
- POST /execute 엔드포인트
- Pydantic 요청/응답 검증
- 통합 테스트 16개 작성 (100% 통과)

### ✅ Phase 1.4: Docker 컨테이너 격리 (완료)
- Docker SDK 통합
- Python 런타임 Docker 이미지 생성
- 컨테이너 기반 코드 실행
- 네트워크 격리
- 리소스 제한 (CPU, 메모리)
- 타임아웃 처리
- 테스트 12개 작성 (100% 통과)

### ✅ Phase 2.1: 런타임 추상화 (완료)
- BaseRuntime 추상 클래스 설계
- PythonRuntime 구현 (DockerExecutor 리팩터링)
- RuntimeRegistry 패턴 구현
- GET /languages API 엔드포인트 추가
- 템플릿 메서드 패턴으로 공통 실행 로직 통합
- 테스트 15개 추가 (100% 통과)

### ✅ Phase 2.2: JavaScript/Node.js 런타임 (완료)
- Node.js 20-slim Docker 이미지 생성
- JavaScriptRuntime 구현 (BaseRuntime 상속)
- RuntimeRegistry에 등록 (javascript, js, nodejs, node 별칭)
- 테스트 8개 추가 (100% 통과)
- Python과 JavaScript 2개 언어 지원

### ✅ Phase 3.1-3.2: 비동기 큐 시스템 (완료)
- Redis 기반 작업 큐 구현 (TaskQueue)
- POST /execute/async 비동기 실행 엔드포인트
- GET /execution/{id} 상태 조회 엔드포인트
- 워커 프로세스 구현 (Worker)
- 큐에서 작업 가져와 실행 및 결과 저장
- 장기 실행 작업 지원

### ✅ Phase 3.3: 워커 풀 관리 (완료)
- 다중 워커 프로세스 관리 (WorkerPool)
- 병렬 작업 처리 지원
- 워커 헬스 체크 및 상태 모니터링
- 동적 워커 스케일링 (scale up/down)
- 우아한 종료 (graceful shutdown)
- Redis 기반 메트릭 수집 및 조회
- 컨텍스트 매니저 지원

### ✅ Phase 4: 고급 보안 및 격리 (완료)
- 읽기 전용 루트 파일시스템 (read-only root filesystem)
- tmpfs 마운트 (/tmp: 512MB, exec 허용)
- 네트워크 격리 강화
- 컨테이너 호스트 접근 차단
- 리소스 제한 강제 (메모리, CPU)
- 권한 상승 방지
- 보안 테스트 10개 작성 (100% 통과)
  - 네트워크 격리 테스트
  - 파일시스템 읽기 전용 테스트
  - 컨테이너 격리 테스트
  - 리소스 제한 테스트
  - 권한 상승 방지 테스트

### ✅ Phase 2.3: C# 런타임 (완료)
- .NET 8.0 SDK Docker 이미지 생성
- CSharpRuntime 구현 (BaseRuntime 상속)
- RuntimeRegistry에 등록 (csharp, cs, dotnet 별칭)
- 컴파일 + 실행 파이프라인 구현
- 테스트 9개 추가
- Python, JavaScript, C# 3개 언어 지원

### ✅ Phase 2.4: 의존성 설치 지원 (완료)
- Python 패키지 설치 지원 (pip)
  - 화이트리스트: numpy, pandas, requests, scipy, matplotlib, pillow, pytest, flask, django, beautifulsoup4
  - tmpfs에 설치하여 보안 유지
- JavaScript 패키지 설치 지원 (npm)
  - 화이트리스트: lodash, axios, moment, express, react, vue, jest, mocha, chalk, commander
  - tmpfs에 설치하여 보안 유지
- 패키지 화이트리스트 관리 시스템
  - 허용된 패키지만 설치 가능
  - 악의적인 패키지 차단
- 테스트 14개 작성 (100% 통과)

### ✅ Phase 5.1: 구조화된 로깅 (완료)
- structlog 기반 구조화된 로깅 구현
  - JSON 로깅 (프로덕션)
  - 컬러 출력 (개발 환경)
- 요청 ID 추적 미들웨어
  - UUID 기반 고유 요청 ID
  - X-Request-ID 헤더 지원
  - 로그 컨텍스트 자동 추가
- 성능 메트릭 로깅
  - 요청/응답 시간 측정
  - X-Response-Time 헤더
  - 에러 자동 추적

### ✅ Phase 5.2: Prometheus 메트릭 (완료)
- Prometheus 메트릭 수집 구현
  - prometheus_client 라이브러리 사용
  - Counter, Histogram, Gauge 메트릭 정의
- 코드 실행 메트릭
  - 언어별, 상태별 실행 횟수 추적
  - 실행 시간 분포 측정
- HTTP 요청 메트릭
  - 메서드, 엔드포인트, 상태코드별 요청 추적
  - 응답 시간 분포 측정
  - 활성 요청 수 게이지
- GET /metrics 엔드포인트 노출
  - Prometheus 형식 메트릭 제공
  - Grafana 대시보드 연동 준비

### 📊 테스트 현황
```
133 passed in 217.29s (0:03:37)
Coverage: 85.51%

구성:
- 110개: Phase 0-4 (기본 기능)
- 9개: Phase 2.3 (C# 런타임)
- 14개: Phase 2.4 (의존성 설치)
```

**Phase 3 테스트 추가** (39개):
- TaskQueue 유닛 테스트: 12개
- Worker 유닛 테스트: 10개
- WorkerPool 유닛 테스트: 10개 (Phase 3.3)
- 비동기 API 통합 테스트: 7개

**Phase 4 테스트 추가** (10개):
- 보안 프로필 테스트: 5개 (네트워크, 파일시스템, 컨테이너 격리, 리소스)
- 보안 강화 테스트: 5개 (권한, 디바이스, proc 접근)
- 리소스 모니터링 테스트: 3개 (duration, memory, cpu)

**Phase 2.4 테스트 추가** (14개):
- Python 패키지 설치 테스트: 5개
- JavaScript 패키지 설치 테스트: 5개
- 패키지 화이트리스트 테스트: 4개

## API 서버 및 워커 실행

### 동기 실행 (Phase 1-2)
```bash
# API 서버 시작
python scripts/run_api.py

# 브라우저에서 접속
# - Swagger UI: http://localhost:8000/docs
# - ReDoc: http://localhost:8000/redoc
```

### 비동기 실행 (Phase 3.1-3.2)
```bash
# 1. Redis 시작 (docker-compose 사용)
docker-compose up -d redis

# 2. API 서버 시작
python scripts/run_api.py

# 3. 워커 프로세스 시작 (별도 터미널)
python scripts/run_worker.py

# 비동기 실행 테스트
curl -X POST http://localhost:8000/execute/async \
  -H "Content-Type: application/json" \
  -d '{"code": "print(\"Hello Async!\")", "language": "python"}'

# 응답: {"execution_id": "...", "status": "queued"}

# 상태 조회
curl http://localhost:8000/execution/{execution_id}
```

### 워커 풀 실행 (Phase 3.3)
```python
from src.worker import WorkerPool

# 워커 풀 생성 (2개 워커)
with WorkerPool(num_workers=2) as pool:
    pool.start()

    # 헬스 체크
    health = pool.get_health_status()
    print(f"Healthy workers: {health['healthy_workers']}/{health['total_workers']}")

    # 메트릭 조회
    metrics = pool.get_metrics()
    print(f"Processed: {metrics['total_processed']}, Failed: {metrics['total_failed']}")

    # 동적 스케일링
    pool.scale(4)  # 4개 워커로 증가
    pool.scale(2)  # 2개 워커로 감소

    # 우아한 종료 (실행 중인 작업 완료 대기)
    pool.stop(graceful=True, timeout=10)
```

### 패키지 설치 사용 예시 (Phase 2.4)

**Python 패키지 설치:**
```python
from src.runtime import RuntimeRegistry
from src.common.models import ExecutionConfig

runtime = RuntimeRegistry.get("python")

# numpy 사용
code = """
import numpy as np
arr = np.array([1, 2, 3, 4, 5])
print(f"Sum: {arr.sum()}")
print(f"Mean: {arr.mean()}")
"""
config = ExecutionConfig(
    packages=["numpy"],
    timeout=30,
    network_enabled=True  # 패키지 다운로드를 위해 필요
)
result = runtime.execute(code, config)
print(result.stdout)
```

**JavaScript 패키지 설치:**
```python
from src.runtime import RuntimeRegistry
from src.common.models import ExecutionConfig

runtime = RuntimeRegistry.get("javascript")

# lodash 사용
code = """
const _ = require('lodash');
const arr = [1, 2, 3, 4, 5];
console.log('Sum:', _.sum(arr));
console.log('Mean:', _.mean(arr));
"""
config = ExecutionConfig(
    packages=["lodash"],
    timeout=30,
    network_enabled=True
)
result = runtime.execute(code, config)
print(result.stdout)
```

**허용된 패키지:**
- **Python**: numpy, pandas, requests, scipy, matplotlib, pillow, pytest, flask, django, beautifulsoup4
- **JavaScript**: lodash, axios, moment, express, react, vue, jest, mocha, chalk, commander

자세한 사용법은 [QUICKSTART.md](QUICKSTART.md)를 참조하세요.

## 다음 단계

### 로컬 우선 개발 (현재 단계)

**✅ 완료된 로컬 기능**:
- Phase 2.3: C# 런타임 지원
- Phase 2.4: 패키지 의존성 설치 (Python, JavaScript)
- Phase 5.1: 구조화된 로깅 (structlog)
- Phase 5.2: Prometheus 메트릭 수집

**🔄 다음 우선순위**:

1. **Phase 5.3: 고급 기능** (선택 단계)
   - gVisor로 추가 격리 계층
   - Seccomp 프로필 커스터마이징
   - 실시간 리소스 사용량 모니터링
   - 작업 우선순위 큐

2. **Grafana 대시보드 구성** (관찰성 완성)
   - Prometheus 데이터 소스 연동
   - 코드 실행 메트릭 시각화
   - API 요청 모니터링 대시보드
   - 리소스 사용량 추적

3. **Phase 6: Kubernetes 배포** (배포 환경)
   - K8s 매니페스트 작성
   - Helm 차트
   - 오토스케일링
   - 프로덕션 보안 설정

자세한 로드맵은 [docs/TASKS.md](docs/TASKS.md)를 참조하세요.
