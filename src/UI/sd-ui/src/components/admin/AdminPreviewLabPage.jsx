import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import './AdminPage.css';
import AdminHeader from './AdminHeader';
import apiWrapper from '../../api/apiWrapper';
import useSignalRClient from '../../hooks/useSignalRClient';
import { useUserDto } from '../../contexts/UserContext';

const CONTEST_ID_STORAGE_KEY = 'admin.previewlab.contestId';
const LEAGUE_STORAGE_KEY = 'admin.previewlab.league';
const PROMPT_ID_STORAGE_KEY = 'admin.previewlab.promptId';

const LEAGUE_OPTIONS = [
  { value: 'ncaa', label: 'NCAAFB', sport: 'FootballNcaa' },
  { value: 'nfl', label: 'NFL', sport: 'FootballNfl' },
];

const MODE_LABELS = {
  Generate: 'Generate',
  Capture: 'Capture',
  Experiment: 'Experiment',
  0: 'Generate',
  1: 'Capture',
  2: 'Experiment',
};

/**
 * Preview Lab — capture prompts and run sandboxed model experiments for a
 * contest WITHOUT touching production previews. Experiment runs store
 * their result on the capture row only; a MatchupPreview is never written,
 * so a prior season's real preview cannot be shadowed on the picks page.
 * Completed (historical) contests are explicitly allowed — that is the
 * backtest workflow. Design: docs/metrics-modeling/
 * matchup-preview-data-inputs.md §3.6.
 */
export default function AdminPreviewLabPage() {
  const { userDto } = useUserDto();

  const [league, setLeague] = useState(() => {
    const stored = localStorage.getItem(LEAGUE_STORAGE_KEY);
    return LEAGUE_OPTIONS.some(o => o.value === stored) ? stored : 'ncaa';
  });
  const [contestId, setContestId] = useState(
    () => localStorage.getItem(CONTEST_ID_STORAGE_KEY) ?? ''
  );
  const [pendingId, setPendingId] = useState(contestId);
  const [promptId, setPromptId] = useState(
    () => localStorage.getItem(PROMPT_ID_STORAGE_KEY) ?? ''
  );
  const [captures, setCaptures] = useState([]);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const leagueSport = useMemo(
    () => LEAGUE_OPTIONS.find(o => o.value === league)?.sport ?? 'FootballNcaa',
    [league]
  );

  // Request sequence token: concurrent loads are reachable (manual submit,
  // contest switch, SignalR refresh) and an out-of-order response must not
  // overwrite newer data — same discipline as AdminPage's cancelled flags.
  const loadSeqRef = useRef(0);

  const loadCaptures = useCallback(async (id) => {
    const seq = ++loadSeqRef.current;
    if (!id) {
      // Clearing the contest ID invalidates any in-flight load (seq bump
      // above), whose finally will therefore skip its setLoading(false) —
      // clear it here so the page can't be stuck on "Loading captures…".
      setCaptures([]);
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const res = await apiWrapper.Admin.getPreviewCaptures(id);
      if (seq !== loadSeqRef.current) return; // stale response
      setCaptures(Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      if (seq !== loadSeqRef.current) return;
      // 404 just means no captures yet — an empty lab, not an error.
      if (err?.response?.status === 404) {
        setCaptures([]);
      } else {
        toast.error(err?.message ?? 'Failed to load captures');
      }
    } finally {
      if (seq === loadSeqRef.current) setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadCaptures(contestId);
  }, [contestId, loadCaptures]);

  // The SignalR callback reads contestId through a ref so its identity stays
  // stable — a changing identity would tear down and renegotiate the hub
  // connection on every contest switch (useSignalRClient keys its effect on
  // the handler), and a completion landing in that window would be lost.
  const contestIdRef = useRef(contestId);
  contestIdRef.current = contestId;

  // Refresh the list when the async run completes for the contest we're
  // looking at. SignalR payload is camelCase; server GUIDs arrive lowercase
  // while the pasted ID may be uppercase — compare case-insensitively.
  const handlePromptCaptured = useCallback(
    (data) => {
      const current = contestIdRef.current;
      if (!current || data?.contestId?.toLowerCase() !== current.toLowerCase()) return;
      toast.success(data?.message ?? 'Prompt capture completed');
      loadCaptures(current);
    },
    [loadCaptures]
  );

  useSignalRClient({
    userId: userDto?.id,
    onPreviewPromptCaptured: handlePromptCaptured,
  });

  const handleSubmit = (e) => {
    e.preventDefault();
    const trimmed = pendingId.trim();
    if (trimmed) {
      localStorage.setItem(CONTEST_ID_STORAGE_KEY, trimmed);
    } else {
      localStorage.removeItem(CONTEST_ID_STORAGE_KEY);
    }
    if (trimmed === contestId) {
      // Same ID re-submitted — the state-keyed effect won't re-run, so
      // fetch explicitly. "Load captures" should always mean a fetch.
      loadCaptures(trimmed);
    } else {
      setContestId(trimmed);
    }
  };

  const handleLeagueChange = (e) => {
    const next = e.target.value;
    setLeague(next);
    localStorage.setItem(LEAGUE_STORAGE_KEY, next);
  };

  const handlePromptIdChange = (e) => {
    const next = e.target.value;
    setPromptId(next);
    if (next.trim()) {
      localStorage.setItem(PROMPT_ID_STORAGE_KEY, next);
    } else {
      localStorage.removeItem(PROMPT_ID_STORAGE_KEY);
    }
  };

  const runAction = async (action, queuedMessage) => {
    if (!contestId) {
      toast.error('Set a contest ID first.');
      return;
    }
    setSubmitting(true);
    try {
      await action(contestId, leagueSport, promptId.trim() || undefined);
      toast.success(queuedMessage);
    } catch (err) {
      toast.error(
        err?.response?.data?.errors?.[0]?.errorMessage
          ?? err?.response?.data
          ?? err.message
          ?? 'Request failed'
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="admin-page">
      <AdminHeader />
      <div style={{ maxWidth: 1100, margin: '0 auto' }}>
        <h2 style={{ marginBottom: 4 }}>Preview Lab</h2>
        <p style={{ color: 'var(--text-secondary)', marginTop: 0 }}>
          Capture prompts and run sandboxed model experiments. Experiments
          never write a MatchupPreview — existing previews (including last
          season's) are untouched. Grab a contest ID from the picks page.
        </p>

        <form
          onSubmit={handleSubmit}
          style={{ display: 'flex', gap: 8, alignItems: 'center', margin: '16px 0', flexWrap: 'wrap' }}
        >
          <label htmlFor="previewlab-league" style={{ fontWeight: 600 }}>League:</label>
          <select
            id="previewlab-league"
            value={league}
            onChange={handleLeagueChange}
            style={{ padding: '6px 8px' }}
          >
            {LEAGUE_OPTIONS.map(o => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
          <label htmlFor="previewlab-contest-id" style={{ fontWeight: 600 }}>Contest ID:</label>
          <input
            id="previewlab-contest-id"
            type="text"
            value={pendingId}
            onChange={(e) => setPendingId(e.target.value)}
            placeholder="paste a contest GUID"
            style={{ flex: 1, padding: '6px 8px', minWidth: 240 }}
          />
          <button type="submit">Load captures</button>
          <label htmlFor="previewlab-prompt-id" style={{ fontWeight: 600 }}>Prompt ID:</label>
          <input
            id="previewlab-prompt-id"
            type="text"
            value={promptId}
            onChange={handlePromptIdChange}
            placeholder="optional — Prompt GUID from GET /admin/prompts"
            title="Explicit Prompt entity override for this run (Guid). Blank = the sport/variant default. An unknown id fails the run rather than silently using a default."
            style={{ flex: 1, padding: '6px 8px', minWidth: 240 }}
          />
          <button
            type="button"
            disabled={submitting || !contestId}
            onClick={() =>
              runAction(
                apiWrapper.Admin.capturePreviewPrompt,
                'Capture queued — no tokens will be burned.'
              )
            }
            title="Persist the exact prompt payload without calling the model"
          >
            Capture prompt
          </button>
          <button
            type="button"
            disabled={submitting || !contestId}
            onClick={() =>
              runAction(
                apiWrapper.Admin.runPreviewExperiment,
                'Experiment queued — result lands here, not on the picks page.'
              )
            }
            title="Call the model; store the result on the capture row only"
          >
            Run experiment
          </button>
        </form>

        {loading && <div>Loading captures…</div>}
        {!loading && contestId && captures.length === 0 && (
          <div style={{ color: 'var(--text-secondary)' }}>
            No captures for this contest yet.
          </div>
        )}

        {captures.map((c) => (
          <CaptureCard key={c.id} capture={c} />
        ))}
      </div>
    </div>
  );
}

function CaptureCard({ capture }) {
  const modeLabel = MODE_LABELS[capture.mode] ?? String(capture.mode);
  const created = capture.createdUtc
    ? new Date(capture.createdUtc).toLocaleString()
    : '';

  return (
    <div
      style={{
        border: '1px solid var(--border-primary)',
        borderRadius: 8,
        padding: 16,
        marginBottom: 16,
        background: 'var(--table-stripe)',
      }}
    >
      <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'baseline', marginBottom: 8 }}>
        <strong>{modeLabel}</strong>
        <span>{capture.promptVersion}</span>
        {capture.model && <span>model: {capture.model}</span>}
        <span>{capture.estTokens?.toLocaleString()} est. tokens</span>
        {capture.matchupPreviewId && <span>→ preview {capture.matchupPreviewId}</span>}
        <span style={{ marginLeft: 'auto', color: 'var(--text-secondary)' }}>{created}</span>
      </div>

      {capture.responseValidationErrors && (
        <div style={{ color: 'var(--color-danger, #c0392b)', marginBottom: 8 }}>
          Validation: {capture.responseValidationErrors}
        </div>
      )}

      {capture.editorNote && (
        <div style={{ marginBottom: 8 }}>
          Editor note: <em>{capture.editorNote}</em>
        </div>
      )}

      <PromptBlock label="Full prompt (as the model receives it)" text={capture.fullPrompt} />
      <PromptBlock label="Data payload (JSON)" text={capture.payloadJson} pretty />
      {capture.rawResponse && (
        <PromptBlock label="Model response" text={capture.rawResponse} pretty />
      )}
    </div>
  );
}

function PromptBlock({ label, text, pretty = false }) {
  // Memoized: reformatting large JSON on every parent re-render is wasted
  // work — recompute only when the text itself changes.
  const display = useMemo(() => {
    if (!text || !pretty) return text;
    try {
      return JSON.stringify(JSON.parse(text), null, 2);
    } catch {
      // Malformed JSON (that's the data, for experiment failures) — show raw.
      return text;
    }
  }, [text, pretty]);

  if (!text) return null;

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(text);
      toast.success('Copied.');
    } catch {
      toast.error('Copy failed.');
    }
  };

  return (
    <details style={{ marginBottom: 8 }}>
      <summary style={{ cursor: 'pointer', fontWeight: 600 }}>
        {label}{' '}
        <button
          type="button"
          onClick={(e) => { e.preventDefault(); handleCopy(); }}
          style={{ marginLeft: 8, fontSize: '0.8rem' }}
        >
          Copy
        </button>
      </summary>
      <pre
        style={{
          whiteSpace: 'pre-wrap',
          wordBreak: 'break-word',
          maxHeight: 420,
          overflow: 'auto',
          padding: 12,
          border: '1px dashed var(--border-primary)',
          borderRadius: 6,
          fontSize: '0.85rem',
        }}
      >
        {display}
      </pre>
    </details>
  );
}
