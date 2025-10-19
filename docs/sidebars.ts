import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

/**
 * Creating a sidebar enables you to:
 - create an ordered group of docs
 - render a sidebar for each doc of that group
 - provide next/previous navigation

 The sidebars can be generated from the filesystem, or explicitly defined here.

 Create as many sidebars as you want.
 */
const sidebars: SidebarsConfig = {
  // Manual sidebar for AsyncEndpoints documentation
  tutorialSidebar: [
    {
      type: 'category',
      label: 'Getting Started',
      collapsible: true,
      collapsed: false,
      items: [
        'introduction',
        'installation',
        'quick-start',
      ],
    },
    {
      type: 'category',
      label: 'Core Concepts',
      collapsible: true,
      collapsed: false,
      items: [
        'architecture',
        'endpoint-mapping',
        'handlers',
        'job-lifecycle',
      ],
    },
    {
      type: 'category',
      label: 'Configuration',
      collapsible: true,
      collapsed: false,
      items: [
        'configuration/configuration',
        'configuration/worker-configuration',
        'configuration/job-manager-configuration',
        'configuration/response-customization',
        'configuration/distributed-recovery-configuration',
        'storage',
      ],
    },
    {
      type: 'category',
      label: 'Advanced Topics',
      collapsible: true,
      collapsed: false,
      items: [
        'advanced-features',
        'error-handling',
        'testing',
        'performance',
        'deployment',
      ],
    },
    {
      type: 'category',
      label: 'Recipes and Examples',
      collapsible: true,
      collapsed: false,
      items: [
        'file-processing',
        'data-export',
        'integration-patterns',
        'monitoring-observability',
      ],
    },
    {
      type: 'category',
      label: 'API Reference',
      collapsible: true,
      collapsed: false,
      items: [
        'api-reference/extension-methods',
        'api-reference/configuration-classes',
        'api-reference/core-interfaces',
        'api-reference/core-models',
        'api-reference/utilities',
      ],
    },
    {
      type: 'category',
      label: 'Community',
      collapsible: true,
      collapsed: false,
      items: [
        'contributing',
        'license',
      ],
    },
  ],
};

export default sidebars;
