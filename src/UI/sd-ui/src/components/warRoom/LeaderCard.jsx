import React, { useEffect, useState } from 'react';
import apiWrapper from '../../api/apiWrapper';
import TeamLogo from '../common/TeamLogo';
import { athleteLink, teamLink } from '../../utils/sportLinks';
import './LeaderCard.css';

/**
 * Top-N season leaders for one athlete stat. The card owns its total /
 * per-game toggle; the season comes from the panel so every card stays on
 * the same year. Ranking rules (newest stat doc, 75%-of-games floor for
 * per-game, FBS scope) live in the Producer, not here.
 */
function LeaderCard({ title, category, stat, seasonYear, limit = 10 }) {
  const [perGame, setPerGame] = useState(false);
  const [leaders, setLeaders] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    // A season or toggle change can land while an earlier request is in
    // flight; only the latest response may write state.
    let current = true;

    const fetchLeaders = async () => {
      try {
        setLoading(true);
        setError(null);
        const response = await apiWrapper.Analytics.getAthleteSeasonLeaders(seasonYear, {
          category,
          stat,
          perGame,
          limit,
        });
        if (current) setLeaders(response.data?.leaders || []);
      } catch (err) {
        if (current) {
          setLeaders([]);
          setError('Failed to load leaders');
        }
        console.error(`Error loading ${category}.${stat} leaders:`, err);
      } finally {
        if (current) setLoading(false);
      }
    };

    fetchLeaders();
    return () => {
      current = false;
    };
  }, [seasonYear, category, stat, perGame, limit]);

  return (
    <section className="leader-card" aria-label={`${title} leaders`}>
      <header className="leader-card-header">
        <h3>{title}</h3>
        <div className="leader-card-toggle" role="group" aria-label={`${title} scope`}>
          <button
            type="button"
            className={perGame ? '' : 'active'}
            aria-pressed={!perGame}
            onClick={() => setPerGame(false)}
          >
            Total
          </button>
          <button
            type="button"
            className={perGame ? 'active' : ''}
            aria-pressed={perGame}
            onClick={() => setPerGame(true)}
          >
            Per game
          </button>
        </div>
      </header>

      {loading && <div className="leader-card-state">Loading...</div>}
      {!loading && error && <div className="leader-card-state">{error}</div>}
      {!loading && !error && leaders.length === 0 && (
        <div className="leader-card-state">No leaders for {seasonYear}.</div>
      )}
      {!loading && !error && leaders.length > 0 && (
        <ol className="leader-card-list">
          {leaders.map((leader) => (
            <li key={leader.athleteId} className="leader-row">
              <span className="leader-rank">{leader.rank}</span>
              <a href={teamLink(leader.franchiseSlug, seasonYear)} className="leader-team-mark">
                <TeamLogo
                  src={leader.franchiseLogoUrl}
                  abbr={leader.franchiseAbbreviation}
                  color={leader.franchiseColorHex}
                  alt={leader.franchiseShortName}
                  className="leader-logo"
                />
              </a>
              <span className="leader-identity">
                <a href={athleteLink(leader.athleteId)} className="leader-name">
                  {leader.athleteName}
                </a>
                <span className="leader-meta">
                  {[leader.position, leader.franchiseShortName].filter(Boolean).join(' · ')}
                </span>
              </span>
              <span className="leader-value">{leader.displayValue}</span>
            </li>
          ))}
        </ol>
      )}
      {perGame && !loading && !error && (
        <p className="leader-card-footnote">Minimum 75% of team games played.</p>
      )}
    </section>
  );
}

export default LeaderCard;
