import React, { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import './AdminPage.css';
import AdminHeader from './AdminHeader';
import apiWrapper from '../../api/apiWrapper';

/**
 * SmackBot Lab — preview what the voice WOULD have pushed for a league's
 * scored picks, rate each line 0–4 stars (training data), and author
 * phrases against real game data. The feature stays dark for users until
 * the operator has blessed real output here.
 * See docs/features/smackbot-lab.md.
 *
 * Phrase text lives only in Notification's database (the repo is public) —
 * this page is the authoring surface, never source control.
 */

// Wire names — must match Notification's PickSituation enum exactly
// (case-sensitive contract). Grouped for the phrase-form dropdown.
const SITUATIONS = [
  { value: 'ShutoutLoss', label: 'Shutout loss', kind: 'loss' },
  { value: 'BlowoutLoss', label: 'Blowout loss (≥21)', kind: 'loss' },
  { value: 'BigDogLoss', label: 'Big dog lost (≥10 dog)', kind: 'loss' },
  { value: 'FavoriteChoked', label: 'Favorite choked (≥10 fav)', kind: 'loss' },
  { value: 'SqueakerLoss', label: 'Squeaker loss (≤3)', kind: 'loss' },
  { value: 'NarrowMissAts', label: 'Missed cover by ≤1 (ATS)', kind: 'loss' },
  { value: 'WonButDidNotCover', label: 'Won game, missed cover (ATS)', kind: 'loss' },
  { value: 'GenericLoss', label: 'Generic loss', kind: 'loss' },
  { value: 'DogWin', label: 'Big dog won (≥10 dog)', kind: 'win' },
  { value: 'ChalkWin', label: 'Chalk win (≥14 fav)', kind: 'win' },
  { value: 'BlowoutWin', label: 'Blowout win (≥21)', kind: 'win' },
  { value: 'UglyWin', label: 'Ugly win (≤3)', kind: 'win' },
  { value: 'CoveredInDefeat', label: 'Covered in defeat (ATS)', kind: 'win' },
  { value: 'GenericWin', label: 'Generic win', kind: 'win' },
];

const situationLabel = (value) =>
  SITUATIONS.find(s => s.value === value)?.label ?? value;

const SPORT_OPTIONS = [
  { value: '', label: 'Any sport' },
  { value: 'FootballNcaa', label: 'NCAAFB' },
  { value: 'FootballNfl', label: 'NFL' },
  { value: 'BaseballMlb', label: 'MLB' },
];

const EMPTY_PHRASE_FORM = {
  voice: 'Smack',
  situation: 'GenericLoss',
  sport: '',
  text: '',
  isActive: true,
  requiresGamblingContent: false,
  weight: 1,
  description: '',
  rowVersion: null, // present only in edit mode; PUT requires the echo
};

/** 0–4 star picker. Stars are the training LABEL, so the widget makes the
 *  zero explicit rather than treating "no stars" as unrated. */
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

export default function AdminSmackLabPage() {
  const [tab, setTab] = useState('preview');

  // ── Preview tab state ─────────────────────────────────────────────────
  const [leagues, setLeagues] = useState([]);
  const [leagueId, setLeagueId] = useState('');
  const [voice, setVoice] = useState('Smack');
  const [picks, setPicks] = useState([]);          // SmackLabPickDto[]
  const [previews, setPreviews] = useState({});    // pickId -> SmackPreviewResultDto
  const [ratings, setRatings] = useState({});      // pickId -> stars, hydrated from the server
  const [allRatings, setAllRatings] = useState([]); // server rows for the league, every voice
  const [loadingPicks, setLoadingPicks] = useState(false);
  const [previewing, setPreviewing] = useState(false);

  // ── Phrase tab state ──────────────────────────────────────────────────
  const [phrases, setPhrases] = useState([]);
  const [phraseForm, setPhraseForm] = useState(EMPTY_PHRASE_FORM);
  const [editingId, setEditingId] = useState(null);
  const [savingPhrase, setSavingPhrase] = useState(false);

  const loadLeagues = useCallback(async () => {
    try {
      const res = await apiWrapper.Admin.getSmackLabLeagues();
      setLeagues(Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      toast.error(err?.message ?? 'Failed to load leagues');
    }
  }, []);

  const loadPhrases = useCallback(async () => {
    try {
      const res = await apiWrapper.Admin.getSmackPhrases();
      setPhrases(Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      toast.error(err?.message ?? 'Failed to load phrases');
    }
  }, []);

  useEffect(() => {
    loadLeagues();
    loadPhrases();
  }, [loadLeagues, loadPhrases]);

  // Fidelity note: preview batches the REAL fact payloads back through
  // Notification's send-path resolution, so what renders here is exactly
  // what a live push would have said for the chosen voice. Returns the
  // byPick map so callers can hydrate ratings against it.
  const runPreview = useCallback(async (pickList, selectedVoice) => {
    if (pickList.length === 0) {
      setPreviews({});
      return {};
    }
    setPreviewing(true);
    try {
      const res = await apiWrapper.Admin.smackLabPreview({
        voice: selectedVoice,
        picks: pickList.map(p => p.facts),
      });
      const byPick = {};
      for (const r of res.data ?? []) byPick[r.pickId] = r;
      setPreviews(byPick);
      return byPick;
    } catch (err) {
      toast.error(err?.message ?? 'Preview failed');
      return {};
    } finally {
      setPreviewing(false);
    }
  }, []);

  // A rating graded a specific rendered line for a specific pick. Hydrate a
  // star ONLY when the stored text still matches what the preview shows now —
  // if a phrase was edited (or selection changed) since the rating, showing
  // the old stars against the new line would mislabel the training signal, so
  // the row reads as unrated and invites a fresh opinion.
  const hydrateRatings = useCallback((serverRatings, previewMap, selectedVoice) => {
    const hydrated = {};
    for (const r of serverRatings) {
      if (r.voice !== selectedVoice) continue;
      const preview = previewMap[r.pickId];
      if (!preview) continue;
      const currentText = preview.text ?? '(standard copy)';
      if (r.renderedText === currentText) hydrated[r.pickId] = r.stars;
    }
    setRatings(hydrated);
  }, []);

  const handleSelectLeague = async (id) => {
    setLeagueId(id);
    setPicks([]);
    setPreviews({});
    setRatings({});
    if (!id) return;
    setLoadingPicks(true);
    try {
      const res = await apiWrapper.Admin.getSmackLabPicks(id);
      const list = Array.isArray(res.data) ? res.data : [];
      setPicks(list);
      const previewMap = await runPreview(list, voice);
      const ratingsRes = await apiWrapper.Admin.getSmackLabRatings(id);
      const serverRatings = Array.isArray(ratingsRes.data) ? ratingsRes.data : [];
      setAllRatings(serverRatings);
      hydrateRatings(serverRatings, previewMap, voice);
    } catch (err) {
      toast.error(err?.message ?? 'Failed to load picks');
    } finally {
      setLoadingPicks(false);
    }
  };

  const handleVoiceChange = async (v) => {
    setVoice(v);
    const previewMap = await runPreview(picks, v);
    // Ratings key on (pick, voice) server-side; re-hydrate this voice's
    // stars from the cached server rows against the fresh previews.
    hydrateRatings(allRatings, previewMap, v);
  };

  const handleRate = async (pick, stars) => {
    const preview = previews[pick.facts.pickId];
    if (!preview) return;
    try {
      await apiWrapper.Admin.rateSmackPreview({
        pickId: pick.facts.pickId,
        contestId: pick.facts.contestId,
        leagueId: pick.facts.leagueId,
        pickerUserId: pick.facts.userId,
        voice,
        situation: preview.situation,
        phraseId: preview.phraseId,
        renderedText: preview.text ?? '(standard copy)',
        stars,
        factsJson: JSON.stringify(pick.facts),
      });
      setRatings(r => ({ ...r, [pick.facts.pickId]: stars }));
      // Keep the server-row cache coherent so a voice flip round-trip
      // re-hydrates this rating without a refetch.
      setAllRatings(rows => [
        ...rows.filter(r => !(r.pickId === pick.facts.pickId && r.voice === voice)),
        {
          pickId: pick.facts.pickId,
          voice,
          situation: preview.situation,
          phraseId: preview.phraseId,
          renderedText: preview.text ?? '(standard copy)',
          stars,
        },
      ]);
    } catch (err) {
      toast.error(err?.message ?? 'Rating failed');
    }
  };

  // ── Phrase form ───────────────────────────────────────────────────────

  const resetPhraseForm = () => {
    setEditingId(null);
    setPhraseForm(EMPTY_PHRASE_FORM);
  };

  const handleEditPhrase = (p) => {
    setEditingId(p.id);
    setPhraseForm({
      voice: p.voice,
      situation: p.situation,
      sport: p.sport ?? '',
      text: p.text,
      isActive: p.isActive,
      requiresGamblingContent: p.requiresGamblingContent,
      weight: p.weight,
      description: p.description ?? '',
      rowVersion: p.rowVersion,
    });
  };

  const handleSavePhrase = async () => {
    setSavingPhrase(true);
    const body = {
      voice: phraseForm.voice,
      situation: phraseForm.situation,
      sport: phraseForm.sport || null,
      text: phraseForm.text,
      isActive: phraseForm.isActive,
      requiresGamblingContent: phraseForm.requiresGamblingContent,
      weight: Number(phraseForm.weight) || 1,
      description: phraseForm.description || null,
      rowVersion: phraseForm.rowVersion,
    };
    try {
      if (editingId) {
        await apiWrapper.Admin.updateSmackPhrase(editingId, body);
        toast.success('Phrase updated');
      } else {
        await apiWrapper.Admin.createSmackPhrase(body);
        toast.success('Phrase created');
      }
      resetPhraseForm();
      await loadPhrases();
      // A changed catalog changes previews — re-run so the preview tab
      // never shows stale lines, and re-hydrate: ratings whose rendered
      // text no longer matches drop back to unrated by design.
      if (picks.length > 0) {
        const previewMap = await runPreview(picks, voice);
        hydrateRatings(allRatings, previewMap, voice);
      }
    } catch (err) {
      if (err?.response?.status === 409) {
        // Stale xmin echo: someone (another tab, most likely you) edited
        // this phrase since it was loaded. Reload and re-apply.
        toast.error('This phrase changed since you loaded it — list refreshed, re-apply your edit.');
        await loadPhrases();
      } else {
        toast.error(err?.response?.data?.title ?? err?.message ?? 'Save failed');
      }
    } finally {
      setSavingPhrase(false);
    }
  };

  // ── Render ────────────────────────────────────────────────────────────

  const tabButton = (key, label) => (
    <button
      type="button"
      onClick={() => setTab(key)}
      style={{
        padding: '8px 14px',
        border: 'none',
        borderBottom: tab === key ? '3px solid var(--color-primary, #3498db)' : '3px solid transparent',
        background: 'none',
        cursor: 'pointer',
        fontWeight: tab === key ? 700 : 500,
        color: 'var(--text-primary)',
      }}
    >
      {label}
    </button>
  );

  return (
    <div className="admin-page">
      <AdminHeader />
      <h2 style={{ margin: '8px 0' }}>SmackBot Lab</h2>
      <div style={{ color: 'var(--text-secondary)', marginBottom: 12 }}>
        Preview what SmackBot would have pushed for scored picks, rate lines to
        build training data, and author new phrases. Nothing here is visible to
        users — the voice ships dark until it earns its stars.
      </div>

      <div style={{ borderBottom: '1px solid var(--border-primary)', marginBottom: 16 }}>
        {tabButton('preview', 'Preview & Rate')}
        {tabButton('phrases', `Phrases (${phrases.length})`)}
      </div>

      {tab === 'preview' && (
        <div>
          <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap', marginBottom: 12 }}>
            <select
              aria-label="League"
              value={leagueId}
              onChange={(e) => handleSelectLeague(e.target.value)}
              style={{ padding: '6px 8px', minWidth: 260 }}
            >
              <option value="">Select a league with scored picks…</option>
              {leagues.map(l => (
                <option key={l.leagueId} value={l.leagueId}>
                  {l.name} — {l.pickType}, {l.scoredPickCount} scored
                </option>
              ))}
            </select>
            <select
              aria-label="Voice"
              value={voice}
              onChange={(e) => handleVoiceChange(e.target.value)}
              style={{ padding: '6px 8px' }}
            >
              <option value="Smack">SmackBot</option>
              <option value="Standard">Standard (control)</option>
            </select>
            {(loadingPicks || previewing) && <span>Loading…</span>}
          </div>

          {leagueId && !loadingPicks && picks.length === 0 && (
            <div style={{ color: 'var(--text-secondary)' }}>
              No previewable picks — contests may not have finalized results yet.
            </div>
          )}

          {picks.length > 0 && (
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr style={{ textAlign: 'left', borderBottom: '2px solid var(--border-primary)' }}>
                  <th style={{ padding: 6 }}>Picker</th>
                  <th style={{ padding: 6 }}>Matchup</th>
                  <th style={{ padding: 6 }}>Pick</th>
                  <th style={{ padding: 6 }}>Result</th>
                  <th style={{ padding: 6 }}>Situation</th>
                  <th style={{ padding: 6 }}>SmackBot says…</th>
                  <th style={{ padding: 6 }}>Rating</th>
                </tr>
              </thead>
              <tbody>
                {picks.map((pick) => {
                  const preview = previews[pick.facts.pickId];
                  return (
                    <tr key={pick.facts.pickId} style={{ borderBottom: '1px solid var(--border-primary)' }}>
                      <td style={{ padding: 6 }}>{pick.pickerName}</td>
                      <td style={{ padding: 6, whiteSpace: 'nowrap' }}>{pick.matchupLabel}</td>
                      <td style={{ padding: 6 }}>{pick.pickLabel}</td>
                      <td style={{ padding: 6 }}>{pick.isCorrect ? '✓' : '✗'}</td>
                      <td style={{ padding: 6, fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
                        {preview ? situationLabel(preview.situation) : '…'}
                      </td>
                      <td style={{ padding: 6 }}>
                        {preview?.text ?? (
                          <span style={{ color: 'var(--text-secondary)', fontStyle: 'italic' }}>
                            — standard copy (no phrase for this situation yet)
                          </span>
                        )}
                      </td>
                      <td style={{ padding: 6 }}>
                        <StarRating
                          value={ratings[pick.facts.pickId] ?? null}
                          disabled={!preview}
                          onRate={(stars) => handleRate(pick, stars)}
                        />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      )}

      {tab === 'phrases' && (
        <div style={{ display: 'grid', gridTemplateColumns: 'minmax(320px, 1fr) minmax(320px, 1fr)', gap: 16 }}>
          {/* Catalog list */}
          <div>
            {phrases.length === 0 && (
              <div style={{ color: 'var(--text-secondary)' }}>
                Catalog is empty — every preview falls back to standard copy
                until lines exist. Author the first one on the right.
              </div>
            )}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {phrases.map((p) => (
                <div
                  key={p.id}
                  style={{
                    border: '1px solid var(--border-primary)',
                    borderLeft: p.isActive ? '4px solid var(--color-success, #27ae60)' : '4px solid var(--text-secondary)',
                    borderRadius: 6,
                    padding: 10,
                    opacity: p.isActive ? 1 : 0.6,
                    background: editingId === p.id ? 'var(--table-stripe)' : 'transparent',
                  }}
                >
                  <div style={{ display: 'flex', gap: 8, alignItems: 'baseline', flexWrap: 'wrap', fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                    <span style={{ fontWeight: 700 }}>{situationLabel(p.situation)}</span>
                    {p.sport && <span>{SPORT_OPTIONS.find(o => o.value === p.sport)?.label ?? p.sport}</span>}
                    {p.requiresGamblingContent && <span title="Only shown where spread talk is allowed">🎲 gambling</span>}
                    {p.weight > 1 && <span>w{p.weight}</span>}
                    {!p.isActive && <span>INACTIVE</span>}
                  </div>
                  <div style={{ margin: '4px 0' }}>{p.text}</div>
                  <button type="button" onClick={() => handleEditPhrase(p)} style={{ fontSize: '0.8rem' }}>
                    Edit
                  </button>
                </div>
              ))}
            </div>
          </div>

          {/* Editor */}
          <div style={{ border: '1px solid var(--border-primary)', borderRadius: 6, padding: 12, alignSelf: 'start' }}>
            <div style={{ fontWeight: 700, marginBottom: 8 }}>
              {editingId ? 'Edit phrase' : 'New phrase'}
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              <select
                aria-label="Situation"
                value={phraseForm.situation}
                onChange={(e) => setPhraseForm(f => ({ ...f, situation: e.target.value }))}
                style={{ padding: '6px 8px' }}
              >
                <optgroup label="Losses">
                  {SITUATIONS.filter(s => s.kind === 'loss').map(s => (
                    <option key={s.value} value={s.value}>{s.label}</option>
                  ))}
                </optgroup>
                <optgroup label="Wins">
                  {SITUATIONS.filter(s => s.kind === 'win').map(s => (
                    <option key={s.value} value={s.value}>{s.label}</option>
                  ))}
                </optgroup>
              </select>

              <select
                aria-label="Sport scope"
                value={phraseForm.sport}
                onChange={(e) => setPhraseForm(f => ({ ...f, sport: e.target.value }))}
                style={{ padding: '6px 8px' }}
              >
                {SPORT_OPTIONS.map(o => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>

              <textarea
                aria-label="Phrase text"
                value={phraseForm.text}
                onChange={(e) => setPhraseForm(f => ({ ...f, text: e.target.value }))}
                maxLength={300}
                rows={3}
                placeholder={'The line. Tokens: {Team} {Opponent} {Score} {OpponentScore} {Margin} {League} {Line}'}
                style={{ padding: '6px 8px', fontFamily: 'inherit' }}
              />
              <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}>
                {phraseForm.text.length}/300 · Mock the pick, never the person ·
                no profanity · wins get grudging credit, not praise
              </div>

              <div style={{ display: 'flex', gap: 14, alignItems: 'center', flexWrap: 'wrap' }}>
                <label style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                  <input
                    type="checkbox"
                    checked={phraseForm.requiresGamblingContent}
                    onChange={(e) => setPhraseForm(f => ({ ...f, requiresGamblingContent: e.target.checked }))}
                  />
                  references the betting line
                </label>
                <label style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                  <input
                    type="checkbox"
                    checked={phraseForm.isActive}
                    onChange={(e) => setPhraseForm(f => ({ ...f, isActive: e.target.checked }))}
                  />
                  active
                </label>
                <label style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                  weight
                  <input
                    type="number"
                    min={1}
                    value={phraseForm.weight}
                    onChange={(e) => setPhraseForm(f => ({ ...f, weight: e.target.value }))}
                    style={{ width: 56, padding: '4px 6px' }}
                  />
                </label>
              </div>

              <input
                type="text"
                aria-label="Description"
                value={phraseForm.description}
                onChange={(e) => setPhraseForm(f => ({ ...f, description: e.target.value }))}
                maxLength={256}
                placeholder="operator note (optional)"
                style={{ padding: '6px 8px' }}
              />

              <div style={{ display: 'flex', gap: 8 }}>
                <button
                  type="button"
                  disabled={savingPhrase || !phraseForm.text.trim()}
                  onClick={handleSavePhrase}
                >
                  {editingId ? 'Save changes' : 'Create phrase'}
                </button>
                {editingId && (
                  <button type="button" onClick={resetPhraseForm} disabled={savingPhrase}>
                    Cancel
                  </button>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
