# 🧪 CodeBeaker

**다중 언어 코드 실행을 위한 안전한 격리 환경 프레임워크**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

CodeBeaker는 여러 프로그래밍 언어의 코드를 안전한 격리된 환경에서 실행하고, 실행 과정을 모니터링하며, 결과를 수집하는 실행 인프라 프레임워크입니다.

---

## 🎯 프로젝트 목적

CodeBeaker는 **신뢰할 수 없는 코드를 안전하게 실행할 수 있는 인프라**를 제공합니다. AI 에이전트 시스템, 온라인 코딩 플랫폼, CI/CD 파이프라인, 교육 플랫폼 등에서 사용할 수 있는 코드 실행 "엔진"입니다.

### 핵심 원칙

**CodeBeaker는 "환경"이지 "애플리케이션"이 아닙니다**

- 실행 환경을 제공하지만, 코드를 생성하거나 분석하지 않습니다
- 안전한 샌드박스를 제공하지만, 무엇을 실행할지 결정하지 않습니다
- 결과를 수집하지만, 결과를 어떻게 사용할지 판단하지 않습니다

---

## 📌 CodeBeaker의 역할과 범위

### ✅ CodeBeaker가 제공하는 것

**실행 인프라**
- 격리된 실행 환경 (Docker, gVisor, Firecracker)
- 다중 언어 런타임 지원 (Python, C#, JavaScript, Go, Rust)
- 리소스 제어 및 제한 (CPU, 메모리, 타임아웃, 네트워크)

**모니터링 및 관찰**
- 실행 메트릭 수집 (실행 시간, 리소스 사용량)
- 에러 분석 및 분류
- 실행 이력 추적

**보안 및 격리**
- 다층 보안 (네트워크 격리, 파일시스템 제한, syscall 필터링)
- 샌드박싱 전략 (컨테이너, microVM)
- 리소스 DoS 방지

### ❌ CodeBeaker가 하지 않는 것

**상위 레벨 기능**
- 코드 생성 (AI/LLM 통합은 소비 애플리케이션의 책임)
- 코드 분석 및 개선 (정적 분석, 최적화 제안)
- 비즈니스 로직 및 의사결정
- 사용자 인터페이스 제공

**데이터 관리**
- 영구 데이터 저장소 (실행 결과 저장은 소비 앱의 책임)
- 사용자 인증 및 권한 관리
- 청구 및 할당량 관리

---

## ✨ 주요 기능

**보안 및 격리**
- 🔒 다층 격리 전략 (Docker, gVisor, Firecracker microVM)
- 🛡️ syscall 필터링 및 capability 제거
- 🌐 네트워크 격리 및 화이트리스트 기반 접근 제어

**다중 언어 지원**
- 🐍 Python (3.8+)
- ⚙️ C# (.NET 6, 8)
- 📜 JavaScript/Node.js (18, 20)
- 🔷 TypeScript (계획 중)
- 🚀 Go, Rust (계획 중)

**실행 및 모니터링**
- ⏱️ 실시간 실행 메트릭 (시간, 메모리, CPU 사용률)
- 📊 상세 에러 분류 및 스택 트레이스
- 🔄 실행 이력 및 버전 관리
- ⚡ 리소스 제한 (CPU, 메모리, 타임아웃, 프로세스 수)

**확장성 및 성능**
- 📦 큐 기반 비동기 실행
- 🔁 수평 확장 가능한 워커 풀
- 💾 결과 캐싱 및 스토리지 계층화
- 🎯 우선순위 기반 작업 스케줄링

---

## 🎯 사용 사례

### AI 에이전트 시스템
- AI가 생성한 코드를 안전한 샌드박스에서 실행
- 실행 결과 검증 및 에러 피드백
- Self-improving 루프 구현

### 온라인 코딩 플랫폼
- 사용자 제출 코드의 안전한 실행 및 채점
- 경쟁 프로그래밍 저지 시스템
- 실시간 코드 실행 및 결과 피드백

### CI/CD 파이프라인
- 배포 전 코드 검증 및 테스트 자동화
- 다중 환경 테스트 실행
- 성능 벤치마킹 및 회귀 탐지

### 교육 플랫폼
- 학생 코드 안전 실행 및 자동 채점
- 실시간 코드 실습 환경 제공
- 에러 분석 및 학습 피드백

### 코드 분석 및 벤치마킹
- 알고리즘 성능 비교 및 분석
- 코드 품질 메트릭 수집
- 리소스 사용량 프로파일링

---

## 🏗️ 아키텍처 개요

CodeBeaker는 계층화된 아키텍처로 설계되었습니다:

**API 계층**
- REST API 또는 gRPC를 통한 실행 요청 접수
- 인증, 속도 제한, 요청 검증

**오케스트레이션 계층**
- 메시지 큐 기반 작업 분산 (RabbitMQ, Redis, Azure Service Bus)
- 상태 비저장 워커 풀을 통한 수평 확장
- 우선순위 기반 스케줄링

**실행 계층**
- 언어별 런타임 어댑터 (Python, C#, JavaScript, Go, Rust)
- 의존성 관리 및 패키지 설치
- 코드 컴파일 및 실행

**격리 계층**
- Docker 컨테이너 기본 격리
- gVisor 또는 Firecracker microVM을 통한 강화된 보안
- cgroups를 통한 리소스 제한 (CPU, 메모리, I/O)

**관찰성 계층**
- Prometheus + Grafana를 통한 메트릭 수집 및 시각화
- 구조화된 로깅 (JSON) 및 로그 집계
- 분산 추적 (OpenTelemetry)

상세한 아키텍처 설계는 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)를 참조하세요.

---

## 🌍 지원 언어

| 언어       | 상태 | 버전           | 우선순위 |
|-----------|------|----------------|---------|
| Python    | 📋   | 3.8 - 3.12     | 1순위   |
| C#        | 📋   | .NET 6, 8      | 1순위   |
| JavaScript| 📋   | Node 18, 20    | 1순위   |
| TypeScript| 📋   | 5.x            | 2순위   |
| Go        | 📋   | 1.20+          | 2순위   |
| Rust      | 📋   | 계획 중         | 3순위   |
| Java      | 📋   | 계획 중         | 3순위   |

**개발 우선순위**: Python → C# → JavaScript/Node.js → TypeScript → Go → Rust/Java

---

## 🔒 보안 원칙

CodeBeaker는 다층 보안 아키텍처를 채택합니다:

**격리 계층**
- Docker 컨테이너 기본 격리
- gVisor 또는 Firecracker microVM을 통한 강화된 격리
- 읽기 전용 파일시스템 + tmpfs 쓰기 영역

**시스템 보안**
- seccomp-bpf를 통한 syscall 필터링
- AppArmor/SELinux 프로필 적용
- 모든 capabilities 제거 + 필요시 최소 권한만 부여

**네트워크 보안**
- 기본적으로 모든 네트워크 차단
- 화이트리스트 기반 외부 접근 (PyPI, npm, NuGet 등)
- 내부 네트워크 격리 (메타데이터 서비스 차단)

**리소스 보안**
- cgroups v2를 통한 CPU, 메모리, I/O 제한
- 프로세스 및 파일 디스크립터 제한
- wall-time 및 CPU-time 타임아웃

상세한 보안 설계는 [docs/SECURITY.md](docs/SECURITY.md)를 참조하세요.

---

## 📖 문서

**아키텍처 및 설계**
- [아키텍처 개요](docs/ARCHITECTURE.md) - 시스템 아키텍처 및 계층 설계
- [보안 설계](docs/SECURITY.md) - 다층 보안 전략 및 위협 모델
- [성능 최적화](docs/PERFORMANCE.md) - 콜드 스타트 최적화 및 리소스 관리

**구현 가이드**
- [구현 로드맵](docs/TASKS.md) - 페이즈별 구현 계획 및 마일스톤
- [연구 자료](docs/research.md) - 기술 스택 비교 및 프로덕션 시스템 분석
- [Azure 배포 가이드](docs/research-1.md) - Azure 기반 프로덕션 아키텍처

**개발 참여**
- [기여 가이드](CONTRIBUTING.md) - 기여 방법 및 코드 스타일
- [개발 환경 설정](docs/development.md) - 로컬 개발 환경 구성

---

## 🗺️ 개발 로드맵

상세한 구현 계획은 [docs/TASKS.md](docs/TASKS.md)를 참조하세요.

**Phase 1: 기본 실행 환경** (1-2개월)
- 단일 언어 (Python) Docker 기반 실행
- 기본 리소스 제한 및 타임아웃
- 동기식 실행 API

**Phase 2: 다중 언어 지원** (3-4개월)
- Python, C#, JavaScript 런타임 구현
- 비동기 실행 큐 시스템
- 기본 모니터링 및 메트릭

**Phase 3: 보안 강화** (5-6개월)
- gVisor 통합
- 네트워크 격리 및 정책
- seccomp, AppArmor 프로필

**Phase 4: 프로덕션 준비** (7-9개월)
- Prometheus + Grafana 관찰성
- Kubernetes 배포
- 성능 최적화 및 캐싱

**Phase 5: 고급 격리** (10-12개월)
- Firecracker microVM 통합
- 스냅샷 및 복원 지원
- 엣지 배포 지원

---

## 📄 라이선스

이 프로젝트는 MIT 라이선스 하에 배포됩니다. 자세한 내용은 [LICENSE](LICENSE) 파일을 참조하세요.

---

## 🙏 영감을 받은 프로젝트

CodeBeaker는 다음 프로덕션 시스템들의 설계와 경험을 참고했습니다:

- **Judge0** - 경쟁 프로그래밍 플랫폼의 검증된 Isolate 샌드박싱 패턴
- **Piston** - 경량 실행 엔진 및 WebSocket 스트리밍 아키텍처
- **E2B** - Firecracker 기반 AI 에이전트 코드 실행 환경
- **AWS Lambda** - 대규모 서버리스 실행 인프라 및 최적화 기법
- **Loopai** - Self-improving 프로그램 실행 루프 패턴

---

## 📧 커뮤니티 및 지원

- **이슈 보고**: [GitHub Issues](https://github.com/iyulab/codebeaker/issues)
- **기능 제안**: [GitHub Discussions](https://github.com/iyulab/codebeaker/discussions)
- **기여 가이드**: [CONTRIBUTING.md](CONTRIBUTING.md)

---

**CodeBeaker - 안전하고 확장 가능한 코드 실행 인프라** 🧪
