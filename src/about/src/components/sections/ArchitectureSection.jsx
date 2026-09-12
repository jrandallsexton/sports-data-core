import React, { useState } from "react";
import CollapsibleSection from "../common/CollapsibleSection";
import MermaidDiagram from "../common/MermaidDiagram";

const REPO = "https://github.com/jrandallsexton/sports-data-core";

const ArchitectureSection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState(null);

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  const providerDiagram = `graph TD
    A[Hangfire Sourcing Jobs] --> C{Cached?}
    H[Resource Index Crawl] --> C
    C -->|No| B[ESPN APIs]
    C -->|Yes| D[(MongoDB)]
    B -->|Rate-limit aware| I[Store Raw JSON]
    I --> D
    I --> E[DocumentCreated Event]
    D --> E
    E --> Q[RabbitMQ]
    Q --> J[Producer]

    style A fill:#1e40af
    style H fill:#0891b2
    style C fill:#0d9488
    style B fill:#7c3aed
    style D fill:#059669
    style I fill:#059669
    style E fill:#6366f1
    style Q fill:#b45309
    style J fill:#1e40af`;

  const producerDiagram = `graph TD
    A[Document Event] --> B[Resolve Processor]
    B --> C[Hangfire Background Job]
    C --> D[Deserialize + Validate]
    D --> E[Transform to Canonical]
    E --> F[(PostgreSQL)]
    F --> G[Publish Domain Events]
    E --> S[Spawn Child Document Requests]
    S --> A
    G --> O[Transactional Outbox]
    O --> Q[RabbitMQ]

    style A fill:#6366f1
    style B fill:#8b5cf6
    style C fill:#1e40af
    style E fill:#7c3aed
    style F fill:#059669
    style G fill:#6366f1
    style S fill:#0d9488
    style O fill:#b45309
    style Q fill:#b45309`;

  const servingDiagram = `graph TD
    W[React Web] --> G[API]
    M[Expo Mobile Apps] --> G
    G --> AU[Firebase JWT Validation]
    AU --> H[Query + Command Handlers]
    H --> R[(Redis Cache)]
    H --> P[(PostgreSQL)]
    H --> PC[Producer HTTP Clients]
    PC --> P
    G --> SR[SignalR Broadcast]
    SR --> W
    SR --> M
    N[Notification Service] --> FCM[Push via FCM/APNs]
    N --> P

    style W fill:#7c3aed
    style M fill:#7c3aed
    style G fill:#1e40af
    style H fill:#0891b2
    style P fill:#059669
    style R fill:#dc2626
    style SR fill:#6366f1
    style N fill:#1e40af
    style FCM fill:#0d9488`;

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">Architecture</h2>
        <p className="section-subtitle">
          Event-driven microservices on self-hosted Kubernetes
        </p>
      </div>

      <div className="section-content">
        <CollapsibleSection
          title="Services"
          isExpanded={expandedSection === "services"}
          onToggle={() => handleToggle("services")}
        >
          <p>
            The platform is roughly a dozen .NET services in a single
            monorepo (
            <a href={REPO} target="_blank" rel="noopener noreferrer">
              sports-data-core
            </a>
            ), each owning a clear slice of the pipeline. Ingestion services
            deploy as <strong>per-sport pods</strong> - the same image, run
            once per sport, each pointed at its own message broker and
            database - so a busy college football Saturday cannot starve MLB
            processing.
          </p>

          <div className="service-detail">
            <h4>Provider - sourcing and raw documents</h4>
            <p>
              Crawls ESPN&rsquo;s APIs on Hangfire schedules, from full
              resource-index backfills to in-game polling. Every response is
              stored as an immutable raw JSON document in MongoDB, addressed
              by a SHA-256 hash of its normalized URL, then announced to the
              rest of the system as an event. Rate limiting is respected via
              request pacing and retry policies tuned to ESPN&rsquo;s
              behavior.
            </p>
            <MermaidDiagram chart={providerDiagram} />
          </div>

          <div className="service-detail">
            <h4>Producer - canonical model</h4>
            <p>
              Consumes document events and turns raw JSON into the canonical
              PostgreSQL model through ~47 type-specific document processors
              sharing one base-class contract. Processors are idempotent
              (at-least-once delivery is assumed everywhere), can spawn
              scoped child-document requests to cascade related data, and
              publish domain events through a transactional outbox so a
              database write and its announcement commit together.
            </p>
            <MermaidDiagram chart={producerDiagram} />
          </div>

          <div className="service-detail">
            <h4>API - serving, real time, and AI orchestration</h4>
            <p>
              The single public backend for web and mobile. Firebase JWT
              validation, CQRS-style query/command handlers injected per
              endpoint, Redis caching with a circuit breaker, SignalR
              broadcast of live game updates, and orchestration of the AI
              preview pipeline. The API never touches ingestion databases
              directly - Producer data arrives through typed, sport-aware
              HTTP clients.
            </p>
            <MermaidDiagram chart={servingDiagram} />
          </div>

          <div className="service-detail">
            <h4>Supporting services</h4>
            <ul>
              <li>
                <strong>Notification</strong> - per-kickoff pick-deadline
                reminder waves, poll-release pushes, device registry, FCM
                delivery
              </li>
              <li>
                <strong>Contest / Franchise / Season / Venue / Player</strong>{" "}
                - aggregate-scoped services behind typed client factories
              </li>
              <li>
                <strong>JobsDashboard</strong> - one pane of glass over every
                service&rsquo;s Hangfire instance
              </li>
              <li>
                <strong>Core</strong> - shared library: DI registration,
                messaging, health checks, HTTP clients, OpenTelemetry wiring
              </li>
            </ul>
          </div>
        </CollapsibleSection>

        <CollapsibleSection
          title="Event Pipeline Principles"
          isExpanded={expandedSection === "pipeline"}
          onToggle={() => handleToggle("pipeline")}
        >
          <ul>
            <li>
              <strong>Content-addressed identity:</strong> every external
              document&rsquo;s ID is a SHA-256 hash of its normalized source
              URL - dedupe and idempotency fall out of the design rather than
              being bolted on
            </li>
            <li>
              <strong>At-least-once, everywhere:</strong> every consumer is
              written to be safely re-entrant; redelivery is normal, not
              exceptional
            </li>
            <li>
              <strong>Dead-letter as buffer, not failure:</strong> documents
              that arrive before their dependencies land in a dead-letter
              queue by design and are replayed once the backlog catches up -
              ordering problems become retry problems
            </li>
            <li>
              <strong>Transactional outbox:</strong> canonical writes and
              their domain events commit atomically; ambient outbox capture
              is scoped so jobs that need direct publish opt out explicitly
            </li>
            <li>
              <strong>Self-healing enrichment:</strong> weekly jobs recompute
              records, refresh statistics from source, and regenerate season
              metrics for every team - drift is corrected on a schedule, not
              by hand
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection
          title="Infrastructure"
          isExpanded={expandedSection === "infrastructure"}
          onToggle={() => handleToggle("infrastructure")}
        >
          <div className="tech-stack-grid">
            <div className="tech-card">
              <h4>Cluster</h4>
              <p>
                Self-hosted Kubernetes on bare metal. Traefik terminates TLS
                at the edge with certificates automated by cert-manager;
                Cloudflare provides DNS, with an in-cluster dynamic-DNS
                controller absorbing residential IP rotation. Ingestion
                workers autoscale on queue depth via KEDA; steady-state
                services hold fixed replica counts on purpose.
              </p>
            </div>
            <div className="tech-card">
              <h4>Data hosts</h4>
              <p>
                PostgreSQL 17 and MongoDB 8 each run on a dedicated
                bare-metal machine with NVMe storage, deliberately outside
                the cluster: the databases outlive any cluster experiment.
                Both take nightly backups that are re-downloaded and
                verified, not just written and trusted.
              </p>
            </div>
            <div className="tech-card">
              <h4>Messaging</h4>
              <p>
                RabbitMQ split into per-sport brokers so each sport&rsquo;s
                event volume is isolated, with cross-broker shovels carrying
                the small set of messages that must cross sport boundaries.
                Managed declaratively via the RabbitMQ topology operator.
              </p>
            </div>
            <div className="tech-card">
              <h4>Configuration</h4>
              <p>
                No appsettings files - all runtime configuration lives in
                Azure App Configuration with label-based multi-tenancy per
                environment, sport, and service. Reloader watches config and
                secret changes and rolls affected pods.
              </p>
            </div>
          </div>
        </CollapsibleSection>

        <CollapsibleSection
          title="Data Architecture"
          isExpanded={expandedSection === "data"}
          onToggle={() => handleToggle("data")}
        >
          <ul>
            <li>
              <strong>MongoDB - the raw layer:</strong> every JSON payload
              ever sourced, immutable and content-addressed. Reprocessing a
              season never re-hits the external API
            </li>
            <li>
              <strong>PostgreSQL - the canonical layer:</strong> one
              normalized, code-first schema per sport family (EF Core
              migrations), from franchises and venues down to individual
              plays and win-probability ticks
            </li>
            <li>
              <strong>Derived layers:</strong> season metrics computed from
              play-by-play, matchup previews and their model captures, league
              scoring - all rebuildable from canonical data by replayable
              jobs
            </li>
            <li>
              <strong>Redis - the serving layer:</strong> distributed cache
              in front of expensive reads, wrapped in a circuit breaker so a
              cache outage degrades to slower responses, never to errors
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection
          title="Authentication"
          isExpanded={expandedSection === "auth"}
          onToggle={() => handleToggle("auth")}
        >
          <p>
            Firebase Authentication on every surface - web, iOS, Android -
            with Google and Apple sign-in. The API validates Firebase JWTs
            and layers custom claims for roles (admin surfaces, league
            commissioners). Ingestion services are never publicly exposed;
            only the API and the web frontends have routes at the edge.
          </p>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default ArchitectureSection;
