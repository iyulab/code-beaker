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

// 링크 재작성이 가리키는 곳. 한 군데만 고치면 되도록 상수로 둔다.
const REPO_BLOB = 'https://github.com/iyulab/code-beaker/blob/main';
const REPO_TREE = 'https://github.com/iyulab/code-beaker/tree/main';

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
  // The API reference used to be a string constant in this file. It listed three REST
  // routes, two of which the server has never served and the third under the wrong
  // name — nothing compared it to the application, so the published site advertised
  // them anyway. It is a repository document now, checked against the running app by
  // `PublicHttpSurfaceTests`, and reaches the site through the same path as every
  // other document here.
  'API.md': {
    target: 'api/overview.md',
    sidebar_position: 1,
    sidebar_label: 'Overview',
    title: 'API Reference',
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

  // 링크 재작성은 코드가 «보호된 상태에서» 한다 — 코드 예제 안의 경로가 링크로
  // 오인돼 재작성되지 않게 하려는 것이다.
  transformed = rewriteLinks(transformed, sourceFile);

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

  return transformed.trim();
}

/**
 * 동기화되는 소스 파일의 «리포 상대 경로» → 이 사이트에서의 문서 경로.
 * DOCS_MAPPING/README_MAPPING이 이미 그 대응을 갖고 있으므로 여기서 다시 적지 않는다.
 */
function siteRoutes() {
  const routes = { 'README.md': README_MAPPING.target.replace(/\.md$/, '') };

  for (const [source, meta] of Object.entries(DOCS_MAPPING)) {
    routes[path.posix.join('docs', source)] = meta.target.replace(/\.md$/, '');
  }

  return routes;
}

/**
 * 상대 링크를 이 사이트가 실제로 해석할 수 있는 곳으로 옮긴다.
 *
 * 이전에는 재작성할 접두어를 손으로 나열했다.
 * 목록에 없는 접두어는 상대 링크인 채로 나가 사이트에서 404가 됐고, 빌드는 그것을
 * 경고로만 알렸다. 그래서 규칙을 뒤집는다 — «동기화되는 문서가 아니면 전부 리포로».
 * 나열이 없으므로 빠뜨릴 것도 없다.
 *
 * 절대 URL·앵커·사이트 절대경로는 그대로 둔다.
 */
function rewriteLinks(content, sourceFile) {
  const routes = siteRoutes();
  const sourceDir = path.posix.dirname(sourceFile.split(path.sep).join('/'));
  // A link into the site is resolved by the browser against the page carrying it, so
  // it has to be written relative to where *this* document lands. Every page used to
  // sit at the docs root, which made `./<route>` right by accident; it stops being
  // right as soon as one of them is nested.
  const currentDir = path.posix.dirname(routes[sourceFile.split(path.sep).join('/')] ?? '.');

  return content.replace(/(!?)\[([^\]]*)\]\(([^)\s]+)\)/g, (match, bang, text, target) => {
    if (/^(?:[a-z][a-z0-9+.-]*:|\/\/|\/|#)/i.test(target)) {
      return match;
    }

    const hash = target.indexOf('#');
    const pathPart = hash === -1 ? target : target.slice(0, hash);
    const anchor = hash === -1 ? '' : target.slice(hash);

    if (!pathPart) {
      return match;
    }

    const isDirectory = pathPart.endsWith('/');
    const repoPath = path.posix
      .normalize(path.posix.join(sourceDir === '.' ? '' : sourceDir, pathPart))
      .replace(/\/$/, '');

    // 이 사이트가 함께 발행하는 문서라면 사이트 안에 머무른다 — 렌더된 페이지가
    // 있는데 원본 마크다운으로 내보낼 이유가 없다.
    if (!bang && routes[repoPath]) {
      const relative = path.posix.relative(currentDir, routes[repoPath]);
      const inSite = relative.startsWith('.') ? relative : `./${relative}`;
      return `[${text}](${inSite}${anchor})`;
    }

    const base = isDirectory ? REPO_TREE : REPO_BLOB;
    return `${bang}[${text}](${base}/${repoPath}${anchor})`;
  });
}

/**
 * 단일 파일 동기화
 */
async function syncFile(sourceFile, targetFile, metadata) {
  const sourcePath = path.join(DOCS_SOURCE, sourceFile);
  const targetPath = path.join(DOCS_TARGET, targetFile);

  console.log(`Syncing: ${sourceFile} → ${targetFile}`);

  // 링크는 리포 루트 기준으로 풀어야 하므로 소스의 리포 상대 경로를 넘긴다.
  const content = transformMarkdown(
    await fs.readFile(sourcePath, 'utf-8'),
    path.posix.join('docs', sourceFile));

  await fs.ensureDir(path.dirname(targetPath));
  await fs.writeFile(targetPath, generateFrontmatter(metadata) + content, 'utf-8');

  console.log(`✅ Synced: ${targetFile}`);
}

/**
 * README 동기화
 */
async function syncReadme() {
  console.log('Syncing README.md → intro.md');

  const content = transformMarkdown(
    await fs.readFile(README_MAPPING.source, 'utf-8'), 'README.md');

  const targetPath = path.join(DOCS_TARGET, README_MAPPING.target);
  await fs.ensureDir(path.dirname(targetPath));
  await fs.writeFile(targetPath, generateFrontmatter(README_MAPPING) + content, 'utf-8');

  console.log('✅ Synced: intro.md');
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

    console.log('✅ Documentation sync completed!');
  } catch (error) {
    // The only error handler in this script. The sync steps above used to catch and log
    // their own failures and carry on, which published a site quietly missing a page and
    // left the build to fail later — if at all — on whatever referenced it.
    console.error('❌ Sync failed:', error);
    process.exit(1);
  }
}

// 스크립트 실행
if (require.main === module) {
  main();
}

module.exports = { main };
