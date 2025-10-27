/**
 * 문서 동기화 스크립트
 * /docs 의 선별된 문서를 docs-site/docs 로 복사하고 Docusaurus 형식에 맞게 변환
 */

const fs = require('fs-extra');
const path = require('path');

// 프로젝트 루트 경로
const ROOT_DIR = path.resolve(__dirname, '../..');
const DOCS_SOURCE = path.join(ROOT_DIR, 'docs');
const DOCS_TARGET = path.join(ROOT_DIR, 'docs-site/docs');

// 동기화할 문서 매핑
// key: 소스 파일, value: 타겟 경로 및 메타데이터
const DOCS_MAPPING = {
  // 핵심 문서
  'ARCHITECTURE.md': {
    target: 'architecture.md',
    sidebar_position: 2,
    sidebar_label: 'Architecture',
    title: 'System Architecture',
  },
  'USAGE.md': {
    target: 'usage.md',
    sidebar_position: 3,
    sidebar_label: 'Usage Guide',
    title: 'API Usage Guide',
  },
  'PRODUCTION_READY.md': {
    target: 'production.md',
    sidebar_position: 4,
    sidebar_label: 'Production Guide',
    title: 'Production Deployment',
  },
  'TASKS.md': {
    target: 'roadmap.md',
    sidebar_position: 5,
    sidebar_label: 'Roadmap',
    title: 'Development Roadmap',
  },
};

// README를 intro로 변환
const README_MAPPING = {
  source: path.join(ROOT_DIR, 'README.md'),
  target: 'intro.md',
  sidebar_position: 1,
  sidebar_label: 'Introduction',
  title: 'Welcome to CodeBeaker',
};

/**
 * Frontmatter 생성
 */
function generateFrontmatter(metadata) {
  const lines = ['---'];

  if (metadata.sidebar_position) {
    lines.push(`sidebar_position: ${metadata.sidebar_position}`);
  }
  if (metadata.sidebar_label) {
    lines.push(`sidebar_label: ${metadata.sidebar_label}`);
  }
  if (metadata.title) {
    lines.push(`title: ${metadata.title}`);
  }

  lines.push('---');
  lines.push(''); // 빈 줄

  return lines.join('\n');
}

/**
 * Markdown 내용 변환
 * - 이미지 경로 수정
 * - 내부 링크 수정
 * - Docusaurus 특정 문법 적용
 * - JSX 충돌 방지
 */
function transformMarkdown(content, sourceFile) {
  let transformed = content;

  // H1 제목 제거 (frontmatter의 title 사용)
  transformed = transformed.replace(/^#\s+.+$/m, '');

  // MDX/JSX 충돌 방지: < 와 > 를 HTML 엔티티로 변환
  // 하지만 코드 블록과 인라인 코드는 제외

  // 먼저 코드 블록과 인라인 코드를 임시로 보호
  const codeBlocks = [];
  const inlineCodes = [];

  // 코드 블록 보호
  transformed = transformed.replace(
    /```[\s\S]*?```/g,
    (match) => {
      codeBlocks.push(match);
      return `__CODE_BLOCK_${codeBlocks.length - 1}__`;
    }
  );

  // 인라인 코드 보호
  transformed = transformed.replace(
    /`[^`]+`/g,
    (match) => {
      inlineCodes.push(match);
      return `__INLINE_CODE_${inlineCodes.length - 1}__`;
    }
  );

  // 이제 < 와 > 를 안전하게 변환
  transformed = transformed.replace(/</g, '&lt;');
  transformed = transformed.replace(/>/g, '&gt;');

  // 코드 블록 복원
  transformed = transformed.replace(
    /__CODE_BLOCK_(\d+)__/g,
    (match, index) => codeBlocks[parseInt(index)]
  );

  // 인라인 코드 복원
  transformed = transformed.replace(
    /__INLINE_CODE_(\d+)__/g,
    (match, index) => inlineCodes[parseInt(index)]
  );

  // 다양한 문서 링크 패턴을 GitHub로 변환
  // (Docusaurus는 docs/ 내부의 문서만 자동 링크 처리)

  // claudedocs/ 링크 → GitHub
  transformed = transformed.replace(
    /\[([^\]]+)\]\(claudedocs\/([^)]+)\)/g,
    '[$1](https://github.com/iyulab/code-beaker/blob/main/claudedocs/$2)'
  );

  // docs/ 경로 포함한 링크 → GitHub
  transformed = transformed.replace(
    /\[([^\]]+)\]\(docs\/([^)]+)\)/g,
    '[$1](https://github.com/iyulab/code-beaker/blob/main/docs/$2)'
  );

  // Archive 링크 → GitHub
  transformed = transformed.replace(
    /\[([^\]]+)\]\(\.\.\/docs\/archive\/([^)]+)\)/g,
    '[$1](https://github.com/iyulab/code-beaker/blob/main/docs/archive/$2)'
  );

  transformed = transformed.replace(
    /\[([^\]]+)\]\(docs\/archive\/([^)]+)\)/g,
    '[$1](https://github.com/iyulab/code-beaker/blob/main/docs/archive/$2)'
  );

  transformed = transformed.replace(
    /\[([^\]]+)\]\(claudedocs\/archive\/([^)]+)\)/g,
    '[$1](https://github.com/iyulab/code-beaker/blob/main/claudedocs/archive/$2)'
  );

  // 대문자로 시작하는 .md 파일 (PHASE1_COMPLETE.md 등) → GitHub
  transformed = transformed.replace(
    /\[([^\]]+)\]\(([A-Z][A-Z_0-9]*\.md)\)/g,
    '[$1](https://github.com/iyulab/code-beaker/blob/main/docs/$2)'
  );

  // LICENSE 같은 루트 파일 → GitHub
  transformed = transformed.replace(
    /\[([^\]]+)\]\((LICENSE|README\.md|DEV_GUIDE\.md)\)/g,
    '[$1](https://github.com/iyulab/code-beaker/blob/main/$2)'
  );

  return transformed.trim();
}

/**
 * 단일 파일 동기화
 */
async function syncFile(sourceFile, targetFile, metadata) {
  const sourcePath = path.join(DOCS_SOURCE, sourceFile);
  const targetPath = path.join(DOCS_TARGET, targetFile);

  console.log(`Syncing: ${sourceFile} → ${targetFile}`);

  try {
    // 소스 파일 읽기
    let content = await fs.readFile(sourcePath, 'utf-8');

    // 내용 변환
    content = transformMarkdown(content, sourceFile);

    // Frontmatter 추가
    const frontmatter = generateFrontmatter(metadata);
    const finalContent = frontmatter + content;

    // 타겟 디렉토리 생성
    await fs.ensureDir(path.dirname(targetPath));

    // 파일 쓰기
    await fs.writeFile(targetPath, finalContent, 'utf-8');

    console.log(`✅ Synced: ${targetFile}`);
  } catch (error) {
    console.error(`❌ Failed to sync ${sourceFile}:`, error.message);
  }
}

/**
 * README 동기화
 */
async function syncReadme() {
  console.log('Syncing README.md → intro.md');

  try {
    let content = await fs.readFile(README_MAPPING.source, 'utf-8');

    // README 특별 처리
    content = transformMarkdown(content, 'README.md');

    // Badges 유지하지만 위치 조정
    // 기존 H1 제거되었으므로 badges를 상단에 배치

    const frontmatter = generateFrontmatter(README_MAPPING);
    const finalContent = frontmatter + content;

    const targetPath = path.join(DOCS_TARGET, README_MAPPING.target);
    await fs.ensureDir(path.dirname(targetPath));
    await fs.writeFile(targetPath, finalContent, 'utf-8');

    console.log('✅ Synced: intro.md');
  } catch (error) {
    console.error('❌ Failed to sync README:', error.message);
  }
}

/**
 * API 문서 생성 (placeholder)
 */
async function generateApiDocs() {
  const apiDir = path.join(DOCS_TARGET, 'api');
  await fs.ensureDir(apiDir);

  // API Overview
  const overviewContent = `---
sidebar_position: 1
sidebar_label: Overview
title: API Reference
---

# API Reference

CodeBeaker provides RESTful API and WebSocket endpoints for code execution.

## Endpoints

### REST API

- **POST /api/execute** - Execute code once
- **GET /api/execute/:id/status** - Get execution status
- **GET /api/languages** - List supported languages

### WebSocket API (JSON-RPC 2.0)

- **ws://localhost:5039/ws/jsonrpc** - WebSocket endpoint
- Supports real-time streaming execution

### Health Checks

- **GET /health** - Overall health status
- **GET /health/ready** - Readiness probe
- **GET /health/live** - Liveness probe
- **GET /health/startup** - Startup probe

### Metrics

- **GET /metrics** - Prometheus metrics

For detailed API documentation, see [Usage Guide](../usage.md).
`;

  await fs.writeFile(
    path.join(apiDir, 'overview.md'),
    overviewContent,
    'utf-8'
  );

  console.log('✅ Generated: api/overview.md');
}

/**
 * 메인 동기화 함수
 */
async function main() {
  console.log('🚀 Starting documentation sync...\n');

  try {
    // 타겟 디렉토리 정리
    await fs.emptyDir(DOCS_TARGET);
    console.log('📁 Cleaned target directory\n');

    // README 동기화
    await syncReadme();
    console.log('');

    // 문서 동기화
    for (const [sourceFile, config] of Object.entries(DOCS_MAPPING)) {
      await syncFile(sourceFile, config.target, config);
    }
    console.log('');

    // API 문서 생성
    await generateApiDocs();
    console.log('');

    console.log('✅ Documentation sync completed!');
  } catch (error) {
    console.error('❌ Sync failed:', error);
    process.exit(1);
  }
}

// 스크립트 실행
if (require.main === module) {
  main();
}

module.exports = { main };
