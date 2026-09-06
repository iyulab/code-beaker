# CodeBeaker Documentation Index

이 리포지토리에 **실제로 커밋돼 있는** 문서만 싣는다. 링크는 fresh clone에서 그대로 열린다.

## 시작하기

- [README.md](README.md) — 프로젝트 개요, 빠른 시작, 구조
- [docs/USAGE.md](docs/USAGE.md) — WebSocket API 사용법과 예제
- [DEV_GUIDE.md](DEV_GUIDE.md) — 개발 환경 설정과 기여 절차

## 설계와 운영

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — 시스템 설계
- [docs/PRODUCTION_READY.md](docs/PRODUCTION_READY.md) — 프로덕션 준비 상태 (Phase 1-3 기준의 과거 문서)
- [k8s/README.md](k8s/README.md) — Kubernetes 배포 매니페스트
- [RELEASE_NOTES_v1.0.md](RELEASE_NOTES_v1.0.md) — v1.0 릴리스 노트

## 로드맵

- [docs/TASKS.md](docs/TASKS.md) — 개발 로드맵 (과거 문서, 현재 상태는 릴리스 노트 참고)

## 샘플

- [samples/CodeBeaker.AI.Agent/README.md](samples/CodeBeaker.AI.Agent/README.md) — AI 에이전트 연동 샘플

## 문서 사이트

`docs-site/`는 [Docusaurus](https://docusaurus.io/) 소스이며, `docs/`의 내용을 정적 사이트로 빌드해 GitHub Pages에 배포한다(`.github/workflows/docs-deploy.yml`). 사이트에서만 보이는 페이지를 이 인덱스에서 소스 경로로 링크하지 않는다 — 그 경로는 빌드 산출물이라 리포에 존재하지 않기 때문이다.

## 보관 문서

`docs/archive/`에 과거 개발 단계의 기록이 남아 있다. 현재 동작을 설명하는 문서가 아니므로 위 목록에는 싣지 않는다.
