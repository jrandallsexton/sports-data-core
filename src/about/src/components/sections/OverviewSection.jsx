import React, { useState } from "react";
import CollapsibleSection from "../common/CollapsibleSection";

const OverviewSection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState("purpose");

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">Overview</h2>
        <p className="section-subtitle">
          Technical Portfolio and Architectural Overview
        </p>
      </div>

      <div className="section-content">
        <CollapsibleSection
          title="Purpose"
          isExpanded={expandedSection === "purpose"}
          onToggle={() => handleToggle("purpose")}
        >
          <p>
            <strong>sportDeets</strong> is a full-stack sports platform for
            college football, NFL, and MLB fans. It powers weekly pick&rsquo;em
            leagues, AI-generated matchup previews backed by real computed
            metrics, and deep team comparisons - on the web and in native
            mobile apps on the App Store and Google Play.
          </p>
          <p>
            The entire platform - roughly a dozen .NET microservices, two
            database engines, a message broker mesh, and a full observability
            stack - runs on self-hosted Kubernetes on bare metal, deployed by
            GitOps, and is built and operated by a single engineer.
          </p>
        </CollapsibleSection>

        <CollapsibleSection
          title="Tech Stack Summary"
          isExpanded={expandedSection === "tech-stack"}
          onToggle={() => handleToggle("tech-stack")}
        >
          <div className="tech-stack-grid">
            <div className="tech-card">
              <h4>Web Frontend</h4>
              <p>
                <strong>React 19</strong> - hooks and functional components
                throughout
              </p>
              <p>
                <strong>React Router 7</strong> - client-side routing
              </p>
              <p>
                <strong>Firebase Authentication</strong> - sign-in and custom
                claims
              </p>
              <p>
                <strong>SignalR</strong> - live game updates pushed from the
                API
              </p>
              <p>
                <strong>Material-UI</strong> - component library
              </p>
              <p>
                <strong>Chart.js &amp; Recharts</strong> - data visualization
              </p>
            </div>

            <div className="tech-card">
              <h4>Mobile</h4>
              <p>
                <strong>Expo SDK 55 / React Native 0.83</strong> - one codebase
                for iOS and Android, shipped to both stores
              </p>
              <p>
                <strong>Expo Router</strong> - file-based navigation
              </p>
              <p>
                <strong>TanStack Query + Zustand</strong> - server cache and
                client state
              </p>
              <p>
                <strong>Firebase Cloud Messaging</strong> - push notifications
                (pick reminders, poll releases)
              </p>
              <p>
                <strong>Sentry</strong> - crash and error reporting
              </p>
              <p>
                <strong>EAS Build</strong> - cloud builds and store submission
              </p>
            </div>

            <div className="tech-card">
              <h4>Backend</h4>
              <p>
                <strong>C# / .NET 10</strong> - ~12 modular services for
                ingestion, enrichment, and serving
              </p>
              <p>
                <strong>ASP.NET Core</strong> - REST endpoints and background
                hosts
              </p>
              <p>
                <strong>Entity Framework Core + Npgsql</strong> - code-first
                canonical PostgreSQL schema
              </p>
              <p>
                <strong>Dapper</strong> - hand-tuned SQL on the hot read paths
              </p>
              <p>
                <strong>MassTransit + RabbitMQ</strong> - event-driven
                messaging with the transactional outbox pattern
              </p>
              <p>
                <strong>Hangfire</strong> - background jobs and recurring
                schedules, PostgreSQL-backed
              </p>
              <p>
                <strong>SignalR</strong> - real-time broadcast from the API
              </p>
              <p>
                <strong>FluentValidation, Polly, Serilog</strong> - validation,
                resilience policies, structured logging
              </p>
            </div>

            <div className="tech-card">
              <h4>Data</h4>
              <p>
                <strong>PostgreSQL 17</strong> - canonical relational store on
                a dedicated bare-metal server (NVMe)
              </p>
              <p>
                <strong>MongoDB 8</strong> - raw external-document store
                (every sourced JSON payload, content-addressed) on its own box
              </p>
              <p>
                <strong>Redis</strong> - distributed cache with a
                circuit-breaker wrapper
              </p>
              <p>
                <strong>Nightly verified backups</strong> - both database
                hosts, with restore verification
              </p>
            </div>

            <div className="tech-card">
              <h4>Infrastructure</h4>
              <p>
                <strong>Kubernetes</strong> - self-hosted, multi-node, on
                bare metal
              </p>
              <p>
                <strong>Flux CD + Kustomize</strong> - GitOps: the cluster
                state is a config repo; merges deploy
              </p>
              <p>
                <strong>KEDA</strong> - queue-depth-driven autoscaling for
                ingestion workers
              </p>
              <p>
                <strong>Traefik + cert-manager</strong> - ingress, TLS
                automation
              </p>
              <p>
                <strong>RabbitMQ</strong> - per-sport broker split with
                cross-broker shovels
              </p>
              <p>
                <strong>Cloudflare</strong> - DNS with in-cluster dynamic-DNS
                (residential IP rotation handled automatically)
              </p>
              <p>
                <strong>Reloader</strong> - config/secret change detection
                with rolling restarts
              </p>
            </div>

            <div className="tech-card">
              <h4>Observability</h4>
              <p>
                <strong>OpenTelemetry</strong> - traces, metrics, and logs
                instrumented across every service
              </p>
              <p>
                <strong>Seq</strong> - structured application logs with
                real-time querying
              </p>
              <p>
                <strong>Prometheus + Grafana</strong> - cluster and pipeline
                dashboards, Alertmanager alerting, blackbox probes
              </p>
              <p>
                <strong>Tempo</strong> - distributed tracing
              </p>
              <p>
                <strong>Umami</strong> - privacy-friendly product analytics
              </p>
            </div>

            <div className="tech-card">
              <h4>AI &amp; ML</h4>
              <p>
                <strong>Hosted LLMs</strong> - production previews behind a
                thin client abstraction; candidates auditioned through one
                OpenRouter gateway in a Model Lab
              </p>
              <p>
                <strong>Computed metrics engine</strong> - season-level
                efficiency metrics (yards per play, success rate, red-zone
                rates, field position) derived from play-by-play data
              </p>
              <p>
                <strong>Structured prompt payloads</strong> - previews are
                grounded in metrics, statistics, and spread-conditioned
                history, never free-form
              </p>
              <p>
                <strong>Accuracy tracking</strong> - every model pick graded
                against results, straight-up and against the spread
              </p>
            </div>
          </div>
        </CollapsibleSection>

        <CollapsibleSection
          title="Key Features"
          isExpanded={expandedSection === "features"}
          onToggle={() => handleToggle("features")}
        >
          <ul>
            <li>
              <strong>Pick'em Leagues:</strong> private leagues with
              configurable scoring, straight-up or against-the-spread picks,
              pick reveal rules, and weekly standings
            </li>
            <li>
              <strong>AI Matchup Previews:</strong> model-generated game
              previews grounded in computed metrics and historical context,
              with prediction accuracy tracked publicly
            </li>
            <li>
              <strong>Team Comparison:</strong> side-by-side statistics,
              advanced metrics with per-side favored highlighting, and
              spread-conditioned history ("as a 10–14 point favorite&hellip;")
            </li>
            <li>
              <strong>Live Updates:</strong> in-progress scores and win
              probabilities streamed to the UI over SignalR
            </li>
            <li>
              <strong>Notifications:</strong> per-kickoff pick-deadline
              reminder waves and AP poll-release pushes to mobile devices
            </li>
            <li>
              <strong>Multi-Sport:</strong> NCAA football, NFL, and MLB on one
              sport-partitioned platform
            </li>
          </ul>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default OverviewSection;
