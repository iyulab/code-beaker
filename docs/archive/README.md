# Archive - 개발 과정 문서

이 폴더에는 CodeBeaker 개발 과정에서 생성된 상세 문서들이 보관되어 있습니다.

---

## 📋 Phase 완료 보고서

### Phase 1: JSON-RPC + WebSocket
- **PHASE1_COMPLETE.md**: JSON-RPC 2.0 + WebSocket 구현 상세
- 실시간 스트리밍, WebSocket transport layer

### Phase 2: Custom Command Interface
- **PHASE2_COMPLETE.md**: 7가지 Command 타입 시스템
- **PHASE2_PROGRESS.md**: Phase 2 개발 진행 중간 보고
- Command executor, Runtime adapter 리팩토링

### Phase 3: Session Management
- **PHASE3_COMPLETE.md**: Session-based stateful execution
- SessionManager, SessionCleanupWorker

### 통합 테스트
- **INTEGRATION_TESTS_COMPLETE.md**: 17개 통합 테스트 상세
- SessionManagerTests (10개), SessionJsonRpcTests (7개)

---

## 🏗️ 아키텍처 & 마이그레이션

### 마이그레이션
- **MIGRATION.md**: Python → C# .NET 8.0 마이그레이션 과정
- **CSHARP_ARCHITECTURE.md**: C# 기반 아키텍처 설계
- **CSHARP_SETUP.md**: C# 프로젝트 설정 가이드

### 아키텍처 상세
- **FILESYSTEM_ARCHITECTURE.md**: Queue/Storage 파일시스템 설계
- 구버전 파일시스템 기반 아키텍처 (현재는 Session 기반으로 발전)

---

## 🔬 연구 & 분석

### 연구 문서
- **research.md**: 심층 연구 문서 (65KB)
  - E2B, Jupyter, LSP, DAP 분석
  - 성능 벤치마크 연구
  - 보안 모델 분석 (gVisor, Firecracker)
  - 아키텍처 패턴 연구

### 완료 요약
- **COMPLETION_SUMMARY.md**: 초기 구현 완료 요약

---

## 🧪 테스트 & 벤치마크

### 테스트 자동화
- **TEST_AUTOMATION.md**: CI/CD 테스트 자동화
- **LOCAL_TESTING.md**: 로컬 테스트 가이드

### 성능 벤치마크
- **INFRASTRUCTURE_BENCHMARKS.md**: 인프라 성능 벤치마크 결과

---

## 📌 문서 사용 가이드

### 최신 정보가 필요한 경우
상위 폴더의 핵심 문서를 참조하세요:
- **../ARCHITECTURE.md**: 최신 시스템 아키텍처
- **../PRODUCTION_READY.md**: 프로덕션 배포 가이드
- **../USAGE.md**: API 사용 가이드
- **../DEVELOPMENT_HISTORY.md**: 개발 과정 요약

### 이 폴더의 문서들은
- 개발 과정의 상세 기록
- 의사결정 배경 및 근거
- 특정 Phase의 구현 세부사항
- 연구 및 분석 자료

---

**보관 날짜**: 2025-10-27
**Phase 1-3 완료 후 정리됨**
