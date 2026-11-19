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

// Filters are laid out in blocks by the question they answer: about the
// game, about the opponent, about the betting line. A chip group's value is
// the Producer's enum name (or a boolean); null = Any. Select rows are
// filled from the response's options rather than fixed chips.
export const FILTER_BLOCKS = [
  {
    title: 'Game',
    rows: [
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
        key: 'seasonType',
        label: 'Season',
        title: 'Conference championship games count as regular season',
        options: [
          { label: 'Regular', value: 'Regular' },
          { label: 'Postseason', value: 'Postseason' },
        ],
      },
      {
        key: 'month',
        label: 'Month',
        options: [
          { label: 'Aug', value: 8 },
          { label: 'Sep', value: 9 },
          { label: 'Oct', value: 10 },
          { label: 'Nov', value: 11 },
          { label: 'Dec', value: 12 },
          { label: 'Jan', value: 1 },
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
    ],
  },
  {
    title: 'Opponent',
    rows: [
      { key: 'opponent', label: 'Team', select: 'opponents' },
      { key: 'opponentConference', label: 'Conference', select: 'conferences' },
      {
        key: 'opponentRank',
        label: 'Rank',
        options: [
          { label: 'Top 10', value: 'Top10' },
          { label: 'Ranked', value: 'Ranked' },
          { label: 'Unranked', value: 'Unranked' },
        ],
      },
      {
        key: 'conferenceGame',
        label: 'Matchup',
        options: [
          { label: 'In-conference', value: true },
          { label: 'Non-conference', value: false },
        ],
      },
    ],
  },
  {
    title: 'Betting',
    rows: [
      {
        key: 'line',
        label: 'Line',
        options: [
          { label: 'Favorite', value: 'Favorite' },
          { label: 'Underdog', value: 'Underdog' },
        ],
      },
      {
        key: 'spreadSize',
        label: 'Spread',
        title: 'Size of the line, either side; combine with Line for direction',
        options: [
          { label: '≤3', value: 'Close' },
          { label: '3.5–6.5', value: 'Moderate' },
          { label: '7–13.5', value: 'Large' },
          { label: '14+', value: 'Huge' },
        ],
      },
    ],
  },
];

const FILTER_ROWS = FILTER_BLOCKS.flatMap((b) => b.rows);

const EMPTY_FILTERS = Object.fromEntries(FILTER_ROWS.map((row) => [row.key, null]));

// Placeholder for each select; a select's "Any" choice clears it.
const SELECT_ANY = { opponents: 'Any opponent', conferences: 'Any conference' };

const EMPTY_OPTIONS = { opponents: [], conferences: [] };

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
  // Picker lists for opponent / opponent conference. Kept apart from
  // `splits` so the selects stay filled while a new request is loading.
  const [options, setOptions] = useState(EMPTY_OPTIONS);
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
        if (!current) return;
        setSplits(response.data || null);
        setOptions(response.data?.options || EMPTY_OPTIONS);
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

  const setSelectFilter = (key, value) =>
    setFilters((prev) => ({ ...prev, [key]: value || null }));

  // Opponents and conferences belong to the team, so a new team drops them.
  const onTeam = (value) => {
    setSlug(value);
    setOptions(EMPTY_OPTIONS);
    setFilters((prev) => ({ ...prev, opponent: null, opponentConference: null }));
  };

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
  const activeCount = useMemo(
    () => Object.values(filters).filter((v) => v != null).length,
    [filters],
  );

  const renderSelect = (row) => (
    <select
      value={filters[row.key] || ''}
      onChange={(e) => setSelectFilter(row.key, e.target.value)}
      className="splits-select splits-filter-select"
      aria-label={row.key === 'opponent' ? 'Opponent team' : 'Opponent conference'}
      title={
        row.key === 'opponentConference'
          ? "The opponent's conference in the season of each game"
          : undefined
      }
      disabled={!slug}
    >
      <option value="">{SELECT_ANY[row.select]}</option>
      {row.select === 'opponents'
        ? options.opponents.map((o) => (
            <option key={o.slug} value={o.slug}>
              {o.name} ({o.games})
            </option>
          ))
        : options.conferences.map((c) => (
            <option key={c.value} value={c.value}>
              vs {c.label} ({c.games})
            </option>
          ))}
    </select>
  );

  const renderChips = (row) =>
    row.options.map((opt) => {
      const active = filters[row.key] === opt.value;
      return (
        <button
          key={String(opt.value)}
          type="button"
          className={`splits-chip${active ? ' active' : ''}`}
          aria-pressed={active}
          onClick={() => toggleFilter(row.key, opt.value)}
        >
          {opt.label}
        </button>
      );
    });

  return (
    <section className="splits-panel" aria-label="Team splits">
      <div className="splits-panel-header">
        <div className="splits-controls">
          <select
            value={slug}
            onChange={(e) => onTeam(e.target.value)}
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
        {activeCount > 0 && (
          <div className="splits-active">
            <span>
              {activeCount} {activeCount === 1 ? 'filter' : 'filters'} on
            </span>
            <span aria-hidden="true">·</span>
            <button type="button" className="splits-clear" onClick={() => setFilters(EMPTY_FILTERS)}>
              Clear all
            </button>
          </div>
        )}
      </div>

      <div className="splits-filters">
        {FILTER_BLOCKS.map((block) => (
          <fieldset key={block.title} className="splits-filter-block">
            <legend className="splits-filter-title">{block.title}</legend>
            {block.rows.map((row) => (
              <div
                key={row.key}
                className="splits-filter-row"
                role={row.select ? undefined : 'group'}
                aria-label={row.select ? undefined : `${block.title} ${row.label.toLowerCase()}`}
                title={row.title}
              >
                <span className="splits-filter-label">{row.label}</span>
                <div className="splits-filter-values">
                  {row.select ? renderSelect(row) : renderChips(row)}
                </div>
              </div>
            ))}
          </fieldset>
        ))}
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
          {filters.month != null && (
            <p className="splits-range-note">
              Month uses the local kickoff date; games without a known venue are left out.
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
