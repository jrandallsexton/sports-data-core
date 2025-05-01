import React, { useEffect, useState } from 'react';
import './BadgesPanel.css';

export default function BadgesPanel() {
  const [badges, setBadges] = useState(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    fetch('/data/badges.json')
      .then(response => {
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }
        return response.json();
      })
      .then(data => setBadges(data))
      .catch(err => {
        console.error('Failed to load badges.json:', err);
        setError(true);
        setBadges([]);
      });
  }, []);

  if (badges === null) {
    return <div className="badges-spinner">Loading badges…</div>;
  }

  return (
    <div className="badges-section">
      <h2 className="badges-heading">🏅 Your Badges</h2>
      {error && (
        <p className="badges-error">Something went wrong while loading your badges. Please try again later.</p>
      )}
      {badges.length === 0 ? (
        <p className="badges-empty">No badges earned yet. Keep playing each week to unlock your first one!</p>
      ) : (
        <div className="badges-grid">
          {badges.map(badge => (
            <div
              className={`badge-card ${!badge.earnedDate ? 'badge-locked' : ''}`}
              key={badge.id}
              title={badge.description}
            >
              <div className="badge-icon">{badge.icon}</div>
              <div className="badge-name">{badge.name}</div>
              <div className="badge-description">{badge.description}</div>
              <div className={`badge-rarity ${badge.rarity.toLowerCase()}`}>
                {badge.rarity}
              </div>
              <div className="badge-date">
                {badge.earnedDate
                  ? `Earned ${new Date(badge.earnedDate).toLocaleDateString()}`
                  : 'Not yet earned'}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
