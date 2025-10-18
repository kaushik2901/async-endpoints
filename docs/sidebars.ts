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
        'intro',
        'category/installation/installation',
        'category/quick-start/quick-start',
      ],
    },
    {
      type: 'category',
      label: 'Core Concepts',
      collapsible: true,
      collapsed: false,
      items: [
        'category/architecture/architecture',
        'category/endpoint-mapping/endpoint-mapping',
        'category/handlers/handlers',
        'category/job-lifecycle/job-lifecycle',
      ],
    },
    {
      type: 'category',
      label: 'Configuration',
      collapsible: true,
      collapsed: false,
      items: [
        'category/configuration/configuration',
        'category/configuration/worker-configuration',
        'category/configuration/job-manager-configuration',
        'category/configuration/response-customization',
        'category/configuration/distributed-recovery-configuration',
        'category/storage/storage',
      ],
    },
    {
      type: 'category',
      label: 'Advanced Topics',
      collapsible: true,
      collapsed: false,
      items: [
        'category/advanced-features/advanced-features',
        'category/error-handling/error-handling',
        'category/testing/testing',
        'category/performance/performance',
        'category/deployment/deployment',
      ],
    },
    {
      type: 'category',
      label: 'Recipes and Examples',
      collapsible: true,
      collapsed: false,
      items: [
        'category/file-processing/file-processing',
        'category/data-export/data-export',
        'category/integration-patterns/integration-patterns',
        'category/monitoring-observability/monitoring-observability',
      ],
    },
    {
      type: 'category',
      label: 'API Reference',
      collapsible: true,
      collapsed: false,
      items: [
        'category/api-reference/extension-methods/extension-methods',
        'category/api-reference/configuration-classes/configuration-classes',
        'category/api-reference/core-interfaces/core-interfaces',
        'category/api-reference/core-models/core-models',
        'category/api-reference/utilities/utilities',
      ],
    },
    {
      type: 'category',
      label: 'Community',
      collapsible: true,
      collapsed: false,
      items: [
        'category/contributing/contributing',
        'category/license/license',
      ],
    },
  ],
};

export default sidebars;
