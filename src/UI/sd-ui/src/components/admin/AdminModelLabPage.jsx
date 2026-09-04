import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import './AdminPage.css';
import AdminHeader from './AdminHeader';
import apiWrapper from '../../api/apiWrapper';
import useSignalRClient from '../../hooks/useSignalRClient';
import { useUserDto } from '../../contexts/UserContext';

const LEAGUE_STORAGE_KEY = 'admin.modellab.league';
const YEAR_STORAGE_KEY = 'admin.modellab.year';
const WEEK_STORAGE_KEY = 'admin.modellab.week';
const PROMPT_ID_STORAGE_KEY = 'admin.modellab.promptId';

const LEAGUE_OPTIONS = [
  { value: 'ncaa', label: 'NCAAFB', sport: 'FootballNcaa' },
  { value: 'nfl', label: 'NFL', sport: 'FootballNfl' },
];

const DEFAULT_YEAR = 2026;

// Queued-marker lifecycle (see the reconcile effect below).
const QUEUED_SKEW_GRACE_MS = 2 * 60 * 1000;
const QUEUED_EXPIRY_MS = 10 * 60 * 1000;

/**
 * Model Consensus Lab — the week matrix. Rows = contests any pick'em
 * league carries for the selected sport/week (one SU line + one ATS line
 * each); columns = active lab-reachable models + Consensus. An empty cell
 * offers a per-(contest, model) generate button; a whole-row Run panel
 * fills every hole for that contest. Actuals + accuracy columns arrive
 * with scoring-on-finalization. Design: docs/features/model-consensus-lab.md.
 */
export default function AdminModelLabPage() {
  const { userDto } = useUserDto();

  const [league, setLeague] = useState(() => {
    const stored = localStorage.getItem(LEAGUE_STORAGE_KEY);
    return LEAGUE_OPTIONS.some(o => o.value === stored) ? stored : 'ncaa';
  });
  const [year, setYear] = useState(() => {
    const stored = Number(localStorage.getItem(YEAR_STORAGE_KEY));
    return Number.isInteger(stored) && stored >= 2000 ? stored : DEFAULT_YEAR;
  });
  const [week, setWeek] = useState(() => {
    const stored = Number(localStorage.getItem(WEEK_STORAGE_KEY));
    return Number.isInteger(stored) && stored >= 1 ? stored : 1;
  });
  const [promptId, setPromptId] = useState(
    () => localStorage.getItem(PROMPT_ID_STORAGE_KEY) ?? ''
  );

  const [matrix, setMatrix] = useState(null); // { models: [], contests: [] }
  const [loading, setLoading] = useState(false);
  // Cells with a generation in flight, keyed `${contestId}|${modelId}`
  // (or `${contestId}|*` for a whole-row panel run).
  const [queued, setQueued] = useState({});

  const leagueSport = useMemo(
    () => LEAGUE_OPTIONS.find(o => o.value === league)?.sport ?? 'FootballNcaa',
    [league]
  );

  // Request sequence token: manual loads and per-completion SignalR
  // refreshes overlap; an out-of-order response must not win.
  const loadSeqRef = useRef(0);

  const loadMatrix = useCallback(async (sport, y, w, { quiet = false } = {}) => {
    const seq = ++loadSeqRef.current;
    if (!quiet) setLoading(true);
    try {
      const res = await apiWrapper.Admin.getModelLabMatrix(sport, y, w);
      if (seq !== loadSeqRef.current) return;
      setMatrix(res.data ?? { models: [], contests: [] });
    } catch (err) {
      if (seq !== loadSeqRef.current) return;
      toast.error(err?.message ?? 'Failed to load matrix');
    } finally {
      if (seq === loadSeqRef.current) setLoading(false);
    }
  }, []);

  useEffect(() => {
    localStorage.setItem(LEAGUE_STORAGE_KEY, league);
    localStorage.setItem(YEAR_STORAGE_KEY, String(year));
    localStorage.setItem(WEEK_STORAGE_KEY, String(week));
    // Year/week arrive digit by digit — debounce so intermediate values
    // ("2", "20", "202"...) never fire a request, and skip invalid ones.
    if (!Number.isInteger(year) || year < 2000 || !Number.isInteger(week) || week < 1) {
      return undefined;
    }
    const timer = setTimeout(() => loadMatrix(leagueSport, year, week), 400);
    return () => clearTimeout(timer);
  }, [league, leagueSport, year, week, loadMatrix]);

  // Read current selection through refs so the SignalR handler identity
  // stays stable (the hook keys its connection on it). Assigned in
  // effects — never during render.
  const selectionRef = useRef({ leagueSport, year, week });
  useEffect(() => {
    selectionRef.current = { leagueSport, year, week };
  }, [leagueSport, year, week]);
  const contestIdsRef = useRef(new Set());
  useEffect(() => {
    contestIdsRef.current = new Set(
      (matrix?.contests ?? []).map(c => c.contestId?.toLowerCase())
    );
  }, [matrix]);

  // Queued markers hold their set-time and clear only when the reloaded
  // matrix shows a cell CREATED AFTER the marker (2-minute clock-skew
  // grace) — a stale completion event can no longer clear an in-flight
  // retry, and a panel marker survives until every column has landed.
  // A 10-minute expiry stops a dead job from pinning "queued…" forever.
  useEffect(() => {
    if (!matrix) return;
    setQueued(q => {
      const entries = Object.entries(q);
      if (entries.length === 0) return q;
      const cellTime = {};
      const modelIds = (matrix.models ?? []).map(m => String(m.id).toLowerCase());
      for (const c of matrix.contests ?? []) {
        for (const cell of c.cells ?? []) {
          cellTime[`${String(c.contestId).toLowerCase()}|${String(cell.modelId).toLowerCase()}`] =
            Date.parse(cell.createdUtc) || 0;
        }
      }
      const now = Date.now();
      const next = {};
      let changed = false;
      for (const [key, setAt] of entries) {
        const [cid, mid] = key.toLowerCase().split('|');
        const landedAfter = (m) => (cellTime[`${cid}|${m}`] ?? 0) > setAt - QUEUED_SKEW_GRACE_MS;
        const done = mid === '*'
          ? modelIds.length > 0 && modelIds.every(landedAfter)
          : landedAfter(mid);
        if (done || now - setAt > QUEUED_EXPIRY_MS) {
          changed = true;
          continue;
        }
        next[key] = setAt;
      }
      return changed ? next : q;
    });
  }, [matrix]);

  const handlePromptCaptured = useCallback(
    (data) => {
      const id = data?.contestId?.toLowerCase();
      if (!id || !contestIdsRef.current.has(id)) return;
      const { leagueSport: s, year: y, week: w } = selectionRef.current;
      loadMatrix(s, y, w, { quiet: true });
    },
    [loadMatrix]
  );

  useSignalRClient({
    userId: userDto?.id,
    onPreviewPromptCaptured: handlePromptCaptured,
  });

  const handlePromptIdChange = (e) => {
    const next = e.target.value;
    setPromptId(next);
    if (next.trim()) {
      localStorage.setItem(PROMPT_ID_STORAGE_KEY, next);
    } else {
      localStorage.removeItem(PROMPT_ID_STORAGE_KEY);
    }
  };

  const generateCell = async (contestId, modelId) => {
    const key = `${contestId}|${modelId}`;
    setQueued(q => ({ ...q, [key]: Date.now() }));
    try {
      await apiWrapper.Admin.runPreviewExperiment(
        contestId, leagueSport, promptId.trim() || undefined, modelId
      );
    } catch (err) {
      setQueued(q => {
        const next = { ...q };
        delete next[key];
        return next;
      });
      toast.error(err?.message ?? 'Generate failed');
    }
  };

  const runPanelForContest = async (contestId) => {
    const key = `${contestId}|*`;
    setQueued(q => ({ ...q, [key]: Date.now() }));
    try {
      const res = await apiWrapper.Admin.runPreviewPanel(
        contestId, leagueSport, promptId.trim() || undefined
      );
      const count = res?.data?.modelCount;
      toast.success(count ? `Panel queued - ${count} model(s).` : 'Panel queued.');
    } catch (err) {
      setQueued(q => {
        const next = { ...q };
        delete next[key];
        return next;
      });
      toast.error(
        err?.response?.data?.error ?? err?.message ?? 'Panel run failed'
      );
    }
  };

  const models = useMemo(() => matrix?.models ?? [], [matrix]);
  const contests = useMemo(() => matrix?.contests ?? [], [matrix]);

  // WEEK records for the footer (scope = the displayed matrix: the
  // selected week's contests only — season-to-date needs a cross-week
  // aggregate and is future work): per model and for the consensus
  // column, X/Y = correct picks / graded picks. A pick is
  // GRADED only when the game is final, the model actually picked, and
  // an actual exists (ATS pushes grade nobody). Abstentions and
  // not-yet-run cells never count against a model here — cost of
  // abstaining is a Phase-4 scoring question, not a display one.
  const records = useMemo(() => {
    const perModel = {};
    const consensus = { su: [0, 0], ats: [0, 0] };
    const tally = (pair, grade) => {
      if (!grade) return;
      pair[1] += 1;
      if (grade === 'correct') pair[0] += 1;
    };
    for (const c of contests) {
      const cellBy = {};
      for (const cell of c.cells ?? []) cellBy[String(cell.modelId).toLowerCase()] = cell;
      for (const m of models) {
        const cell = cellBy[String(m.id).toLowerCase()];
        const rec = (perModel[m.id] ??= { su: [0, 0], ats: [0, 0] });
        tally(rec.su, gradePick(cell?.predictedStraightUpWinnerId, c.actualWinnerId, c.isFinal));
        tally(rec.ats, gradePick(cell?.predictedSpreadWinnerId, c.actualSpreadWinnerId, c.isFinal));
      }
      const suC = consensusOf(models.map(m => cellBy[String(m.id).toLowerCase()]?.predictedStraightUpWinnerId ?? null));
      const atsC = consensusOf(models.map(m => cellBy[String(m.id).toLowerCase()]?.predictedSpreadWinnerId ?? null));
      tally(consensus.su, gradePick(suC, c.actualWinnerId, c.isFinal));
      tally(consensus.ats, gradePick(atsC, c.actualSpreadWinnerId, c.isFinal));
    }
    return { perModel, consensus };
  }, [contests, models]);

  return (
    <div className="admin-page">
      <AdminHeader />
      <div style={{ maxWidth: 1400, margin: '0 auto' }}>
        <h2 style={{ marginBottom: 4 }}>Model Consensus Lab</h2>
        <p style={{ color: 'var(--text-secondary)', marginTop: 0 }}>
          Every contest any pick'em league carries for the week, against
          every active lab-reachable model. Empty cell = no run yet - the
          button generates just that pair. Experiments never write a
          MatchupPreview. Models live on /admin/models; consensus is a
          simple majority of the picks cast.
        </p>

        <div style={{ display: 'flex', gap: 8, alignItems: 'center', margin: '16px 0', flexWrap: 'wrap' }}>
          <label htmlFor="modellab-league" style={{ fontWeight: 600 }}>League:</label>
          <select
            id="modellab-league"
            value={league}
            onChange={(e) => setLeague(e.target.value)}
            style={{ padding: '6px 8px' }}
          >
            {LEAGUE_OPTIONS.map(o => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
          <label htmlFor="modellab-year" style={{ fontWeight: 600 }}>Season:</label>
          <input
            id="modellab-year"
            type="number"
            min="2000"
            max="2100"
            value={year}
            onChange={(e) => setYear(Number(e.target.value))}
            style={{ width: 90, padding: '6px 8px' }}
          />
          <label htmlFor="modellab-week" style={{ fontWeight: 600 }}>Week:</label>
          <input
            id="modellab-week"
            type="number"
            min="1"
            max="30"
            value={week}
            onChange={(e) => setWeek(Number(e.target.value))}
            style={{ width: 70, padding: '6px 8px' }}
          />
          <button type="button" className="model-lab-btn" onClick={() => loadMatrix(leagueSport, year, week)}>
            Refresh
          </button>
          <label htmlFor="modellab-prompt-id" style={{ fontWeight: 600 }}>Prompt ID:</label>
          <input
            id="modellab-prompt-id"
            type="text"
            value={promptId}
            onChange={handlePromptIdChange}
            placeholder="optional - Prompt GUID override for generated runs"
            title="Explicit Prompt entity override (Guid) applied to runs started from this page. Blank = the sport/variant default."
            style={{ flex: 1, padding: '6px 8px', minWidth: 240 }}
          />
        </div>

        {loading && <div>Loading matrix…</div>}
        {!loading && models.length === 0 && (
          <div style={{ color: 'var(--text-secondary)' }}>
            No active lab-reachable models - add models (gateway: OpenRouter)
            on /admin/models first.
          </div>
        )}
        {!loading && models.length > 0 && contests.length === 0 && (
          <div style={{ color: 'var(--text-secondary)' }}>
            No pick'em league carries contests for this sport/week.
          </div>
        )}

        {models.length > 0 && contests.length > 0 && (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ borderCollapse: 'collapse', width: '100%', fontSize: '0.9rem' }}>
              <thead>
                <tr>
                  <th style={headerStyle}>Contest</th>
                  {models.map(m => (
                    <th key={m.id} style={headerStyle}>{m.name}</th>
                  ))}
                  <th style={headerStyle}>Consensus</th>
                  <th style={headerStyle} aria-label="Row actions" />
                </tr>
              </thead>
              <tbody>
                {contests.map((c) => (
                  <ContestRows
                    key={c.contestId}
                    contest={c}
                    models={models}
                    queued={queued}
                    onGenerateCell={generateCell}
                    onRunPanel={runPanelForContest}
                  />
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <td style={footerStyle}>Week Record - SU</td>
                  {models.map(m => (
                    <td key={m.id} style={footerStyle}>{formatRecord(records.perModel[m.id]?.su)}</td>
                  ))}
                  <td style={footerStyle}>{formatRecord(records.consensus.su)}</td>
                  <td style={footerStyle} />
                </tr>
                <tr>
                  <td style={footerStyle}>Week Record - ATS</td>
                  {models.map(m => (
                    <td key={m.id} style={footerStyle}>{formatRecord(records.perModel[m.id]?.ats)}</td>
                  ))}
                  <td style={footerStyle}>{formatRecord(records.consensus.ats)}</td>
                  <td style={footerStyle} />
                </tr>
              </tfoot>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

const headerStyle = {
  textAlign: 'left',
  padding: '6px 10px',
  borderBottom: '2px solid var(--border-primary)',
  whiteSpace: 'nowrap',
};

const cellStyle = {
  padding: '6px 10px',
  borderBottom: '1px solid var(--border-primary)',
  whiteSpace: 'nowrap',
};

const footerStyle = {
  padding: '8px 10px',
  borderTop: '2px solid var(--border-primary)',
  whiteSpace: 'nowrap',
  fontWeight: 700,
};

/** Majority of the picks cast; needs at least 2 votes and no tie. */
/**
 * Home-relative line as " (-22.5)" / " (+3.5)" / " (PK)" — no team name,
 * the spread is always the home team's. Empty string when no odds.
 */
function formatSpread(spread) {
  if (spread == null) return '';
  if (spread === 0) return ' (PK)';
  return ` (${spread > 0 ? '+' : ''}${spread})`;
}

/**
 * 'correct' | 'incorrect' | null. Null = ungraded: game not final, no
 * pick, or no actual to grade against (an ATS PUSH leaves
 * actualSpreadWinnerId null on a finalized game — a push grades nobody).
 */
function gradePick(pickId, actualId, isFinal) {
  if (!isFinal || !pickId || !actualId) return null;
  return String(pickId).toLowerCase() === String(actualId).toLowerCase()
    ? 'correct'
    : 'incorrect';
}

const GRADE_STYLES = {
  correct: {
    backgroundColor: 'rgba(39, 174, 96, 0.18)',
    color: 'var(--color-success, #27ae60)',
  },
  incorrect: {
    backgroundColor: 'rgba(192, 57, 43, 0.18)',
    color: 'var(--color-danger, #c0392b)',
  },
};

function formatRecord(pair) {
  if (!pair || pair[1] === 0) return '—';
  return `${pair[0]}/${pair[1]}`;
}

function consensusOf(picks) {
  const votes = picks.filter(Boolean);
  if (votes.length < 2) return null;
  const counts = {};
  for (const v of votes) counts[v] = (counts[v] ?? 0) + 1;
  const ranked = Object.entries(counts).sort((a, b) => b[1] - a[1]);
  if (ranked.length > 1 && ranked[0][1] === ranked[1][1]) return null; // tie
  return ranked[0][1] * 2 > votes.length ? ranked[0][0] : null;
}

function ContestRows({ contest, models, queued, onGenerateCell, onRunPanel }) {
  const teamById = useMemo(() => ({
    [String(contest.awayFranchiseSeasonId).toLowerCase()]: contest.awayShort || contest.away,
    [String(contest.homeFranchiseSeasonId).toLowerCase()]: contest.homeShort || contest.home,
  }), [contest]);

  const cellByModel = useMemo(() => {
    const map = {};
    for (const cell of contest.cells ?? []) map[String(cell.modelId).toLowerCase()] = cell;
    return map;
  }, [contest]);

  const teamFor = (fsId) => {
    if (!fsId) return null;
    // A pick GUID outside the matchup is itself a finding - show it raw.
    return teamById[String(fsId).toLowerCase()] ?? `${String(fsId).substring(0, 8)}…?`;
  };

  const rowPanelQueued = queued[`${contest.contestId}|*`];
  const label = `${contest.away} @ ${contest.home}`;

  const renderPickCell = (model, pickField) => {
    const cell = cellByModel[String(model.id).toLowerCase()];
    const isQueued = rowPanelQueued || queued[`${contest.contestId}|${model.id}`];

    if (!cell) {
      return (
        <td key={model.id} style={cellStyle}>
          {isQueued ? (
            <span style={{ color: 'var(--text-secondary)' }}>queued…</span>
          ) : (
            <button
              type="button"
              className="model-lab-btn model-lab-btn--small"
              onClick={() => onGenerateCell(contest.contestId, model.id)}
              title={`Run ${model.name} on ${label}`}
            >
              generate
            </button>
          )}
        </td>
      );
    }

    const actualId = pickField === 'predictedStraightUpWinnerId'
      ? contest.actualWinnerId
      : contest.actualSpreadWinnerId;
    const grade = gradePick(cell[pickField], actualId, contest.isFinal);

    const pick = teamFor(cell[pickField]);
    if (!pick) {
      // No pick + recorded problems = the run FAILED (parse error, bad
      // response); a clean row with no pick = the model genuinely
      // abstained. Different facts, different cells - and both keep a
      // retry (a rerun supersedes; the failed capture stays as history).
      return (
        <td key={model.id} style={cellStyle}>
          {cell.problems ? (
            <span
              title={cell.problems}
              style={{ color: 'var(--color-danger, #c0392b)', fontWeight: 700, cursor: 'help' }}
            >
              error
            </span>
          ) : (
            <span style={{ color: 'var(--text-secondary)' }}>abstained</span>
          )}
          {isQueued ? (
            <span style={{ color: 'var(--text-secondary)', marginLeft: 6 }}>queued…</span>
          ) : (
            <button
              type="button"
              className="model-lab-btn model-lab-btn--small"
              onClick={() => onGenerateCell(contest.contestId, model.id)}
              title={`Retry ${model.name} on ${label}`}
              style={{ marginLeft: 6 }}
            >
              retry
            </button>
          )}
        </td>
      );
    }
    return (
      <td key={model.id} style={{ ...cellStyle, fontWeight: 600, ...(grade ? GRADE_STYLES[grade] : null) }}>
        {pick}
        {cell.problems && (
          <span
            title={cell.problems}
            style={{ color: 'var(--color-warning, #e67e22)', marginLeft: 4, cursor: 'help' }}
          >
            *
          </span>
        )}
      </td>
    );
  };

  const suConsensus = consensusOf(models.map(m =>
    cellByModel[String(m.id).toLowerCase()]?.predictedStraightUpWinnerId ?? null));
  const atsConsensus = consensusOf(models.map(m =>
    cellByModel[String(m.id).toLowerCase()]?.predictedSpreadWinnerId ?? null));

  const suGrade = gradePick(suConsensus, contest.actualWinnerId, contest.isFinal);
  const atsGrade = gradePick(atsConsensus, contest.actualSpreadWinnerId, contest.isFinal);

  const hasHoles = models.some(m => !cellByModel[String(m.id).toLowerCase()]);

  return (
    <>
      <tr>
        <td style={{ ...cellStyle, fontWeight: 600 }}>{label} - SU</td>
        {models.map(m => renderPickCell(m, 'predictedStraightUpWinnerId'))}
        <td style={{ ...cellStyle, fontWeight: 700, ...(suGrade ? GRADE_STYLES[suGrade] : null) }}>
          {suConsensus ? teamFor(suConsensus) : '-'}
        </td>
        <td rowSpan={2} style={{ ...cellStyle, verticalAlign: 'middle' }}>
          {hasHoles && !rowPanelQueued && (
            <button
              type="button"
              className="model-lab-btn model-lab-btn--small"
              onClick={() => onRunPanel(contest.contestId)}
              title={`Run every lab model on ${label}`}
            >
              run panel
            </button>
          )}
          {rowPanelQueued && <span style={{ color: 'var(--text-secondary)' }}>panel queued…</span>}
        </td>
      </tr>
      <tr>
        <td style={{ ...cellStyle, color: 'var(--text-secondary)' }}>
          {label} - ATS{formatSpread(contest.spread)}
        </td>
        {models.map(m => renderPickCell(m, 'predictedSpreadWinnerId'))}
        <td style={{ ...cellStyle, fontWeight: 700, ...(atsGrade ? GRADE_STYLES[atsGrade] : null) }}>
          {atsConsensus ? teamFor(atsConsensus) : '—'}
        </td>
      </tr>
    </>
  );
}
