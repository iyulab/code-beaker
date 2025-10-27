# Infrastructure Performance Benchmarks

CodeBeaker 인프라 프레임워크의 성능 특성 및 처리 능력 분석

## 벤치마크 환경

- **.NET 버전**: .NET 8.0
- **런타임**: RyuJIT AVX2
- **GC 모드**: Concurrent Workstation
- **측정 도구**: BenchmarkDotNet

---

## FileQueue 성능 (파일시스템 기반 큐)

### 단일 작업 처리

| 작업 | 평균 시간 | 처리량 (ops/sec) | 특징 |
|------|---------|----------------|------|
| Submit single task | ~710 μs | ~1,408 | 파일 생성 + atomic 작성 |
| Submit and retrieve | ~2ms | ~500 | FIFO 큐 조회 포함 |
| Full cycle (submit→retrieve→complete) | ~3ms | ~333 | 완전한 작업 사이클 |

### 배치 작업 처리

| 작업 | 평균 시간 | 작업당 시간 | 처리량 (tasks/sec) |
|------|---------|------------|-------------------|
| Submit 10 tasks | ~6.9ms | ~690μs/task | ~1,449 |
| Submit 100 tasks | ~65ms | ~650μs/task | ~1,538 |
| Full cycle: 10 tasks | ~30ms | ~3ms/task | ~333 |

### 성능 특성

**강점**:
- ✅ **안정적 지연시간**: 작업당 650-710μs (일관성)
- ✅ **선형 확장성**: 배치 크기에 선형 비례
- ✅ **외부 의존성 없음**: Redis/PostgreSQL 불필요
- ✅ **원자성 보장**: Atomic file operations으로 중복 없음

**제약사항**:
- ⚠️ **동시성 제한**: 3-10 워커 권장 (고도의 동시성은 타이밍 이슈 가능)
- ⚠️ **파일 I/O 오버헤드**: 디스크 기반 → 메모리 기반 큐 대비 느림
- ⚠️ **처리량**: ~1,500 tasks/sec (단일 큐 기준)

**권장 사용 시나리오**:
- 로컬 개발 환경
- 중소 규모 워크로드 (< 1,000 req/min)
- 외부 인프라 없는 단순 배포

**프로덕션 대안**:
- **고처리량 필요시**: Redis Queue (10,000+ ops/sec)
- **고신뢰성 필요시**: PostgreSQL Queue (트랜잭션 보장)
- **분산 환경**: RabbitMQ, AWS SQS, Azure Service Bus

---

## FileStorage 성능 (파일시스템 기반 저장소)

### 단일 결과 저장

| 작업 | 평균 시간 | 처리량 (ops/sec) | 특징 |
|------|---------|----------------|------|
| Save single result | ~1-2ms | ~500-1,000 | stdout/stderr 파일 분리 저장 |
| Save and retrieve | ~3-4ms | ~250-333 | JSON 직렬화/역직렬화 |

### 배치 저장

| 작업 | 평균 시간 | 작업당 시간 | 처리량 (results/sec) |
|------|---------|------------|---------------------|
| Save 10 results | ~15-20ms | ~1.5-2ms | ~500-666 |
| Save 100 results | ~150-200ms | ~1.5-2ms | ~500-666 |

### 성능 특성

**강점**:
- ✅ **일관된 성능**: 배치 크기와 무관하게 안정적
- ✅ **파일 기반 영속성**: 프로세스 재시작 후에도 보존
- ✅ **단순 구조**: 파일시스템만으로 완전 기능

**제약사항**:
- ⚠️ **검색 성능**: 대규모 데이터 조회는 비효율적 (인덱싱 없음)
- ⚠️ **동시 쓰기**: 대규모 동시 쓰기에는 제약

**권장 사용 시나리오**:
- 실행 결과 단순 저장/조회
- 작은 규모 데이터셋 (< 100,000 results)

**프로덕션 대안**:
- **고성능 조회**: Redis (메모리 캐싱)
- **대규모 저장**: PostgreSQL, MongoDB
- **장기 보관**: S3, Azure Blob Storage

---

## 인프라 성능 목표 vs 실측

| 항목 | 목표 | 실측 | 상태 |
|------|------|------|------|
| API 응답 시간 (p99) | < 5ms | ~5ms | ✅ 목표 달성 |
| 워커 처리량 | > 200 req/s | ~330 req/s | ✅ 목표 초과 |
| 메모리 사용 (API) | < 100MB | TBD | 🔄 측정 필요 |
| 동시 워커 수 | > 50개 | 3-10개 권장 | ⚠️ FileQueue 제약 |

---

## 프로덕션 배포 권장사항

### 소규모 배포 (< 100 req/min)
```yaml
인프라: FileQueue + FileStorage
장점: 단순, 외부 의존성 없음, 낮은 운영 비용
적합: 프로토타입, 내부 도구, 교육용
```

### 중규모 배포 (100-1,000 req/min)
```yaml
인프라: Redis Queue + FileStorage 또는 PostgreSQL
장점: 높은 처리량, 안정적 동시성
적합: 소비 앱 초기 버전, MVP
```

### 대규모 배포 (> 1,000 req/min)
```yaml
인프라: Redis/RabbitMQ Queue + PostgreSQL/MongoDB Storage
장점: 수평 확장, 고가용성, 분산 처리
적합: 프로덕션 서비스, 엔터프라이즈
```

---

## 벤치마크 실행 방법

```bash
cd benchmarks/CodeBeaker.Benchmarks
dotnet run -c Release
```

개별 벤치마크 실행:
```bash
dotnet run -c Release -- --filter "*QueueBenchmarks*"
dotnet run -c Release -- --filter "*StorageBenchmarks*"
```

---

## 다음 단계

1. **메모리 프로파일링**: 힙 사용량, GC 압력 측정
2. **동시성 벤치마크**: 3, 5, 10 워커 부하 테스트
3. **Redis Queue 구현**: IQueue 인터페이스 구현 및 성능 비교
4. **PostgreSQL Storage 구현**: IStorage 인터페이스 구현

---

**마지막 업데이트**: 2025-10-27
**벤치마크 버전**: v1.0.0
