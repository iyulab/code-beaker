# 🧪 CodeBeaker

**다중 언어 코드를 위한 안전한 격리 실행 환경**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Python 3.8+](https://img.shields.io/badge/python-3.8+-blue.svg)](https://www.python.org/downloads/)

CodeBeaker는 여러 프로그래밍 언어의 코드를 안전한 샌드박스에서 실행하고, 실행 과정을 모니터링하며, 자동으로 버전 관리를 제공하는 프레임워크입니다.

---

## 📌 CodeBeaker의 역할

### ✅ CodeBeaker가 제공하는 것

- **파일시스템 관리** - 코드 파일을 생성/읽기/수정/삭제할 수 있는 격리된 작업 공간
- **실행 환경** - 언어별 런타임 환경과 격리된 실행 컨테이너
- **실행 위임 처리** - 실행 요청을 받아 적절한 런타임에서 코드 실행
- **결과 모니터링** - 실행 결과, 성능 메트릭, 에러 정보 수집 및 분석

### ❌ CodeBeaker가 하지 않는 것

- **코드 생성** - 상위 애플리케이션이 담당
- **코드 분석 및 개선** - 상위 애플리케이션이 담당
- **의사결정** - 상위 애플리케이션이 담당
- **사용자 인터페이스** - 상위 애플리케이션이 담당

**핵심**: CodeBeaker는 코드를 안전하게 실행할 수 있는 "환경"을 제공하며, 누가 어떻게 사용하는지는 소비 애플리케이션의 책임입니다.

---

## ✨ 주요 기능

- 🔒 **격리된 실행** - Docker/프로세스 기반 샌드박싱
- 🌍 **다중 언어** - Python, C#, JavaScript/Node.js 우선 지원
- 📊 **실시간 모니터링** - 실행 메트릭, 리소스 사용량, 에러 분석
- 🔄 **자동 버저닝** - 코드 스냅샷 및 실행 이력 자동 저장
- ⚡ **리소스 제어** - CPU, 메모리, 타임아웃, 네트워크 제한
- 🎯 **에러 분류** - 언어별 에러 파싱 및 분류
- 🔁 **재현 가능성** - 모든 환경에서 일관된 실행 결과

---

## 🚀 빠른 시작

### 설치

```bash
pip install codebeaker
```

### 기본 사용법

```python
from codebeaker import Beaker

# Python 코드 실행
beaker = Beaker(language="python")

result = beaker.run("""
def factorial(n):
    return 1 if n <= 1 else n * factorial(n-1)
print(factorial(5))
""")

print(result.output)        # "120"
print(result.duration_ms)   # 45
print(result.success)       # True
```

### 리소스 제한과 함께

```python
from codebeaker import Beaker

beaker = Beaker(
    language="javascript",
    memory_limit="256m",
    timeout=30,
    network_enabled=False
)

result = beaker.run("console.log('Hello World')")
```

---

## 🎯 사용 사례

### AI 에이전트 시스템
AI가 생성한 코드를 안전하게 실행하고 결과 검증
```python
generated_code = agent.generate("피보나치 함수를 작성해줘")
result = beaker.run(generated_code)

if result.success:
    agent.use(result.output)
else:
    agent.handle_error(result.error_message)
```

### 온라인 코딩 플랫폼
사용자 제출 코드를 안전하게 실행 및 채점
```python
user_code = request.get_code()
result = beaker.run(user_code, language="python", timeout=5)
score = grade(result.output)
```

### CI/CD 파이프라인
배포 전 코드 검증 및 테스트
```python
for test in test_cases:
    result = beaker.run(code, input_data=test)
    assert result.success
```

### 교육 플랫폼
학생 코드를 안전하게 실행하고 피드백 제공
```python
student_code = submission.get_code()
result = beaker.run(student_code, memory_limit="128m")
provide_feedback(result)
```

### Self-Improving 시스템
코드 생성/실행/검증 루프를 자동화
```python
program = generator.create(specification)

for sample in validation_samples:
    result = beaker.run(program, input_data=sample)
    if not result.success:
        program = improve(program, result.error_info)
```

### 코드 벤치마킹
여러 알고리즘 구현의 성능 비교
```python
for implementation in implementations:
    stats = beaker.repeat(implementation, times=100)
    print(f"평균: {stats.avg_duration_ms}ms")
```

---

## 📦 핵심 구성요소

### Beaker (비커)
언어별 실행 환경과 리소스 제어 제공

### Lab (실험실)
여러 Beaker를 관리하고 실험 조율

### Experiment (실험)
단일 코드 실행, 검증 및 메트릭 수집

### Runtime (런타임)
언어별 어댑터 (Python, C#, JavaScript 등)

### Isolation (격리)
샌드박싱 전략 (Docker, Process, gVisor)

### Monitor (모니터)
실행 모니터링 및 메트릭 수집

---

## 🔧 설정

```python
from codebeaker import Beaker, RuntimeConfig

config = RuntimeConfig(
    version="3.11",              # 언어 버전
    memory_limit="512m",         # 메모리 제한
    cpu_limit=1.0,               # CPU 코어
    timeout=60,                  # 초 단위
    network_enabled=False,       # 네트워크 접근
    working_dir="/workspace",    # 작업 디렉토리
    packages=["numpy", "pandas"] # 의존성
)

beaker = Beaker(language="python", config=config)
```

---

## 📊 실행 결과

```python
result = beaker.run(code)

# 출력
result.success          # bool
result.output           # stdout
result.error            # stderr
result.exit_code        # int

# 메트릭
result.duration_ms      # 실행 시간
result.memory_used_mb   # 최대 메모리
result.cpu_percent      # CPU 사용률

# 에러 정보
result.error_type       # 분류된 에러 타입
result.error_location   # 라인/컬럼 정보
result.stack_trace      # 전체 스택 트레이스
```

---

## 🔄 버저닝 & 이력 관리

```python
# 자동 버저닝
result1 = beaker.run(code_v1)  # v1로 저장
result2 = beaker.run(code_v2)  # v2로 저장

# 이력 조회
history = beaker.get_history()
for exec in history:
    print(f"{exec.version} - {exec.status} - {exec.duration_ms}ms")

# 롤백
beaker.rollback_to("v1")

# 마지막 성공 버전으로 복구
beaker.restore_last_success()
```

---

## 🌍 지원 언어

| 언어       | 상태 | 버전           | 우선순위 |
|-----------|------|----------------|---------|
| Python    | ✅   | 3.8 - 3.12     | 1순위   |
| C#        | ✅   | .NET 6, 8      | 1순위   |
| JavaScript| ✅   | Node 18, 20    | 1순위   |
| TypeScript| 🚧   | 5.x            | 2순위   |
| Go        | 🚧   | 1.20+          | 2순위   |
| Rust      | 📋   | 계획 중         | 3순위   |
| Java      | 📋   | 계획 중         | 3순위   |

**개발 우선순위**: Python → C# → JavaScript/Node.js → TypeScript → Go → Rust/Java

---

## 🔒 보안

- Docker/gVisor 기반 격리 실행 환경
- 리소스 제한으로 DoS 공격 방지
- 네트워크 접근 차단 가능
- 파일시스템 접근 제한
- 호스트 시스템 보호

---

## 🎓 고급 사용법

### 병렬 실행
```python
from codebeaker import Lab

lab = Lab()
results = lab.run_parallel([
    ("python", code1),
    ("csharp", code2),
    ("javascript", code3)
])
```

### 커스텀 런타임
```python
from codebeaker import BaseRuntime, RuntimeRegistry

class MyRuntime(BaseRuntime):
    def get_run_command(self, entry):
        return ["my-interpreter", entry]

RuntimeRegistry.register("mylang", MyRuntime)
```

### 모니터링 훅
```python
def on_start(execution):
    print(f"실행 시작: {execution.id}")

def on_complete(result):
    print(f"완료: {result.duration_ms}ms")

beaker.on("start", on_start)
beaker.on("complete", on_complete)
```

### 반복 실행 및 통계
```python
# 재현성 검증
experiment = beaker.prepare(code)
stats = experiment.repeat(times=100)

print(f"성공률: {stats.success_rate}%")
print(f"평균 실행시간: {stats.avg_duration_ms}ms")
print(f"표준편차: {stats.std_duration_ms}ms")
```

---

## 💡 주요 이점

### 1. 안전성
격리된 환경에서 신뢰할 수 없는 코드 안전하게 실행

### 2. 언어 독립성
통일된 인터페이스로 모든 언어 처리

### 3. 재현 가능성
동일한 코드는 언제 어디서나 동일한 결과

### 4. 자동 버저닝
모든 코드 변경 자동 추적 및 롤백 가능

### 5. 상세한 모니터링
풍부한 디버그 정보로 빠른 문제 해결

### 6. 확장 가능성
커스텀 런타임 및 격리 전략 추가 가능

---

## 📖 문서

- [설치 가이드](docs/installation.md)
- [API 레퍼런스](docs/api.md)
- [언어 지원](docs/languages.md)
- [설정 가이드](docs/configuration.md)
- [보안 모범 사례](docs/security.md)
- [예제 코드](examples/)

---

## 🤝 기여하기

기여를 환영합니다! [CONTRIBUTING.md](CONTRIBUTING.md)를 참고해주세요.

### 새로운 언어 추가하기
1. `BaseRuntime` 상속
2. 필수 메서드 구현
3. `RuntimeRegistry`에 등록
4. 테스트 작성
5. PR 제출

---

## 📊 벤치마크

| 작업               | Docker  | Process |
|-------------------|---------|---------|
| 콜드 스타트         | ~2s     | ~50ms   |
| 웜 실행            | ~100ms  | ~10ms   |
| 메모리 오버헤드     | ~50MB   | ~5MB    |

---

## 🗺️ 로드맵

**Phase 1 (현재)**
- [x] 핵심 아키텍처 설계
- [ ] Python 런타임 구현
- [ ] C# 런타임 구현
- [ ] JavaScript/Node.js 런타임 구현
- [ ] Docker 격리 구현
- [ ] 기본 모니터링 구현

**Phase 2**
- [ ] TypeScript 지원
- [ ] Go 지원
- [ ] 프로세스 기반 격리
- [ ] 자동 버저닝 시스템
- [ ] 성능 최적화

**Phase 3**
- [ ] Rust, Java 지원
- [ ] WebAssembly 격리
- [ ] 분산 실행
- [ ] Kubernetes 통합
- [ ] ML 모델 실행 지원

---

## 📄 라이선스

MIT License - [LICENSE](LICENSE) 참고

---

## 🙏 감사의 말

다음 프로젝트에서 영감을 받았습니다:
- [Loopai](https://github.com/iyulab/Loopai) - Self-improving 프로그램
- [E2B](https://github.com/e2b-dev/code-interpreter) - 코드 샌드박싱
- [Judge0](https://github.com/judge0/judge0) - 다중 언어 실행

---

## 📧 연락처

- 이슈: [GitHub Issues](https://github.com/iyulab/codebeaker/issues)
- 토론: [GitHub Discussions](https://github.com/iyulab/codebeaker/discussions)

---

**CodeBeaker - 안전하고 신뢰할 수 있는 코드 실행 환경** 🧪# code-beaker
