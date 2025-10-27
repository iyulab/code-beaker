import React from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';

import styles from './index.module.css';

function HomepageHeader() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <header className={clsx('hero hero--primary', styles.heroBanner)}>
      <div className="container">
        <Heading as="h1" className="hero__title">
          {siteConfig.title}
        </Heading>
        <p className="hero__subtitle">{siteConfig.tagline}</p>
        <div className={styles.buttons}>
          <Link
            className="button button--secondary button--lg"
            to="/docs/intro">
            Get Started →
          </Link>
        </div>
      </div>
    </header>
  );
}

function HomepageFeatures() {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          <div className="col col--4">
            <div className="text--center padding-horiz--md">
              <h3>🚀 Multi-Runtime Architecture</h3>
              <p>
                Supports Docker, Deno, and Bun runtimes with intelligent automatic selection.
                Execute Python, JavaScript, TypeScript, Go, and C# code with optimal performance.
              </p>
            </div>
          </div>
          <div className="col col--4">
            <div className="text--center padding-horiz--md">
              <h3>⚡ High Performance</h3>
              <p>
                Session reuse provides 50-75% performance improvement. Native runtimes (Deno/Bun)
                offer 11-40x faster startup times for JavaScript/TypeScript execution.
              </p>
            </div>
          </div>
          <div className="col col--4">
            <div className="text--center padding-horiz--md">
              <h3>🛡️ Production Ready</h3>
              <p>
                Docker-based isolation, Kubernetes health checks, Prometheus metrics integration,
                and comprehensive observability for production infrastructure.
              </p>
            </div>
          </div>
        </div>
        <div className="row margin-top--lg">
          <div className="col col--4">
            <div className="text--center padding-horiz--md">
              <h3>🔄 Real-time Streaming</h3>
              <p>
                WebSocket-based JSON-RPC 2.0 protocol enables real-time stdout/stderr streaming
                with bidirectional communication support.
              </p>
            </div>
          </div>
          <div className="col col--4">
            <div className="text--center padding-horiz--md">
              <h3>📊 RESTful & WebSocket APIs</h3>
              <p>
                Dual protocol support with REST API for simple executions and WebSocket for
                advanced session management and streaming scenarios.
              </p>
            </div>
          </div>
          <div className="col col--4">
            <div className="text--center padding-horiz--md">
              <h3>🧩 Infrastructure Focus</h3>
              <p>
                Designed as middleware infrastructure with health checks, metrics, and
                Kubernetes-ready deployment patterns for integration into larger systems.
              </p>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

function ContributorSection() {
  return (
    <section className={styles.contributors}>
      <div className="container">
        <div className="row">
          <div className="col col--12">
            <div className="text--center padding-horiz--md">
              <Heading as="h2">Contributors</Heading>
              <p className="margin-bottom--lg">
                CodeBeaker is developed and maintained by iyulab organization with core contributions from Caveman and Junhyung.
              </p>
              <div className={styles.contributorLinks}>
                <Link
                  className="button button--outline button--primary margin--sm"
                  href="https://github.com/iyulab"
                  target="_blank"
                  rel="noopener noreferrer">
                  iyulab (Organization)
                </Link>
                <Link
                  className="button button--outline button--primary margin--sm"
                  href="https://github.com/iyulab-caveman"
                  target="_blank"
                  rel="noopener noreferrer">
                  Caveman (Core Contributor)
                </Link>
                <Link
                  className="button button--outline button--primary margin--sm"
                  href="https://github.com/iujunhyung"
                  target="_blank"
                  rel="noopener noreferrer">
                  Junhyung (Core Contributor)
                </Link>
                <Link
                  className="button button--outline button--primary margin--sm"
                  href="https://github.com/iyulab/code-beaker"
                  target="_blank"
                  rel="noopener noreferrer">
                  GitHub Repository
                </Link>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

function StatusSection() {
  return (
    <section className={styles.status}>
      <div className="container">
        <div className="row">
          <div className="col col--12">
            <div className="text--center padding-horiz--md">
              <div className={styles.statusBadge}>
                <span className={styles.statusLabel}>Status:</span>
                <span className={styles.statusValue}>Initial Documentation Site (Welcome page only)</span>
              </div>
              <p className={styles.statusDate}>Last Updated: October 27, 2025</p>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

export default function Home(): JSX.Element {
  const {siteConfig} = useDocusaurusContext();
  return (
    <Layout
      title={`Welcome to ${siteConfig.title}`}
      description="Safe and Fast Code Execution Platform - Multi-runtime infrastructure with Docker, Deno, and Bun support">
      <HomepageHeader />
      <main>
        <HomepageFeatures />
        <ContributorSection />
        <StatusSection />
      </main>
    </Layout>
  );
}
