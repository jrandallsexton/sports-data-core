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
            <strong>sportDeets</strong> is a full-stack sports analytics
            platform built for NCAA football fans. It powers weekly pick’em
            contests, AI-backed predictions, and rich visualizations for both
            competitive and casual users.
          </p>
          <p>
            By blending traditional fan engagement with machine learning,
            sportDeets enables users to create private leagues, make data-driven
            picks, and explore deep performance insights throughout the season.
          </p>
        </CollapsibleSection>

        <CollapsibleSection
          title="Tech Stack Summary"
          isExpanded={expandedSection === "tech-stack"}
          onToggle={() => handleToggle("tech-stack")}
        >
          <div className="tech-stack-grid">
            <div className="tech-card">
              <h4>Frontend</h4>
              <p>
                <strong>React 19.1.0</strong> - Modern UI with hooks and
                functional components
              </p>
              <p>
                <strong>React Router 7</strong> - Client-side routing and navigation
              </p>
              <p>
                <strong>Firebase 11</strong> - Authentication and custom claims
              </p>
              <p>
                <strong>SignalR</strong> - Real-time updates for live contest data
              </p>
              <p>
                <strong>Material-UI</strong> - Component library for consistent design
              </p>
              <p>
                <strong>Chart.js & Recharts</strong> - Data visualization
              </p>
              <p>
                <strong>Axios</strong> - HTTP client for API communication
              </p>
            </div>

            <div className="tech-card">
              <h4>Backend</h4>
              <p>
                <strong>C# / .NET 9</strong> – Modular services powering
                ingestion, enrichment, and API layers
              </p>
              <p>
                <strong>ASP.NET Core</strong> – RESTful endpoints and background
                service hosts
              </p>
              <p>
                <strong>AutoMapper</strong> – Object-to-object mapping for DTOs
                and entity transformations
              </p>
              <p>
                <strong>Entity Framework Core</strong> – Code-first ORM for
                canonical PostgreSQL schema
              </p>
              <p>
                <strong>FluentValidation</strong> – Declarative validation rules
                for commands and queries
              </p>
              <p>
                <strong>Hangfire</strong> – Background job orchestration and
                recurring task execution
              </p>
              <p>
                <strong>MassTransit</strong> – Distributed messaging over Azure
                Service Bus with outbox pattern
              </p>
              <p>
                <strong>MediatR</strong> – CQRS and mediator pattern
                implementation for clean architecture
              </p>
              <p>
                <strong>Polly</strong> – Retry, circuit breaker, and timeout
                policies for resilient API calls
              </p>
              <p>
                <strong>Serilog</strong> – Structured logging with Seq and
                console sinks
              </p>
              <p>
                <strong>SportsData.Core</strong> – Shared library with DI
                registration extensions, data persistence, messaging, health
                checks, and HTTP clients used across all services
              </p>
            </div>

            <div className="tech-card">
              <h4>Cloud & Infrastructure</h4>
              <p>
                <strong>Azure API Management</strong> – Consumption-tier gateway
                proxying Front Door requests to internal k3s API with CORS and
                rate limiting
              </p>
              <p>
                <strong>Azure App Configuration</strong> – Centralized app
                settings with dynamic refresh
              </p>
              <p>
                <strong>Azure App Services</strong> – Hosts API and background
                workers in development
              </p>
              <p>
                <strong>Azure Arc</strong> – Connects on-prem k3s cluster to
                Azure with managed identity for secure Key Vault access
              </p>
              <p>
                <strong>Azure Bastion</strong> – Secure SSH access to PostgreSQL
                VM using RSA keys without public IP exposure
              </p>
              <p>
                <strong>Azure Blob & Disk Storage</strong> – Blob storage for
                logos, images, logs, and backups; managed disks for PostgreSQL VM
              </p>
              <p>
                <strong>Azure Container Registry</strong> – Private Docker
                image repository for service deployments to App Services and k3s
              </p>
              <p>
                <strong>Azure Cosmos DB</strong> – Raw document store for
                ingested external JSON (ESPN APIs, etc.)
              </p>
              <p>
                <strong>Azure Data Factory</strong> – Orchestrates Cosmos DB
                data sync from production to development for realistic testing
              </p>
              <p>
                <strong>Azure Front Door</strong> – Global entry point for all
                environments with routing to k3s cluster
              </p>
              <p>
                <strong>Azure Key Vault</strong> – Secrets and credentials
                management for services and pipelines
              </p>
              <p>
                <strong>Azure Pipelines</strong> – CI/CD automation for
                building, testing, and deploying services to ACR and environments
              </p>
              <p>
                <strong>Azure Service Bus</strong> – Distributed messaging
                backbone between Provider, Producer, and API services
              </p>
              <p>
                <strong>Azure SignalR Service</strong> – Real-time push from API
                to UI for contest updates and status broadcasting
              </p>
              <p>
                <strong>Azure Static Web Apps</strong> – Hosts React UI in both
                development and production with global CDN distribution
              </p>
              <p>
                <strong>Azure Virtual Machine</strong> – Self-managed PostgreSQL
                server in development environment
              </p>
              <p>
                <strong>Azure Virtual Network</strong> – Private networking
                connecting App Services to PostgreSQL VM in development
              </p>
            </div>

            <div className="tech-card">
              <h4>Container Orchestration</h4>
              <p>
                <strong>Kubernetes (k3s)</strong> – Lightweight,
                production-grade cluster powering sportDeets on-prem
              </p>
              <p>
                <strong>Vagrant + Hyper-V</strong> – Automates local k3s node
                provisioning (4 nodes) for dev/test parity
              </p>
              <p>
                <strong>Flux CD</strong> – GitOps controller syncing manifests
                from <code>sports-data-config</code> repo
              </p>
              <p>
                <strong>Kustomize</strong> – Overlay-based environment
                templating (dev, prod) for scalable deployments
              </p>
              <p>
                <strong>Reloader</strong> – Detects config and secret changes,
                triggers rolling restarts for affected pods
              </p>
              <p>
                <strong>Traefik</strong> – Ingress controller for internal
                routing, TLS termination, and dashboard access
              </p>
            </div>

            <div className="tech-card">
              <h4>Observability</h4>
              <p>
                <strong>Prometheus</strong> – Time-series metrics collection for
                all cluster and service components
              </p>
              <p>
                <strong>Grafana</strong> – Unified dashboards for system health,
                job queues, and usage trends
              </p>
              <p>
                <strong>Loki</strong> – Centralized log aggregation and
                filtering across microservices
              </p>
              <p>
                <strong>Tempo</strong> – Distributed trace visualization for
                request flows and latency hotspots
              </p>
              <p>
                <strong>Seq</strong> – Structured application logging with
                real-time querying and alerting
              </p>
            </div>

            <div className="tech-card">
              <h4>AI & ML</h4>
              <p>
                <strong>Ollama</strong> – Local LLM hosting and prompt-based
                insights (e.g., matchup previews, user-facing analysis)
              </p>
              <p>
                <strong>Python</strong> – Scripts for model training, weekly
                prediction generation, and post-game evaluation
              </p>
              <p>
                <strong>Scikit-learn</strong> – Baseline classification models
                (Logistic Regression, Random Forest) for straight-up and ATS
                picks
              </p>
              <p>
                <strong>PostgreSQL</strong> – Features and results stored for
                training and evaluation with week-over-week rollups
              </p>
              <p>
                <strong>Custom Prompt Templates</strong> – Persona-driven
                narratives (e.g., “MetricBot”) for LLM-generated content
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
              <strong>Pick'em Contests:</strong> Weekly games where users
              predict outcomes straight-up or against the spread, with optional
              tiebreakers
            </li>
            <li>
              <strong>AI-Driven Predictions:</strong> Metrics-based models
              generate SU and ATS picks with confidence percentages and win
              probabilities
            </li>
            <li>
              <strong>League Creation:</strong> Users can create private
              leagues, invite participants, and configure scoring, tiebreaker
              rules, and team filters
            </li>
            <li>
              <strong>Standings & Results:</strong> Weekly rollups, contest
              outcomes, and league standings automatically generated and
              published
            </li>
            <li>
              <strong>Team & Game Explorer:</strong> Browse detailed team pages
              with schedules, results, venues, and recent news
            </li>
            <li>
              <strong>Responsive Design:</strong> Optimized for desktop-first
              interaction with smooth adaptation to tablets and mobile devices
            </li>
          </ul>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default OverviewSection;
