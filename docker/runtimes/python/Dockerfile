# Python 런타임 Docker 이미지
FROM python:3.13-slim

# 보안: 비root 사용자 생성
RUN useradd -m -u 1000 -s /bin/bash coderunner

# 작업 디렉토리 설정 및 권한 부여 — WORKDIR 이 만든 디렉터리는 root 소유라 비root 사용자가 쓰지 못한다
WORKDIR /workspace
RUN chown coderunner:coderunner /workspace

# 사용자 전환
USER coderunner

# 기본 명령어
CMD ["python", "/workspace/code.py"]
