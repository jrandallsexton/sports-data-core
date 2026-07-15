import { useState, useEffect } from "react";
import "./ImportPicksDialog.css";

/**
 * Import-picks dialog. A single import always draws from ONE source league:
 * with multiple candidates the user picks one from the dropdown, then the
 * checkbox list shows that league's importable picks (all checked by default).
 * `sources` is [{ leagueId, name, items: [{ contestId, franchiseSeasonId, team,
 * matchupLabel }] }], already enriched and filtered to useful sources by the parent.
 */
function ImportPicksDialog({ isOpen, sources, importing, onClose, onImport }) {
  const [selectedSourceId, setSelectedSourceId] = useState(sources[0]?.leagueId ?? null);

  const currentSource =
    sources.find((s) => s.leagueId === selectedSourceId) ?? sources[0] ?? null;
  const items = currentSource?.items ?? [];

  const [selected, setSelected] = useState(() => new Set(items.map((i) => i.contestId)));

  // Close on Escape (unless mid-import), matching the app's Gallery modal.
  useEffect(() => {
    const onKey = (e) => {
      if (e.key === "Escape" && !importing) onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [importing, onClose]);

  if (!isOpen || !currentSource) return null;

  // Switch source and re-seed the checkboxes to all of that source's picks.
  // Done here (on the user's action) rather than in an effect so a background
  // data refresh can never wipe in-progress checkbox edits.
  const changeSource = (leagueId) => {
    setSelectedSourceId(leagueId);
    const src = sources.find((s) => s.leagueId === leagueId);
    setSelected(new Set((src?.items ?? []).map((i) => i.contestId)));
  };

  const toggle = (contestId) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(contestId)) next.delete(contestId);
      else next.add(contestId);
      return next;
    });
  };

  const allSelected = items.length > 0 && items.every((i) => selected.has(i.contestId));
  const toggleAll = () =>
    setSelected(allSelected ? new Set() : new Set(items.map((i) => i.contestId)));

  const count = selected.size;

  return (
    <div className="import-dialog-overlay" onClick={importing ? undefined : onClose}>
      <div
        className="import-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="import-dialog-title"
        onClick={(e) => e.stopPropagation()}
      >
        <h3 id="import-dialog-title" className="import-dialog-title">
          Import picks
        </h3>

        {sources.length > 1 ? (
          <div className="import-dialog-source">
            <label htmlFor="import-source">Import from</label>
            <select
              id="import-source"
              value={selectedSourceId}
              onChange={(e) => changeSource(e.target.value)}
              disabled={importing}
            >
              {sources.map((s) => (
                <option key={s.leagueId} value={s.leagueId}>
                  {s.name}
                </option>
              ))}
            </select>
          </div>
        ) : (
          <p className="import-dialog-message">
            Copy your picks from <strong>{currentSource.name}</strong>. Uncheck any you
            don&rsquo;t want.
          </p>
        )}

        <label className="import-dialog-selectall">
          <input type="checkbox" checked={allSelected} onChange={toggleAll} />
          Select all
        </label>

        <div className="import-dialog-list">
          {items.map((item) => (
            <label key={item.contestId} className="import-dialog-row">
              <input
                type="checkbox"
                checked={selected.has(item.contestId)}
                onChange={() => toggle(item.contestId)}
              />
              <span className="import-dialog-row-matchup">{item.matchupLabel}</span>
              <span className="import-dialog-row-team">{item.team}</span>
            </label>
          ))}
        </div>

        <div className="import-dialog-buttons">
          <button
            className="import-dialog-button cancel"
            onClick={onClose}
            disabled={importing}
          >
            Cancel
          </button>
          <button
            className="import-dialog-button confirm"
            onClick={() => onImport(currentSource.leagueId, [...selected])}
            disabled={importing || count === 0}
          >
            {importing ? "Importing…" : `Import ${count} pick${count === 1 ? "" : "s"}`}
          </button>
        </div>
      </div>
    </div>
  );
}

export default ImportPicksDialog;
