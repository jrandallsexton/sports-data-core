import React, { useCallback, useState } from 'react';
import toast from 'react-hot-toast';
import './AdminPage.css';
import AdminHeader from './AdminHeader';
import apiWrapper from '../../api/apiWrapper';

/**
 * Instant Analysis Lab — replay a completed game's plays through the
 * instant-analysis pipeline as a lab run (never mixed with live facts), see
 * every fact it said and every fact it dropped with the reason, and rate
 * facts 0–4 stars to learn which patterns are weak.
 * See docs/features/instant-analysis/09-replay-lab.md.
 *
 * Season facts only use a season document written before the game's kickoff;
 * documents are rewritten in place, so most past games show season facts as
 * dropped ("No season document written before kickoff").
 */

const CONTEST_ID_STORAGE_KEY = 'admin.instantanalysislab.contestId';
const LEAGUE_STORAGE_KEY = 'admin.instantanalysislab.league';

const LEAGUE_OPTIONS = [
  { value: 'ncaa', label: 'NCAAFB' },
  { value: 'nfl', label: 'NFL' },
];

const readStored = (key, fallback) => {
  try {
    return window.localStorage.getItem(key) ?? fallback;
  } catch {
    return fallback;
  }
};

const writeStored = (key, value) => {
  try {
    window.localStorage.setItem(key, value);
  } catch {
    // Storage blocked (private window); the page works without it.
  }
};

const formatDate = (value) => (value ? new Date(value).toLocaleString() : '—');

/** 0–4 star picker (same widget as SmackBot Lab). Zero is explicit, so
 *  "rated bad" is distinct from "not rated". */
function StarRating({ value, onRate, disabled }) {
  return (
    <span style={{ whiteSpace: 'nowrap' }}>
      {[0, 1, 2, 3, 4].map((star) => (
        <button
          key={star}
          type="button"
          disabled={disabled}
          onClick={() => onRate(star)}
          title={star === 0 ? '0 stars (rate as bad)' : `${star} star${star > 1 ? 's' : ''}`}
          aria-label={`Rate ${star} star${star === 1 ? '' : 's'}`}
          style={{
            background: 'none',
            border: 'none',
            cursor: disabled ? 'default' : 'pointer',
            padding: '0 1px',
            fontSize: '1.05rem',
            color:
              value != null && star <= value && !(star === 0 && value > 0)
                ? 'var(--color-warning, #f39c12)'
                : 'var(--text-secondary)',
            opacity: star === 0 ? 0.8 : 1,
          }}
        >
          {star === 0 ? '∅' : (value != null && star <= value ? '★' : '☆')}
        </button>
      ))}
    </span>
  );
}

export default function AdminInstantAnalysisLabPage() {
  const [league, setLeague] = useState(() => readStored(LEAGUE_STORAGE_KEY, 'ncaa'));
  const [contestId, setContestId] = useState(() => readStored(CONTEST_ID_STORAGE_KEY, ''));
  const [runs, setRuns] = useState([]);
  const [run, setRun] = useState(null); // PlayAnalysisRunDetailDto
  const [onlyInteresting, setOnlyInteresting] = useState(true);
  const [busy, setBusy] = useState(false);
  const [ratingId, setRatingId] = useState(null);

  const trimmedContestId = contestId.trim();

  const loadRun = useCallback(async (runId) => {
    setBusy(true);
    try {
      const res = await apiWrapper.Admin.getInstantAnalysisLabRun(league, runId);
      setRun(res.data);
    } catch (err) {
      toast.error(err?.response?.data?.title ?? err?.message ?? 'Failed to load run');
    } finally {
      setBusy(false);
    }
  }, [league]);

  const loadRuns = useCallback(async () => {
    if (!trimmedContestId) return;
    writeStored(CONTEST_ID_STORAGE_KEY, trimmedContestId);
    writeStored(LEAGUE_STORAGE_KEY, league);
    setBusy(true);
    try {
      const res = await apiWrapper.Admin.getInstantAnalysisLabRuns(league, trimmedContestId);
      setRuns(Array.isArray(res.data) ? res.data : []);
      setRun(null);
    } catch (err) {
      toast.error(err?.response?.data?.title ?? err?.message ?? 'Failed to load runs');
    } finally {
      setBusy(false);
    }
  }, [league, trimmedContestId]);

  const startRun = async () => {
    if (!trimmedContestId) return;
    writeStored(CONTEST_ID_STORAGE_KEY, trimmedContestId);
    writeStored(LEAGUE_STORAGE_KEY, league);
    setBusy(true);
    try {
      const res = await apiWrapper.Admin.runInstantAnalysisLab(league, trimmedContestId);
      const summary = res.data;
      toast.success(`Run complete: ${summary.factCount} facts, ${summary.droppedCount} dropped`);
      setRuns((prev) => [summary, ...prev]);
      await loadRun(summary.runId);
    } catch (err) {
      toast.error(err?.response?.data?.title ?? err?.message ?? 'Run failed');
    } finally {
      setBusy(false);
    }
  };

  const handleRate = async (factId, stars) => {
    setRatingId(factId);
    try {
      await apiWrapper.Admin.rateInstantAnalysisFact(league, factId, stars);
      setRun((prev) => prev && {
        ...prev,
        plays: prev.plays.map((p) => ({
          ...p,
          facts: p.facts.map((f) => (f.id === factId ? { ...f, stars } : f)),
        })),
      });
    } catch (err) {
      toast.error(err?.response?.data?.title ?? err?.message ?? 'Rating failed');
    } finally {
      setRatingId(null);
    }
  };

  const plays = run
    ? run.plays.filter((p) => !onlyInteresting || p.facts.length > 0 || p.dropped.length > 0)
    : [];

  return (
    <div className="admin-page">
      <AdminHeader />
      <h2 style={{ margin: '8px 0' }}>Instant Analysis Lab</h2>
      <div style={{ color: 'var(--text-secondary)', marginBottom: 12 }}>
        Replay a completed game through instant analysis, review every fact said
        and dropped, and rate facts. Lab runs never mix with live facts and
        nothing here reaches users. Season facts use only a season document
        written before kickoff, so past games mostly show them as dropped.
      </div>

      <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap', marginBottom: 12 }}>
        <select
          aria-label="League"
          value={league}
          onChange={(e) => setLeague(e.target.value)}
          style={{ padding: '6px 8px' }}
        >
          {LEAGUE_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>{o.label}</option>
          ))}
        </select>
        <input
          aria-label="Contest id"
          placeholder="Contest id"
          value={contestId}
          onChange={(e) => setContestId(e.target.value)}
          style={{ padding: '6px 8px', minWidth: 320 }}
        />
        <button type="button" onClick={loadRuns} disabled={busy || !trimmedContestId}>
          Load runs
        </button>
        <button type="button" onClick={startRun} disabled={busy || !trimmedContestId}>
          Run lab replay
        </button>
        {busy && <span>Working…</span>}
      </div>

      {runs.length > 0 && (
        <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 16 }}>
          <thead>
            <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border-primary)' }}>
              <th style={{ padding: 6 }}>Run</th>
              <th style={{ padding: 6 }}>Started</th>
              <th style={{ padding: 6 }}>Plays</th>
              <th style={{ padding: 6 }}>Facts</th>
              <th style={{ padding: 6 }}>Dropped</th>
              <th style={{ padding: 6 }}>Templates</th>
              <th style={{ padding: 6 }} />
            </tr>
          </thead>
          <tbody>
            {runs.map((r) => (
              <tr
                key={r.runId}
                style={{
                  borderBottom: '1px solid var(--border-primary)',
                  fontWeight: run?.run.runId === r.runId ? 700 : 400,
                }}
              >
                <td style={{ padding: 6, fontFamily: 'monospace', fontSize: '0.85rem' }}>{r.runId.slice(0, 8)}</td>
                <td style={{ padding: 6 }}>{formatDate(r.createdUtc)}</td>
                <td style={{ padding: 6 }}>{r.playCount}</td>
                <td style={{ padding: 6 }}>{r.factCount}</td>
                <td style={{ padding: 6 }}>{r.droppedCount}</td>
                <td style={{ padding: 6 }}>v{r.templateVersion}</td>
                <td style={{ padding: 6 }}>
                  <button type="button" onClick={() => loadRun(r.runId)} disabled={busy}>View</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {run && (
        <div>
          <label style={{ display: 'inline-flex', gap: 6, alignItems: 'center', marginBottom: 8 }}>
            <input
              type="checkbox"
              checked={onlyInteresting}
              onChange={(e) => setOnlyInteresting(e.target.checked)}
            />
            Only plays with facts or drops ({plays.length} of {run.plays.length} shown)
          </label>

          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border-primary)' }}>
                <th style={{ padding: 6 }}>#</th>
                <th style={{ padding: 6 }}>Play</th>
                <th style={{ padding: 6 }}>Facts</th>
                <th style={{ padding: 6 }}>Dropped</th>
              </tr>
            </thead>
            <tbody>
              {plays.map((p) => (
                <tr key={p.playId} style={{ borderBottom: '1px solid var(--border-primary)', verticalAlign: 'top' }}>
                  <td style={{ padding: 6, whiteSpace: 'nowrap' }}>{p.sequenceNumber}</td>
                  <td style={{ padding: 6, maxWidth: 420 }}>
                    <div>{p.text}</div>
                    <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>type {p.typeId}</div>
                  </td>
                  <td style={{ padding: 6 }}>
                    {p.facts.map((f) => (
                      <div key={f.id} style={{ marginBottom: 6 }}>
                        <div>{f.text}</div>
                        <div style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                          <span>{f.factType}</span>
                          <StarRating value={f.stars ?? null} disabled={ratingId === f.id} onRate={(stars) => handleRate(f.id, stars)} />
                        </div>
                        <details style={{ fontSize: '0.75rem' }}>
                          <summary>provenance</summary>
                          <pre style={{ whiteSpace: 'pre-wrap', margin: 0 }}>{f.facts}</pre>
                        </details>
                      </div>
                    ))}
                  </td>
                  <td style={{ padding: 6, fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
                    {p.dropped.map((d, i) => (
                      <div key={i} style={{ marginBottom: 4 }}>
                        <strong>{d.factType}</strong> ({d.kind}): {d.reason}
                      </div>
                    ))}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
