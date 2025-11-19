import React, { useState } from "react";
import CollapsibleSection from "../common/CollapsibleSection";
import MermaidDiagram from "../common/MermaidDiagram";
import ArchitectureDiagrams from "../diagrams/ArchitectureDiagrams";

const ArchitectureSection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState(null);

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  const providerDiagram = `graph TD
    H[HTTP Request] --> C
    A[Hangfire Job] --> C{BypassCache?}
    C -->|Yes| B[ESPN API]
    C -->|No| D[(Cosmos DB)]
    D -->|Not Found| B
    D -->|Found| E[Publish Event]
    B --> F{Resource Is Index?}
    F -->|Yes| G[Resolve References]
    F -->|No| I[Store in Cosmos DB]
    G --> I
    I --> E
    E --> J[Producer Service]
    
    style H fill:#0891b2
    style A fill:#1e40af
    style C fill:#0d9488
    style B fill:#7c3aed
    style D fill:#059669
    style F fill:#0d9488
    style I fill:#059669
    style E fill:#6366f1
    style J fill:#1e40af`;

  const producerDiagram = `graph TD
    A[Event from Provider] --> B{Has JSON Payload?}
    B -->|Yes| C[Resolve Processor O1]
    B -->|No - Has Document ID| D[Call Provider HTTP Client]
    D --> E[Provider Returns JSON]
    E --> C
    C --> F[Queue Background Job]
    F --> G[Deserialize JSON]
    G --> H[Transform to Canonical]
    H --> I[Normalize Data]
    I --> J[Deduplicate]
    J --> K[(PostgreSQL)]
    K --> L[Raise Domain/Canonical Event/s]
    L --> M[Update Cache]
    
    style A fill:#1e40af
    style B fill:#0d9488
    style C fill:#8b5cf6
    style D fill:#7c3aed
    style E fill:#7c3aed
    style H fill:#7c3aed
    style K fill:#059669
    style L fill:#6366f1
    style M fill:#dc2626`;

  const apiDiagram = `graph TD
    A[React UI] --> B{API Gateway}
    B --> C[Authentication]
    C --> D[Application Layer]
    D --> E{Canonical Data Provider}
    E -->|Direct Access| F[(PostgreSQL)]
    E -->|HTTP Client| G[Producer Service]
    G --> F
    F --> H[Apply Business Logic]
    H --> I[Build DTOs]
    I --> J[Return JSON]
    
    F --> K[Cache Layer]
    K --> F
    
    style A fill:#7c3aed
    style B fill:#1e40af
    style D fill:#0891b2
    style E fill:#0d9488
    style F fill:#059669
    style G fill:#1e40af
    style K fill:#dc2626`;

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">Architecture</h2>
        <p className="section-subtitle">
          Services, Infrastructure, and Data Flow
        </p>
      </div>

      <div className="section-content">
        <ArchitectureDiagrams />

        <CollapsibleSection
          title="Services"
          isExpanded={expandedSection === "services"}
          onToggle={() => handleToggle("services")}
        >
          <div className="tech-stack-grid">
            {/* Provider Service */}
            <div className="tech-card">
              <h4>
                Provider
                <a
                  href="https://github.com/jrandallsexton/sports-data-core/tree/main/src/SportsData.Provider"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="github-link"
                  title="View Provider source on GitHub"
                >
                  <svg
                    height="20"
                    width="20"
                    viewBox="0 0 16 16"
                    fill="currentColor"
                    style={{ marginLeft: "8px", verticalAlign: "middle" }}
                  >
                    <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z" />
                  </svg>
                </a>
              </h4>
              <p className="service-description">
                Raw ESPN API ingestion service that recursively fetches JSON
                data with reference resolution. Stores raw payloads in Cosmos DB
                for audit and replay.
              </p>
              <MermaidDiagram chart={providerDiagram} />
            </div>

            {/* Producer Service */}
            <div className="tech-card">
              <h4>
                Producer
                <a
                  href="https://github.com/jrandallsexton/sports-data-core/tree/main/src/SportsData.Producer"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="github-link"
                  title="View Producer source on GitHub"
                >
                  <svg
                    height="20"
                    width="20"
                    viewBox="0 0 16 16"
                    fill="currentColor"
                    style={{ marginLeft: "8px", verticalAlign: "middle" }}
                  >
                    <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z" />
                  </svg>
                </a>
              </h4>
              <p className="service-description">
                Canonical ingestion service that transforms raw ESPN data into
                the sportDeets canonical model. Handles data normalization,
                deduplication, and enrichment.
              </p>
              <MermaidDiagram chart={producerDiagram} />
            </div>

            {/* API Service */}
            <div className="tech-card">
              <h4>
                API (UI BFF)
                <a
                  href="https://github.com/jrandallsexton/sports-data-core/tree/main/src/SportsData.Api"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="github-link"
                  title="View API source on GitHub"
                >
                  <svg
                    height="20"
                    width="20"
                    viewBox="0 0 16 16"
                    fill="currentColor"
                    style={{ marginLeft: "8px", verticalAlign: "middle" }}
                  >
                    <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z" />
                  </svg>
                </a>
              </h4>
              <p className="service-description">
                Backend-for-frontend that serves the React application.
                Aggregates data from multiple sources, applies business logic,
                and provides optimized DTOs for UI consumption.
              </p>
              <MermaidDiagram chart={apiDiagram} />
            </div>
          </div>

          <div className="future-services">
            <h4>Planned Services</h4>
            <ul>
              <li>
                <strong>Enricher:</strong> Assigns advanced game traits like
                "DayGame", "Rivalry", "Conference Championship" to enhance
                predictions and UX
              </li>
              <li>
                <strong>Model Agents:</strong> Dedicated services for ML model
                training, versioning, and prediction generation with A/B testing
                support
              </li>
            </ul>
          </div>
        </CollapsibleSection>

        <CollapsibleSection
          title="Infrastructure"
          isExpanded={expandedSection === "infrastructure"}
          onToggle={() => handleToggle("infrastructure")}
        >
          <p>
            <strong>Development Environment:</strong>
          </p>
          <ul>
            <li>
              <strong>Azure App Services:</strong> Hosts .NET services
              (Producer, Provider, API) during development for consistent CI/CD
              deployment
            </li>
            <li>
              <strong>Azure VM (PostgreSQL):</strong> Self-managed PostgreSQL
              server running on a dev-tier VM to reduce hosting costs and
              support real-world data scale
            </li>
            <li>
              <strong>Azure Virtual Network:</strong> Connects App Services to
              the PostgreSQL VM via private IP, ensuring secure internal traffic
              without public exposure
            </li>
            <li>
              <strong>Azure Cosmos DB:</strong> Raw JSON document storage for
              ESPN-sourced data used in ingestion and enrichment pipelines
            </li>
            <li>
              <strong>Azure Blob Storage:</strong> Stores team logos, structured
              logs, and PostgreSQL backup files
            </li>
          </ul>

          <p>
            <strong>Production Environment:</strong>
          </p>
          <ul>
            <li>
              <strong>Azure API Management (APIM):</strong> External gateway for{" "}
              <code>api.sportdeets.com</code>, routing public traffic to
              internal services
            </li>
            <li>
              <strong>Azure App Configuration:</strong> Centralized
              configuration store with dynamic refresh support for .NET services
            </li>
            <li>
              <strong>Azure Blob Storage:</strong> Static asset and backup store
              for logos, image uploads, log exports, and daily PostgreSQL
              backups
            </li>
            <li>
              <strong>Azure Front Door:</strong> TLS termination and global edge
              entry point for <code>www.sportdeets.com</code>, with CDN caching
              and failover
            </li>
            <li>
              <strong>Azure Key Vault:</strong> Secrets, connection strings, and
              service credentials accessed securely via Kubernetes workload
              identity
            </li>
            <li>
              <strong>Flux GitOps:</strong> Manages desired cluster state via
              Kustomize overlays in the <code>sports-data-config</code> repo
            </li>
            <li>
              <strong>Local PostgreSQL VM:</strong> Canonical database hosted on
              a dedicated Linux VM on the same physical host as the cluster,
              accessed via private networking
            </li>
            <li>
              <strong>Observability Stack:</strong> Prometheus, Grafana, Loki,
              and Tempo deployed in-cluster with persistent volumes and
              dashboards via Grafana
            </li>
            <li>
              <strong>Persistent Volumes:</strong> Backed by local SSD for
              Prometheus, Loki, and other stateful services
            </li>
            <li>
              <strong>Private Networking:</strong> VMs and cluster nodes
              communicate via internal vSwitch; external DNS routed through
              split-horizon local DNS
            </li>
            <li>
              <strong>Reloader:</strong> Automatically triggers pod restarts on
              changes to mounted ConfigMaps or Secrets
            </li>
            <li>
              <strong>Traefik Ingress:</strong> Handles all HTTP routing inside
              the cluster, with TLS passthrough and dashboard access
            </li>
            <li>
              <strong>Vagrant + k3s:</strong> Local Kubernetes cluster hosted on
              Hyper-V (nodes sd0–sd3), provisioned automatically via Vagrant
              scripts
            </li>
          </ul>

          <p>
            <strong>CI/CD Pipeline:</strong>
          </p>
          <ul>
            <li>
              <strong>Azure App Configuration & Key Vault:</strong> Secure
              storage for environment-specific settings and secrets
            </li>
            <li>
              <strong>Azure Container Registry (ACR):</strong> Stores versioned
              Docker images for services and UI
            </li>
            <li>
              <strong>Azure DevOps Pipelines:</strong> Per-service pipelines
              triggered on PR merges to <code>main</code>
            </li>
            <li>
              <strong>Flux GitOps:</strong> Monitors image tags and applies
              updates to the k3s cluster automatically
            </li>
            <li>
              <strong>Pre-deployment Checks:</strong> Unit tests (backend) and
              lint checks (UI) must pass before builds proceed
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection
          title="Data Architecture"
          isExpanded={expandedSection === "data"}
          onToggle={() => handleToggle("data")}
        >
          <p>
            <strong>Canonical PostgreSQL Model:</strong>
          </p>
          <ul>
            <li>
              <strong>Comprehensive Coverage:</strong> Includes franchises,
              players, contests, stats, picks, leagues, and predictions
            </li>
            <li>
              <strong>EF Core Entities:</strong> Code-first Entity Framework
              Core models reflect domain concepts and business rules
            </li>
            <li>
              <strong>Index Optimization:</strong> Strategic indexes support
              efficient access patterns for leaderboard queries, pick accuracy,
              etc.
            </li>
            <li>
              <strong>Relational Integrity:</strong> Fully normalized schema
              with foreign key constraints and navigation properties
            </li>
          </ul>

          <p>
            <strong>Cosmos DB (Raw JSON):</strong>
          </p>
          <ul>
            <li>
              <strong>Deduplication via Hashing:</strong> Uses stable URL hashes
              to prevent duplicate ingestion across parallel sourcing jobs
            </li>
            <li>
              <strong>Ingestion Replay:</strong> Supports full pipeline
              reprocessing as DTOs or processors evolve, aiding validation and
              debugging
            </li>
            <li>
              <strong>Lifecycle Management:</strong> TTL policies automatically
              expire stale documents to reduce cost and clutter
            </li>
            <li>
              <strong>Partitioning Strategy:</strong> Partitioned by document
              type (e.g., teams, games, stats, athletes) to optimize for query
              scalability
            </li>
            <li>
              <strong>Raw Source Preservation:</strong> Retains exact ESPN API
              responses for auditing, regression testing, and model training
              transparency
            </li>
            <li>
              <strong>Separation of Concerns:</strong> Isolated from the
              canonical model to enforce clean boundaries between raw ingestion
              and business entities
            </li>
          </ul>

          <p>
            <strong>Azure Blob Storage:</strong>
          </p>
          <ul>
            <li>
              <strong>Asset Caching:</strong> Hosts images and static files for
              efficient delivery across environments
            </li>
            <li>
              <strong>Backup Storage:</strong> Stores PostgreSQL and Cosmos DB
              backups with enforced retention policies
            </li>
            <li>
              <strong>Helmet & Logo Hosting:</strong> Central source for team
              visuals used across TeamCards, Picks, and Leaderboards
            </li>
            <li>
              <strong>Image Hashing:</strong> Deduplicates external image
              requests via hashed filenames to minimize bandwidth and storage
            </li>
            <li>
              <strong>Static Content Delivery:</strong> Provides public-read
              paths for UI elements (e.g., splash screens, overlays)
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection
          title="Authentication & Authorization"
          isExpanded={expandedSection === "auth"}
          onToggle={() => handleToggle("auth")}
        >
          <ul>
            <li>
              <strong>API-Backed Authorization:</strong> Authenticated users are
              issued enriched <code>UserDto</code> objects containing roles,
              league memberships, and profile metadata
            </li>
            <li>
              <strong>Firebase Authentication:</strong> Manages user sign-up,
              login, and session lifecycle with secure JWTs
            </li>
            <li>
              <strong>JWT Validation:</strong> All protected API endpoints
              require valid Firebase-issued tokens for access
            </li>
            <li>
              <strong>Onboarding Flow:</strong> First-time users complete a
              guided setup to define profile and discover leagues
            </li>
            <li>
              <strong>Role & Access Control:</strong> Access is determined by
              API logic based on <code>UserDto</code> contents (e.g., admin
              privileges, league participation)
            </li>
          </ul>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default ArchitectureSection;
