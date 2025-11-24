import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  // 메인 문서 사이드바
  docsSidebar: [
    'intro',
    'architecture',
    'usage',
    'production',
    'roadmap',
  ],

  // API 레퍼런스 사이드바
  apiSidebar: [
    {
      type: 'category',
      label: 'API Reference',
      items: [
        'api/overview',
      ],
    },
  ],
};

export default sidebars;
