import React, { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import './AdminPage.css';
import AdminHeader from './AdminHeader';
import FootballDebugCard from './signalr-debug/FootballDebugCard';
import MatchupCard from '../matchups/MatchupCard';
import apiWrapper from '../../api/apiWrapper';
import { useContestUpdates } from '../../contexts/ContestUpdatesContext';

const CONTEST_ID_STORAGE_KEY = 'admin.football.debugContestId';
const LEAGUE_STORAGE_KEY = 'admin.football.debugLeague';

const LEAGUE_OPTIONS = [
  { value: 'ncaa', label: 'NCAAFB', sport: 'FootballNcaa' },
  { value: 'nfl', label: 'NFL', sport: 'FootballNfl' },
];

/**
 * Football SignalR debug harness page. Mirrors AdminBaseballPage — left
 * column renders a real <MatchupCard /> for a chosen contest so SignalR-
 * driven updates can be observed on the same component the picks page
 * uses; right column hosts the FootballDebugCard control panel that
 * fires synthetic events.
 *
 * Replay supports both NCAA and NFL — the league selector drives both
 * the matchup fetch and the replay endpoint. Contest ID + league persist
 * to localStorage so reloads keep the last values.
 */
export default function AdminFootballPage() {
  const [league, setLeague] = useState(() => {
    // Validate against LEAGUE_OPTIONS so a stale/unsupported value in
    // localStorage (older app version, manual edit) can't reach the
    // backend — ModeMapper.ResolveMode throws on unknown leagues.
    const stored = localStorage.getItem(LEAGUE_STORAGE_KEY);
    return LEAGUE_OPTIONS.some(o => o.value === stored) ? stored : 'ncaa';
  });
  const [contestId, setContestId] = useState(
    () => localStorage.getItem(CONTEST_ID_STORAGE_KEY) ?? ''
  );
  const [pendingId, setPendingId] = useState(contestId);
  const [matchup, setMatchup] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [replaying, setReplaying] = useState(false);

  const { getContestUpdate } = useContestUpdates();
  const live = contestId ? getContestUpdate(contestId) : null;

  const leagueSport = useMemo(
    () => LEAGUE_OPTIONS.find(o => o.value === league)?.sport ?? 'FootballNcaa',
    [league]
  );

  // Mirror the picks-page enrichment pattern (PicksPage.jsx) so the
  // canonical MatchupCard re-renders as SignalR events land — football
  // live fields (period, clock, scores, possession, ballOnYardLine,
  // isScoringPlay, last play) are written onto the context record by
  // handleFootballPlayCompleted.
  const enrichedMatchup = useMemo(() => {
    if (!matchup) return null;
    if (!live) return matchup;
    return {
      ...matchup,
      status: live.status ?? matchup.status,
      awayScore: live.awayScore ?? matchup.awayScore,
      homeScore: live.homeScore ?? matchup.homeScore,
      period: live.period ?? matchup.period,
      clock: live.clock ?? matchup.clock,
      possessionFranchiseSeasonId:
        live.possessionFranchiseSeasonId ?? matchup.possessionFranchiseSeasonId,
      isScoringPlay: live.isScoringPlay ?? matchup.isScoringPlay,
      ballOnYardLine: live.ballOnYardLine ?? matchup.ballOnYardLine,
      lastPlayDescription: live.lastPlayDescription ?? matchup.lastPlayDescription,
    };
  }, [matchup, live]);

  useEffect(() => {
    if (!contestId) {
      setMatchup(null);
      setError(null);
      return;
    }
    let cancelled = false;
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const res = await apiWrapper.Admin.getFootballMatchupForContest(contestId, league);
        if (!cancelled) setMatchup(res.data ?? null);
      } catch (err) {
        if (!cancelled) {
          setError(err?.response?.data?.errors?.[0]?.errorMessage ?? err.message ?? 'Failed to load matchup');
          setMatchup(null);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    load();
    return () => {
      cancelled = true;
    };
  }, [contestId, league]);

  const handleSubmit = (e) => {
    e.preventDefault();
    const trimmed = pendingId.trim();
    setContestId(trimmed);
    if (trimmed) {
      localStorage.setItem(CONTEST_ID_STORAGE_KEY, trimmed);
    } else {
      localStorage.removeItem(CONTEST_ID_STORAGE_KEY);
    }
  };

  const handleLeagueChange = (e) => {
    const next = e.target.value;
    setLeague(next);
    localStorage.setItem(LEAGUE_STORAGE_KEY, next);
  };

  // Producer fires ContestStatusChanged once and a FootballPlayCompleted
  // per stored play. The matchup card on the left reacts to the resulting
  // SignalR fan-out; the field on the right (FootballDebugCard) is for
  // synthetic-event testing and uses its own hardcoded sandbox contestId.
  const handleStartReplay = async () => {
    if (!contestId) {
      toast.error('Load a matchup first.');
      return;
    }
    setReplaying(true);
    try {
      await apiWrapper.Admin.replayFootballContest(contestId, league);
      toast.success('Replay queued — events will start flowing.');
    } catch (err) {
      toast.error(
        err?.response?.data?.errors?.[0]?.errorMessage
          ?? err.message
          ?? 'Replay failed'
      );
    } finally {
      setReplaying(false);
    }
  };

  const seasonYear = matchup?.startDateUtc
    ? new Date(matchup.startDateUtc).getUTCFullYear()
    : undefined;

  return (
    <div className="admin-page">
      <AdminHeader />
      <a href="https://messaging-footballncaa.sportdeets.com/#/" target="_blank" rel="noreferrer">Rabbit - NCAAFB</a>
      <br/>
      <a href="https://messaging-footballnfl.sportdeets.com/#/" target="_blank" rel="noreferrer">Rabbit - NFL</a>
      <div className="admin-sport-debug-grid">
        {/* Left column — the observer: pick a contest, render its MatchupCard */}
        <section className="admin-football-matchup-debug">
          <form onSubmit={handleSubmit} style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 12, flexWrap: 'wrap' }}>
            <label htmlFor="admin-football-league" style={{ fontWeight: 600 }}>
              League:
            </label>
            <select
              id="admin-football-league"
              value={league}
              onChange={handleLeagueChange}
              style={{ padding: '6px 8px' }}
            >
              {LEAGUE_OPTIONS.map(o => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
            <label htmlFor="admin-football-contest-id" style={{ fontWeight: 600 }}>
              Contest ID:
            </label>
            <input
              id="admin-football-contest-id"
              type="text"
              value={pendingId}
              onChange={(e) => setPendingId(e.target.value)}
              placeholder="paste a football contest GUID"
              style={{ flex: 1, padding: '6px 8px', minWidth: 0 }}
            />
            <button type="submit">Load matchup</button>
            <button
              type="button"
              onClick={handleStartReplay}
              disabled={replaying || !contestId}
              title={contestId ? 'Replay this contest through the bus → SignalR pipeline' : 'Load a matchup first'}
            >
              {replaying ? 'Queuing…' : 'Start replay'}
            </button>
          </form>

          {loading && <div>Loading matchup…</div>}
          {error && <div style={{ color: 'red' }}>{error}</div>}
          {enrichedMatchup && !loading && !error && (
            <>
              <MatchupCard
                matchup={enrichedMatchup}
                leagueSport={leagueSport}
                leagueSeasonYear={seasonYear}
              />
              <FootballLiveStatePanel live={live} />
            </>
          )}
        </section>

        {/* Right column — the controls: synthetic SignalR events */}
        <section className="admin-signalr-debug">
          <FootballDebugCard />
        </section>
      </div>
    </div>
  );
}

/**
 * Debug-only readout of the per-contest live state arriving via SignalR.
 * MatchupCard renders the same fields through GameStatus, but suppresses
 * rows whose source values are at defaults — useful for the end-user,
 * less so when debugging the pipeline. This panel renders every field
 * with a `?? '—'` placeholder so you can distinguish "event arrived but
 * field was default" from "no event at all".
 */
function FootballLiveStatePanel({ live }) {
  const empty = !live;
  return (
    <div
      style={{
        marginTop: 12,
        padding: 12,
        border: '1px dashed var(--border-primary)',
        borderRadius: 6,
        background: 'var(--table-stripe)',
        fontSize: '0.9rem',
      }}
    >
      <div style={{ fontWeight: 600, marginBottom: 6 }}>
        Live state (debug readout)
      </div>
      {empty && (
        <div style={{ color: 'var(--text-secondary)' }}>
          Waiting for the first SignalR event for this contest…
        </div>
      )}
      {!empty && (
        <ul style={{ margin: 0, paddingLeft: 18, lineHeight: 1.5 }}>
          <li>Status: <strong>{live.status ?? '—'}</strong></li>
          <li>Score: away <strong>{live.awayScore ?? 0}</strong> · home <strong>{live.homeScore ?? 0}</strong></li>
          <li>
            Period: <strong>{live.period ?? '—'}</strong>
            {' · '}Clock: <strong>{live.clock ?? '—'}</strong>
          </li>
          <li>
            Possession: <strong>{live.possessionFranchiseSeasonId ?? '—'}</strong>
            {' · '}Ball on yard: <strong>{typeof live.ballOnYardLine === 'number' ? live.ballOnYardLine : '—'}</strong>
            {' · '}Scoring: <strong>{live.isScoringPlay ? '✓' : '·'}</strong>
          </li>
          {live.lastPlayDescription && (
            <li>Last play: <em>{live.lastPlayDescription}</em></li>
          )}
        </ul>
      )}
    </div>
  );
}
