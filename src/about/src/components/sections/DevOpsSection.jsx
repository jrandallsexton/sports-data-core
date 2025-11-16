import React, { useState } from 'react';
import CollapsibleSection from '../common/CollapsibleSection';

const DevOpsSection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState(null);

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">DevOps and GitOps</h2>
        <p className="section-subtitle">CI/CD, Infrastructure as Code, and Automation</p>
      </div>
      
      <div className="section-content">
        <CollapsibleSection 
          title="CI/CD Pipeline" 
          isExpanded={expandedSection === 'cicd'}
          onToggle={() => handleToggle('cicd')}
        >
          <p><strong>Azure DevOps Pipelines:</strong></p>
          <ul>
            <li>
              <strong>Per-Service Pipelines:</strong> Each microservice (Producer, Provider, API) has 
              dedicated build and release pipelines
            </li>
            <li>
              <strong>Automated Builds:</strong> Triggered on PR merge to main branch
            </li>
            <li>
              <strong>Build Steps:</strong>
              <ul>
                <li>Restore NuGet packages</li>
                <li>Compile .NET projects</li>
                <li>Run unit tests with code coverage</li>
                <li>Build Docker container images</li>
                <li>Push images to Azure Container Registry</li>
                <li>Update Flux manifests with new image tags</li>
              </ul>
            </li>
            <li>
              <strong>Deployment Strategy:</strong> GitOps-based - Flux monitors manifest repo and 
              automatically deploys updated images to k3s cluster
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Secrets Management" 
          isExpanded={expandedSection === 'secrets'}
          onToggle={() => handleToggle('secrets')}
        >
          <p><strong>Azure App Configuration + Key Vault:</strong></p>
          <ul>
            <li>
              <strong>Centralized Configuration:</strong> All service settings stored in Azure App Configuration
            </li>
            <li>
              <strong>Secret References:</strong> Sensitive values (connection strings, API keys) stored 
              in Azure Key Vault and referenced from App Configuration
            </li>
            <li>
              <strong>Runtime Injection:</strong> Services load configuration at startup, automatically 
              resolving Key Vault references
            </li>
            <li>
              <strong>Kubernetes Secrets:</strong> Production cluster uses Sealed Secrets for encrypted 
              secret storage in Git
            </li>
            <li>
              <strong>Rotation:</strong> Automated secret rotation policies for database credentials and API keys
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="GitOps with Flux" 
          isExpanded={expandedSection === 'gitops'}
          onToggle={() => handleToggle('gitops')}
        >
          <p><strong>Repository Structure:</strong></p>
          <ul>
            <li>
              <strong>sports-data-config:</strong> Dedicated repository for Kubernetes manifests and Flux configuration
            </li>
            <li>
              <strong>Declarative State:</strong> All cluster resources defined in YAML (deployments, services, 
              ingress, configmaps)
            </li>
            <li>
              <strong>Automatic Reconciliation:</strong> Flux continuously monitors the config repo and applies 
              changes to the cluster
            </li>
          </ul>

          <p><strong>Benefits:</strong></p>
          <ul>
            <li>Infrastructure state is version-controlled and auditable</li>
            <li>Rollbacks are simple git reverts</li>
            <li>No manual kubectl commands needed for deployments</li>
            <li>Cluster state always matches Git (source of truth)</li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Infrastructure Provisioning" 
          isExpanded={expandedSection === 'provisioning'}
          onToggle={() => handleToggle('provisioning')}
        >
          <p><strong>Bicep Templates:</strong></p>
          <ul>
            <li>
              <strong>Azure Resources:</strong> App Services, PostgreSQL, Cosmos DB, Blob Storage defined in Bicep
            </li>
            <li>
              <strong>Parameterized Deployments:</strong> Support for dev, staging, and production environments
            </li>
            <li>
              <strong>Idempotent:</strong> Can be re-run safely without creating duplicates
            </li>
          </ul>

          <p><strong>PowerShell Automation:</strong></p>
          <ul>
            <li>
              <strong>Environment Bootstrap:</strong> Scripts to provision complete environments from scratch
            </li>
            <li>
              <strong>Test Data Reset:</strong> Automated scripts to reset dev/test databases to known state
            </li>
            <li>
              <strong>Backup/Restore:</strong> Scheduled database backups with retention policies
            </li>
            <li>
              <strong>Deployment Helpers:</strong> Scripts for common tasks like updating secrets, scaling services
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Monitoring and Alerting" 
          isExpanded={expandedSection === 'monitoring'}
          onToggle={() => handleToggle('monitoring')}
        >
          <ul>
            <li>
              <strong>Pipeline Notifications:</strong> Azure DevOps alerts on build failures via email and Slack
            </li>
            <li>
              <strong>Deployment Tracking:</strong> Grafana annotations mark deployments on metric timelines
            </li>
            <li>
              <strong>Rollback Automation:</strong> Failed health checks trigger automatic rollback to previous version
            </li>
            <li>
              <strong>Performance Regression Detection:</strong> Compare metrics before/after deployment to catch issues
            </li>
          </ul>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default DevOpsSection;
