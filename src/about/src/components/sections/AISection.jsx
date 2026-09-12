import React, { useState } from "react";
import CollapsibleSection from "../common/CollapsibleSection";
import MermaidDiagram from "../common/MermaidDiagram";

const AISection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState(null);

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  const previewDiagram = `graph TD
    A[Play-by-Play Data] --> B[Season Metrics Engine]
    S[Team Statistics] --> P[Structured Payload]
    B --> P
    H[Spread-Conditioned History] --> P
    P --> G[OpenRouter Gateway]
    G --> M1[Model A]
    G --> M2[Model B]
    G --> M3[Model N]
    M1 --> C[Capture + Grade]
    M2 --> C
    M3 --> C
    C --> V[(PostgreSQL)]
    C --> U[Preview Shown to Users]

    style A fill:#059669
    style S fill:#059669
    style H fill:#059669
    style B fill:#0891b2
    style P fill:#0d9488
    style G fill:#7c3aed
    style M1 fill:#8b5cf6
    style M2 fill:#8b5cf6
    style M3 fill:#8b5cf6
    style C fill:#1e40af
    style V fill:#059669
    style U fill:#6366f1`;

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">AI &amp; Predictive Insights</h2>
        <p className="section-subtitle">
          Grounded generation, multi-model evaluation, graded results
        </p>
      </div>

      <div className="section-content">
        <CollapsibleSection
          title="Philosophy: Numbers First, Narrative Second"
          isExpanded={expandedSection === "philosophy"}
          onToggle={() => handleToggle("philosophy")}
        >
          <p>
            The models never invent facts. Every matchup preview is generated
            from a structured payload the platform computes itself: season
            efficiency metrics derived from play-by-play data, team
            statistics sourced weekly, and spread-conditioned history
            (&ldquo;how has this team actually done as a 10&ndash;14 point
            favorite?&rdquo;). The LLM&rsquo;s job is analysis and prose -
            the numbers arrive pre-computed, and every number shown to a user
            traces back to a query, never to model output.
          </p>
          <MermaidDiagram chart={previewDiagram} />
        </CollapsibleSection>

        <CollapsibleSection
          title="The Metrics Engine"
          isExpanded={expandedSection === "metrics"}
          onToggle={() => handleToggle("metrics")}
        >
          <p>
            A per-team, per-season metrics pipeline computes advanced
            efficiency measures from canonical play-by-play: yards per play,
            success rate, explosive-play rate, points per drive, third/fourth
            down conversion, red-zone TD and score rates, time-of-possession
            ratio, field-position differential, turnover margin per drive,
            and defensive mirrors of each. Metrics regenerate on a weekly
            schedule for every team in every supported sport, so the model
            payload - and the comparison UI - always reflects the season as
            it stands.
          </p>
        </CollapsibleSection>

        <CollapsibleSection
          title="Model Lab"
          isExpanded={expandedSection === "modellab"}
          onToggle={() => handleToggle("modellab")}
        >
          <p>
            Production previews run on a hosted model behind a thin client
            abstraction; candidate models are evaluated through a single
            OpenRouter gateway integration, which turns &ldquo;which LLM
            should write previews?&rdquo; into a measurable question. An
            admin Model Lab runs the same matchup and the same prompt across
            a matrix of candidate models, captures every raw response, and
            records each model&rsquo;s pick. Every prediction is graded
            against final results - straight-up and against the spread - so
            model choice is an accuracy leaderboard, not a vibe.
          </p>
          <ul>
            <li>
              <strong>Prompt-scoped comparisons:</strong> a model swap and a
              prompt change are never conflated
            </li>
            <li>
              <strong>Response capture:</strong> the full generation for any
              cell of the matrix is one click away
            </li>
            <li>
              <strong>Graded history:</strong> accuracy tracked over weeks,
              with pushes handled honestly (a push grades nobody)
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection
          title="What's Next"
          isExpanded={expandedSection === "next"}
          onToggle={() => handleToggle("next")}
        >
          <p>
            The direction is <em>&ldquo;model predicts, LLM
            explains&rdquo;</em>: a dedicated statistical service owns the
            win-probability and spread predictions (with calibration as the
            headline goal), and the language models explain a prediction they
            are handed rather than making one. Week-versioned metric
            snapshots will let any historical prediction be reproduced
            exactly as the model saw it.
          </p>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default AISection;
