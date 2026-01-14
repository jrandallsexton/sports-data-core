import React, { useState } from "react";
import CollapsibleSection from "../common/CollapsibleSection";

const AISection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState(null);

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">AI and Predictive Insights</h2>
        <p className="section-subtitle">
          Machine Learning Models and LLM Integration
        </p>
      </div>

      <div className="section-content">
        <CollapsibleSection
          title="LLM Integration"
          isExpanded={expandedSection === "llm"}
          onToggle={() => handleToggle("llm")}
        >
          <ul>
            <li>
              <strong>Ollama Local LLM:</strong> sportDeets runs large language
              models locally via
              <code> Ollama </code>, preserving full control over prompt
              engineering, data privacy, and performance - while avoiding
              third-party API costs entirely.
            </li>
            <li>
              <strong>StatBot:</strong> The original LLM persona that generates
              matchup previews based on raw team stats, historical performance,
              and record comparisons
            </li>
            <li>
              <strong>MetricBot:</strong> A newer persona that incorporates
              Python-based regression outputs (e.g. win probabilities, spread
              deltas) to generate more analytical and model-driven previews
            </li>
            <li>
              <strong>Contextual Prompting:</strong> Each preview is generated
              from a carefully constructed prompt that includes recent
              performance, team-specific metrics, betting lines, and more
            </li>
            <li>
              <strong>Scheduled Inference:</strong> Matchup previews are
              generated weekly in batch using background jobs in the Producer
              service and stored for UI display
            </li>
            <li>
              <strong>Auditability:</strong> All generated previews are
              versioned, tied to both the prompt structure and input data
              snapshot used at inference time
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection
          title="Prediction Models"
          isExpanded={expandedSection === "models"}
          onToggle={() => handleToggle("models")}
        >
          <ul>
            <li>
              <strong>Logistic Regression:</strong> Used for straight-up (SU)
              winner predictions. Fast, interpretable, and effective when paired
              with engineered features.
            </li>
            <li>
              <strong>Random Forest:</strong> Deployed for against-the-spread
              (ATS) predictions. Captures non-linear relationships and complex
              feature interactions.
            </li>
            <li>
              <strong>Feature Engineering:</strong> The models are trained using
              a rich set of derived inputs, including:
              <ul>
                <li>Conference strength indicators</li>
                <li>Historical head-to-head performance</li>
                <li>Home field advantage factors</li>
                <li>Recent trends (last 3 games)</li>
                <li>Strength of schedule adjustments</li>
                <li>Team offensive and defensive metrics</li>
              </ul>
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection
          title="Training Strategy"
          isExpanded={expandedSection === "training"}
          onToggle={() => handleToggle("training")}
        >
          <p>
            <strong>Blended Data Approach:</strong> To handle the early-season
            cold-start challenge, the models blend prior season performance with
            current season metrics in a gradually shifting ratio.
          </p>
          <ul>
            <li>
              <strong>Early Season:</strong> 70% prior season, 30% current
              season
            </li>
            <li>
              <strong>Mid Season:</strong> 50/50 blend as data volume increases
            </li>
            <li>
              <strong>Late Season:</strong> 80% current season, 20% prior season
              for sharper real-time accuracy
            </li>
            <li>
              <strong>Continuous Retraining:</strong> Models are retrained
              weekly using the latest available game data
            </li>
          </ul>

          <p>
            <strong>Model Evaluation:</strong>
          </p>
          <ul>
            <li>
              <strong>A/B Testing:</strong> Compare new model versions against
              prior baselines in controlled experiments
            </li>
            <li>
              <strong>Accuracy, Precision, Recall:</strong> Tracked weekly to
              monitor classification performance
            </li>
            <li>
              <strong>Brier Score:</strong> Measures calibration of predicted
              probabilities
            </li>
            <li>
              <strong>Segmented Analysis:</strong> Model performance sliced by
              week, conference, and game type
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection
          title="Prediction Output"
          isExpanded={expandedSection === "output"}
          onToggle={() => handleToggle("output")}
        >
          <p>
            <strong>ContestPrediction DTOs:</strong> Each game receives
            structured predictions that include:
          </p>
          <ul>
            <li>
              <strong>Straight-Up (SU):</strong> Predicted winner with
              confidence percentage (0-100%)
            </li>
            <li>
              <strong>Against-the-Spread (ATS):</strong> Predicted cover with
              confidence percentage
            </li>
            <li>
              <strong>Model Version:</strong> Tracks which model generated the
              prediction for audit purposes
            </li>
            <li>
              <strong>Prediction Timestamp:</strong> When the prediction was
              generated
            </li>
          </ul>

          <p>
            Predictions are surfaced in the UI via the{" "}
            <strong>DeetsMeter™</strong> component, which displays confidence
            levels as horizontal gradient bars for easy interpretation.
          </p>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default AISection;
