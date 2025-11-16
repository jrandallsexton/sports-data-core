import React, { useState } from 'react';
import CollapsibleSection from '../common/CollapsibleSection';

const FutureEnhancementsSection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState(null);

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">Future Enhancements</h2>
        <p className="section-subtitle">Roadmap and Planned Features</p>
      </div>
      
      <div className="section-content">
        <CollapsibleSection 
          title="Mobile Application" 
          isExpanded={expandedSection === 'mobile'}
          onToggle={() => handleToggle('mobile')}
        >
          <p><strong>React Native Development:</strong></p>
          <ul>
            <li>
              <strong>Cross-Platform App:</strong> Single codebase for iOS and Android using React Native
            </li>
            <li>
              <strong>Push Notifications:</strong> Real-time alerts for game scores, pick deadlines, 
              league standings changes, and personalized insights
            </li>
            <li>
              <strong>Offline Support:</strong> Cache picks and league data for viewing without connectivity
            </li>
            <li>
              <strong>Native Features:</strong> Leverage device capabilities like biometric authentication, 
              calendar integration, and share functionality
            </li>
          </ul>

          <p><strong>Monetization Strategy:</strong></p>
          <ul>
            <li>
              <strong>Freemium Model:</strong> Free access to basic pick'em with ads
            </li>
            <li>
              <strong>Premium Tier:</strong> Ad-free experience, advanced stats, early access to AI insights
            </li>
            <li>
              <strong>In-App Purchases:</strong> Custom themes, badges, and league enhancements
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Enrichment Service" 
          isExpanded={expandedSection === 'enrichment'}
          onToggle={() => handleToggle('enrichment')}
        >
          <p>
            <strong>Advanced Game Traits:</strong> New Enricher service will assign contextual attributes 
            to games, enhancing predictions and user experience.
          </p>

          <p><strong>Planned Traits:</strong></p>
          <ul>
            <li>
              <strong>Game Type:</strong> "DayGame", "NightGame", "PrimeTime" based on kickoff time
            </li>
            <li>
              <strong>Rivalry Detector:</strong> Historical matchup analysis to flag rivalry games
            </li>
            <li>
              <strong>Conference Championship:</strong> Identify games with playoff implications
            </li>
            <li>
              <strong>Weather Impact:</strong> Flag games with significant weather conditions (rain, wind, cold)
            </li>
            <li>
              <strong>Venue Significance:</strong> Neutral site, bowl game, historic venue indicators
            </li>
            <li>
              <strong>Streak Tracking:</strong> Winning/losing streaks, bowl eligibility implications
            </li>
          </ul>

          <p><strong>Benefits:</strong></p>
          <ul>
            <li>Improved prediction accuracy by incorporating contextual factors</li>
            <li>Enhanced user insights and game previews</li>
            <li>Better understanding of historical performance patterns</li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="User-Driven Content" 
          isExpanded={expandedSection === 'community'}
          onToggle={() => handleToggle('community')}
        >
          <p><strong>Community Features:</strong></p>
          <ul>
            <li>
              <strong>Meme Gallery:</strong> User-submitted memes for teams, games, and rivalries with voting
            </li>
            <li>
              <strong>YouTube Highlight Tagging:</strong> Crowd-sourced linking of YouTube highlights to specific plays
            </li>
            <li>
              <strong>Game Threads:</strong> Live discussion forums for each matchup
            </li>
            <li>
              <strong>Pick Analysis Sharing:</strong> Users can publish their pick strategies and reasoning
            </li>
            <li>
              <strong>Custom Badges:</strong> Achievement system for prediction accuracy, league participation, etc.
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Advanced Analytics" 
          isExpanded={expandedSection === 'analytics'}
          onToggle={() => handleToggle('analytics')}
        >
          <ul>
            <li>
              <strong>Trend Analysis:</strong> Multi-season performance trends with visualizations
            </li>
            <li>
              <strong>What-If Scenarios:</strong> Simulate league standings with different pick outcomes
            </li>
            <li>
              <strong>Pick Confidence Calibration:</strong> Show users how well-calibrated their confidence levels are
            </li>
            <li>
              <strong>Opponent Analysis:</strong> Head-to-head comparison tools for league members
            </li>
            <li>
              <strong>Bracket Predictions:</strong> Playoff bracket predictions with probabilistic outcomes
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Platform Expansion" 
          isExpanded={expandedSection === 'expansion'}
          onToggle={() => handleToggle('expansion')}
        >
          <ul>
            <li>
              <strong>Additional Sports:</strong> Expand beyond NCAA football to NFL, NBA, MLB with 
              same architecture pattern
            </li>
            <li>
              <strong>Fantasy Integration:</strong> Allow users to import fantasy rosters and track 
              player performance
            </li>
            <li>
              <strong>Social Features:</strong> Friend connections, trash talk, group messaging
            </li>
            <li>
              <strong>Public API:</strong> Developer API for third-party integrations and custom applications
            </li>
            <li>
              <strong>White-Label Solution:</strong> Allow organizations to host their own branded 
              pick'em platforms
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection 
          title="Technical Improvements" 
          isExpanded={expandedSection === 'technical'}
          onToggle={() => handleToggle('technical')}
        >
          <ul>
            <li>
              <strong>GraphQL API:</strong> Migrate from REST to GraphQL for more efficient data fetching
            </li>
            <li>
              <strong>Real-Time Updates:</strong> WebSocket integration for live score updates without polling
            </li>
            <li>
              <strong>Advanced Caching:</strong> Redis layer for frequently accessed data
            </li>
            <li>
              <strong>Multi-Region Deployment:</strong> CDN and edge computing for global users
            </li>
            <li>
              <strong>A/B Testing Framework:</strong> Experimentation platform for feature rollouts
            </li>
          </ul>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default FutureEnhancementsSection;
