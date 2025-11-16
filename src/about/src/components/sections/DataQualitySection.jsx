import React, { useState } from 'react';
import CollapsibleSection from '../common/CollapsibleSection';

const DataQualitySection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState(null);

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">Data Quality and Ingestion</h2>
        <p className="section-subtitle">Sources, Canonical Model, and Quality Assurance</p>
      </div>
      
      <div className="section-content">
        <CollapsibleSection 
          title="Data Sources" 
          isExpanded={expandedSection === 'sources'}
          onToggle={() => handleToggle('sources')}
        >
          <p><strong>ESPN JSON APIs:</strong></p>
          <ul>
            <li>
              <strong>Recursive Ingestion:</strong> Provider service fetches ESPN JSON with automatic 
              reference resolution. Follows $ref links to build complete data graphs
            </li>
            <li>
              <strong>Data Types:</strong>
              <ul>
                <li>Teams and rosters</li>
                <li>Game schedules and scores</li>
                <li>Play-by-play data</li>
                <li>Box score statistics</li>
                <li>Player statistics and profiles</li>
                <li>Betting lines (spreads, over/under)</li>
              </ul>
            </li>
            <li>
              <strong>Update Frequency:</strong> Games updated every 5 minutes during live play, 
              schedules refreshed daily
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Canonical Data Model" 
          isExpanded={expandedSection === 'model'}
          onToggle={() => handleToggle('model')}
        >
          <p>
            <strong>Entity Framework Core Schema:</strong> Every statistic and schedule detail is mapped 
            to rich, strongly-typed entities in PostgreSQL.
          </p>

          <p><strong>Core Entities:</strong></p>
          <ul>
            <li>
              <strong>Teams:</strong> FranchiseSeason entities with conference, division, and season context
            </li>
            <li>
              <strong>Games:</strong> Contest entities with comprehensive metadata (venue, officials, weather)
            </li>
            <li>
              <strong>Stats:</strong> Normalized statistics across multiple dimensions (team, player, drive, play)
            </li>
            <li>
              <strong>Picks:</strong> User selections with timestamps, confidence levels, and league context
            </li>
            <li>
              <strong>Predictions:</strong> AI-generated forecasts with model versions and confidence scores
            </li>
            <li>
              <strong>Leagues:</strong> User-created competitions with standings, rules, and membership
            </li>
          </ul>

          <p><strong>Data Relationships:</strong></p>
          <ul>
            <li>Proper foreign key constraints ensure referential integrity</li>
            <li>Cascade delete policies prevent orphaned records</li>
            <li>Optimized indexes on commonly queried relationships</li>
            <li>Materialized views for expensive aggregations</li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Quality Assurance" 
          isExpanded={expandedSection === 'quality'}
          onToggle={() => handleToggle('quality')}
        >
          <p><strong>Hash-Based Deduplication:</strong></p>
          <ul>
            <li>
              Every ingested record generates a hash of key fields to detect duplicates
            </li>
            <li>
              Prevents redundant processing and database bloat
            </li>
            <li>
              Enables idempotent ingestion for replay scenarios
            </li>
          </ul>

          <p><strong>Strict DTO Mapping:</strong></p>
          <ul>
            <li>
              All ESPN JSON mapped through strongly-typed DTOs with validation
            </li>
            <li>
              Schema mismatches caught early and logged for investigation
            </li>
            <li>
              Default values prevent null propagation errors
            </li>
          </ul>

          <p><strong>Anomaly Detection:</strong></p>
          <ul>
            <li>
              <strong>SQL Scripts:</strong> Regularly executed queries to identify data anomalies:
              <ul>
                <li>Missing play-by-play data for completed games</li>
                <li>Incorrect total yardage calculations</li>
                <li>Games with no betting lines when expected</li>
                <li>Stats that exceed reasonable thresholds</li>
              </ul>
            </li>
            <li>
              Results tracked in Grafana dashboards with alerting
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Debug and Admin Tools" 
          isExpanded={expandedSection === 'debug'}
          onToggle={() => handleToggle('debug')}
        >
          <p><strong>Admin Dashboards:</strong></p>
          <ul>
            <li>
              <strong>Missing Plays Report:</strong> Identifies games missing expected play-by-play records
            </li>
            <li>
              <strong>Ingestion Error Log:</strong> Surfaces JSON parsing failures, validation errors, 
              and API timeouts
            </li>
            <li>
              <strong>Data Freshness Monitor:</strong> Shows last update timestamp for each data type
            </li>
            <li>
              <strong>Stats Reconciliation:</strong> Compares box score totals against summed play-by-play stats
            </li>
          </ul>

          <p><strong>Manual Override Capabilities:</strong></p>
          <ul>
            <li>Admin UI for correcting ingestion errors</li>
            <li>Ability to re-trigger specific game ingestion</li>
            <li>Manual stat adjustments with audit trail</li>
          </ul>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default DataQualitySection;
