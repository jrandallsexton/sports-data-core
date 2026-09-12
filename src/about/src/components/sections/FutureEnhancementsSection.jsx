import React, { useState } from "react";
import CollapsibleSection from "../common/CollapsibleSection";

const FutureEnhancementsSection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState("roadmap");

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">Roadmap</h2>
        <p className="section-subtitle">Where the platform goes next</p>
      </div>

      <div className="section-content">
        <CollapsibleSection
          title="In Flight"
          isExpanded={expandedSection === "roadmap"}
          onToggle={() => handleToggle("roadmap")}
        >
          <div className="future-services">
            <ul>
              <li>
                <strong>Player Pick&rsquo;em</strong> - zero-draft fantasy:
                pick individual player performances weekly with no draft and
                no roster management; scoring pipeline is live, athlete-data
                audit gates general availability
              </li>
              <li>
                <strong>Statistical prediction service</strong> - a dedicated
                Python service owning win-probability and spread models, with
                calibration as the success metric; the LLM explains a
                prediction instead of making one
              </li>
              <li>
                <strong>Week-versioned metric snapshots</strong> - reproduce
                any historical prediction with exactly the inputs the model
                saw that week
              </li>
              <li>
                <strong>Deeper league social features</strong> - pick reveal
                drill-ins, richer week-over-week comparisons, and a league
                activity feed
              </li>
            </ul>
          </div>
        </CollapsibleSection>

        <CollapsibleSection
          title="Direction"
          isExpanded={expandedSection === "direction"}
          onToggle={() => handleToggle("direction")}
        >
          <p>
            The product stays free to play. If a paid tier ever exists it
            will gate premium insights - model previews, advanced analytics
            - never contest entry. Sport coverage grows on the same
            partitioned architecture that carried football and MLB: each new
            sport is another set of per-sport pods, another broker, and the
            same canonical pipeline.
          </p>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default FutureEnhancementsSection;
