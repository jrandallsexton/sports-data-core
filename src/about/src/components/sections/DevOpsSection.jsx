import React, { useState } from "react";
import CollapsibleSection from "../common/CollapsibleSection";

const DevOpsSection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState(null);

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">DevOps &amp; GitOps</h2>
        <p className="section-subtitle">
          Merges deploy; the cluster is a repo
        </p>
      </div>

      <div className="section-content">
        <CollapsibleSection
          title="GitOps"
          isExpanded={expandedSection === "gitops"}
          onToggle={() => handleToggle("gitops")}
        >
          <p>
            The cluster&rsquo;s entire desired state - workloads, ingress,
            certificates, monitoring dashboards, RabbitMQ topology - lives
            in a dedicated configuration repository. Flux CD reconciles the
            cluster against it continuously; Kustomize overlays separate
            base manifests from environment specifics. Changing production
            means merging a pull request, and the audit trail is git
            history.
          </p>
        </CollapsibleSection>

        <CollapsibleSection
          title="CI/CD"
          isExpanded={expandedSection === "cicd"}
          onToggle={() => handleToggle("cicd")}
        >
          <div className="tech-stack-grid">
            <div className="tech-card">
              <h4>.NET services</h4>
              <p>
                Azure Pipelines running on a self-hosted agent build, test,
                and push per-service container images to a private registry.
                One monorepo means one PR can change a contract and both its
                producer and consumer atomically.
              </p>
            </div>
            <div className="tech-card">
              <h4>Mobile</h4>
              <p>
                GitHub Actions plus EAS Build produce signed iOS and Android
                binaries and submit them to the App Store and Google Play.
                Over-the-air updates handle JS-only changes between store
                releases.
              </p>
            </div>
            <div className="tech-card">
              <h4>Review</h4>
              <p>
                Branch protection on main with pull requests for every
                change - reviewed by AI code reviewers (CodeRabbit and a
                correctness-focused adversarial reviewer) whose findings are
                verified against the code before any fix lands.
              </p>
            </div>
            <div className="tech-card">
              <h4>Local parity</h4>
              <p>
                Per-sport Docker Compose files stand up any slice of the
                platform locally - databases, broker, services, jobs
                dashboard - so end-to-end validation happens before a PR,
                not after a deploy.
              </p>
            </div>
          </div>
        </CollapsibleSection>

        <CollapsibleSection
          title="Operational Automation"
          isExpanded={expandedSection === "automation"}
          onToggle={() => handleToggle("automation")}
        >
          <ul>
            <li>
              <strong>Reloader</strong> - configuration and secret changes
              trigger rolling restarts of exactly the affected workloads
            </li>
            <li>
              <strong>KEDA</strong> - ingestion deployments scale on RabbitMQ
              queue depth: game-day bursts scale out, quiet weekdays scale
              in
            </li>
            <li>
              <strong>cert-manager + Cloudflare DDNS</strong> - TLS renewal
              and DNS-over-rotating-IP are fully automated; neither has
              needed a human since installation
            </li>
            <li>
              <strong>Nightly verified backups</strong> - both database
              hosts back up nightly, and the backups are re-downloaded and
              checked, because an unverified backup is a hope, not a plan
            </li>
          </ul>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default DevOpsSection;
