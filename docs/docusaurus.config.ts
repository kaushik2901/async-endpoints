import { themes as prismThemes } from 'prism-react-renderer';
import type { Config } from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

// --- AsyncEndpoints official site configuration ---
const config: Config = {
  title: 'async-endpoints',
  tagline: 'Enterprise-Grade Asynchronous Processing for .NET',
  favicon: 'img/favicon.ico',

  url: 'https://asyncendpoints.com',
  baseUrl: '/',

  organizationName: 'kaushik2901',
  projectName: 'async-endpoints',

  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',

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
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
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
          label: 'Docs',
        },
        {
          label: 'GitHub',
          href: 'https://github.com/kaushik2901/async-endpoints',
          position: 'right',
        },
      ],
      hideOnScroll: false,
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Project',
          items: [
            {
              html: `
                <p style="max-width:280px; opacity:0.85; font-size:0.9rem;">
                  A modern .NET library for asynchronous API processing — offload long-running tasks to background workers with full visibility and reliability.
                </p>
              `,
            },
          ],
        },
        {
          title: 'Documentation',
          items: [
            {
              label: 'Getting Started',
              to: '/docs/intro',
            },
            {
              label: 'API Reference',
              to: '/docs/category/api-reference',
            },
            {
              label: 'Configuration',
              to: '/docs/category/configuration',
            },
            {
              label: 'Contributing Guide',
              to: '/docs/contributing',
            },
          ],
        },
        {
          title: 'Community',
          items: [
            {
              label: 'GitHub Repository',
              href: 'https://github.com/kaushik2901/async-endpoints',
            },
            {
              label: 'Report Issues',
              href: 'https://github.com/kaushik2901/async-endpoints/issues',
            },
            {
              label: 'Discussions',
              href: 'https://github.com/kaushik2901/async-endpoints/discussions',
            },
          ],
        },
        {
          title: 'Legal',
          items: [
            { label: 'License', to: '/docs/license' },
            { label: 'Privacy Policy', href: '#' },
            { label: 'Terms of Use', href: '#' },
          ],
        },
      ],

      // Footer copyright section
      copyright: `
        <div style="text-align:center; margin-top:1rem; line-height:1.6;">
          <p>© ${new Date().getFullYear()} <strong>AsyncEndpoints</strong>. Built with Docusaurus.</p>
          <p style="opacity:0.75; font-size:0.85rem;">Designed for developers building scalable, background job-enabled APIs.</p>
        </div>
      `,
    },

    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
    },

    // Optional: add smooth scroll and better metadata
    metadata: [
      { name: 'keywords', content: 'async, endpoints, dotnet, background jobs, c#, api performance, queue processing' },
      { name: 'theme-color', content: '#1a3a5c' },
    ],
  } satisfies Preset.ThemeConfig,
};

export default config;
