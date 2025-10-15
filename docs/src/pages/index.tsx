import React from "react";
import type { ReactNode } from "react";
import clsx from "clsx";
import Link from "@docusaurus/Link";
import useDocusaurusContext from "@docusaurus/useDocusaurusContext";
import Layout from "@theme/Layout";
import Heading from "@theme/Heading";
import styles from "./index.module.css";

/* ---------- Data ---------- */

type FeatureItem = { title: string; description: ReactNode; icon?: ReactNode };
const KeyFeaturesList: FeatureItem[] = [
  {
    title: "Lightweight Architecture",
    description:
      "Minimal overhead with focused functionality — small API surface, easy adoption.",
    icon: <FeatureSvgRocket />,
  },
  {
    title: "Distributed Processing",
    description:
      "Multi-instance safe workers with automatic recovery and leader-fencing strategies.",
    icon: <FeatureSvgCluster />,
  },
  {
    title: "AOT + JIT Compatible",
    description:
      "Works with both AOT and JIT deployments — suitable for modern hosting targets.",
    icon: <FeatureSvgChip />,
  },
  {
    title: "Reflection-Free",
    description:
      "Minimal runtime reflection for predictable cold-start performance and AOT friendliness.",
    icon: <FeatureSvgBolt />,
  },
  {
    title: "Instant Response",
    description:
      "APIs return 202 Accepted immediately — never block callers during long operations.",
    icon: <FeatureSvgClock />,
  },
  {
    title: "Resilient by Design",
    description:
      "Built-in retries, backoff strategies and observability hooks for reliability.",
    icon: <FeatureSvgShield />,
  },
];

type UseCaseItem = { title: string; description: string; accent?: string };
const UseCasesList: UseCaseItem[] = [
  {
    title: "File Processing",
    description: "Image/video transcode, document convert",
    accent: "blue",
  },
  {
    title: "Email / SMS",
    description: "Bulk notifications, transactional delivery",
    accent: "green",
  },
  {
    title: "Data Analytics",
    description: "Reports, heavy calculations, BI tasks",
    accent: "purple",
  },
  {
    title: "3rd-Party APIs",
    description: "Slow APIs, webhooks, rate-limited services",
    accent: "amber",
  },
  {
    title: "Import / Export",
    description: "Migrations, syncing and ETL jobs",
    accent: "teal",
  },
  {
    title: "Background Compute",
    description: "ML pipelines, heavy CPU/IO jobs",
    accent: "red",
  },
];

/* ---------- Functional Components ---------- */

function KeyFeature({ title, description, icon }: FeatureItem) {
  return (
    <div className="col col--4 padding--sm">
      <div
        className={clsx(
          "padding--md text--left feature-card",
          styles.featureCard
        )}
      >
        <div className={styles.featureHeader}>
          <div className={styles.featureIcon}>{icon}</div>
          <h3 className={styles.featureTitle}>{title}</h3>
        </div>
        <p className={styles.featureDescription}>{description}</p>
      </div>
    </div>
  );
}

function UseCase({ title, description, accent }: UseCaseItem) {
  return (
    <div className="col col--4 padding--sm">
      <div
        className={clsx(
          "padding--md text--left use-case-card",
          styles.useCaseCard
        )}
      >
        <div className={styles.useCaseAccent} data-accent={accent} />
        <h3 className={styles.useCaseTitle}>{title}</h3>
        <p className={styles.useCaseDescription}>{description}</p>
      </div>
    </div>
  );
}

/* ---------- Hero Code / JSON snippets (presentation only) ---------- */

const HeroCode = `var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAsyncEndpoints() // Core services
    .AddAsyncEndpointsInMemoryStore() // Dev store
    .AddAsyncEndpointsWorker();       // Background processing

var app = builder.Build();

// Define async endpoint - returns 202 immediately
app.MapAsyncPost<Request>("ProcessData", "/api/process-data");
app.MapAsyncGetJobDetails("/jobs/{jobId:guid}"); // Job status endpoint

await app.RunAsync();`;

const HeroJson = `{
  "id":"5b7e0e4a-8f8b-4c8a-9f1f-8d8f8e8f8e8f",
  "name":"ProcessData",
  "status":"Queued",
  "retryCount":0,
  "maxRetries":3,
  "createdAt":"2025-10-14T10:30:00.000Z",
  "startedAt":null,
  "completedAt":null,
  "lastUpdatedAt":"2025-10-14T10:30:00.000Z",
  "result":null
}`;

/* ---------- Page Sections ---------- */

function HeroSection() {
  const { siteConfig } = useDocusaurusContext();

  return (
    <header
      className={clsx(styles.heroSection, styles.heroGradient)}
      role="region"
      aria-label="Hero"
    >
      <div className="container">
        <div className={styles.heroGrid}>
          <div className={styles.heroLeft}>
            <div className={styles.brandIdentity}>
              <Heading
                as="h1"
                className={clsx("hero__title", styles.heroTitle)}
              >
                {siteConfig.title}
              </Heading>
              <p className="hero__subtitle" aria-hidden>
                Enterprise-Grade Asynchronous Processing for .NET
              </p>
            </div>

            <div className={styles.badgesRow} aria-hidden>
              <span className={styles.badge}>MIT Licensed</span>
              <span className={styles.badge}>Open Source</span>
            </div>

            <div className={styles.headline}>
              {/* <h2 className={styles.mainHeadline}>
                Eliminate API bottlenecks — run long work in background.
              </h2> */}
              <p className={styles.executiveSummary}>
                A sophisticated .NET library enabling developers to offload
                time-consuming operations to background workers while
                maintaining full visibility and control through comprehensive
                job tracking and resilient failure handling
              </p>
            </div>

            <div className={styles.ctaCluster}>
              <Link
                className="button button--primary button--lg"
                to="/docs/intro"
                aria-label="Get started with AsyncEndpoints"
              >
                Get Started
              </Link>
              <Link
                className="button button--outline button--lg"
                to="https://github.com/kaushik2901/async-endpoints"
                aria-label="AsyncEndpoints on GitHub"
              >
                GitHub
              </Link>
            </div>
          </div>

          <div className={styles.heroRight} aria-hidden>
            <img src="img/code.png" style={{width: '100%', borderRadius: '0.4rem'}}/>
          </div>
        </div>

        <div className={styles.technicalCompatibility} aria-hidden>
          <span>
            .NET 8+ | C# 12+ | Minimal APIs | Background Services | Distributed Workers | Redis
          </span>
        </div>
      </div>
    </header>
  );
}

function KeyFeaturesSection() {
  return (
    <section
      className={clsx(styles.featuresSection)}
      aria-label="Key features"
    >
      <div className="container">
        <div className="row">
          {KeyFeaturesList.map((props, idx) => (
            <KeyFeature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}

/* ---------- Page Export ---------- */

export default function Home(): ReactNode {
  const { siteConfig } = useDocusaurusContext();
  return (
    <Layout
      title={`AsyncEndpoints — ${siteConfig.tagline}`}
      description="AsyncEndpoints — Modern asynchronous endpoint framework for .NET"
    >
      <HeroSection />
      <main>
        <KeyFeaturesSection />
      </main>
    </Layout>
  );
}

/* ---------- Inline SVG helpers (kept local so no external icon dependency) ---------- */

function FeatureSvgRocket() {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden>
      <path
        d="M12 2c.6 0 3 2 3 2s2.01 2.4 2.01 4.2c0 1.8-2.01 4.2-2.01 4.2S12.6 17 12 17s-3-2-3-2-2.01-2.4-2.01-4.2C6.99 9 9 6.6 9 6.6S11.4 2 12 2z"
        stroke="currentColor"
        strokeWidth="1.2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}
function FeatureSvgCluster() {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden>
      <circle cx="7" cy="7" r="2" stroke="currentColor" strokeWidth="1.2" />
      <circle cx="17" cy="7" r="2" stroke="currentColor" strokeWidth="1.2" />
      <circle cx="12" cy="17" r="2" stroke="currentColor" strokeWidth="1.2" />
    </svg>
  );
}
function FeatureSvgChip() {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden>
      <rect
        x="4"
        y="4"
        width="16"
        height="16"
        rx="2"
        stroke="currentColor"
        strokeWidth="1.2"
      />
    </svg>
  );
}
function FeatureSvgBolt() {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden>
      <path
        d="M13 2L3 14h7l-1 8 10-12h-7l1-8z"
        stroke="currentColor"
        strokeWidth="1.2"
        strokeLinejoin="round"
      />
    </svg>
  );
}
function FeatureSvgClock() {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden>
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="1.2" />
      <path
        d="M12 7v5l3 2"
        stroke="currentColor"
        strokeWidth="1.2"
        strokeLinecap="round"
      />
    </svg>
  );
}
function FeatureSvgShield() {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden>
      <path
        d="M12 2l8 4v5c0 5-3 9-8 11-5-2-8-6-8-11V6l8-4z"
        stroke="currentColor"
        strokeWidth="1.2"
      />
    </svg>
  );
}

