import { useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import LeaguesApi from "api/leagues/leaguesApi";
import { useUserDto } from "../../contexts/UserContext";
import { leaguePicksPath } from "../../routes/paths";
import "./LeagueCreatePage.css";

/**
 * Player Pick'em league creation — the companion to the team-league
 * create page. Deliberately minimal: player leagues carry none of the
 * team-pick configuration (pick type, tiebreakers, confidence,
 * ranking/conference filters) — the roster is the game. Admin-only
 * during the alpha (route is AdminRoute-wrapped; the API enforces it
 * server-side too).
 *
 * Optional date window mirrors team leagues: bounds scope which weeks
 * bootstrap materializes (a preseason-only test league sets both dates
 * inside preseason). Blank = full season.
 */
function PlayerLeagueCreatePage() {
  const navigate = useNavigate();
  const { refreshUserDto } = useUserDto();
  // Once the POST succeeds the league EXISTS — a later failure (the
  // userDto refresh) must not funnel back into another POST, or a retry
  // click creates a duplicate league. Holds the created id so retries
  // resume at the refresh/navigate step.
  const createdIdRef = useRef(null);
  const [sport, setSport] = useState("FootballNfl");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [isPublic, setIsPublic] = useState(false);
  const [startsOn, setStartsOn] = useState("");
  const [endsOn, setEndsOn] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);

  const canSubmit = name.trim().length > 0 && !submitting;

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!canSubmit) return;
    setSubmitting(true);
    setError(null);

    if (createdIdRef.current === null) {
      try {
        const { id } = await LeaguesApi.createPlayerLeague({
          sport,
          name: name.trim(),
          description: description.trim() || null,
          isPublic,
          // Z-suffixed like the team create form: date-only strings
          // deserialize as Kind=Unspecified server-side, which Npgsql
          // rejects for timestamptz.
          startsOn: startsOn ? `${startsOn}T00:00:00Z` : null,
          endsOn: endsOn ? `${endsOn}T23:59:59Z` : null,
        });
        createdIdRef.current = id;
      } catch (err) {
        const first = err?.response?.data?.errors;
        setError(
          (Array.isArray(first) && first[0]?.errorMessage) ||
            "Could not create the league. Please try again."
        );
        setSubmitting(false);
        return;
      }
    }

    // Refresh /user/me BEFORE navigating: LeaguePicksRouter resolves the
    // league from userDto, and a stale DTO can't find the new league —
    // PicksPage's bad-id fallback would then bounce to the remembered
    // league instead. A refresh failure is NOT a creation failure: the
    // league exists, so the retry path re-runs only this step.
    const refreshed = await refreshUserDto();
    if (!refreshed) {
      setError(
        "League created, but loading it failed. Retry to open it."
      );
      setSubmitting(false);
      return;
    }
    // Straight to the roster builder — the router canonicalizes to the
    // league's current week once bootstrap materializes its weeks.
    navigate(leaguePicksPath(createdIdRef.current));
  };

  return (
    <div className="league-create-container">
      <h1>Create a Player Pick&rsquo;em League</h1>
      <p>
        Weekly fantasy-style rosters &mdash; no team picks, no spreads.
        Admin-only while the game is in alpha.
      </p>

      <form className="card" onSubmit={handleSubmit}>
        <div
          className="segmented-control sport-selector"
          role="tablist"
          aria-label="Sport"
        >
          <button
            type="button"
            role="tab"
            aria-selected={sport === "FootballNfl"}
            className={`segmented-tab${sport === "FootballNfl" ? " active" : ""}`}
            onClick={() => setSport("FootballNfl")}
          >
            NFL
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={sport === "FootballNcaa"}
            className={`segmented-tab${sport === "FootballNcaa" ? " active" : ""}`}
            onClick={() => setSport("FootballNcaa")}
          >
            NCAAFB
          </button>
        </div>

        <label htmlFor="pl-name">League Name</label>
        <input
          id="pl-name"
          type="text"
          maxLength={100}
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g. Friday Night Rosters"
        />

        <label htmlFor="pl-description">Description (optional)</label>
        <input
          id="pl-description"
          type="text"
          maxLength={100}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />

        <label className="checkbox-label" htmlFor="pl-public">
          <input
            id="pl-public"
            type="checkbox"
            checked={isPublic}
            onChange={(e) => setIsPublic(e.target.checked)}
          />
          Public league (discoverable by anyone)
        </label>

        <fieldset className="date-window">
          <legend>Date window (optional &mdash; blank = full season)</legend>
          <label htmlFor="pl-starts">Starts on</label>
          <input
            id="pl-starts"
            type="date"
            value={startsOn}
            onChange={(e) => setStartsOn(e.target.value)}
          />
          <label htmlFor="pl-ends">Ends on</label>
          <input
            id="pl-ends"
            type="date"
            value={endsOn}
            onChange={(e) => setEndsOn(e.target.value)}
          />
        </fieldset>

        {error && (
          <div className="form-error" role="alert">
            {error}
          </div>
        )}

        <button type="submit" className="submit-button" disabled={!canSubmit}>
          {submitting ? "Creating…" : "Create League"}
        </button>
      </form>
    </div>
  );
}

export default PlayerLeagueCreatePage;
