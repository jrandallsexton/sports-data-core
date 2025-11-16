import React, { useState } from 'react';
import CollapsibleSection from '../common/CollapsibleSection';

const AISection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState(null);

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">AI and Predictive Insights</h2>
        <p className="section-subtitle">Machine Learning Models and LLM Integration</p>
      </div>
      
      <div className="section-content">
        <CollapsibleSection 
          title="LLM Integration" 
          isExpanded={expandedSection === 'llm'}
          onToggle={() => handleToggle('llm')}
        >
          <p>
            <strong>Ollama Local LLM:</strong> The platform uses Ollama to host large language models locally, 
            ensuring data privacy and eliminating API costs for content generation.
          </p>
          <ul>
            <li>
              <strong>MetricBot:</strong> Custom-prompted LLM that generates game previews and analysis based 
              on statistical data, team performance, and historical matchups
            </li>
            <li>
              <strong>Custom Prompts:</strong> Engineered prompts that incorporate team stats, recent performance, 
              injuries, and betting lines to produce contextual insights
            </li>
            <li>
              <strong>Batch Processing:</strong> Generates previews for all weekly matchups in scheduled jobs
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Prediction Models" 
          isExpanded={expandedSection === 'models'}
          onToggle={() => handleToggle('models')}
        >
          <p><strong>Model Types:</strong></p>
          <ul>
            <li>
              <strong>Logistic Regression:</strong> Binary classification for straight-up (SU) game winners. 
              Fast, interpretable, and performs well with feature engineering
            </li>
            <li>
              <strong>Random Forest:</strong> Ensemble method for against-the-spread (ATS) predictions. 
              Handles non-linear relationships and feature interactions effectively
            </li>
          </ul>

          <p><strong>Feature Engineering:</strong></p>
          <ul>
            <li>Team offensive and defensive efficiency metrics</li>
            <li>Strength of schedule adjustments</li>
            <li>Home field advantage factors</li>
            <li>Recent performance trends (last 3 games)</li>
            <li>Conference strength indicators</li>
            <li>Historical head-to-head results</li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Training Strategy" 
          isExpanded={expandedSection === 'training'}
          onToggle={() => handleToggle('training')}
        >
          <p>
            <strong>Blended Data Approach:</strong> To address the cold-start problem at the beginning of each 
            season, the models blend prior season data with current season performance.
          </p>
          <ul>
            <li>
              <strong>Early Season:</strong> 70% weight on prior season stats, 30% on current season
            </li>
            <li>
              <strong>Mid Season:</strong> 50/50 blend as current season data becomes more reliable
            </li>
            <li>
              <strong>Late Season:</strong> 80% current season, 20% prior season for maximum accuracy
            </li>
            <li>
              <strong>Continuous Retraining:</strong> Models retrained weekly with updated game results
            </li>
          </ul>

          <p><strong>Model Evaluation:</strong></p>
          <ul>
            <li>Accuracy, precision, recall tracked weekly</li>
            <li>Brier score for calibration assessment</li>
            <li>Performance segmented by conference, week, and game type</li>
            <li>A/B testing framework for comparing model versions</li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Prediction Output" 
          isExpanded={expandedSection === 'output'}
          onToggle={() => handleToggle('output')}
        >
          <p>
            <strong>ContestPrediction DTOs:</strong> Each game receives structured predictions that include:
          </p>
          <ul>
            <li>
              <strong>Straight-Up (SU):</strong> Predicted winner with confidence percentage (0-100%)
            </li>
            <li>
              <strong>Against-the-Spread (ATS):</strong> Predicted cover with confidence percentage
            </li>
            <li>
              <strong>Model Version:</strong> Tracks which model generated the prediction for audit purposes
            </li>
            <li>
              <strong>Prediction Timestamp:</strong> When the prediction was generated
            </li>
          </ul>

          <p>
            Predictions are surfaced in the UI via the <strong>DeetsMeter™</strong> component, which displays 
            confidence levels as horizontal gradient bars for easy interpretation.
          </p>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default AISection;
