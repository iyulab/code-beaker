# CodeBeaker Documentation Site

Docusaurus로 구축된 CodeBeaker 문서 사이트입니다.

## 🚀 빠른 시작

### 로컬 개발

\`\`\`bash
cd docs-site
npm install
npm start
\`\`\`

브라우저에서 \`http://localhost:3000\`이 자동으로 열립니다.

### 문서 동기화

\`/docs\` 디렉토리의 문서를 \`docs-site/docs\`로 동기화:

\`\`\`bash
npm run sync
\`\`\`

### 빌드

프로덕션 빌드:

\`\`\`bash
npm run build
\`\`\`

## 🌐 GitHub Pages 배포

\`.github/workflows/docs-deploy.yml\`이 자동으로 배포합니다.

**배포 URL**: https://iyulab.github.io/code-beaker/

## 📚 문서 매핑

| 소스 | 타겟 | 설명 |
|------|------|------|
| README.md | intro.md | 소개 |
| docs/ARCHITECTURE.md | architecture.md | 아키텍처 |
| docs/USAGE.md | usage.md | 사용법 |

`docs/archive/`는 과거 개발 단계의 기록이라 사이트로 발행하지 않는다.

## 👥 Contributors

- **Organization**: [iyulab](https://github.com/iyulab)
- **Core Contributors**:
  - [Caveman](https://github.com/iyulab-caveman)
  - [Junhyung](https://github.com/iujunhyung)
- **Repository**: [CodeBeaker](https://github.com/iyulab/code-beaker)

**현재 상태**: Welcome 블로그 포스트 작성 완료 (2025/10/27)
