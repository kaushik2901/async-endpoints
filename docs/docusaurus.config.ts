import { themes as prismThemes } from 'prism-react-renderer';
import type { Config } from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

// --- AsyncEndpoints official site configuration ---
const config: Config = {
  title: 'AsyncEndpoints - Enterprise-Grade Asynchronous Processing for .NET',
  tagline: 'Modern .NET library for asynchronous API processing with background jobs, job tracking, and resilience',
  favicon: 'img/favicon.ico',

  url: 'https://asyncendpoints.com',
  baseUrl: '/',

  organizationName: 'AsyncEndpoints',
  projectName: 'async-endpoints',

  onBrokenLinks: 'throw',

  markdown: {
    mermaid: true,
    hooks: {
      onBrokenMarkdownImages: 'throw',
      onBrokenMarkdownLinks: 'throw'
    }
  },

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  future: { v4: true },

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          editUrl:
            'https://github.com/kaushik2901/async-endpoints/edit/main/docs/',
          // Add breadcrumbs for better SEO navigation
          breadcrumbs: true,
          // Add versions support if needed later
          showLastUpdateAuthor: true,
          showLastUpdateTime: true,
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
        sitemap: {
          changefreq: 'weekly',
          priority: 0.7,
          filename: 'sitemap.xml',
          ignorePatterns: ['/tags/**']
        },
      } satisfies Preset.Options,
    ],
  ],

  plugins: [
    // Client redirects plugin if needed
    [
      '@docusaurus/plugin-client-redirects',
      {
        redirects: [
          // Add redirects for SEO purposes if needed
        ],
      },
    ],
  ],

  themeConfig: {
    image: 'img/async-endpoints-banner.png',

    colorMode: {
      defaultMode: 'light',
      respectPrefersColorScheme: true,
    },

    navbar: {
      title: 'async-endpoints',
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'tutorialSidebar',
          position: 'right',
          label: 'Documentation',
        },
        {
          label: 'GitHub',
          href: 'https://github.com/kaushik2901/async-endpoints',
          position: 'right',
        },
        {
          label: 'NuGet',
          href: 'https://www.nuget.org/packages/AsyncEndpoints',
          position: 'right',
        },
      ],
      hideOnScroll: false,
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Documentation',
          items: [
            {
              label: 'Introduction',
              to: '/docs/introduction',
            },
            {
              label: 'Getting Started',
              to: '/docs/quick-start',
            },
            {
              label: 'Configuration',
              to: '/docs/configuration',
            },
            {
              label: 'API Reference',
              to: '/docs/api-reference/extension-methods',
            },
          ],
        },
        {
          title: 'Community',
          items: [
            {
              label: 'GitHub',
              href: 'https://github.com/kaushik2901/async-endpoints',
            },
            {
              label: 'Issues',
              href: 'https://github.com/kaushik2901/async-endpoints/issues',
            },
            {
              label: 'Discussions',
              href: 'https://github.com/kaushik2901/async-endpoints/discussions',
            },
          ],
        },
        {
          title: 'Resources',
          items: [
            {
              label: 'NuGet Package',
              href: 'https://www.nuget.org/packages/AsyncEndpoints',
            },
            {
              label: 'Contributing',
              to: '/docs/contributing',
            },
            {
              label: 'License',
              to: '/docs/license',
            },
          ],
        },
      ],

      // Footer copyright section
      copyright: `
        <div style="text-align:center; margin-top:1rem; line-height:1.6;">
          <p>© ${new Date().getFullYear()} <strong>AsyncEndpoints</strong>. MIT Licensed.</p>
          <p style="opacity:0.75; font-size:0.85rem;">Open source project for .NET developers building scalable, background job-enabled APIs.</p>
        </div>
      `,
    },

    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
    },

    // Comprehensive SEO metadata
    metadata: [
      { name: 'keywords', content: 'async, endpoints, dotnet, background jobs, c#, api performance, queue processing, distributed systems, asynchronous processing, .NET 8, web development, background workers, job queue, task processing' },
      { name: 'theme-color', content: '#1a3a5c' },
      { name: 'author', content: 'Kaushik Jadav' },
      { name: 'copyright', content: '© 2025 AsyncEndpoints. MIT Licensed.' },
      { name: 'robots', content: 'index, follow' },
      { name: 'googlebot', content: 'index, follow' },
      { name: 'distribution', content: 'global' },
      { name: 'revisit-after', content: '7 days' },
      { name: 'og:site_name', content: 'AsyncEndpoints' },
      { name: 'og:type', content: 'website' },
      { name: 'og:locale', content: 'en_US' },
      { name: 'twitter:card', content: 'summary_large_image' },
      { name: 'twitter:site', content: '@asyncendpoints' },
      { name: 'twitter:creator', content: '@asyncendpoints' },
    ],

    // SEO and social media integration
    announcementBar: {
      id: 'announcement-bar',
      content:
        '⭐ If you find AsyncEndpoints helpful, please <a target="_blank" rel="noopener noreferrer" href="https://github.com/kaushik2901/async-endpoints">star our repo on GitHub</a>! ⭐',
      backgroundColor: '#1a3a5c',
      textColor: '#ffffff',
      isCloseable: true,
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
