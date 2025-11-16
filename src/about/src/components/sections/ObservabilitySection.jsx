import React, { useState } from 'react';
import CollapsibleSection from '../common/CollapsibleSection';

const ObservabilitySection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState(null);

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">Observability</h2>
        <p className="section-subtitle">Logging, Metrics, and Tracing</p>
      </div>
      
      <div className="section-content">
        <CollapsibleSection 
          title="Logging Infrastructure" 
          isExpanded={expandedSection === 'logging'}
          onToggle={() => handleToggle('logging')}
        >
          <p><strong>Structured Logging:</strong></p>
          <ul>
            <li>
              <strong>Seq:</strong> Structured log aggregation for development and detailed debugging. 
              Rich querying with filters on log levels, services, and custom properties
            </li>
            <li>
              <strong>Loki:</strong> Production log aggregation integrated with Grafana. Label-based 
              indexing for efficient log queries across all services
            </li>
            <li>
              <strong>Log Levels:</strong> Consistent use of Debug, Info, Warning, Error, and Critical 
              across all services
            </li>
            <li>
              <strong>Correlation IDs:</strong> Request tracking across service boundaries for distributed debugging
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Metrics Collection" 
          isExpanded={expandedSection === 'metrics'}
          onToggle={() => handleToggle('metrics')}
        >
          <p><strong>Prometheus:</strong></p>
          <ul>
            <li>
              <strong>Service Metrics:</strong> Scrapes /metrics endpoints from all .NET services
            </li>
            <li>
              <strong>Key Metrics:</strong>
              <ul>
                <li>Ingestion rates (games/minute, stats/minute)</li>
                <li>Job success/failure counts</li>
                <li>API response times and error rates</li>
                <li>Database connection pool statistics</li>
                <li>Queue depths and processing times</li>
              </ul>
            </li>
            <li>
              <strong>Custom Metrics:</strong> Business-specific metrics like prediction accuracy, 
              user picks per week, league activity levels
            </li>
          </ul>

          <p><strong>Grafana Dashboards:</strong></p>
          <ul>
            <li>Real-time service health overview</li>
            <li>Ingestion pipeline monitoring with alerts</li>
            <li>API performance and usage patterns</li>
            <li>Database query performance and slow query tracking</li>
            <li>Prediction model accuracy trends over time</li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Distributed Tracing" 
          isExpanded={expandedSection === 'tracing'}
          onToggle={() => handleToggle('tracing')}
        >
          <p><strong>Tempo Integration (In Progress):</strong></p>
          <ul>
            <li>
              <strong>Request-Level Tracing:</strong> Tracks individual requests as they flow through 
              multiple services (UI → API → Producer → Database)
            </li>
            <li>
              <strong>Performance Analysis:</strong> Identifies bottlenecks and slow operations in distributed workflows
            </li>
            <li>
              <strong>OpenTelemetry:</strong> Using industry-standard instrumentation libraries for consistent tracing
            </li>
            <li>
              <strong>Grafana Integration:</strong> Unified view of logs, metrics, and traces in single pane of glass
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Alerting & Monitoring" 
          isExpanded={expandedSection === 'alerting'}
          onToggle={() => handleToggle('alerting')}
        >
          <ul>
            <li>
              <strong>Alert Rules:</strong> Prometheus AlertManager configured for critical conditions
            </li>
            <li>
              <strong>Alert Conditions:</strong>
              <ul>
                <li>Service downtime or unavailability</li>
                <li>Ingestion job failures</li>
                <li>Database connection failures</li>
                <li>API error rates above threshold</li>
                <li>Disk space and memory utilization warnings</li>
              </ul>
            </li>
            <li>
              <strong>Notification Channels:</strong> Email and Slack integration for immediate awareness
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Future Observability" 
          isExpanded={expandedSection === 'future'}
          onToggle={() => handleToggle('future')}
        >
          <p>
            <strong>Public Observability Stack:</strong> Plans to expose a read-only version of the 
            Grafana dashboards publicly, allowing users and potential employers to view:
          </p>
          <ul>
            <li>Real-time system health and performance metrics</li>
            <li>Ingestion pipeline activity and data freshness</li>
            <li>Prediction model accuracy and performance trends</li>
            <li>API usage patterns and response times</li>
          </ul>
          <p>
            This transparency will demonstrate operational excellence and provide insight into the 
            platform's scale and reliability.
          </p>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default ObservabilitySection;
