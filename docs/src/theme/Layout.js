import React from 'react';
import OriginalLayout from '@theme-original/Layout';
import Head from '@docusaurus/Head';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import useBaseUrl from '@docusaurus/useBaseUrl';

export default function Layout(props) {
  const { siteConfig } = useDocusaurusContext();
  const { title, description, image, children } = props;
  
  const siteTitle = siteConfig.title;
  const siteDescription = siteConfig.tagline;
  const siteUrl = siteConfig.url;
  const defaultImage = useBaseUrl('/img/async-endpoints-banner.png');

  const resolvedTitle = title ? `${title} | ${siteTitle}` : siteTitle;
  const resolvedDescription = description || siteDescription;
  const resolvedImage = image ? `${siteUrl}${useBaseUrl(image)}` : `${siteUrl}${defaultImage}`;

  return (
    <>
      <Head>
        {/* JSON-LD Structured Data */}
        <script type="application/ld+json">
          {JSON.stringify({
            "@context": "https://schema.org",
            "@type": "SoftwareApplication",
            "name": siteTitle,
            "description": siteDescription,
            "url": siteUrl,
            "applicationCategory": "DeveloperApplication",
            "operatingSystem": "Cross-platform",
            "softwareVersion": "1.1.1",
            "softwareHelp": `${siteUrl}/docs/introduction`,
            "featureList": [
              "Asynchronous Processing",
              ".NET Integration",
              "Background Job Queue",
              "Job Status Tracking",
              "Distributed Processing"
            ],
            "creator": {
              "@type": "Person",
              "name": "Kaushik Jadav",
              "url": "https://github.com/kaushik2901"
            },
            "offers": {
              "@type": "Offer",
              "price": "0",
              "priceCurrency": "USD"
            }
          })}
        </script>
      </Head>
      <OriginalLayout {...props}>{children}</OriginalLayout>
    </>
  );
}