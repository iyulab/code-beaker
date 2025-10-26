# Python 런타임 Docker 이미지
FROM python:3.11-slim

# 보안: 비root 사용자 생성
RUN useradd -m -u 1000 -s /bin/bash coderunner

# 작업 디렉토리 설정
WORKDIR /workspace

# 사용자 전환
USER coderunner

# 기본 명령어
CMD ["python", "/workspace/code.py"]
