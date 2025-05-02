import React, { useEffect, useState } from 'react';
import { Badge } from '../../types/badge';
import './BadgesPanel.css';

const BadgesPanel: React.FC = () => {
  const [badges, setBadges] = useState<Badge[] | null>(null);
  const [error, setError] = useState<boolean>(false);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  useEffect(() => {
    let isMounted = true;

    const fetchBadges = async (): Promise<void> => {
      try {
        const response = await fetch('/data/badges.json');
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }
        const data = await response.json() as Badge[];
        if (isMounted) {
          setBadges(data);
          setIsLoading(false);
        }
      } catch (err) {
        console.error('Failed to load badges.json:', err);
        if (isMounted) {
          setError(true);
          setBadges([]);
          setIsLoading(false);
        }
      }
    };

    fetchBadges();

    return () => {
      isMounted = false;
    };
  }, []);

  if (isLoading) {
    return <div className="badges-spinner">Loading badges…</div>;
  }

  return (
    <div className="badges-section">
      <h2 className="badges-heading">🏅 Your Badges</h2>
      {error && (
        <p className="badges-error">Something went wrong while loading your badges. Please try again later.</p>
      )}
      {badges?.length === 0 ? (
        <p className="badges-empty">No badges earned yet. Keep playing each week to unlock your first one!</p>
      ) : (
        <div className="badges-grid">
          {badges?.map(badge => (
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
};

export default BadgesPanel; 