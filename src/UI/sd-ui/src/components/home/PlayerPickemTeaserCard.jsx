import { useState } from "react";
import "./PlayerPickemTeaserCard.css";

// Dismissal survives sessions — a teaser that resurrects every visit is an
// ad, not an announcement.
const DISMISSED_KEY = "playerPickemTeaserDismissed";

// Teaser lineup shape (illustrative — the real lineup shape is a v1 design
// decision, see docs/features/player-pickem.md). Counts, not duplicate
// boxes: density wins in an advertisement; the gameplay UI will render
// individual slots.
const SLOTS = [
  { label: "QB", filled: true },
  { label: "RB", count: 2 },
  { label: "WR", count: 2 },
  { label: "TE" },
  { label: "FLEX" },
  { label: "K" },
  { label: "DEF" },
];

/**
 * "Coming Soon: Player Pick'em" teaser — the lineup-slot banner concept
 * (design chosen 2026-08-04; see docs/features/player-pickem.md). The empty
 * roster row shows the game rather than describing it: QB filled in accent,
 * the rest dashed ("yours to pick"). No dates, no interactivity beyond
 * dismiss.
 */
function PlayerPickemTeaserCard() {
  const [dismissed, setDismissed] = useState(
    () => localStorage.getItem(DISMISSED_KEY) === "true"
  );

  if (dismissed) return null;

  const dismiss = () => {
    localStorage.setItem(DISMISSED_KEY, "true");
    setDismissed(true);
  };

  return (
    <section className="pickem-teaser" aria-label="Coming soon: Player Pick'em">
      <button
        type="button"
        className="pickem-teaser-dismiss"
        aria-label="Dismiss announcement"
        onClick={dismiss}
      >
        &times;
      </button>
      <div className="pickem-teaser-eyebrow">Coming Soon</div>
      <div className="pickem-teaser-title">Player Pick&rsquo;em</div>
      <p className="pickem-teaser-pitch">
        Pick any players, any week - no draft, no ownership. Know the
        matchups better than your league and prove it.
      </p>
      <div className="pickem-teaser-slots" aria-hidden="true">
        {SLOTS.map((slot) => (
          <div
            key={slot.label}
            className={`pickem-teaser-slot${slot.filled ? " pickem-teaser-slot--filled" : ""}`}
          >
            {slot.label}
            {slot.count ? (
              <span className="pickem-teaser-slot-count"> &times;{slot.count}</span>
            ) : null}
          </div>
        ))}
      </div>
    </section>
  );
}

export default PlayerPickemTeaserCard;
