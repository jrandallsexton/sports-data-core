import React, { useState } from "react";
import CollapsibleSection from "../common/CollapsibleSection";

const DataQualitySection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState(null);

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">Data Quality</h2>
        <p className="section-subtitle">
          External data is messy; the platform assumes it
        </p>
      </div>

      <div className="section-content">
        <CollapsibleSection
          title="Designed for Messy Sources"
          isExpanded={expandedSection === "messy"}
          onToggle={() => handleToggle("messy")}
        >
          <p>
            Sports data arrives late, revised, occasionally contradictory,
            and sometimes wrong: home and away teams get swapped after the
            fact, records appear before the games behind them, play feeds
            publish inconsistently during live action. The pipeline treats
            these as normal inputs rather than exceptions:
          </p>
          <ul>
            <li>
              <strong>Idempotent reprocessing:</strong> any document can be
              replayed at any time; upserts converge on the same canonical
              state
            </li>
            <li>
              <strong>Dependency buffering:</strong> documents that reference
              not-yet-sourced entities wait in dead-letter queues and replay
              cleanly once dependencies land
            </li>
            <li>
              <strong>Self-healing jobs:</strong> weekly enrichment
              recomputes team records, refreshes statistics from source, and
              regenerates season metrics for every team - a bad week heals
              itself on the next pass
            </li>
            <li>
              <strong>Immutability classification:</strong> settled documents
              (finalized plays, past probabilities) are recognized and
              skipped at the index level, keeping live-game queues focused on
              data that can still change
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection
          title="Honesty Rules"
          isExpanded={expandedSection === "honesty"}
          onToggle={() => handleToggle("honesty")}
        >
          <p>
            A recurring design rule across the platform: <strong>absence
            must read as absence, never as zero.</strong> A team with no
            red-zone possessions shows a dash, not 0%; a missing record
            renders nothing, not 0-0; a data window is always labeled with
            its actual floor (&ldquo;since 2022&rdquo;) and that floor is
            enforced in the query, not just asserted in the sentence. Every
            statistic a user sees traces to a database row.
          </p>
        </CollapsibleSection>

        <CollapsibleSection
          title="Verification Culture"
          isExpanded={expandedSection === "verification"}
          onToggle={() => handleToggle("verification")}
        >
          <ul>
            <li>
              <strong>Preseason quarantine:</strong> preseason games are
              excluded from records, metrics, and model signal at the
              consumption layer - system-testing data never pollutes the
              regular season
            </li>
            <li>
              <strong>Consistency audits:</strong> ad-hoc and scheduled
              checks compare derived data (winners, records, scoring)
              against canonical results, and remediation is scripted and
              kept
            </li>
            <li>
              <strong>Local end-to-end before merge:</strong> pipeline
              changes are validated against a local copy of the full stack -
              Docker Compose spins up any sport&rsquo;s slice of the
              platform, including its jobs dashboard
            </li>
          </ul>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default DataQualitySection;
