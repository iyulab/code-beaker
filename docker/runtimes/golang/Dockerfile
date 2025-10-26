# Go 런타임 Docker 이미지
FROM golang:1.21-alpine

# 보안: 비root 사용자 생성
RUN adduser -D -u 1000 coderunner

# 작업 디렉토리 설정 및 권한 부여
WORKDIR /workspace
RUN chown coderunner:coderunner /workspace

# 사용자 전환
USER coderunner

# 기본 명령어
CMD ["go", "run", "/workspace/main.go"]
