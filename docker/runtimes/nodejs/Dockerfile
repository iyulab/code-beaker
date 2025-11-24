# Node.js 런타임 Docker 이미지
FROM node:20-slim

# 작업 디렉토리 설정
WORKDIR /workspace

# 보안: 비root 사용자로 전환 (node 사용자는 기본 제공)
USER node

# 기본 명령어
CMD ["node", "/workspace/code.js"]
