import React, { useState } from 'react';
import CollapsibleSection from '../common/CollapsibleSection';
import MermaidDiagram from '../common/MermaidDiagram';
import ArchitectureDiagrams from '../diagrams/ArchitectureDiagrams';

const ArchitectureSection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState(null);

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  const providerDiagram = `graph TD
    A[Hangfire Job] --> B[ESPN API]
    B --> C{Parse Response}
    C --> D[Resolve References]
    D --> E[(Cosmos DB)]
    E --> F[Publish Event]
    F --> G[Producer Service]
    
    style A fill:#1e40af
    style B fill:#7c3aed
    style E fill:#059669
    style F fill:#6366f1
    style G fill:#1e40af`;

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
        <p className="section-subtitle">Services, Infrastructure, and Data Flow</p>
      </div>
      
      <div className="section-content">
        <ArchitectureDiagrams />
        
        <CollapsibleSection 
          title="Services" 
          isExpanded={expandedSection === 'services'}
          onToggle={() => handleToggle('services')}
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
                  <svg height="20" width="20" viewBox="0 0 16 16" fill="currentColor" style={{ marginLeft: '8px', verticalAlign: 'middle' }}>
                    <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/>
                  </svg>
                </a>
              </h4>
              <p className="service-description">
                Raw ESPN API ingestion service that recursively fetches JSON data 
                with reference resolution. Stores raw payloads in Cosmos DB for audit and replay.
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
                  <svg height="20" width="20" viewBox="0 0 16 16" fill="currentColor" style={{ marginLeft: '8px', verticalAlign: 'middle' }}>
                    <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/>
                  </svg>
                </a>
              </h4>
              <p className="service-description">
                Canonical ingestion service that transforms raw ESPN data into 
                the sportDeets canonical model. Handles data normalization, deduplication, and enrichment.
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
                  <svg height="20" width="20" viewBox="0 0 16 16" fill="currentColor" style={{ marginLeft: '8px', verticalAlign: 'middle' }}>
                    <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/>
                  </svg>
                </a>
              </h4>
              <p className="service-description">
                Backend-for-frontend that serves the React application. Aggregates 
                data from multiple sources, applies business logic, and provides optimized DTOs for UI consumption.
              </p>
              <MermaidDiagram chart={apiDiagram} />
            </div>
          </div>

          <div className="future-services">
            <h4>Planned Services</h4>
            <ul>
              <li>
                <strong>Enricher:</strong> Assigns advanced game traits like "DayGame", 
                "Rivalry", "Conference Championship" to enhance predictions and UX
              </li>
              <li>
                <strong>Model Agents:</strong> Dedicated services for ML model training, versioning, 
                and prediction generation with A/B testing support
              </li>
            </ul>
          </div>
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
