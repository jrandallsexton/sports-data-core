import React, { useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import './AdminPage.css';
import AdminHeader from './AdminHeader';
import BaseballDebugCard from './signalr-debug/BaseballDebugCard';
import MatchupCard from '../matchups/MatchupCard';
import apiWrapper from '../../api/apiWrapper';
import { useContestUpdates } from '../../contexts/ContestUpdatesContext';

const CONTEST_ID_STORAGE_KEY = 'admin.baseball.debugContestId';

/**
 * Baseball SignalR debug harness page. Hosts the baseball-specific debug
 * card on its own route (`/admin/baseball`) so the parent /admin page
 * stays focused on system-health/data-quality widgets.
 *
 * Also renders a real <MatchupCard /> for a chosen contest (entered in
 * the input below) so we can visually verify how SignalR-driven updates
 * will land on the same component the picks page uses. The contestId
 * persists to localStorage so reloads keep the last value.
 */
export default function AdminBaseballPage() {
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

  // Mirror the picks-page enrichment pattern (PicksPage.jsx) so the
  // canonical MatchupCard re-renders as SignalR events land — including
  // baseball-specific fields (inning, count, runners, last play) that
  // BaseballGameStatusInProgress now renders inside MatchupCard.
  const enrichedMatchup = useMemo(() => {
    if (!matchup) return null;
    if (!live) return matchup;
    return {
      ...matchup,
      status: live.status ?? matchup.status,
      awayScore: live.awayScore ?? matchup.awayScore,
      homeScore: live.homeScore ?? matchup.homeScore,
      // Baseball-shaped live fields (handleBaseballPlayCompleted writes
      // these onto the context record).
      inning: live.inning,
      halfInning: live.halfInning,
      balls: live.balls,
      strikes: live.strikes,
      outs: live.outs,
      runnerOnFirst: live.runnerOnFirst,
      runnerOnSecond: live.runnerOnSecond,
      runnerOnThird: live.runnerOnThird,
      lastPlayDescription: live.lastPlayDescription,
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
        const res = await apiWrapper.Admin.getBaseballMatchupForContest(contestId);
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
  }, [contestId]);

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

  // Producer fires ContestStatusChanged once and a BaseballPlayCompleted
  // per stored play ~1s apart. The matchup card on the left and the
  // diamond on the right both react to the resulting SignalR fan-out.
  const handleStartReplay = async () => {
    if (!contestId) {
      toast.error('Load a matchup first.');
      return;
    }
    setReplaying(true);
    try {
      await apiWrapper.Admin.replayBaseballContest(contestId);
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

      <div className="admin-baseball-debug-grid">
        {/* Left column — the observer: pick a contest, render its MatchupCard */}
        <section className="admin-baseball-matchup-debug">
          <form onSubmit={handleSubmit} style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 12 }}>
            <label htmlFor="admin-baseball-contest-id" style={{ fontWeight: 600 }}>
              Contest ID:
            </label>
            <input
              id="admin-baseball-contest-id"
              type="text"
              value={pendingId}
              onChange={(e) => setPendingId(e.target.value)}
              placeholder="paste an MLB contest GUID"
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
                leagueSport="BaseballMlb"
                leagueSeasonYear={seasonYear}
              />
              <BaseballLiveStatePanel live={live} />
            </>
          )}
        </section>

        {/* Right column — the controls: synthetic SignalR events */}
        <section className="admin-signalr-debug">
          <BaseballDebugCard />
        </section>
      </div>
    </div>
  );
}

/**
 * Debug-only readout of the per-contest live state arriving via SignalR.
 * MatchupCard now renders the same fields via BaseballGameStatusInProgress,
 * but suppresses rows whose source values are at defaults — useful for the
 * end-user, less so when debugging the pipeline. This panel renders every
 * field with a `?? '—'` placeholder so you can distinguish "event arrived
 * but field was default" from "no event at all," which matters while the
 * canonical play data still emits empty HalfInning / 0 outs / all-false
 * runners. Drop this panel once the AtBat sourcing pipeline populates
 * those fields for real.
 */
function BaseballLiveStatePanel({ live }) {
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
            Inning: <strong>{live.halfInning || '—'} {live.inning ?? '—'}</strong>
            {' · '}Count: <strong>{live.balls ?? 0}-{live.strikes ?? 0}</strong>
            {' · '}Outs: <strong>{live.outs ?? 0}</strong>
          </li>
          <li>
            Runners: 1B <strong>{live.runnerOnFirst ? '✓' : '·'}</strong>
            {' / '}2B <strong>{live.runnerOnSecond ? '✓' : '·'}</strong>
            {' / '}3B <strong>{live.runnerOnThird ? '✓' : '·'}</strong>
          </li>
          {live.lastPlayDescription && (
            <li>Last play: <em>{live.lastPlayDescription}</em></li>
          )}
        </ul>
      )}
    </div>
  );
}
