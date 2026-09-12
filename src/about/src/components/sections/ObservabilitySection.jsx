import React, { useState } from "react";
import CollapsibleSection from "../common/CollapsibleSection";

const ObservabilitySection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState(null);

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">Observability</h2>
        <p className="section-subtitle">
          One operator, full visibility
        </p>
      </div>

      <div className="section-content">
        <CollapsibleSection
          title="Instrumentation"
          isExpanded={expandedSection === "instrumentation"}
          onToggle={() => handleToggle("instrumentation")}
        >
          <p>
            Every service is instrumented with OpenTelemetry - traces,
            metrics, and logs - wired once in the shared Core library so a
            new service is observable by construction. Serilog provides
            structured application logging with OTLP and Seq sinks.
          </p>
        </CollapsibleSection>

        <CollapsibleSection
          title="The Stack"
          isExpanded={expandedSection === "stack"}
          onToggle={() => handleToggle("stack")}
        >
          <div className="tech-stack-grid">
            <div className="tech-card">
              <h4>Seq - application logs</h4>
              <p>
                Structured, queryable logs across all services. The first
                stop for &ldquo;what did the pipeline do with this
                game?&rdquo; - filterable by contest, document, correlation
                ID.
              </p>
            </div>
            <div className="tech-card">
              <h4>Prometheus + Grafana - metrics</h4>
              <p>
                Cluster health, queue depths, pipeline throughput, and
                job-level dashboards. Batch jobs report through Pushgateway;
                Alertmanager and blackbox probes cover alerting and uptime
                checks from outside the request path.
              </p>
            </div>
            <div className="tech-card">
              <h4>Tempo - traces</h4>
              <p>
                Distributed traces for following a request or a document
                across service boundaries and finding the slow hop.
              </p>
            </div>
            <div className="tech-card">
              <h4>Umami - product analytics</h4>
              <p>
                Privacy-friendly, self-hosted usage analytics: which
                features fans actually use on game day, with no third-party
                tracking.
              </p>
            </div>
          </div>
        </CollapsibleSection>

        <CollapsibleSection
          title="Operational Discipline"
          isExpanded={expandedSection === "discipline"}
          onToggle={() => handleToggle("discipline")}
        >
          <ul>
            <li>
              <strong>Grafana for metrics, Seq for logs:</strong> each
              question has a designated first tool; dashboards live in the
              GitOps config repo alongside the workloads they watch
            </li>
            <li>
              <strong>Game-day as stress test:</strong> a full college
              football Saturday - dozens of concurrent live games - is the
              recurring load event the pipeline is tuned against, with
              queue-depth autoscaling absorbing the burst
            </li>
            <li>
              <strong>Jobs dashboard:</strong> a single pane over every
              service&rsquo;s Hangfire scheduler for on-demand triggers and
              backfill supervision
            </li>
          </ul>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default ObservabilitySection;
