import React, { useState } from 'react';
import CollapsibleSection from '../common/CollapsibleSection';

const OverviewSection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState('purpose');

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">Overview</h2>
        <p className="section-subtitle">Technical Portfolio and Architectural Overview</p>
      </div>
      
      <div className="section-content">
        <CollapsibleSection 
          title="Purpose" 
          isExpanded={expandedSection === 'purpose'}
          onToggle={() => handleToggle('purpose')}
        >
          <p>
            <strong>sportDeets</strong> is a full-stack sports analytics platform focused on NCAA football. 
            It supports pick'em games, predictive analytics, and deep stat visualizations for fans and leagues.
          </p>
          <p>
            The platform provides an engaging way for users to compete in weekly pick'em contests while 
            offering sophisticated AI-powered predictions and comprehensive statistical insights.
          </p>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Tech Stack Summary" 
          isExpanded={expandedSection === 'tech-stack'}
          onToggle={() => handleToggle('tech-stack')}
        >
          <div className="tech-stack-grid">
            <div className="tech-card">
              <h4>Frontend</h4>
              <p><strong>React 19.1.0</strong> - Modern UI with hooks and functional components</p>
              <p><strong>React Router</strong> - Client-side routing</p>
              <p><strong>Firebase</strong> - Authentication and custom claims</p>
            </div>

            <div className="tech-card">
              <h4>Backend</h4>
              <p><strong>C# / .NET</strong> - Core services and APIs</p>
              <p><strong>Entity Framework Core</strong> - ORM for canonical model</p>
              <p><strong>ASP.NET Core</strong> - RESTful API services</p>
            </div>

            <div className="tech-card">
              <h4>Cloud & Infrastructure</h4>
              <p><strong>Azure App Services</strong> - Development environment</p>
              <p><strong>Azure PostgreSQL</strong> - Canonical data model</p>
              <p><strong>Azure Cosmos DB</strong> - Raw JSON storage</p>
              <p><strong>Azure Blob Storage</strong> - Logos and backups</p>
            </div>

            <div className="tech-card">
              <h4>Container Orchestration</h4>
              <p><strong>Kubernetes (k3s)</strong> - Production cluster</p>
              <p><strong>Vagrant</strong> - Local k3s provisioning</p>
              <p><strong>Flux</strong> - GitOps workflow</p>
            </div>

            <div className="tech-card">
              <h4>Observability</h4>
              <p><strong>Prometheus</strong> - Metrics collection</p>
              <p><strong>Grafana</strong> - Dashboards and visualization</p>
              <p><strong>Loki</strong> - Log aggregation</p>
              <p><strong>Tempo</strong> - Distributed tracing</p>
              <p><strong>Seq</strong> - Structured logging</p>
            </div>

            <div className="tech-card">
              <h4>AI & ML</h4>
              <p><strong>Ollama</strong> - Local LLM hosting</p>
              <p><strong>Python</strong> - ML model training</p>
              <p><strong>Scikit-learn</strong> - Logistic Regression, Random Forest</p>
            </div>
          </div>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Key Features" 
          isExpanded={expandedSection === 'features'}
          onToggle={() => handleToggle('features')}
        >
          <ul>
            <li><strong>Weekly Pick'em Contests:</strong> Users compete to predict game outcomes straight-up and against the spread</li>
            <li><strong>AI Predictions:</strong> Machine learning models provide SU and ATS predictions with confidence percentages</li>
            <li><strong>League Management:</strong> Create private leagues, invite friends, track standings and statistics</li>
            <li><strong>Real-time Updates:</strong> Live score updates and leaderboard tracking throughout the season</li>
            <li><strong>Comprehensive Stats:</strong> Deep dive into team schedules, player stats, and historical performance</li>
            <li><strong>Responsive Design:</strong> Optimized for desktop, tablet, and mobile devices</li>
          </ul>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default OverviewSection;
