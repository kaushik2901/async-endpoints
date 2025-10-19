import React from 'react';
import Head from '@docusaurus/Head';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import useBaseUrl from '@docusaurus/useBaseUrl';

type SEOHeadProps = {
  title?: string;
  description?: string;
  image?: string;
  pathname?: string;
  type?: string;
  noIndex?: boolean;
};

const SEOHead = ({
  title,
  description,
  image,
  pathname,
  type = 'website',
  noIndex = false,
}: SEOHeadProps): JSX.Element => {
  const { siteConfig } = useDocusaurusContext();
  const siteTitle = siteConfig.title;
  const siteDescription = siteConfig.tagline;
  const siteUrl = siteConfig.url;
  const defaultImage = useBaseUrl('/img/async-endpoints-banner.png');

  const resolvedTitle = title ? `${title} | ${siteTitle}` : siteTitle;
  const resolvedDescription = description || siteDescription;
  const resolvedImage = image ? `${siteUrl}${useBaseUrl(image)}` : `${siteUrl}${defaultImage}`;
  const resolvedPathname = pathname || '/';

  return (
    <Head>
      {/* Essential SEO tags */}
      <title>{resolvedTitle}</title>
      <meta name="description" content={resolvedDescription} />
      {noIndex && <meta name="robots" content="noindex, nofollow" />}
      
      {/* Open Graph / Facebook */}
      <meta property="og:type" content={type} />
      <meta property="og:url" content={`${siteUrl}${resolvedPathname}`} />
      <meta property="og:title" content={resolvedTitle} />
      <meta property="og:description" content={resolvedDescription} />
      <meta property="og:image" content={resolvedImage} />
      <meta property="og:image:alt" content={resolvedTitle} />
      <meta property="og:site_name" content={siteTitle} />
      <meta property="og:locale" content="en_US" />
      
      {/* Twitter */}
      <meta name="twitter:card" content="summary_large_image" />
      <meta name="twitter:title" content={resolvedTitle} />
      <meta name="twitter:description" content={resolvedDescription} />
      <meta name="twitter:image" content={resolvedImage} />
      <meta name="twitter:image:alt" content={resolvedTitle} />
      
      {/* Canonical URL */}
      <link rel="canonical" href={`${siteUrl}${resolvedPathname}`} />
      
      {/* Structured Data - JSON-LD */}
      <script type="application/ld+json">
        {JSON.stringify({
          "@context": "https://schema.org",
          "@type": type === 'article' ? "Article" : "WebSite",
          "name": resolvedTitle,
          "description": resolvedDescription,
          "url": `${siteUrl}${resolvedPathname}`,
          "image": resolvedImage,
          "publisher": {
            "@type": "SoftwareApplication",
            "name": "AsyncEndpoints",
            "description": "Modern .NET library for asynchronous API processing with background jobs, job tracking, and resilience",
            "url": siteUrl,
          },
        })}
      </script>
      
      {/* Social Media Tags */}
      <meta property="article:tag" content="async, endpoints, .NET, background jobs, C#" />
      <meta property="article:section" content="Technology" />
      <meta property="article:published_time" content="2024-01-01T00:00:00+00:00" />
      <meta property="article:modified_time" content={new Date().toISOString()} />
    </Head>
  );
};

export default SEOHead;