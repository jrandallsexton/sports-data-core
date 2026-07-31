import React, { useState, useEffect, useMemo } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import toast from "react-hot-toast";
import apiWrapper from "../../api/apiWrapper.js";
import { useUserDto } from "../../contexts/UserContext";
import {
  getLeagueCreationGates,
  formatGateDateOrSoon,
} from "../../utils/leagueCreationGates";

import "./LeagueCreatePage.css";

import {
  buildCreateFootballNcaaLeagueRequest,
  buildCreateFootballNflLeagueRequest,
  buildCreateBaseballMlbLeagueRequest,
} from "api/leagues/requests/createLeagueRequests";
import LeaguesApi from "api/leagues/leaguesApi";

const SPORT_NCAA = "FootballNcaa";
const SPORT_NFL = "FootballNfl";
const SPORT_MLB = "BaseballMlb";

const NFL_DIVISIONS = [
  { slug: "afc-east", shortName: "AFC East" },
  { slug: "afc-north", shortName: "AFC North" },
  { slug: "afc-south", shortName: "AFC South" },
  { slug: "afc-west", shortName: "AFC West" },
  { slug: "nfc-east", shortName: "NFC East" },
  { slug: "nfc-north", shortName: "NFC North" },
  { slug: "nfc-south", shortName: "NFC South" },
  { slug: "nfc-west", shortName: "NFC West" },
];

const MLB_DIVISIONS = [
  { slug: "american-league-east", shortName: "AL East" },
  { slug: "american-league-central", shortName: "AL Cent" },
  { slug: "american-league-west", shortName: "AL West" },
  { slug: "national-league-east", shortName: "NL East" },
  { slug: "national-league-central", shortName: "NL Cent" },
  { slug: "national-league-west", shortName: "NL West" },
];

const SPORT_COPY = {
  [SPORT_NCAA]: {
    label: "NCAA",
    groupLabel: "Conferences",
    groupEmoji: "🏈",
    tiebreakerTotalLabel: "Closest to Total Points",
    namePlaceholder: "e.g., Saturday Showdown",
    descPlaceholder: "A fun league for SEC fans.",
  },
  [SPORT_NFL]: {
    label: "NFL",
    groupLabel: "Divisions",
    groupEmoji: "🏈",
    tiebreakerTotalLabel: "Closest to Total Points",
    namePlaceholder: "e.g., Sunday Funday",
    descPlaceholder: "A fun league for NFL fans.",
  },
  [SPORT_MLB]: {
    label: "MLB",
    groupLabel: "Divisions",
    groupEmoji: "⚾",
    tiebreakerTotalLabel: "Closest to Total Runs",
    namePlaceholder: "e.g., Ninth Inning",
    descPlaceholder: "A fun league for MLB fans.",
  },
};

// Suggested-description building blocks. A description is trivially skipped at
// create time but is what makes a league legible on YourLeaguesCard for members
// in several leagues — so we offer a one-tap, non-destructive suggestion derived
// from the sport + pick type the commissioner already chose. Friendlier sport
// phrasing than SPORT_COPY.label ("College football" vs "NCAA").
const SPORT_DESC_PHRASE = {
  [SPORT_NCAA]: "NCAAFB",
  [SPORT_NFL]: "NFL",
  [SPORT_MLB]: "MLB",
};

const PICK_TYPE_DESC_PHRASE = {
  StraightUp: "SU",
  AgainstTheSpread: "ATS",
  OverUnder: "O/U",
};

// "Aug 29" from a YYYY-MM-DD date-input value. Parsed at local midnight (append
// T00:00:00) so the date input's calendar day isn't shifted back a day by
// UTC-parsing. Returns null for empty input.
function formatDateShort(iso) {
  if (!iso) return null;
  const d = new Date(`${iso}T00:00:00`);
  return Number.isNaN(d.getTime())
    ? null
    : d.toLocaleDateString(undefined, { month: "short", day: "numeric" });
}

// Returns the suggested description as a compact tag, enriched by whatever's
// chosen so far. Gated on sport alone (always set) so it's robust; pick type /
// confidence / window refine it. Terse by design — it's a glanceable
// distinguisher on the home card, not a sentence: e.g. "NCAAFB ATS w/Confidence",
// "MLB SU · Aug 29". The Description field lives at the BOTTOM of the form, so by
// the time the user reaches it every input is set and the tag is complete; that
// placement also primes the writer. `windowLabel` is a pre-formatted
// span/day/week string, or null for full season.
function buildSuggestedDescription(sport, pickType, useConfidencePoints, windowLabel) {
  const sportPhrase = SPORT_DESC_PHRASE[sport];
  if (!sportPhrase) return null;
  const pickPhrase = PICK_TYPE_DESC_PHRASE[pickType]; // may be undefined pre-selection
  let tag = pickPhrase ? `${sportPhrase} ${pickPhrase}` : sportPhrase;
  if (useConfidencePoints) tag += " w/Confidence";
  if (windowLabel) tag += ` · ${windowLabel}`;
  return tag;
}

const DURATION_FULL = "full";
const DURATION_WEEKS = "weeks";
const DURATION_DATES = "dates";

// Backend Sport enum values accepted via the `?sport=` query param.
// Anything not in this set falls back to the NCAA default.
const VALID_SPORT_PARAMS = new Set([SPORT_NCAA, SPORT_NFL, SPORT_MLB]);

const LeagueCreatePage = () => {
  const { userDto, loading: userLoading, refreshUserDto } = useUserDto();
  const [searchParams] = useSearchParams();
  // Preselect the sport tab when the landing page (or any other caller)
  // deep-links here with ?sport=FootballNcaa / FootballNfl / BaseballMlb.
  // MLB is currently admin-gated, but honoring it here is harmless — the
  // segmented control below hides the MLB tab for non-admins and falls
  // back to the default selection via the sport-change effect.
  const initialSport = (() => {
    const raw = searchParams.get("sport");
    return raw && VALID_SPORT_PARAMS.has(raw) ? raw : SPORT_NCAA;
  })();
  const [sport, setSport] = useState(initialSport);
  // Active league-creation gates: { FootballNcaa: "2026-08-17T00:00:00Z", ... }.
  // A sport present here is locked until that instant; empty until loaded (and
  // on fetch failure — the server guard is the real enforcement). See
  // docs/features/league-creation-availability-gate.md.
  const [creationGates, setCreationGates] = useState({});
  const [gatesLoaded, setGatesLoaded] = useState(false);
  const [leagueName, setLeagueName] = useState("");
  const [description, setDescription] = useState("");
  // True once the user types in the description field, which freezes the
  // auto-suggestion so their input is never overwritten. See effectiveDescription.
  const [descriptionEdited, setDescriptionEdited] = useState(false);
  const [pickType, setPickType] = useState("");
  const [tiebreaker, setTiebreaker] = useState("");
  const [useConfidencePoints, setUseConfidencePoints] = useState(false);
  const [teamFilter, setTeamFilter] = useState([]);
  const [rankingFilter, setRankingFilter] = useState("");
  const [showConfirmDialog, setShowConfirmDialog] = useState(false);
  const [isPublic, setIsPublic] = useState(false);
  // BE JoinPolicy enum name. "Open" = joinable while the league is live;
  // "CloseAtFirstGame" = roster locks when the first scheduled game starts.
  const [joinPolicy, setJoinPolicy] = useState("Open");
  const [dropLowWeeksCount, setDropLowWeeksCount] = useState(0);
  const [allConferences, setAllConferences] = useState([]);
  const [fbsOnly, setFbsOnly] = useState(true);
  const [durationMode, setDurationMode] = useState(DURATION_FULL);
  // Week Range selections are SeasonWeek ids from the season calendar, not
  // bare numbers -- week numbers restart per phase ("Week 4" exists in both
  // Preseason and Regular Season), so only the id is unambiguous.
  const [startWeekId, setStartWeekId] = useState("");
  const [endWeekId, setEndWeekId] = useState("");
  // The sport's season calendar (all phases except Off Season, StartDate
  // order). Drives the Week Range picker, the week->date translation at
  // submit, and the drop-week limit for every window mode.
  const [seasonWeeks, setSeasonWeeks] = useState([]);
  const [seasonWeeksLoaded, setSeasonWeeksLoaded] = useState(false);
  const [startsOn, setStartsOn] = useState("");
  const [endsOn, setEndsOn] = useState("");

  const navigate = useNavigate();

  // Today as a `YYYY-MM-DD` string for the date-input `min` attribute and
  // the pre-submit guard. Anchored at the user's local calendar day so the
  // picker rejects yesterday in the user's timezone (the server-side
  // EffectiveEndsOn > now check is the trust boundary; this is just UX).
  const todayIsoDate = useMemo(() => {
    const now = new Date();
    const y = now.getFullYear();
    const m = String(now.getMonth() + 1).padStart(2, "0");
    const d = String(now.getDate()).padStart(2, "0");
    return `${y}-${m}-${d}`;
  }, []);

  const isNcaa = sport === SPORT_NCAA;
  const isMlbAvailable = userDto?.isAdmin === true;
  const copy = SPORT_COPY[sport];

  // Locked while the sport's configured "opens" instant is still in the future.
  // Derived from the current time (not merely presence in the fetched snapshot),
  // so a gate that elapses while the page is open clears on the next render —
  // no reload. (The gate flips once, weeks out; a page-lifetime setTimeout at
  // that instant would be unreliable and exceeds the ~24.8-day timer ceiling.)
  const isSportLocked = (s) => {
    const opensUtc = creationGates[s];
    return Boolean(opensUtc) && new Date(opensUtc).getTime() > Date.now();
  };

  // Load the active creation gates once on mount. Fails open (empty map) — the
  // server guard still rejects a locked create.
  useEffect(() => {
    let cancelled = false;
    getLeagueCreationGates().then((gates) => {
      if (cancelled) return;
      setCreationGates(gates);
      setGatesLoaded(true);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  // The sports this user can choose from (MLB is admin-only). Single source for
  // the fallback selection and the all-locked guard so they can't drift.
  const eligibleSports = useMemo(
    () => [SPORT_NCAA, SPORT_NFL, ...(isMlbAvailable ? [SPORT_MLB] : [])],
    [isMlbAvailable]
  );

  // Every selectable sport is currently gated → nothing can be created. Disable
  // submission and show an unavailable note (the server enforces this too).
  const allSportsLocked =
    gatesLoaded && eligibleSports.every((s) => isSportLocked(s));

  // Keep the selected sport valid. The current sport is a valid selection only
  // if it's eligible for this user (MLB is admin-only) AND not gated; otherwise
  // fall back to the first eligible, open sport. This covers both a locked
  // eligible sport and an unlocked-but-ineligible deep-link (e.g.
  // ?sport=BaseballMlb for a non-admin). Wait until gates AND the user (admin
  // status) are known so an admin's MLB deep-link isn't bounced mid-load, and
  // only move when a valid fallback exists.
  useEffect(() => {
    if (!gatesLoaded || userLoading) return;
    const isSelectable = eligibleSports.includes(sport) && !isSportLocked(sport);
    if (isSelectable) return;
    const fallback = eligibleSports.find((s) => !isSportLocked(s));
    if (fallback) setSport(fallback);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [gatesLoaded, userLoading, creationGates, sport, eligibleSports]);

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

  // "MM/DD" from the week's UTC boundary instants. Formatted in UTC so the
  // authored wall-clock day isn't shifted in western timezones (same trick as
  // formatGateDate).
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

  // How many weeks the chosen window spans -- the drop-week ceiling is one
  // less ("drop all weeks" is not a league). null = unknown (calendar not
  // loaded / dates not chosen) -> legacy cap of 3.
  const leagueWeekCount = (() => {
    if (!seasonWeeksLoaded || seasonWeeks.length === 0) return null;
    if (durationMode === DURATION_WEEKS) {
      if (startWeekIndex < 0 || endWeekIndex < 0) return null;
      return endWeekIndex - startWeekIndex + 1;
    }
    if (durationMode === DURATION_DATES) {
      if (!startsOn || !endsOn) return null;
      const from = new Date(`${startsOn}T00:00:00Z`).getTime();
      const to = new Date(`${endsOn}T23:59:59Z`).getTime();
      const count = seasonWeeks.filter((w) => {
        const ws = new Date(w.startDateUtc).getTime();
        const we = new Date(w.endDateUtc).getTime();
        return ws <= to && we >= from;
      }).length;
      return count > 0 ? count : null;
    }
    // Full season scores over the regular season.
    const reg = seasonWeeks.filter((w) =>
      /regular/i.test(w.phaseName)
    ).length;
    return reg > 0 ? reg : null;
  })();
  const maxDropWeeks = leagueWeekCount === null ? 3 : Math.max(leagueWeekCount - 1, 0);

  // Keep the selection valid as the window shrinks.
  useEffect(() => {
    setDropLowWeeksCount((c) => Math.min(c, maxDropWeeks));
  }, [maxDropWeeks]);

  // Human-readable window for the suggested description: a single day, a date
  // range, a single week, or a week range. null for a full-season league.
  const descriptionWindowLabel = (() => {
    if (durationMode === DURATION_WEEKS) {
      if (!startWeekObj || !endWeekObj) return null;
      return startWeekObj.id === endWeekObj.id
        ? startWeekObj.label
        : `${startWeekObj.label} – ${endWeekObj.label}`;
    }
    if (durationMode === DURATION_DATES) {
      const s = formatDateShort(startsOn);
      const e = formatDateShort(endsOn);
      if (!s && !e) return null;
      // Single-day is decided by the raw ISO values, not the formatted labels —
      // the label drops the year, so dates a year apart would format identically.
      if (s && e) return startsOn === endsOn ? s : `${s}–${e}`;
      return s || e;
    }
    return null; // full season
  })();

  // The suggested description, recomputed each render from every parameter
  // chosen so far (sport/pickType/confidence/window).
  const suggestedDescription = buildSuggestedDescription(
    sport,
    pickType,
    useConfidencePoints,
    descriptionWindowLabel
  );

  // Prefill the description field with the suggestion by default, but stop
  // tracking once the user edits it — so the field is populated (readily
  // visible) without ever clobbering what someone deliberately types. The
  // effective value is what both the field shows and the submit payload sends.
  const effectiveDescription = descriptionEdited
    ? description
    : suggestedDescription ?? "";

  useEffect(() => {
    if (!isNcaa) return;
    const fetchConferences = async () => {
      try {
        const result =
          await apiWrapper.Conferences.getConferenceNamesAndSlugs();
        setAllConferences(result.data);
      } catch (error) {
        console.error("Failed to load conferences", error);
      }
    };

    fetchConferences();
  }, [isNcaa]);

  // Slugs don't overlap across sports — reset group selection on switch.
  // NFL and MLB have small, fixed division sets; pre-select all so the
  // typical "include every team" case is one click instead of eight.
  // NCAA starts empty — commissioners usually cherry-pick conferences.
  useEffect(() => {
    if (sport === SPORT_NFL) {
      setTeamFilter(NFL_DIVISIONS.map((d) => d.slug));
    } else if (sport === SPORT_MLB) {
      setTeamFilter(MLB_DIVISIONS.map((d) => d.slug));
    } else {
      setTeamFilter([]);
    }
    if (!isNcaa) {
      setRankingFilter("");
    }
    // Week selections don't survive a sport switch -- ids are season-scoped.
    setStartWeekId("");
    setEndWeekId("");
  }, [sport, isNcaa]);

  // Season calendar per sport. Needed beyond Week Range: drop-week limits for
  // Full Season and Date Range derive from it too. Fails soft -- an empty
  // list disables the Week Range tab and leaves drop weeks on a legacy cap.
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


  const chunk = (array, size) => {
    const result = [];
    for (let i = 0; i < array.length; i += size) {
      result.push(array.slice(i, i + size));
    }
    return result;
  };

  const handleCheckboxChange = (event) => {
    const { value, checked } = event.target;
    setTeamFilter((prev) =>
      checked ? [...prev, value] : prev.filter((v) => v !== value)
    );
  };

  const handleFormSubmit = (e) => {
    e.preventDefault();

    // Every eligible sport is gated — nothing can be created. Guards the
    // Enter-key submit path (the button is also disabled). Server enforces too.
    if (allSportsLocked) {
      toast.error(
        "League creation isn't open yet — check back when your sport unlocks."
      );
      return;
    }

    // Mirror of the server `EffectiveEndsOn > now` rule. The `min` attribute
    // on the date inputs handles the native-picker UX, but the keyboard /
    // paste path can bypass it — guard before opening the confirm dialog so
    // the user gets a clear message instead of a confirmed-then-rejected
    // round-trip.
    if (durationMode === DURATION_DATES) {
      if (endsOn && endsOn < todayIsoDate) {
        toast.error("End date can't be in the past.");
        return;
      }
      if (startsOn && endsOn && endsOn < startsOn) {
        toast.error("End date must be on or after the start date.");
        return;
      }
    }

    if (durationMode === DURATION_WEEKS && (!startWeekObj || !endWeekObj)) {
      toast.error("Choose a start and end week for your league.");
      return;
    }

    setShowConfirmDialog(true);
  };

  const finalizeLeagueCreation = async () => {
    const formState = {
      leagueName,
      description: effectiveDescription,
      pickType,
      tiebreaker,
      useConfidencePoints,
      rankingFilter,
      teamFilter,
      isPublic,
      joinPolicy,
      dropLowWeeksCount,
      durationMode,
      startsOn,
      endsOn,
      // Week Range translated to the selected weeks' real UTC boundaries --
      // raw ISO pass-through, NOT the date-input local-midnight conversion.
      weekStartsOnIso: startWeekObj?.startDateUtc ?? null,
      weekEndsOnIso: endWeekObj?.endDateUtc ?? null,
    };

    const dispatch = {
      [SPORT_NCAA]: {
        build: buildCreateFootballNcaaLeagueRequest,
        send: LeaguesApi.createFootballNcaaLeague,
      },
      [SPORT_NFL]: {
        build: buildCreateFootballNflLeagueRequest,
        send: LeaguesApi.createFootballNflLeague,
      },
      [SPORT_MLB]: {
        build: buildCreateBaseballMlbLeagueRequest,
        send: LeaguesApi.createBaseballMlbLeague,
      },
    }[sport];

    const payload = dispatch.build(formState);

    try {
      const response = await dispatch.send(payload);
      await refreshUserDto();
      navigate(`/app/league/${response.id}`);
    } catch (error) {
      console.error("Failed to create league:", error);
      // Surface the server's validation message (e.g. the blackout-date guard's
      // "No games are scheduled in the selected date range.") when present.
      const serverMessage = error?.response?.data?.errors?.[0]?.errorMessage;
      toast.error(serverMessage || "An error occurred while creating the league.");
    }

    setShowConfirmDialog(false);
  };

  const teamGroups = useMemo(() => {
    if (sport === SPORT_NCAA) {
      return fbsOnly
        ? allConferences.filter((c) => c.division === "FBS (I-A)")
        : allConferences;
    }
    if (sport === SPORT_NFL) return NFL_DIVISIONS;
    if (sport === SPORT_MLB) return MLB_DIVISIONS;
    return [];
  }, [sport, fbsOnly, allConferences]);

  return (
    <div className="league-create-container">
      <h1>Create a New Pick’em League</h1>
      <p>
        Let’s set up your custom league so you can compete with friends - or
        publish it for others to join!
      </p>

      <div className="card">
        <div
          className="segmented-control sport-selector"
          role="tablist"
          aria-label="Sport"
        >
          <button
            type="button"
            role="tab"
            aria-selected={sport === SPORT_NCAA}
            disabled={isSportLocked(SPORT_NCAA)}
            title={
              isSportLocked(SPORT_NCAA)
                ? `Opens ${formatGateDateOrSoon(creationGates[SPORT_NCAA])}`
                : undefined
            }
            className={`segmented-tab${sport === SPORT_NCAA ? " active" : ""}${
              isSportLocked(SPORT_NCAA) ? " locked" : ""
            }`}
            onClick={() => setSport(SPORT_NCAA)}
          >
            NCAA
            {isSportLocked(SPORT_NCAA) &&
              ` · opens ${formatGateDateOrSoon(creationGates[SPORT_NCAA])}`}
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={sport === SPORT_NFL}
            disabled={isSportLocked(SPORT_NFL)}
            title={
              isSportLocked(SPORT_NFL)
                ? `Opens ${formatGateDateOrSoon(creationGates[SPORT_NFL])}`
                : undefined
            }
            className={`segmented-tab${sport === SPORT_NFL ? " active" : ""}${
              isSportLocked(SPORT_NFL) ? " locked" : ""
            }`}
            onClick={() => setSport(SPORT_NFL)}
          >
            NFL
            {isSportLocked(SPORT_NFL) &&
              ` · opens ${formatGateDateOrSoon(creationGates[SPORT_NFL])}`}
          </button>
          {isMlbAvailable && (
            <button
              type="button"
              role="tab"
              aria-selected={sport === SPORT_MLB}
              disabled={isSportLocked(SPORT_MLB)}
              title={
                isSportLocked(SPORT_MLB)
                  ? `Opens ${formatGateDateOrSoon(creationGates[SPORT_MLB])}`
                  : undefined
              }
              className={`segmented-tab${sport === SPORT_MLB ? " active" : ""}${
                isSportLocked(SPORT_MLB) ? " locked" : ""
              }`}
              onClick={() => setSport(SPORT_MLB)}
            >
              MLB
              {isSportLocked(SPORT_MLB) &&
                ` · opens ${formatGateDateOrSoon(creationGates[SPORT_MLB])}`}
            </button>
          )}
        </div>

        <form className="league-form" onSubmit={handleFormSubmit}>
          <div className="form-group">
            <label htmlFor="leagueName">League Name</label>
            <input
              type="text"
              id="leagueName"
              name="leagueName"
              value={leagueName}
              onChange={(e) => setLeagueName(e.target.value)}
              placeholder={copy.namePlaceholder}
              required
            />
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="pickType">Pick Type</label>
              <select
                id="pickType"
                name="pickType"
                value={pickType}
                onChange={(e) => setPickType(e.target.value)}
                required
              >
                <option value="">Select...</option>
                <option value="StraightUp">Straight Up (Win/Loss)</option>
                <option value="AgainstTheSpread">Against the Spread (ATS)</option>
              </select>
            </div>

            <div className="form-group">
              <label htmlFor="tiebreaker">Tiebreaker Method</label>
              <select
                id="tiebreaker"
                name="tiebreaker"
                value={tiebreaker}
                onChange={(e) => setTiebreaker(e.target.value)}
              >
                <option value="">Select...</option>
                <option value="earliest">Earliest Submission Wins</option>
                <option value="closest">{copy.tiebreakerTotalLabel}</option>
              </select>
            </div>

          </div>

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
                  <label htmlFor="startWeek">Start Week</label>
                  <select
                    id="startWeek"
                    value={startWeekId}
                    onChange={(e) => setStartWeekId(e.target.value)}
                  >
                    <option value="">Select...</option>
                    {/* Past weeks stay visible but disabled; an in-progress
                        week remains selectable (you can still pick its
                        remaining games). */}
                    {seasonWeeks.map((w) => (
                      <option key={w.id} value={w.id} disabled={isWeekPast(w)}>
                        {weekOptionLabel(w)}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="form-group">
                  <label htmlFor="endWeek">End Week</label>
                  <select
                    id="endWeek"
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
                  <label htmlFor="startsOn">Start Date</label>
                  <input
                    type="date"
                    id="startsOn"
                    value={startsOn}
                    min={todayIsoDate}
                    onChange={(e) => setStartsOn(e.target.value)}
                  />
                </div>
                <div className="form-group">
                  <label htmlFor="endsOn">End Date</label>
                  <input
                    type="date"
                    id="endsOn"
                    value={endsOn}
                    min={startsOn || todayIsoDate}
                    onChange={(e) => setEndsOn(e.target.value)}
                  />
                </div>
              </div>
            )}

            {/* Below League Window because the window drives its ceiling:
                a league can drop at most LeagueWeekCount - 1 weeks. The
                count derives from the season calendar for every mode
                (selected span for Week Range, overlapping weeks for Date
                Range, regular-season weeks for Full Season). */}
            <div className="form-group duration-detail">
              <label htmlFor="dropLowWeeksCount">Drop Low Weeks</label>
              <select
                id="dropLowWeeksCount"
                name="dropLowWeeksCount"
                value={dropLowWeeksCount}
                onChange={(e) => setDropLowWeeksCount(Number(e.target.value))}
              >
                <option value={0}>None. Use All Weeks</option>
                {Array.from({ length: maxDropWeeks }, (_, i) => i + 1).map(
                  (n) => (
                    <option key={n} value={n}>
                      {n}
                    </option>
                  )
                )}
              </select>
            </div>
          </div>

          <div className="form-group">
            <label>Teams Included</label>

            <div className="checkbox-section">
              {isNcaa && (
                <div className="form-group ranking-select">
                  <label htmlFor="rankingFilter">🏆 Rankings</label>
                  <select
                    id="rankingFilter"
                    name="rankingFilter"
                    value={rankingFilter}
                    onChange={(e) => setRankingFilter(e.target.value)}
                  >
                    <option value="">None</option>
                    <option value="AP_TOP_25">AP Top 25</option>
                    <option value="AP_TOP_20">AP Top 20</option>
                    <option value="AP_TOP_15">AP Top 15</option>
                    <option value="AP_TOP_10">AP Top 10</option>
                    <option value="AP_TOP_5">AP Top 5</option>
                  </select>
                </div>
              )}

              <h4>
                {copy.groupEmoji} {copy.groupLabel}
              </h4>
              {isNcaa && (
                <div className="form-group">
                  <label>
                    <input
                      type="checkbox"
                      checked={fbsOnly}
                      onChange={(e) => setFbsOnly(e.target.checked)}
                    />{" "}
                    FBS Only (I-A)
                  </label>
                </div>
              )}

              <table className="checkbox-table">
                <tbody>
                  {chunk(teamGroups, 3).map((row, rowIndex) => (
                    <tr key={rowIndex}>
                      {row.map((group) => (
                        <td key={group.slug}>
                          <label className="table-checkbox">
                            <input
                              type="checkbox"
                              value={group.slug}
                              checked={teamFilter.includes(group.slug)}
                              onChange={handleCheckboxChange}
                            />
                            {group.shortName}
                          </label>
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>

              <h4>🌐 Other</h4>
              <div className="inline-options">
                <label>
                  <input
                    type="checkbox"
                    checked={useConfidencePoints}
                    onChange={(e) => setUseConfidencePoints(e.target.checked)}
                  />{" "}
                  Use Confidence Points
                </label>
                <label>
                  <input
                    type="checkbox"
                    checked={isPublic}
                    onChange={(e) => setIsPublic(e.target.checked)}
                  />{" "}
                  Make this league public (anyone can join)
                </label>
              </div>

              {/* Applies to invite links too, not just public discovery — a
                  shared link to a closed league stops working at kickoff. */}
              <h4>🚪 Who can join, and until when?</h4>
              <div className="inline-options join-policy-options">
                <label>
                  <input
                    type="radio"
                    name="joinPolicy"
                    value="Open"
                    checked={joinPolicy === "Open"}
                    onChange={() => setJoinPolicy("Open")}
                  />{" "}
                  Open — new members can join any time while the league is live
                </label>
                <label>
                  <input
                    type="radio"
                    name="joinPolicy"
                    value="CloseAtFirstGame"
                    checked={joinPolicy === "CloseAtFirstGame"}
                    onChange={() => setJoinPolicy("CloseAtFirstGame")}
                  />{" "}
                  Locked at kickoff — closes when the first game starts
                </label>
              </div>
            </div>
          </div>

          {/* Description is intentionally last: it's optional flavor, and its
              suggested value derives from the parameters chosen above — so by the
              time the user reaches it, the field is pre-filled with a fully
              informed suggestion the user can accept, edit, or clear. */}
          <div className="form-group">
            <label htmlFor="description">Description (optional)</label>
            <textarea
              id="description"
              name="description"
              value={effectiveDescription}
              onChange={(e) => {
                setDescriptionEdited(true);
                setDescription(e.target.value);
              }}
              placeholder={copy.descPlaceholder}
            />
          </div>

          {allSportsLocked && (
            <p className="sport-locked-note">
              League creation isn’t open yet. Check back when your sport unlocks.
            </p>
          )}

          <button
            type="submit"
            className="submit-button"
            disabled={allSportsLocked}
          >
            Create League
          </button>
        </form>
      </div>

      {showConfirmDialog && (
        <div className="modal-overlay">
          <div className="modal">
            <h3>Confirm League Settings</h3>
            <ul>
              <li>
                <strong>Name:</strong> {leagueName}
              </li>
              <li>
                <strong>{copy.groupLabel}:</strong>{" "}
                {teamFilter.length
                  ? teamFilter
                      .map((slug) => {
                        const group = teamGroups.find((g) => g.slug === slug);
                        return group?.shortName || slug;
                      })
                      .join(", ")
                  : "None selected"}
              </li>
              <li>
                <strong>Confidence Points:</strong>{" "}
                {useConfidencePoints ? "Yes" : "No"}
              </li>
              <li>
                <strong>Description:</strong> {effectiveDescription || "None"}
              </li>
              <li>
                <strong>Pick Deadline:</strong> 5 minutes before kickoff
                (not-configurable)
              </li>
              <li>
                <strong>Pick Type:</strong> {pickType || "Not selected"}
              </li>
              {isNcaa && (
                <li>
                  <strong>Ranking Filter:</strong> {rankingFilter || "None"}
                </li>
              )}
              <li>
                <strong>Tiebreaker:</strong> {tiebreaker || "Not selected"}
              </li>
              <li>
                <strong>Drop Low Weeks:</strong>{" "}
                {dropLowWeeksCount === 0
                  ? "None. Use All Weeks"
                  : dropLowWeeksCount}
              </li>
              <li>
                <strong>League Window:</strong>{" "}
                {durationMode === DURATION_FULL && "Full Season"}
                {durationMode === DURATION_WEEKS &&
                  (startWeekObj && endWeekObj
                    ? startWeekObj.id === endWeekObj.id
                      ? startWeekObj.label
                      : `${startWeekObj.label} – ${endWeekObj.label}`
                    : "—")}
                {durationMode === DURATION_DATES &&
                  `${startsOn || "—"} to ${endsOn || "—"}`}
              </li>
              <li>
                <strong>Visibility:</strong> {isPublic ? "Public" : "Private"}
              </li>
              <li>
                <strong>Joining:</strong>{" "}
                {joinPolicy === "CloseAtFirstGame"
                  ? "Locked at kickoff — closes when the first game starts"
                  : "Open while the league is live"}
              </li>
            </ul>
            <div className="modal-actions">
              <button onClick={() => setShowConfirmDialog(false)}>
                Cancel
              </button>
              <button onClick={finalizeLeagueCreation}>Confirm & Create</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default LeagueCreatePage;
