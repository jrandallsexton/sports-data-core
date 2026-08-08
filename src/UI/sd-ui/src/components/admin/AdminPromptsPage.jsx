import React, { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import './AdminPage.css';
import AdminHeader from './AdminHeader';
import apiWrapper from '../../api/apiWrapper';

const SPORT_OPTIONS = [
  { value: '', label: 'Any sport' },
  { value: 'FootballNcaa', label: 'NCAAFB' },
  { value: 'FootballNfl', label: 'NFL' },
  { value: 'BaseballMlb', label: 'MLB' },
];

const sportLabel = (sport) =>
  SPORT_OPTIONS.find(o => o.value === (sport ?? ''))?.label ?? String(sport);

const EMPTY_FORM = {
  name: '',
  sport: '',
  withStats: false,
  isDefault: false,
  description: '',
  text: '',
};

/**
 * Prompt Manager — CRUD over the per-sport-league Prompt entities that
 * drive matchup-preview generation (e.g. an NCAAFB prompt that discusses
 * AP/CFP rankings vs an NFL prompt that never mentions them). Name and
 * slot (sport, withStats) are immutable after creation; "New version
 * from this" is the replace workflow, and Set default flips which
 * version is live for a slot — effective next run, no deploy. Prompt
 * text lives in the API database only (the repo is public; the DB is
 * not) — treat it as the secret sauce it is.
 */
export default function AdminPromptsPage() {
  const [prompts, setPrompts] = useState([]);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  // Editor state: mode 'create' (form holds everything) or 'edit'
  // (selected prompt id; only description/text are editable).
  const [mode, setMode] = useState('create');
  const [selectedId, setSelectedId] = useState(null);
  const [form, setForm] = useState(EMPTY_FORM);
  const [importForm, setImportForm] = useState({
    blobName: '',
    sport: '',
    withStats: false,
    isDefault: false,
  });

  const loadPrompts = useCallback(async () => {
    setLoading(true);
    try {
      const res = await apiWrapper.Admin.getPrompts();
      setPrompts(Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      toast.error(err?.message ?? 'Failed to load prompts');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadPrompts();
  }, [loadPrompts]);

  const resetToCreate = (prefill = EMPTY_FORM) => {
    setMode('create');
    setSelectedId(null);
    setForm(prefill);
  };

  const handleSelect = async (promptId) => {
    try {
      const res = await apiWrapper.Admin.getPrompt(promptId);
      const p = res.data;
      setMode('edit');
      setSelectedId(promptId);
      setForm({
        name: p.name,
        sport: p.sport ?? '',
        withStats: p.withStats,
        isDefault: p.isDefault,
        description: p.description ?? '',
        text: p.text,
      });
    } catch (err) {
      toast.error(err?.message ?? 'Failed to load prompt');
    }
  };

  const handleNewVersionFrom = () => {
    // Replace workflow: same slot and text, new name; operator edits then
    // creates (optionally as the new default).
    resetToCreate({
      ...form,
      name: `${form.name}-v`,
      isDefault: false,
    });
    toast('Editing a NEW version — give it a name and save.', { icon: '📝' });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!form.text.trim()) {
      toast.error('Prompt text cannot be empty.');
      return;
    }
    setSubmitting(true);
    try {
      if (mode === 'edit') {
        await apiWrapper.Admin.updatePrompt(selectedId, {
          description: form.description || null,
          text: form.text,
        });
        toast.success('Prompt updated.');
      } else {
        if (!form.name.trim()) {
          toast.error('Name is required.');
          return;
        }
        await apiWrapper.Admin.createPrompt({
          name: form.name.trim(),
          sport: form.sport || null,
          withStats: form.withStats,
          isDefault: form.isDefault,
          description: form.description || null,
          text: form.text,
        });
        toast.success('Prompt created.');
        resetToCreate();
      }
      loadPrompts();
    } catch (err) {
      toast.error(
        err?.response?.data?.errors?.[0]?.errorMessage
          ?? err.message
          ?? 'Save failed'
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleSetDefault = async (promptId) => {
    setSubmitting(true);
    try {
      await apiWrapper.Admin.setDefaultPrompt(promptId);
      toast.success('Default flipped — effective on the next run.');
      loadPrompts();
      if (selectedId === promptId) setForm(f => ({ ...f, isDefault: true }));
    } catch (err) {
      toast.error(err?.message ?? 'Set default failed');
    } finally {
      setSubmitting(false);
    }
  };

  const handleImport = async () => {
    setSubmitting(true);
    try {
      await apiWrapper.Admin.importPromptFromBlob({
        blobName: importForm.blobName.trim(),
        sport: importForm.sport || null,
        withStats: importForm.withStats,
        isDefault: importForm.isDefault,
      });
      toast.success('Imported.');
      setImportForm(f => ({ ...f, blobName: '' }));
      loadPrompts();
    } catch (err) {
      toast.error(
        err?.response?.data?.errors?.[0]?.errorMessage
          ?? err.message
          ?? 'Import failed'
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleCopyId = async (promptId) => {
    try {
      await navigator.clipboard.writeText(promptId);
      toast.success('Prompt ID copied — paste it into the Preview Lab.');
    } catch {
      toast.error('Copy failed.');
    }
  };

  return (
    <div className="admin-page">
      <AdminHeader />
      <div style={{ maxWidth: 1200, margin: '0 auto' }}>
        <h2 style={{ marginBottom: 4 }}>Prompt Manager</h2>
        <p style={{ color: 'var(--text-secondary)', marginTop: 0 }}>
          Per-sport-league prompts for preview generation. Name and slot are
          immutable — use “New version from this” to replace, and “Set
          default” to flip what's live (next run, no deploy). Copy an ID
          into the Preview Lab to experiment before promoting.
        </p>

        <div style={{ display: 'grid', gridTemplateColumns: 'minmax(320px, 1fr) minmax(420px, 1.4fr)', gap: 20, alignItems: 'start' }}>
          {/* Left: the registry */}
          <section>
            {loading && <div>Loading prompts…</div>}
            {!loading && prompts.length === 0 && (
              <div style={{ color: 'var(--text-secondary)' }}>
                No prompts yet — create one, or seed from the legacy blobs
                below.
              </div>
            )}

            <details style={{ marginBottom: 12 }}>
              <summary style={{ cursor: 'pointer', fontWeight: 600 }}>
                Import from legacy blob
              </summary>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: '10px 0' }}>
                <input
                  type="text"
                  aria-label="Blob name"
                  value={importForm.blobName}
                  onChange={(e) => setImportForm(f => ({ ...f, blobName: e.target.value }))}
                  placeholder="blob name — e.g. prediction-insights-v1"
                  style={{ padding: '6px 8px' }}
                />
                <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
                  <select
                    aria-label="Sport scope for imported prompt"
                    value={importForm.sport}
                    onChange={(e) => setImportForm(f => ({ ...f, sport: e.target.value }))}
                    style={{ padding: '6px 8px' }}
                  >
                    {SPORT_OPTIONS.map(o => (
                      <option key={o.value} value={o.value}>{o.label}</option>
                    ))}
                  </select>
                  <label style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                    <input
                      type="checkbox"
                      checked={importForm.withStats}
                      onChange={(e) => setImportForm(f => ({ ...f, withStats: e.target.checked }))}
                    />
                    with-stats variant
                  </label>
                  <label style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                    <input
                      type="checkbox"
                      checked={importForm.isDefault}
                      onChange={(e) => setImportForm(f => ({ ...f, isDefault: e.target.checked }))}
                    />
                    make default
                  </label>
                  <button type="button" disabled={submitting || !importForm.blobName.trim()} onClick={handleImport}>
                    Import
                  </button>
                </div>
              </div>
            </details>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {prompts.map((p) => (
                <div
                  key={p.id}
                  style={{
                    border: '1px solid var(--border-primary)',
                    borderLeft: p.isDefault ? '4px solid var(--color-success, #27ae60)' : '1px solid var(--border-primary)',
                    borderRadius: 6,
                    padding: 10,
                    background: selectedId === p.id ? 'var(--table-stripe)' : 'transparent',
                  }}
                >
                  <div style={{ display: 'flex', gap: 8, alignItems: 'baseline', flexWrap: 'wrap' }}>
                    <button
                      type="button"
                      onClick={() => handleSelect(p.id)}
                      style={{ fontWeight: 600, background: 'none', border: 'none', cursor: 'pointer', padding: 0, color: 'var(--text-primary)' }}
                      title="Open in the editor"
                    >
                      {p.name}
                    </button>
                    {p.isDefault && <span style={{ color: 'var(--color-success, #27ae60)', fontSize: '0.8rem', fontWeight: 700 }}>DEFAULT</span>}
                  </div>
                  <div style={{ fontSize: '0.85rem', color: 'var(--text-secondary)', display: 'flex', gap: 10, flexWrap: 'wrap', marginTop: 4 }}>
                    <span>{sportLabel(p.sport)}</span>
                    <span>{p.withStats ? 'with stats' : 'no stats'}</span>
                    <span>{p.textLength?.toLocaleString()} chars</span>
                  </div>
                  <div style={{ display: 'flex', gap: 8, marginTop: 6 }}>
                    {!p.isDefault && (
                      <button type="button" disabled={submitting} onClick={() => handleSetDefault(p.id)}>
                        Set default
                      </button>
                    )}
                    <button type="button" onClick={() => handleCopyId(p.id)}>Copy ID</button>
                  </div>
                </div>
              ))}
            </div>
          </section>

          {/* Right: the editor */}
          <section>
            <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                <strong>{mode === 'edit' ? `Editing: ${form.name}` : 'New prompt'}</strong>
                {mode === 'edit' && (
                  <>
                    <button type="button" onClick={handleNewVersionFrom}>New version from this</button>
                    <button type="button" onClick={() => resetToCreate()}>Cancel</button>
                  </>
                )}
              </div>

              {mode === 'create' && (
                <>
                  <input
                    type="text"
                    aria-label="Prompt name"
                    value={form.name}
                    onChange={(e) => setForm(f => ({ ...f, name: e.target.value }))}
                    placeholder="name (immutable, unique — e.g. prediction-insights-nfl-v2)"
                    style={{ padding: '6px 8px' }}
                  />
                  <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
                    <select
                      aria-label="Sport scope"
                      value={form.sport}
                      onChange={(e) => setForm(f => ({ ...f, sport: e.target.value }))}
                      style={{ padding: '6px 8px' }}
                      title="Sport/league scope — a sport-specific default outranks the any-sport default"
                    >
                      {SPORT_OPTIONS.map(o => (
                        <option key={o.value} value={o.value}>{o.label}</option>
                      ))}
                    </select>
                    <label style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                      <input
                        type="checkbox"
                        checked={form.withStats}
                        onChange={(e) => setForm(f => ({ ...f, withStats: e.target.checked }))}
                      />
                      with-stats variant
                    </label>
                    <label style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                      <input
                        type="checkbox"
                        checked={form.isDefault}
                        onChange={(e) => setForm(f => ({ ...f, isDefault: e.target.checked }))}
                      />
                      make default for its slot
                    </label>
                  </div>
                </>
              )}

              <input
                type="text"
                aria-label="Description"
                value={form.description}
                onChange={(e) => setForm(f => ({ ...f, description: e.target.value }))}
                placeholder="description (optional operator note)"
                style={{ padding: '6px 8px' }}
              />

              <textarea
                aria-label="Prompt instruction text"
                value={form.text}
                onChange={(e) => setForm(f => ({ ...f, text: e.target.value }))}
                placeholder="prompt instruction text"
                rows={24}
                spellCheck={false}
                style={{
                  fontFamily: 'monospace',
                  fontSize: '0.85rem',
                  padding: 10,
                  whiteSpace: 'pre',
                  overflowX: 'auto',
                }}
              />

              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <button type="submit" disabled={submitting}>
                  {mode === 'edit' ? 'Save changes' : 'Create prompt'}
                </button>
                <span style={{ color: 'var(--text-secondary)', fontSize: '0.85rem' }}>
                  {form.text.length.toLocaleString()} chars (~{Math.round(form.text.length / 4).toLocaleString()} tokens)
                </span>
              </div>
            </form>
          </section>
        </div>
      </div>
    </div>
  );
}
