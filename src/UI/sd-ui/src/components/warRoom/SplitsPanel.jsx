import React, { useEffect, useMemo, useState } from 'react';
import apiWrapper from '../../api/apiWrapper';
import { contestLink, teamLink } from '../../utils/sportLinks';
import './SplitsPanel.css';

// House convention: a season is labeled by its starting year, and the
// rollover happens in June (month >= 6). Same rule as LeadersPanel.
const getCurrentSeasonYear = () => {
  const now = new Date();
  return now.getMonth() + 1 >= 6 ? now.getFullYear() : now.getFullYear() - 1;
};

// Fact rows are projected from 2001; each filter's own data floor is applied
// by the Producer, which reports the effective start back.
const EARLIEST_SEASON = 2001;
const POSTSEASON_PHASE = 3;

const CURRENT_SEASON = getCurrentSeasonYear();
const seasonYears = [];
for (let y = CURRENT_SEASON; y >= EARLIEST_SEASON; y--) {
  seasonYears.push(y);
}

// Chip groups: value is the Producer's enum name (or a boolean); null = Any.
export const SPLIT_FILTERS = [
  {
    key: 'site',
    label: 'Site',
    options: [
      { label: 'Home', value: 'Home' },
      { label: 'Away', value: 'Away' },
      { label: 'Neutral', value: 'Neutral' },
    ],
  },
  {
    key: 'opponentRank',
    label: 'Opponent',
    options: [
      { label: 'Ranked', value: 'Ranked' },
      { label: 'Top 10', value: 'Top10' },
      { label: 'Unranked', value: 'Unranked' },
    ],
  },
  {
    key: 'line',
    label: 'Line',
    options: [
      { label: 'Favorite', value: 'Favorite' },
      { label: 'Underdog', value: 'Underdog' },
    ],
  },
  {
    key: 'kickoffWindow',
    label: 'Kickoff',
    options: [
      { label: 'Noon', value: 'Noon' },
      { label: 'Afternoon', value: 'Afternoon' },
      { label: 'Night', value: 'Night' },
    ],
  },
  {
    key: 'conferenceGame',
    label: 'Conference',
    options: [
      { label: 'Conference', value: true },
      { label: 'Non-conf', value: false },
    ],
  },
];

const EMPTY_FILTERS = Object.fromEntries(SPLIT_FILTERS.map((f) => [f.key, null]));

const isRanked = (rank) => rank != null && rank >= 1 && rank <= 25;

export const formatLine = (spread) => {
  if (spread == null) return '—';
  if (Number(spread) === 0) return 'PK';
  return spread > 0 ? `+${spread}` : `${spread}`;
};

const ATS_LABELS = { Covered: 'Cover', NotCovered: 'Miss', Push: 'Push' };
const RESULT_LETTERS = { Win: 'W', Loss: 'L', Tie: 'T' };
const SITE_MARKERS = { Home: 'vs', Away: '@', Neutral: 'vs' };

const formatPct = (wins, losses, ties) => {
  const games = wins + losses + ties;
  if (games === 0) return null;
  return ((wins + ties / 2) / games).toFixed(3).replace(/^0/, '');
};

const formatAvg = (value) => (value == null ? '—' : Number(value).toFixed(1));

const formatMargin = (value) => {
  if (value == null) return '—';
  const n = Number(value);
  return `${n > 0 ? '+' : ''}${n.toFixed(1)}`;
};

const record = (w, l, t = 0) => (t > 0 ? `${w}-${l}-${t}` : `${w}-${l}`);

// The score shows only when both sides are known; a result without a full
// score keeps its letter rather than rendering "W null-null".
export const formatResult = (game) => {
  if (!game.result) return '—';
  const letter = RESULT_LETTERS[game.result] || game.result;
  if (game.pointsFor == null || game.pointsAgainst == null) return letter;
  return `${letter} ${game.pointsFor}-${game.pointsAgainst}`;
};

const weekLabel = (game) => {
  if (game.seasonPhaseTypeCode === POSTSEASON_PHASE) return 'Post';
  return game.week != null ? `Wk ${game.week}` : '—';
};

/**
 * War Room Splits: one team's record over a season range, narrowed by
 * filter chips, with the matching games listed so every number can be
 * checked. NCAAFB only for now. Filter rules (rank buckets, pick'em,
 * per-filter season floors) live in the Producer, not here.
 */
function SplitsPanel() {
  const [teams, setTeams] = useState([]);
  const [slug, setSlug] = useState('');
  const [seasonFrom, setSeasonFrom] = useState(EARLIEST_SEASON);
  const [seasonTo, setSeasonTo] = useState(CURRENT_SEASON);
  const [filters, setFilters] = useState(EMPTY_FILTERS);
  const [splits, setSplits] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  // Team picker: FBS teams from the current season's franchise list.
  useEffect(() => {
    let current = true;
    apiWrapper.Analytics.getFranchiseSeasonMetrics(CURRENT_SEASON)
      .then((response) => {
        if (!current) return;
        const fbs = (response.data || [])
          .filter((t) => t.conferenceParent === 'FBS')
          .map((t) => ({ slug: t.franchiseSlug, name: t.franchiseName }))
          .sort((a, b) => a.name.localeCompare(b.name));
        setTeams(fbs);
      })
      .catch((err) => console.error('Error loading teams for splits:', err));
    return () => {
      current = false;
    };
  }, []);

  useEffect(() => {
    if (!slug) {
      setSplits(null);
      return undefined;
    }

    // A filter change can land while an earlier request is in flight; only
    // the latest response may write state.
    let current = true;
    const params = { seasonFrom, seasonTo };
    Object.entries(filters).forEach(([key, value]) => {
      if (value != null) params[key] = value;
    });

    setLoading(true);
    setError(null);
    apiWrapper.Analytics.getFranchiseSplits(slug, params)
      .then((response) => {
        if (current) setSplits(response.data || null);
      })
      .catch((err) => {
        if (current) {
          setSplits(null);
          setError('Failed to load splits');
        }
        console.error(`Error loading splits for ${slug}:`, err);
      })
      .finally(() => {
        if (current) setLoading(false);
      });

    return () => {
      current = false;
    };
  }, [slug, seasonFrom, seasonTo, filters]);

  const toggleFilter = (key, value) =>
    setFilters((prev) => ({ ...prev, [key]: prev[key] === value ? null : value }));

  const onSeasonFrom = (value) => {
    setSeasonFrom(value);
    if (value > seasonTo) setSeasonTo(value);
  };

  const onSeasonTo = (value) => {
    setSeasonTo(value);
    if (value < seasonFrom) setSeasonFrom(value);
  };

  const summary = splits?.summary;
  const games = splits?.games || [];
  const rangeRaised = splits && splits.seasonFrom > splits.requestedSeasonFrom;
  const rangeEmpty = splits && splits.seasonFrom > splits.seasonTo;
  const pct = summary ? formatPct(summary.wins, summary.losses, summary.ties) : null;
  const hasFilters = useMemo(() => Object.values(filters).some((v) => v != null), [filters]);

  return (
    <section className="splits-panel" aria-label="Team splits">
      <div className="splits-panel-header">
        <div className="splits-controls">
          <select
            value={slug}
            onChange={(e) => setSlug(e.target.value)}
            className="splits-select splits-team-select"
            aria-label="Team"
          >
            <option value="">Pick a team…</option>
            {teams.map((t) => (
              <option key={t.slug} value={t.slug}>
                {t.name}
              </option>
            ))}
          </select>
          <select
            value={seasonFrom}
            onChange={(e) => onSeasonFrom(Number(e.target.value))}
            className="splits-select"
            aria-label="From season"
          >
            {seasonYears.map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
          <span className="splits-range-dash">to</span>
          <select
            value={seasonTo}
            onChange={(e) => onSeasonTo(Number(e.target.value))}
            className="splits-select"
            aria-label="To season"
          >
            {seasonYears.map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="splits-chips">
        {SPLIT_FILTERS.map((group) => (
          <div key={group.key} className="splits-chip-group" role="group" aria-label={group.label}>
            <span className="splits-chip-label">{group.label}</span>
            {group.options.map((opt) => {
              const active = filters[group.key] === opt.value;
              return (
                <button
                  key={String(opt.value)}
                  type="button"
                  className={`splits-chip${active ? ' active' : ''}`}
                  aria-pressed={active}
                  onClick={() => toggleFilter(group.key, opt.value)}
                >
                  {opt.label}
                </button>
              );
            })}
          </div>
        ))}
        {hasFilters && (
          <button type="button" className="splits-clear" onClick={() => setFilters(EMPTY_FILTERS)}>
            Clear filters
          </button>
        )}
      </div>

      {!slug && <div className="splits-state">Pick a team to see its splits.</div>}
      {slug && loading && <div className="splits-state">Loading...</div>}
      {slug && !loading && error && <div className="splits-state">{error}</div>}

      {slug && !loading && !error && splits && (
        <>
          {rangeRaised && (
            <p className="splits-range-note">
              {rangeEmpty
                ? `These filters need data from ${splits.seasonFrom} on; widen the range to see games.`
                : `These filters have data from ${splits.seasonFrom}; showing ${splits.seasonFrom}–${splits.seasonTo}.`}
            </p>
          )}

          <div className="splits-summary" aria-label="Splits summary">
            <div className="splits-stat">
              <span className="splits-stat-value">
                {record(summary.wins, summary.losses, summary.ties)}
              </span>
              <span className="splits-stat-label">{pct ? `Record (${pct})` : 'Record'}</span>
            </div>
            <div className="splits-stat">
              <span className="splits-stat-value">
                {record(summary.atsWins, summary.atsLosses, summary.atsPushes)}
              </span>
              <span className="splits-stat-label">ATS</span>
            </div>
            <div className="splits-stat">
              <span className="splits-stat-value">{formatMargin(summary.avgMargin)}</span>
              <span className="splits-stat-label">Avg margin</span>
            </div>
            <div className="splits-stat">
              <span className="splits-stat-value">
                {formatAvg(summary.avgPointsFor)} / {formatAvg(summary.avgPointsAgainst)}
              </span>
              <span className="splits-stat-label">PF / PA</span>
            </div>
            <div className="splits-stat">
              <span className="splits-stat-value">
                {record(summary.favoriteWins, summary.favoriteLosses)}
              </span>
              <span className="splits-stat-label">As favorite</span>
            </div>
            <div className="splits-stat">
              <span className="splits-stat-value">
                {record(summary.underdogWins, summary.underdogLosses)}
              </span>
              <span className="splits-stat-label">As underdog</span>
            </div>
          </div>

          {games.length === 0 ? (
            <div className="splits-state">No games match.</div>
          ) : (
            <table className="splits-games">
              <thead>
                <tr>
                  <th>Season</th>
                  <th>Week</th>
                  <th>Opponent</th>
                  <th>Result</th>
                  <th className="num">Line</th>
                  <th>ATS</th>
                </tr>
              </thead>
              <tbody>
                {games.map((g) => (
                  <tr key={g.contestId}>
                    <td>{g.seasonYear}</td>
                    <td>{weekLabel(g)}</td>
                    <td>
                      <span className="splits-site">{SITE_MARKERS[g.site] || ''}</span>{' '}
                      {isRanked(g.opponentRankGoingIn) && (
                        <span className="splits-rank">#{g.opponentRankGoingIn} </span>
                      )}
                      <a href={teamLink(g.opponentSlug, g.seasonYear)}>{g.opponentName}</a>
                      {g.site === 'Neutral' && <span className="splits-neutral"> (N)</span>}
                    </td>
                    <td className={`splits-result ${g.result ? g.result.toLowerCase() : ''}`}>
                      <a href={contestLink(g.contestId)}>{formatResult(g)}</a>
                    </td>
                    <td className="num">{formatLine(g.spread)}</td>
                    <td>{g.atsResult ? ATS_LABELS[g.atsResult] || g.atsResult : '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </>
      )}
    </section>
  );
}

export default SplitsPanel;
