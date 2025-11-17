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
            contests, predictive insights, and rich stat visualizations for
            competitive and casual users alike.
          </p>
          <p>
            The platform blends traditional sports engagement with advanced
            machine learning, allowing users to compete in private leagues while
            leveraging AI-driven forecasts and deep performance data.
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
                <strong>React Router</strong> - Client-side routing
              </p>
              <p>
                <strong>Firebase</strong> - Authentication and custom claims
              </p>
            </div>

            <div className="tech-card">
              <h4>Backend</h4>
              <p>
                <strong>C# / .NET</strong> - Core services and APIs
              </p>
              <p>
                <strong>Entity Framework Core</strong> - ORM for canonical model
              </p>
              <p>
                <strong>ASP.NET Core</strong> - RESTful API services
              </p>
            </div>

            <div className="tech-card">
              <h4>Cloud & Infrastructure</h4>
              <p>
                <strong>Azure App Services</strong> - Development environment
              </p>
              <p>
                <strong>Azure PostgreSQL</strong> - Canonical data model
              </p>
              <p>
                <strong>Azure Cosmos DB</strong> - Raw JSON storage
              </p>
              <p>
                <strong>Azure Blob Storage</strong> - Logos and backups
              </p>
            </div>

            <div className="tech-card">
              <h4>Container Orchestration</h4>
              <p>
                <strong>Kubernetes (k3s)</strong> - Production cluster
              </p>
              <p>
                <strong>Vagrant</strong> - Local k3s provisioning
              </p>
              <p>
                <strong>Flux</strong> - GitOps workflow
              </p>
            </div>

            <div className="tech-card">
              <h4>Observability</h4>
              <p>
                <strong>Prometheus</strong> - Metrics collection
              </p>
              <p>
                <strong>Grafana</strong> - Dashboards and visualization
              </p>
              <p>
                <strong>Loki</strong> - Log aggregation
              </p>
              <p>
                <strong>Tempo</strong> - Distributed tracing
              </p>
              <p>
                <strong>Seq</strong> - Structured logging
              </p>
            </div>

            <div className="tech-card">
              <h4>AI & ML</h4>
              <p>
                <strong>Ollama</strong> - Local LLM hosting
              </p>
              <p>
                <strong>Python</strong> - ML model training
              </p>
              <p>
                <strong>Scikit-learn</strong> - Logistic Regression, Random
                Forest
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
              <strong>Responsive UI:</strong> Designed for desktop-first use
              with mobile and tablet layout support where needed
            </li>
          </ul>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default OverviewSection;
