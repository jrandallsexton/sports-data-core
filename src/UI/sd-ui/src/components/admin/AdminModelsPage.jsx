import React, { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import './AdminPage.css';
import AdminHeader from './AdminHeader';
import apiWrapper from '../../api/apiWrapper';

const PROVIDER_KINDS = [
  { value: 'DeepSeek', label: 'DeepSeek' },
  { value: 'Anthropic', label: 'Anthropic' },
  { value: 'OpenAi', label: 'OpenAI' },
  { value: 'Google', label: 'Google' },
];

// 2025 season window for the risk chip (see llm-training-dates.md):
// NCAA wk0 Aug 23, 2025 -> Super Bowl Feb 8, 2026. A cutoff BEFORE the
// window start is lower-risk for the whole season; inside the window is
// partial; after (or unpublished) is higher-risk.
const SEASON_START = new Date('2025-08-23T00:00:00Z');
const SEASON_END = new Date('2026-02-09T00:00:00Z');

function riskFor2025(cutoffUtc) {
  if (!cutoffUtc) return { label: 'UNPUBLISHED — treat higher-risk', color: 'var(--color-danger, #c0392b)' };
  const cutoff = new Date(cutoffUtc);
  if (cutoff < SEASON_START) return { label: 'LOWER-RISK (full 2025 season)', color: 'var(--color-success, #27ae60)' };
  if (cutoff < SEASON_END) return { label: 'PARTIAL — games before cutoff contaminated', color: 'var(--color-warning, #e67e22)' };
  return { label: 'HIGHER-RISK (full 2025 season in training)', color: 'var(--color-danger, #c0392b)' };
}

const EMPTY_FORM = {
  modelProviderId: '',
  name: '',
  apiModelId: '',
  releaseDate: '',
  knowledgeCutoffUtc: '',
  cutoffEvidence: '',
  cutoffVerifiedUtc: '',
  inputCostPerMTok: '',
  outputCostPerMTok: '',
  isDefault: false,
  isActive: true,
};

const toDateInput = (iso) => (iso ? iso.substring(0, 10) : '');
const fromDateInput = (v) => (v ? `${v}T00:00:00Z` : null);

/**
 * Model Manager — providers + model identity records that drive the
 * experiment harness and production routing. Cutoffs are DECLARED
 * classification inputs (verify + record evidence); the risk chip
 * compares them against the 2025-season window client-side, and the
 * scoring harness does the same per-game server-side. The DEFAULT badge
 * marks THE production model — pre-season selection and in-season swaps
 * are one Set-default click.
 */
export default function AdminModelsPage() {
  const [providers, setProviders] = useState([]);
  const [models, setModels] = useState([]);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const [mode, setMode] = useState('create'); // create | edit
  const [selectedId, setSelectedId] = useState(null);
  const [form, setForm] = useState(EMPTY_FORM);
  const [newProvider, setNewProvider] = useState({ name: '', kind: 'DeepSeek', description: '' });

  const loadAll = useCallback(async () => {
    setLoading(true);
    try {
      const [prov, mods] = await Promise.all([
        apiWrapper.Admin.getModelProviders(),
        apiWrapper.Admin.getModels(),
      ]);
      setProviders(Array.isArray(prov.data) ? prov.data : []);
      setModels(Array.isArray(mods.data) ? mods.data : []);
    } catch (err) {
      toast.error(err?.message ?? 'Failed to load models');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadAll();
  }, [loadAll]);

  const resetToCreate = () => {
    setMode('create');
    setSelectedId(null);
    setForm(EMPTY_FORM);
  };

  const handleSelect = (m) => {
    setMode('edit');
    setSelectedId(m.id);
    setForm({
      modelProviderId: m.modelProviderId,
      name: m.name,
      apiModelId: m.apiModelId,
      releaseDate: toDateInput(m.releaseDate),
      knowledgeCutoffUtc: toDateInput(m.knowledgeCutoffUtc),
      cutoffEvidence: m.cutoffEvidence ?? '',
      cutoffVerifiedUtc: toDateInput(m.cutoffVerifiedUtc),
      inputCostPerMTok: m.inputCostPerMTok ?? '',
      outputCostPerMTok: m.outputCostPerMTok ?? '',
      isDefault: m.isDefault,
      isActive: m.isActive,
    });
  };

  const handleCreateProvider = async () => {
    if (!newProvider.name.trim()) {
      toast.error('Provider name is required.');
      return;
    }
    setSubmitting(true);
    try {
      await apiWrapper.Admin.createModelProvider({
        name: newProvider.name.trim(),
        kind: newProvider.kind,
        description: newProvider.description || null,
      });
      toast.success('Provider created.');
      setNewProvider({ name: '', kind: 'DeepSeek', description: '' });
      loadAll();
    } catch (err) {
      toast.error(err?.response?.data?.errors?.[0]?.errorMessage ?? err.message ?? 'Create failed');
    } finally {
      setSubmitting(false);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      if (mode === 'edit') {
        await apiWrapper.Admin.updateModel(selectedId, {
          releaseDate: fromDateInput(form.releaseDate),
          knowledgeCutoffUtc: fromDateInput(form.knowledgeCutoffUtc),
          cutoffEvidence: form.cutoffEvidence || null,
          cutoffVerifiedUtc: fromDateInput(form.cutoffVerifiedUtc),
          inputCostPerMTok: form.inputCostPerMTok === '' ? null : Number(form.inputCostPerMTok),
          outputCostPerMTok: form.outputCostPerMTok === '' ? null : Number(form.outputCostPerMTok),
          isActive: form.isActive,
        });
        toast.success('Model updated.');
      } else {
        if (!form.modelProviderId || !form.name.trim() || !form.apiModelId.trim()) {
          toast.error('Provider, name, and API model id are required.');
          return;
        }
        await apiWrapper.Admin.createModel({
          modelProviderId: form.modelProviderId,
          name: form.name.trim(),
          apiModelId: form.apiModelId.trim(),
          releaseDate: fromDateInput(form.releaseDate),
          knowledgeCutoffUtc: fromDateInput(form.knowledgeCutoffUtc),
          cutoffEvidence: form.cutoffEvidence || null,
          cutoffVerifiedUtc: fromDateInput(form.cutoffVerifiedUtc),
          inputCostPerMTok: form.inputCostPerMTok === '' ? null : Number(form.inputCostPerMTok),
          outputCostPerMTok: form.outputCostPerMTok === '' ? null : Number(form.outputCostPerMTok),
          isDefault: form.isDefault,
        });
        toast.success('Model created.');
        resetToCreate();
      }
      loadAll();
    } catch (err) {
      toast.error(err?.response?.data?.errors?.[0]?.errorMessage ?? err.message ?? 'Save failed');
    } finally {
      setSubmitting(false);
    }
  };

  const handleSetDefault = async (modelId) => {
    setSubmitting(true);
    try {
      await apiWrapper.Admin.setDefaultModel(modelId);
      toast.success('Production default flipped — effective on the next run.');
      loadAll();
    } catch (err) {
      toast.error(err?.response?.data?.errors?.[0]?.errorMessage ?? err.message ?? 'Set default failed');
    } finally {
      setSubmitting(false);
    }
  };

  const handleCopyId = async (modelId) => {
    try {
      await navigator.clipboard.writeText(modelId);
      toast.success('Model ID copied.');
    } catch {
      toast.error('Copy failed.');
    }
  };

  return (
    <div className="admin-page">
      <AdminHeader />
      <div style={{ maxWidth: 1200, margin: '0 auto' }}>
        <h2 style={{ marginBottom: 4 }}>Model Manager</h2>
        <p style={{ color: 'var(--text-secondary)', marginTop: 0 }}>
          Provider fleets and model identity records for the experiment
          harness. Cutoffs are declared, not proven — verify and record
          evidence. Seed data: docs/metrics-modeling/llm-training-dates.md.
        </p>

        <details style={{ marginBottom: 16 }}>
          <summary style={{ cursor: 'pointer', fontWeight: 600 }}>
            Providers ({providers.length})
          </summary>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: '10px 0' }}>
            {providers.map(p => (
              <div key={p.id} style={{ fontSize: '0.9rem' }}>
                <strong>{p.name}</strong> — {p.kind} · {p.modelCount} model(s)
              </div>
            ))}
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <input
                type="text"
                aria-label="New provider name"
                value={newProvider.name}
                onChange={(e) => setNewProvider(f => ({ ...f, name: e.target.value }))}
                placeholder="provider name (e.g. Anthropic)"
                style={{ padding: '6px 8px' }}
              />
              <select
                aria-label="Provider kind (client implementation)"
                value={newProvider.kind}
                onChange={(e) => setNewProvider(f => ({ ...f, kind: e.target.value }))}
                style={{ padding: '6px 8px' }}
              >
                {PROVIDER_KINDS.map(k => (
                  <option key={k.value} value={k.value}>{k.label}</option>
                ))}
              </select>
              <button type="button" disabled={submitting} onClick={handleCreateProvider}>
                Add provider
              </button>
            </div>
          </div>
        </details>

        <div style={{ display: 'grid', gridTemplateColumns: 'minmax(360px, 1.1fr) minmax(380px, 1fr)', gap: 20, alignItems: 'start' }}>
          {/* Left: the registry */}
          <section>
            {loading && <div>Loading models…</div>}
            {!loading && models.length === 0 && (
              <div style={{ color: 'var(--text-secondary)' }}>
                No models yet — add a provider above, then create models
                from the registry doc.
              </div>
            )}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {models.map((m) => {
                const risk = riskFor2025(m.knowledgeCutoffUtc);
                return (
                  <div
                    key={m.id}
                    style={{
                      border: '1px solid var(--border-primary)',
                      borderLeft: m.isDefault ? '4px solid var(--color-success, #27ae60)' : '1px solid var(--border-primary)',
                      borderRadius: 6,
                      padding: 10,
                      opacity: m.isActive ? 1 : 0.55,
                      background: selectedId === m.id ? 'var(--table-stripe)' : 'transparent',
                    }}
                  >
                    <div style={{ display: 'flex', gap: 8, alignItems: 'baseline', flexWrap: 'wrap' }}>
                      <strong>{m.name}</strong>
                      <span style={{ color: 'var(--text-secondary)', fontSize: '0.85rem' }}>{m.providerName}</span>
                      {m.isDefault && <span style={{ color: 'var(--color-success, #27ae60)', fontSize: '0.8rem', fontWeight: 700 }}>PRODUCTION DEFAULT</span>}
                      {!m.isActive && <span style={{ fontSize: '0.8rem', fontWeight: 700, color: 'var(--text-secondary)' }}>INACTIVE</span>}
                    </div>
                    <div style={{ fontSize: '0.85rem', color: 'var(--text-secondary)', marginTop: 4, display: 'flex', gap: 10, flexWrap: 'wrap' }}>
                      <span>{m.apiModelId}</span>
                      <span>cutoff: {m.knowledgeCutoffUtc ? m.knowledgeCutoffUtc.substring(0, 10) : '—'}</span>
                      <span>{m.cutoffVerifiedUtc ? `verified ${m.cutoffVerifiedUtc.substring(0, 10)}` : 'UNVERIFIED'}</span>
                    </div>
                    <div style={{ fontSize: '0.8rem', fontWeight: 700, color: risk.color, marginTop: 4 }}>
                      {risk.label}
                    </div>
                    <div style={{ display: 'flex', gap: 8, marginTop: 6, flexWrap: 'wrap' }}>
                      <button type="button" onClick={() => handleSelect(m)}>Open</button>
                      {!m.isDefault && m.isActive && (
                        <button type="button" disabled={submitting} onClick={() => handleSetDefault(m.id)}>
                          Set default
                        </button>
                      )}
                      <button type="button" onClick={() => handleCopyId(m.id)}>Copy ID</button>
                    </div>
                  </div>
                );
              })}
            </div>
          </section>

          {/* Right: the editor */}
          <section>
            <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                <strong>{mode === 'edit' ? `Editing metadata: ${form.name}` : 'New model'}</strong>
                {mode === 'edit' && (
                  <button type="button" onClick={resetToCreate}>Cancel</button>
                )}
              </div>
              {mode === 'edit' && (
                <div style={{ color: 'var(--text-secondary)', fontSize: '0.85rem' }}>
                  Identity (name, API id, provider) is immutable — a
                  different API identifier is a different model.
                </div>
              )}

              {mode === 'create' && (
                <>
                  <select
                    aria-label="Provider"
                    value={form.modelProviderId}
                    onChange={(e) => setForm(f => ({ ...f, modelProviderId: e.target.value }))}
                    style={{ padding: '6px 8px' }}
                  >
                    <option value="">— provider —</option>
                    {providers.map(p => (
                      <option key={p.id} value={p.id}>{p.name}</option>
                    ))}
                  </select>
                  <input
                    type="text"
                    aria-label="Model display name"
                    value={form.name}
                    onChange={(e) => setForm(f => ({ ...f, name: e.target.value }))}
                    placeholder="display name (e.g. Claude Haiku 4.5)"
                    style={{ padding: '6px 8px' }}
                  />
                  <input
                    type="text"
                    aria-label="API model id"
                    value={form.apiModelId}
                    onChange={(e) => setForm(f => ({ ...f, apiModelId: e.target.value }))}
                    placeholder="exact API id (e.g. claude-haiku-4-5)"
                    style={{ padding: '6px 8px', fontFamily: 'monospace' }}
                  />
                </>
              )}

              <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}>
                <label style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: '0.9rem' }}>
                  released
                  <input type="date" aria-label="Release date" value={form.releaseDate}
                    onChange={(e) => setForm(f => ({ ...f, releaseDate: e.target.value }))} />
                </label>
                <label style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: '0.9rem' }}>
                  training cutoff
                  <input type="date" aria-label="Knowledge cutoff" value={form.knowledgeCutoffUtc}
                    onChange={(e) => setForm(f => ({ ...f, knowledgeCutoffUtc: e.target.value }))} />
                </label>
                <label style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: '0.9rem' }}>
                  verified
                  <input type="date" aria-label="Cutoff verified date" value={form.cutoffVerifiedUtc}
                    onChange={(e) => setForm(f => ({ ...f, cutoffVerifiedUtc: e.target.value }))} />
                </label>
              </div>

              <input
                type="text"
                aria-label="Cutoff evidence"
                value={form.cutoffEvidence}
                onChange={(e) => setForm(f => ({ ...f, cutoffEvidence: e.target.value }))}
                placeholder="cutoff evidence (doc URL / note)"
                style={{ padding: '6px 8px' }}
              />

              <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}>
                <label style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: '0.9rem' }}>
                  $/MTok in
                  <input type="number" step="0.0001" min="0" aria-label="Input cost per million tokens"
                    value={form.inputCostPerMTok}
                    onChange={(e) => setForm(f => ({ ...f, inputCostPerMTok: e.target.value }))}
                    style={{ width: 100, padding: '4px 6px' }} />
                </label>
                <label style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: '0.9rem' }}>
                  $/MTok out
                  <input type="number" step="0.0001" min="0" aria-label="Output cost per million tokens"
                    value={form.outputCostPerMTok}
                    onChange={(e) => setForm(f => ({ ...f, outputCostPerMTok: e.target.value }))}
                    style={{ width: 100, padding: '4px 6px' }} />
                </label>
                {mode === 'create' ? (
                  <label style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                    <input type="checkbox" checked={form.isDefault}
                      onChange={(e) => setForm(f => ({ ...f, isDefault: e.target.checked }))} />
                    make production default
                  </label>
                ) : (
                  <label style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                    <input type="checkbox" checked={form.isActive}
                      onChange={(e) => setForm(f => ({ ...f, isActive: e.target.checked }))} />
                    active
                  </label>
                )}
              </div>

              <div>
                <button type="submit" disabled={submitting}>
                  {mode === 'edit' ? 'Save metadata' : 'Create model'}
                </button>
              </div>
            </form>
          </section>
        </div>
      </div>
    </div>
  );
}
