import React, { useState } from 'react';
import CollapsibleSection from '../common/CollapsibleSection';
import ArchitectureDiagrams from '../diagrams/ArchitectureDiagrams';

const ArchitectureSection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState(null);

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">Architecture</h2>
        <p className="section-subtitle">Services, Infrastructure, and Data Flow</p>
      </div>
      
      <div className="section-content">
        <ArchitectureDiagrams />
        
        <CollapsibleSection 
          title="Services" 
          isExpanded={expandedSection === 'services'}
          onToggle={() => handleToggle('services')}
        >
          <ul>
            <li>
              <strong>Producer:</strong> Canonical ingestion service that transforms raw ESPN data into 
              the sportDeets canonical model. Handles data normalization, deduplication, and enrichment.
            </li>
            <li>
              <strong>Provider:</strong> Raw ESPN API ingestion service that recursively fetches JSON data 
              with reference resolution. Stores raw payloads in Cosmos DB for audit and replay.
            </li>
            <li>
              <strong>API (UI BFF):</strong> Backend-for-frontend that serves the React application. Aggregates 
              data from multiple sources, applies business logic, and provides optimized DTOs for UI consumption.
            </li>
            <li>
              <strong>Enricher (planned):</strong> Future service to assign advanced game traits like "DayGame", 
              "Rivalry", "Conference Championship", etc. Will enhance predictions and user experience.
            </li>
            <li>
              <strong>Model Agents (planned):</strong> Dedicated services for ML model training, versioning, 
              and prediction generation. Will support A/B testing and model performance tracking.
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Infrastructure" 
          isExpanded={expandedSection === 'infrastructure'}
          onToggle={() => handleToggle('infrastructure')}
        >
          <p><strong>Development Environment:</strong></p>
          <ul>
            <li>Azure App Services host all .NET services (Producer, Provider, API)</li>
            <li>Azure PostgreSQL Flexible Server for canonical data model</li>
            <li>Azure Cosmos DB for raw JSON document storage</li>
            <li>Azure Blob Storage for team logos and database backups</li>
          </ul>

          <p><strong>Production Environment:</strong></p>
          <ul>
            <li>Vagrant-based k3s cluster running on local hardware</li>
            <li>Flux GitOps managing cluster state from <code>sports-data-config</code> repository</li>
            <li>Prometheus/Grafana/Loki/Tempo observability stack</li>
            <li>PostgreSQL deployed as StatefulSet with persistent volumes</li>
          </ul>

          <p><strong>CI/CD Pipeline:</strong></p>
          <ul>
            <li>Azure DevOps pipelines for each service</li>
            <li>Automated builds on PR merge to main branch</li>
            <li>Container images pushed to Azure Container Registry</li>
            <li>Flux automatically deploys updated images to k3s cluster</li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Data Architecture" 
          isExpanded={expandedSection === 'data'}
          onToggle={() => handleToggle('data')}
        >
          <p><strong>Canonical PostgreSQL Model:</strong></p>
          <ul>
            <li>Rich Entity Framework Core entities for all NCAA football data</li>
            <li>Normalized schema with proper foreign key relationships</li>
            <li>Supports teams, players, games, stats, picks, leagues, and predictions</li>
            <li>Optimized indexes for common query patterns</li>
          </ul>

          <p><strong>Cosmos DB (Raw JSON):</strong></p>
          <ul>
            <li>Stores original ESPN API responses for audit trail</li>
            <li>Enables replay of ingestion pipeline for debugging</li>
            <li>Partitioned by data type (teams, games, stats)</li>
            <li>TTL policies for data retention management</li>
          </ul>

          <p><strong>Blob Storage:</strong></p>
          <ul>
            <li>Team logos and helmet images</li>
            <li>Database backups with retention policies</li>
            <li>Static assets for UI consumption</li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Authentication & Authorization" 
          isExpanded={expandedSection === 'auth'}
          onToggle={() => handleToggle('auth')}
        >
          <ul>
            <li>
              <strong>Firebase Authentication:</strong> Handles user sign-up, login, and session management
            </li>
            <li>
              <strong>Custom Claims:</strong> Extends Firebase tokens with app-specific roles and permissions
            </li>
            <li>
              <strong>Onboarding Flow:</strong> New users guided through profile setup and league discovery
            </li>
            <li>
              <strong>API Security:</strong> JWT validation on all protected endpoints with role-based access control
            </li>
          </ul>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default ArchitectureSection;
