# CodeBeaker 아키텍처 설계

## 목차

1. [시스템 개요](#시스템-개요)
2. [아키텍처 원칙](#아키텍처-원칙)
3. [계층 구조](#계층-구조)
4. [핵심 컴포넌트](#핵심-컴포넌트)
5. [데이터 흐름](#데이터-흐름)
6. [배포 아키텍처](#배포-아키텍처)

---

## 시스템 개요

CodeBeaker는 **다중 언어 코드를 안전하게 격리된 환경에서 실행하는 프레임워크**입니다. 시스템의 핵심 목표는:

1. **보안**: 신뢰할 수 없는 코드로부터 호스트 시스템 보호
2. **격리**: 실행 간 완전한 독립성 보장
3. **확장성**: 수평 확장을 통한 처리량 증대
4. **관찰성**: 모든 실행에 대한 상세 메트릭 및 로그 수집
5. **언어 독립성**: 통일된 인터페이스를 통한 다중 언어 지원

---

## 아키텍처 원칙

### 1. 다층 보안 (Defense in Depth)

단일 보안 계층에 의존하지 않고, 여러 독립적인 보안 메커니즘을 조합:

- **격리 계층**: Docker/gVisor/Firecracker
- **시스템 계층**: seccomp, AppArmor/SELinux, capabilities
- **네트워크 계층**: 기본 차단 + 화이트리스트
- **리소스 계층**: cgroups를 통한 제한

### 2. 상태 비저장 (Stateless Workers)

워커는 실행 중인 작업 외에는 어떠한 상태도 유지하지 않음:

- 수평 확장 용이
- 장애 복구 간소화
- 롤링 업데이트 무중단 가능
- 리소스 효율성 향상

### 3. 비동기 우선 (Async-First)

장기 실행 작업을 위한 큐 기반 아키텍처:

- 클라이언트는 즉시 작업 ID 수신
- 큐를 통한 작업 분산
- 워커 풀에서 비동기 처리
- 결과는 폴링 또는 웹훅으로 전달

### 4. 관찰 가능성 (Observability by Design)

시스템의 모든 계층에서 메트릭, 로그, 추적 수집:

- 구조화된 JSON 로깅
- Prometheus 메트릭 노출
- OpenTelemetry 분산 추적
- 실행 이력 시계열 저장

---

## 계층 구조

CodeBeaker는 5개의 주요 계층으로 구성됩니다:

```
┌─────────────────────────────────────────────────────────────┐
│                  관찰성 계층 (Layer 5)                        │
│  Prometheus + Grafana │ Loki │ OpenTelemetry                │
│  메트릭 수집          │ 로그 │ 분산 추적                      │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│                   격리 계층 (Layer 4)                         │
│  ┌─────────────┬─────────────┬──────────────┐              │
│  │   Docker    │   gVisor    │  Firecracker │              │
│  │   (기본)     │(프로덕션)    │  (고급)       │              │
│  └─────────────┴─────────────┴──────────────┘              │
│  + seccomp-bpf + AppArmor + cgroups v2                      │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│                 실행 계층 (Layer 3)                           │
│  ┌──────────┬──────────┬──────────┬──────────┐             │
│  │ Python   │   C#     │ Node.js  │   Go     │             │
│  │ Runtime  │ Runtime  │ Runtime  │ Runtime  │             │
│  └──────────┴──────────┴──────────┴──────────┘             │
│  공통: prepare() │ execute() │ cleanup()                     │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│              오케스트레이션 계층 (Layer 2)                     │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────┐  │
│  │ Message Queue│→│ Worker Pool  │→│ Result Storage  │  │
│  │(RabbitMQ/    │  │(Stateless    │  │(S3/Azure Blob)  │  │
│  │ Redis)       │  │ Horizontal)  │  │                 │  │
│  └──────────────┘  └──────────────┘  └─────────────────┘  │
│  ┌────────────────────────────────────────────────────┐    │
│  │ Metadata DB: PostgreSQL (실행 이력, 메타데이터)     │    │
│  └────────────────────────────────────────────────────┘    │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│                   API 계층 (Layer 1)                          │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────┐  │
│  │ REST API     │  │ gRPC API     │  │ WebSocket       │  │
│  │ (기본)        │  │(고성능)       │  │(스트리밍)        │  │
│  └──────────────┘  └──────────────┘  └─────────────────┘  │
│  + 인증/인가 + 속도 제한 + 요청 검증                          │
└─────────────────────────────────────────────────────────────┘
```

---

## 핵심 컴포넌트

### Layer 1: API 계층

#### API Gateway

**역할**: 클라이언트 요청 접수 및 검증

**기능**:
- REST API (동기/비동기 실행)
- gRPC API (고성능 바이너리 프로토콜)
- WebSocket (실시간 실행 스트리밍)
- 인증 및 인가 (JWT, API Key)
- 속도 제한 (사용자별, 전역)
- 요청 검증 (스키마 검증, 크기 제한)

**기술 선택**:
- **언어**: Go (성능) 또는 TypeScript (생산성)
- **프레임워크**: Gin/Fiber (Go), NestJS (TypeScript)
- **문서화**: OpenAPI/Swagger

---

### Layer 2: 오케스트레이션 계층

#### Message Queue

**역할**: 실행 요청을 워커에게 분산

**기능**:
- 비동기 작업 큐잉
- 우선순위 기반 스케줄링
- Dead Letter Queue (DLQ) 처리
- 재시도 정책

**기술 선택**:
| 기술 | 장점 | 단점 | 사용 시나리오 |
|------|------|------|--------------|
| **RabbitMQ** | 보장된 전달, 복잡한 라우팅 | 운영 복잡도 | 프로덕션, 메시지 손실 불가 |
| **Redis** | 빠름, 단순함 | 내구성 낮음 | 개발, 빠른 프로토타이핑 |
| **Azure Service Bus** | 관리형, 엔터프라이즈 기능 | 비용, 벤더 락인 | Azure 환경 |

#### Worker Pool

**역할**: 큐에서 작업을 가져와 실행

**설계 원칙**:
- Pull 모델 (워커가 능동적으로 큐에서 작업 가져옴)
- 상태 비저장 (실행 중인 작업만 메모리에 유지)
- 수평 확장 (워커 추가/제거 자동)
- 장애 복구 (워커 실패 시 작업 재분배)

**구성**:
```yaml
worker_pool:
  min_workers: 2
  max_workers: 50
  scale_metric: queue_depth
  scale_threshold: 10  # 큐에 10개 이상이면 스케일 아웃
  health_check_interval: 30s
```

#### Metadata Database

**역할**: 실행 메타데이터 및 이력 저장

**스키마**:
```sql
-- 실행 요청
CREATE TABLE executions (
    id UUID PRIMARY KEY,
    user_id VARCHAR(255),
    language VARCHAR(50),
    code_hash VARCHAR(64),
    status VARCHAR(50),  -- pending, running, completed, failed
    created_at TIMESTAMP,
    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    worker_id VARCHAR(255)
);

-- 실행 결과
CREATE TABLE execution_results (
    execution_id UUID PRIMARY KEY,
    exit_code INT,
    stdout TEXT,
    stderr TEXT,
    duration_ms INT,
    memory_mb INT,
    cpu_percent FLOAT,
    error_type VARCHAR(100),
    FOREIGN KEY (execution_id) REFERENCES executions(id)
);

-- 메트릭 (시계열)
CREATE TABLE execution_metrics (
    timestamp TIMESTAMPTZ,
    execution_id UUID,
    metric_name VARCHAR(100),
    metric_value FLOAT
);
```

**기술 선택**:
- **PostgreSQL**: 일반 메타데이터
- **TimescaleDB**: 시계열 메트릭 (PostgreSQL 확장)
- **InfluxDB**: 대규모 시계열 (100M+ 레코드)

#### Result Storage

**역할**: 실행 결과 및 아티팩트 저장

**스토리지 계층**:
1. **Hot Storage** (최근 24시간): S3 Standard, 빠른 접근
2. **Warm Storage** (7-30일): S3 Infrequent Access
3. **Cold Storage** (90일+): S3 Glacier, 아카이브

**저장 구조**:
```
s3://codebeaker-results/
├── YYYY/MM/DD/
│   ├── {execution_id}/
│   │   ├── metadata.json
│   │   ├── stdout.txt
│   │   ├── stderr.txt
│   │   └── artifacts/
│   │       ├── output.dat
│   │       └── ...
```

---

### Layer 3: 실행 계층

#### Runtime Adapters

각 언어별 런타임 어댑터는 공통 인터페이스를 구현:

```python
class BaseRuntime(ABC):
    @abstractmethod
    def prepare(self, code: str, config: RuntimeConfig) -> PrepareResult:
        """코드 준비 (파일 작성, 의존성 설치)"""
        pass

    @abstractmethod
    def execute(self, context: ExecutionContext) -> ExecutionResult:
        """코드 실행 및 결과 수집"""
        pass

    @abstractmethod
    def cleanup(self, context: ExecutionContext) -> None:
        """리소스 정리"""
        pass
```

#### Python Runtime

**구현**:
- CPython 3.8-3.12 지원
- venv를 통한 의존성 격리
- pip를 통한 패키지 설치 (화이트리스트)

**최적화**:
- 일반 패키지 사전 설치 (numpy, pandas, requests)
- .pyc 바이트코드 캐싱
- Docker 레이어 캐싱

**보안**:
- AST 분석으로 위험 패턴 감지 (`eval`, `exec`, `__import__`)
- `--no-deps` 플래그로 의존성 자동 설치 차단
- 읽기 전용 venv 마운트

#### C# Runtime

**구현**:
- .NET 6, 8 지원
- ReadyToRun AOT 컴파일 (20-30% 콜드 스타트 감소)
- NuGet 패키지 관리

**최적화**:
- 다단계 Docker 빌드 (SDK → Runtime)
- NuGet 캐시 재사용
- 계층화된 컴파일 (Tiered Compilation)

**보안**:
- 별도 프로세스 격리 (AppDomain 사용 불가)
- `--locked-mode` 복원으로 패키지 고정
- 컨테이너 수준 격리

#### JavaScript/Node.js Runtime

**구현**:
- Node.js 18, 20 지원
- isolated-vm을 통한 V8 격리 (선택적)
- npm 패키지 관리

**최적화**:
- `npm ci` 사용 (2-10배 빠름)
- V8 스냅샷 (컨텍스트 생성 40ms → 2ms)
- Worker Threads (신뢰할 수 있는 코드)

**보안**:
- 전체 프로세스 격리 (vm2 사용 불가 - 취약점)
- `--ignore-scripts` 플래그로 설치 스크립트 차단
- npm audit를 통한 취약점 스캔

---

### Layer 4: 격리 계층

#### Docker (Phase 1)

**기본 격리 메커니즘**

**설정**:
```bash
docker run \
  --security-opt no-new-privileges \
  --cap-drop=ALL \
  --read-only \
  --tmpfs /tmp:rw,noexec,nosuid,size=100m \
  --cpus="0.5" \
  --memory="256m" \
  --memory-swap="256m" \
  --pids-limit=100 \
  --network=none \
  -u 1000:1000 \
  python-runner:latest
```

**장점**:
- 빠른 구현 (1-2주)
- 성숙한 생태계
- 낮은 오버헤드 (0-3% CPU, 12-50MB 메모리)

**단점**:
- 공유 커널 (컨테이너 탈출 위험)
- 추가 강화 필요

#### gVisor (Phase 2-3)

**사용자 공간 커널을 통한 강화된 격리**

**설정**:
```json
{
  "runtimes": {
    "runsc": {
      "path": "/usr/local/bin/runsc",
      "runtimeArgs": [
        "--platform=ptrace"
      ]
    }
  }
}
```

**실행**:
```bash
docker run --runtime=runsc ...
```

**장점**:
- syscall 공격 표면 감소 (300+ → 70개)
- VM 수준 격리, 컨테이너 성능
- 10-15% CPU 오버헤드 (허용 가능)

**단점**:
- I/O 집약적 워크로드에서 30% 오버헤드
- 일부 syscall 미지원 (호환성 확인 필요)

#### Firecracker (Phase 4-5)

**하드웨어 가상화를 통한 최고 수준 격리**

**특징**:
- 각 실행마다 독립된 커널
- 125ms 콜드 스타트
- <5MB 메모리 오버헤드
- AWS Lambda 검증 기술

**사용 사례**:
- 멀티테넌트 고위험 코드 실행
- 규제 산업 (금융, 헬스케어)
- 대규모 (1000+ 동시 실행)

**제약**:
- Linux 전용 (KVM 필요)
- 운영 복잡도 높음
- 블록 디바이스 필요 (overlay2 미지원)

---

### Layer 5: 관찰성 계층

#### Prometheus + Grafana

**메트릭 수집**:
```yaml
# 주요 메트릭
- codebeaker_execution_duration_seconds (histogram)
- codebeaker_execution_total (counter)
- codebeaker_execution_failures_total (counter)
- codebeaker_queue_depth (gauge)
- codebeaker_worker_count (gauge)
- codebeaker_memory_usage_bytes (gauge)
```

**대시보드**:
- 실행 성공률 (success rate)
- P50, P95, P99 지연 시간
- 언어별 실행 분포
- 워커 리소스 사용량
- 큐 깊이 및 백로그

#### Loki

**로그 수집 및 집계**

**구조화된 로깅**:
```json
{
  "timestamp": "2025-01-15T10:30:45.123Z",
  "level": "info",
  "service": "worker",
  "execution_id": "a1b2c3d4",
  "language": "python",
  "duration_ms": 1234,
  "status": "success",
  "message": "Execution completed"
}
```

#### OpenTelemetry

**분산 추적**:
- API 요청 → 큐 삽입 → 워커 처리 → 결과 저장
- 각 단계의 지연 시간 추적
- 실패 지점 식별

---

## 데이터 흐름

### 동기 실행 흐름

```
Client → API Gateway → Execute Directly → Return Result
  1. POST /execute {code, language, config}
  2. 요청 검증 및 인증
  3. 워커 직접 실행 (타임아웃 <5초)
  4. 결과 즉시 반환
```

### 비동기 실행 흐름

```
Client → API Gateway → Queue → Worker Pool → Result Storage
  1. POST /execute/async {code, language, config}
  2. 큐에 작업 삽입
  3. 실행 ID 즉시 반환
  4. 워커가 큐에서 작업 가져옴
  5. 코드 실행
  6. 결과를 S3/Blob Storage에 저장
  7. 메타데이터를 DB에 저장
  8. (선택) 웹훅 전송 또는 폴링 응답
```

### 상세 실행 흐름

```
1. Prepare Phase
   ├─ 코드 파일 작성
   ├─ 의존성 설치 (허용 목록 검증)
   └─ 컨테이너/VM 생성

2. Execute Phase
   ├─ 컨테이너 시작
   ├─ 리소스 제한 적용
   ├─ 타임아웃 설정
   ├─ stdout/stderr 캡처
   └─ 종료 코드 수집

3. Monitor Phase
   ├─ CPU 사용량 샘플링
   ├─ 메모리 사용량 추적
   ├─ I/O 통계 수집
   └─ 실행 시간 측정

4. Cleanup Phase
   ├─ 컨테이너/VM 종료
   ├─ 임시 파일 삭제
   ├─ 리소스 해제
   └─ 메트릭 전송
```

---

## 배포 아키텍처

### 개발 환경 (Phase 1)

```
┌─────────────────────────────────────┐
│  Developer Machine                  │
│  ├─ Docker Desktop                  │
│  ├─ PostgreSQL (Docker)             │
│  ├─ Redis (Docker)                  │
│  └─ CodeBeaker API (localhost)      │
└─────────────────────────────────────┘
```

### Azure 프로덕션 환경 (Phase 2-4)

```
┌────────────────────────────────────────────────────────────┐
│  Azure Container Apps (API + Frontend)                     │
│  ├─ Auto-scaling (scale-to-zero)                           │
│  ├─ Built-in HTTPS                                         │
│  └─ VNET Integration                                       │
└───────────────────┬────────────────────────────────────────┘
                    │
┌───────────────────▼────────────────────────────────────────┐
│  Azure Kubernetes Service (Worker Pool)                    │
│  ├─ Pod Sandboxing (Kata Containers)                       │
│  ├─ Horizontal Pod Autoscaler                              │
│  ├─ Network Policies (Calico)                              │
│  └─ Azure Monitor + Prometheus                             │
└───────────────────┬────────────────────────────────────────┘
                    │
┌───────────────────▼────────────────────────────────────────┐
│  Data Layer                                                │
│  ├─ Azure Database for PostgreSQL                          │
│  ├─ Azure Cache for Redis                                  │
│  ├─ Azure Blob Storage (결과 저장)                          │
│  └─ Azure Key Vault (비밀 관리)                             │
└────────────────────────────────────────────────────────────┘
```

### 하이브리드 환경 (Phase 5)

```
┌─────────────────────────────────────────────────────────────┐
│  Azure Cloud                                                │
│  ├─ Azure Arc (통합 관리)                                    │
│  ├─ AKS (클라우드 워커)                                      │
│  └─ Blob Storage                                            │
└─────────────────────┬───────────────────────────────────────┘
                      │ Azure Arc
┌─────────────────────▼───────────────────────────────────────┐
│  On-Premises                                                │
│  ├─ Kubernetes (Arc-enabled)                                │
│  ├─ gVisor/Kata Containers                                  │
│  ├─ MinIO (S3-compatible)                                   │
│  └─ Connected Registry                                      │
└─────────────────────────────────────────────────────────────┘
```

---

## 확장 및 성능

### 수평 확장 전략

**API 계층**:
- Stateless 설계로 무제한 확장
- Azure Container Apps Auto-scaling
- 최소 2 인스턴스 (HA)

**워커 풀**:
- HPA (Horizontal Pod Autoscaler)
- 메트릭: 큐 깊이, CPU, 메모리
- 스케일링 정책:
  ```yaml
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: codebeaker-worker
  minReplicas: 2
  maxReplicas: 50
  metrics:
  - type: External
    external:
      metric:
        name: queue_depth
      target:
        type: AverageValue
        averageValue: "10"
  ```

### 성능 목표

| 메트릭 | 목표 | 측정 방법 |
|--------|------|----------|
| API 응답 시간 (동기) | <100ms | P95 |
| 큐 대기 시간 | <1초 | 평균 |
| 콜드 스타트 (Docker) | <2초 | P95 |
| 콜드 스타트 (gVisor) | <3초 | P95 |
| 처리량 | 100 req/sec/워커 | 평균 |

---

## 보안 아키텍처

상세한 보안 설계는 [SECURITY.md](SECURITY.md)를 참조하세요.

**핵심 원칙**:
1. 최소 권한 (Least Privilege)
2. 다층 방어 (Defense in Depth)
3. 기본 차단 (Deny by Default)
4. 감사 및 모니터링 (Audit Everything)

---

## 다음 단계

1. [보안 설계](SECURITY.md) - 상세 보안 전략 및 위협 모델
2. [구현 로드맵](TASKS.md) - 페이즈별 구현 계획
3. [성능 최적화](PERFORMANCE.md) - 성능 튜닝 가이드
