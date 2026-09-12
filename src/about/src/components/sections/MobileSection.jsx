import React, { useState } from "react";
import CollapsibleSection from "../common/CollapsibleSection";

const MobileSection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState(null);

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  return (
    <section id={id} className="about-section">
      <div className="section-header">
        <h2 className="section-title">Mobile</h2>
        <p className="section-subtitle">
          Native apps on both stores, one codebase
        </p>
      </div>

      <div className="section-content">
        <CollapsibleSection
          title="The App"
          isExpanded={expandedSection === "app"}
          onToggle={() => handleToggle("app")}
        >
          <p>
            sportDeets ships as native iOS and Android apps from a single
            Expo / React Native codebase, published on the App Store and
            Google Play. The product philosophy splits the surfaces: the
            web app is the weekday research workbench (desktop-first,
            information-dense), while mobile is the weekend companion -
            &ldquo;am I winning?&rdquo;, live scores, and making picks
            before kickoff.
          </p>
          <ul>
            <li>
              <strong>Picks and leagues</strong> - full pick&rsquo;em flow
              with league standings and week-by-week reveal rules
            </li>
            <li>
              <strong>Matchup comparison</strong> - statistics, advanced
              metrics, and spread-conditioned history at feature parity with
              the web dialog
            </li>
            <li>
              <strong>Live game day</strong> - SignalR-streamed scores and
              win probability
            </li>
            <li>
              <strong>Push notifications</strong> - per-kickoff pick
              reminders (only if you haven&rsquo;t picked) and AP poll
              releases, with per-category preferences
            </li>
          </ul>
        </CollapsibleSection>

        <CollapsibleSection
          title="Stack & Delivery"
          isExpanded={expandedSection === "stack"}
          onToggle={() => handleToggle("stack")}
        >
          <div className="tech-stack-grid">
            <div className="tech-card">
              <h4>Runtime</h4>
              <p>
                Expo SDK 55 on React Native 0.83 with Expo Router. TanStack
                Query owns server state; Zustand owns client state. Firebase
                handles auth (Google and Apple sign-in) and cloud messaging.
              </p>
            </div>
            <div className="tech-card">
              <h4>Quality</h4>
              <p>
                TypeScript throughout, Jest + React Native Testing Library
                for component behavior, Sentry for crash reporting in the
                field.
              </p>
            </div>
            <div className="tech-card">
              <h4>Delivery</h4>
              <p>
                EAS Build produces signed binaries in the cloud with remote
                build-number management; GitHub Actions drives CI. JS-only
                changes ship between store releases as over-the-air updates.
              </p>
            </div>
            <div className="tech-card">
              <h4>Monorepo</h4>
              <p>
                Mobile lives beside the web app and the backend in one
                repository - an API contract change and both clients&rsquo;
                adaptations land in a single atomic pull request.
              </p>
            </div>
          </div>
        </CollapsibleSection>
      </div>
    </section>
  );
};

export default MobileSection;
