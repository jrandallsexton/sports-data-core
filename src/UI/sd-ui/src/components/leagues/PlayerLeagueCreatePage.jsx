import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import LeaguesApi from "api/leagues/leaguesApi";
import {
  toStartOfDayIso,
  toEndOfDayIso,
} from "api/leagues/requests/createLeagueRequests";
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
 * League Window mirrors the team create page (LeagueCreatePage): Full
 * Season, Week Range or Date Range. The bounds scope which weeks bootstrap
 * materializes (a preseason-only test league picks preseason weeks). The
 * window state, calendar fetch, validation and week->bound translation are
 * copied from LeagueCreatePage on purpose, not shared -- consolidating the
 * two is a separate, later refactor.
 */
const DURATION_FULL = "full";
const DURATION_WEEKS = "weeks";
const DURATION_DATES = "dates";

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
  const [durationMode, setDurationMode] = useState(DURATION_FULL);
  // Week Range selections are SeasonWeek ids from the season calendar, not
  // bare numbers -- week numbers restart per phase ("Week 4" exists in both
  // Preseason and Regular Season), so only the id is unambiguous.
  const [startWeekId, setStartWeekId] = useState("");
  const [endWeekId, setEndWeekId] = useState("");
  // The sport's season calendar (all phases except Off Season, StartDate
  // order). Drives the Week Range picker and the week->date translation at
  // submit.
  const [seasonWeeks, setSeasonWeeks] = useState([]);
  const [seasonWeeksLoaded, setSeasonWeeksLoaded] = useState(false);
  const [startsOn, setStartsOn] = useState("");
  const [endsOn, setEndsOn] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);

  const canSubmit = name.trim().length > 0 && !submitting;

  // Today as a `YYYY-MM-DD` string for the date-input `min` attribute and
  // the pre-submit guard, anchored at the user's local calendar day.
  const todayIsoDate = useMemo(() => {
    const now = new Date();
    const y = now.getFullYear();
    const m = String(now.getMonth() + 1).padStart(2, "0");
    const d = String(now.getDate()).padStart(2, "0");
    return `${y}-${m}-${d}`;
  }, []);

  // A week id from one sport's calendar means nothing in another's.
  useEffect(() => {
    setStartWeekId("");
    setEndWeekId("");
  }, [sport]);

  // Season calendar per sport. Fails soft -- an empty list disables the
  // Week Range tab.
  useEffect(() => {
    let cancelled = false;
    setSeasonWeeksLoaded(false);
    setSeasonWeeks([]);
    LeaguesApi.getSeasonWeeks(sport)
      .then((data) => {
        if (cancelled) return;
        setSeasonWeeks(data?.weeks ?? []);
        setSeasonWeeksLoaded(true);
      })
      .catch((err) => {
        console.error("Failed to load season weeks:", err);
        if (cancelled) return;
        setSeasonWeeks([]);
        setSeasonWeeksLoaded(true);
      });
    return () => {
      cancelled = true;
    };
  }, [sport]);

  const startWeekIndex = seasonWeeks.findIndex((w) => w.id === startWeekId);
  const endWeekIndex = seasonWeeks.findIndex((w) => w.id === endWeekId);
  const startWeekObj = startWeekIndex >= 0 ? seasonWeeks[startWeekIndex] : null;
  const endWeekObj = endWeekIndex >= 0 ? seasonWeeks[endWeekIndex] : null;

  // End >= Start: if the start moves past the end (or end is unset), pull the
  // end up to match.
  useEffect(() => {
    if (!startWeekId) return;
    if (!endWeekId || (endWeekIndex >= 0 && startWeekIndex > endWeekIndex)) {
      setEndWeekId(startWeekId);
    }
  }, [startWeekId, endWeekId, startWeekIndex, endWeekIndex]);

  // "MM/DD" from the week's UTC boundary instants, formatted in UTC so the
  // authored wall-clock day isn't shifted in western timezones.
  const fmtWeekDate = (iso) => {
    const d = new Date(iso);
    return Number.isNaN(d.getTime())
      ? ""
      : d.toLocaleDateString(undefined, {
          month: "2-digit",
          day: "2-digit",
          timeZone: "UTC",
        });
  };
  const weekOptionLabel = (w) =>
    `${w.label}: ${fmtWeekDate(w.startDateUtc)}-${fmtWeekDate(w.endDateUtc)}`;
  const isWeekPast = (w) => new Date(w.endDateUtc).getTime() <= Date.now();

  // The window bounds sent to the API, per mode (the same translation as the
  // team form's request builder): Date Range = the user's local calendar
  // days; Week Range = the selected weeks' real UTC boundaries, passed
  // through raw; Full Season = no bounds.
  const windowBounds = () => {
    if (durationMode === DURATION_DATES) {
      return { startsOn: toStartOfDayIso(startsOn), endsOn: toEndOfDayIso(endsOn) };
    }
    if (durationMode === DURATION_WEEKS) {
      return {
        startsOn: startWeekObj?.startDateUtc ?? null,
        endsOn: endWeekObj?.endDateUtc ?? null,
      };
    }
    return { startsOn: null, endsOn: null };
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!canSubmit) return;
    setError(null);

    // Window guards, mirroring the team form (the server's EffectiveEndsOn
    // check is the trust boundary; these just give a clear message). Only
    // before the POST: a retry after a successful create skips them.
    if (createdIdRef.current === null) {
      if (durationMode === DURATION_DATES) {
        if (endsOn && endsOn < todayIsoDate) {
          setError("End date can't be in the past.");
          return;
        }
        if (startsOn && endsOn && endsOn < startsOn) {
          setError("End date must be on or after the start date.");
          return;
        }
      }
      if (durationMode === DURATION_WEEKS && (!startWeekObj || !endWeekObj)) {
        setError("Choose a start and end week for your league.");
        return;
      }
    }

    setSubmitting(true);

    if (createdIdRef.current === null) {
      try {
        const { id } = await LeaguesApi.createPlayerLeague({
          sport,
          name: name.trim(),
          description: description.trim() || null,
          isPublic,
          ...windowBounds(),
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

        <div className="form-group">
          <label>League Window</label>
          <div
            className="segmented-control"
            role="tablist"
            aria-label="League Window"
          >
            <button
              type="button"
              role="tab"
              aria-selected={durationMode === DURATION_FULL}
              className={`segmented-tab${
                durationMode === DURATION_FULL ? " active" : ""
              }`}
              onClick={() => setDurationMode(DURATION_FULL)}
            >
              Full Season
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={durationMode === DURATION_WEEKS}
              disabled={seasonWeeksLoaded && seasonWeeks.length === 0}
              title={
                seasonWeeksLoaded && seasonWeeks.length === 0
                  ? "Season calendar unavailable"
                  : undefined
              }
              className={`segmented-tab${
                durationMode === DURATION_WEEKS ? " active" : ""
              }`}
              onClick={() => setDurationMode(DURATION_WEEKS)}
            >
              Week Range
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={durationMode === DURATION_DATES}
              className={`segmented-tab${
                durationMode === DURATION_DATES ? " active" : ""
              }`}
              onClick={() => setDurationMode(DURATION_DATES)}
            >
              Date Range
            </button>
          </div>

          {durationMode === DURATION_WEEKS && (
            <div className="form-row duration-detail">
              <div className="form-group">
                <label htmlFor="pl-start-week">Start Week</label>
                <select
                  id="pl-start-week"
                  value={startWeekId}
                  onChange={(e) => setStartWeekId(e.target.value)}
                >
                  <option value="">Select...</option>
                  {/* Past weeks stay visible but disabled; an in-progress
                      week remains selectable. */}
                  {seasonWeeks.map((w) => (
                    <option key={w.id} value={w.id} disabled={isWeekPast(w)}>
                      {weekOptionLabel(w)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-group">
                <label htmlFor="pl-end-week">End Week</label>
                <select
                  id="pl-end-week"
                  value={endWeekId}
                  onChange={(e) => setEndWeekId(e.target.value)}
                >
                  <option value="">Select...</option>
                  {seasonWeeks.map((w, i) => (
                    <option
                      key={w.id}
                      value={w.id}
                      disabled={
                        isWeekPast(w) ||
                        (startWeekIndex >= 0 && i < startWeekIndex)
                      }
                    >
                      {weekOptionLabel(w)}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          )}

          {durationMode === DURATION_DATES && (
            <div className="form-row duration-detail">
              <div className="form-group">
                <label htmlFor="pl-starts">Start Date</label>
                <input
                  type="date"
                  id="pl-starts"
                  value={startsOn}
                  min={todayIsoDate}
                  onChange={(e) => setStartsOn(e.target.value)}
                />
              </div>
              <div className="form-group">
                <label htmlFor="pl-ends">End Date</label>
                <input
                  type="date"
                  id="pl-ends"
                  value={endsOn}
                  min={startsOn || todayIsoDate}
                  onChange={(e) => setEndsOn(e.target.value)}
                />
              </div>
            </div>
          )}
        </div>

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
