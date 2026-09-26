# Node.js 런타임 Docker 이미지
FROM node:24-slim

# 작업 디렉토리 설정 및 권한 부여 — WORKDIR 이 만든 디렉터리는 root 소유라 비root 사용자가 쓰지 못한다
WORKDIR /workspace
RUN chown node:node /workspace

# 보안: 비root 사용자로 전환 (node 사용자는 기본 제공)
USER node

# 기본 명령어
CMD ["node", "/workspace/code.js"]
