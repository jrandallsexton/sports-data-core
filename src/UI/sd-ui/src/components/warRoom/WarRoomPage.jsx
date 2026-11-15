import React, { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import FranchiseMetricsGrid from './FranchiseMetricsGrid';
import LeadersPanel from './LeadersPanel';
import SplitsPanel from './SplitsPanel';
import './WarRoomPage.css';

export const WAR_ROOM_TABS = [
  { key: 'splits', label: 'Splits', Panel: SplitsPanel },
  { key: 'leaders', label: 'Season Leaders', Panel: LeadersPanel },
  { key: 'metrics', label: 'Franchise Season Metrics', Panel: FranchiseMetricsGrid },
];

const DEFAULT_TAB = WAR_ROOM_TABS[0].key;
const isTab = (key) => WAR_ROOM_TABS.some((t) => t.key === key);

/**
 * The active tab lives in the URL (?tab=leaders) so a view can be linked
 * and survives a reload. A panel mounts on first visit and stays mounted
 * (hidden) afterwards, so switching back keeps its picks and avoids
 * re-fetching; panels never visited never load.
 */
function WarRoomPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const requested = searchParams.get('tab');
  const activeTab = isTab(requested) ? requested : DEFAULT_TAB;
  const [visited, setVisited] = useState(() => new Set([activeTab]));

  if (!visited.has(activeTab)) {
    setVisited((prev) => new Set(prev).add(activeTab));
  }

  const selectTab = (key) => {
    const next = new URLSearchParams(searchParams);
    next.set('tab', key);
    setSearchParams(next, { replace: true });
  };

  const onTabKeyDown = (e, index) => {
    const step = { ArrowRight: 1, ArrowLeft: -1 }[e.key];
    if (!step) return;
    e.preventDefault();
    const nextIndex = (index + step + WAR_ROOM_TABS.length) % WAR_ROOM_TABS.length;
    selectTab(WAR_ROOM_TABS[nextIndex].key);
    document.getElementById(`war-room-tab-${WAR_ROOM_TABS[nextIndex].key}`)?.focus();
  };

  return (
    <div className="war-room-page">
      <div className="war-room-header">
        <h1>War Room</h1>
        <p>Command center for advanced analytics and customizable widgets</p>
      </div>

      <div className="war-room-tabs" role="tablist" aria-label="War Room views">
        {WAR_ROOM_TABS.map((tab, index) => {
          const selected = tab.key === activeTab;
          return (
            <button
              key={tab.key}
              id={`war-room-tab-${tab.key}`}
              type="button"
              role="tab"
              aria-selected={selected}
              aria-controls={`war-room-panel-${tab.key}`}
              tabIndex={selected ? 0 : -1}
              className={`war-room-tab${selected ? ' active' : ''}`}
              onClick={() => selectTab(tab.key)}
              onKeyDown={(e) => onTabKeyDown(e, index)}
            >
              {tab.label}
            </button>
          );
        })}
      </div>

      {WAR_ROOM_TABS.map(({ key, Panel }) => (
        <div
          key={key}
          id={`war-room-panel-${key}`}
          role="tabpanel"
          aria-labelledby={`war-room-tab-${key}`}
          className="war-room-content"
          hidden={key !== activeTab}
        >
          {visited.has(key) && <Panel />}
        </div>
      ))}
    </div>
  );
}

export default WarRoomPage;
