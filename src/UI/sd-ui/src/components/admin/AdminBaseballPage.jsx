import React, { useEffect, useState } from 'react';
import './AdminPage.css';
import AdminHeader from './AdminHeader';
import BaseballDebugCard from './signalr-debug/BaseballDebugCard';
import MatchupCard from '../matchups/MatchupCard';
import apiWrapper from '../../api/apiWrapper';

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
          </form>

          {loading && <div>Loading matchup…</div>}
          {error && <div style={{ color: 'red' }}>{error}</div>}
          {matchup && !loading && !error && (
            <MatchupCard
              matchup={matchup}
              leagueSport="BaseballMlb"
              leagueSeasonYear={seasonYear}
            />
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
