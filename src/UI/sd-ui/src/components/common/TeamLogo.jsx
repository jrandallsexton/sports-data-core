import "./TeamLogo.css";

/**
 * Renders a team's generated mark when available; otherwise a license-free
 * fallback — a team-color circle with the abbreviation. Never renders a broken
 * <img>. Logos can now be null: selection is fail-closed (a licensed ESPN logo
 * is never served), so any team without a generated mark returns no URL. See
 * docs/logo-license-audit.md.
 *
 * `className` should carry the sizing (e.g. "matchup-logo", "team-logo"); it's
 * applied to both the img and the fallback so they occupy the same box.
 */
function TeamLogo({ src, abbr, color, alt, className = "" }) {
  if (src) {
    return <img src={src} alt={alt} className={className} />;
  }

  const bg = color
    ? (color.startsWith("#") ? color : `#${color}`)
    : "#4b5563"; // neutral slate when a team has no color either
  const label = (abbr || "").slice(0, 4).toUpperCase();

  return (
    <span
      className={`team-logo-fallback ${className}`}
      style={{ backgroundColor: bg }}
      role="img"
      aria-label={alt}
    >
      {label}
    </span>
  );
}

export default TeamLogo;
