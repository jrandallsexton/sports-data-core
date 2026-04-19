import React, { useState, useEffect, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import apiWrapper from "../../api/apiWrapper.js";
import { useUserDto } from "../../contexts/UserContext";

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
    maxWeeks: 16,
  },
  [SPORT_NFL]: {
    label: "NFL",
    groupLabel: "Divisions",
    groupEmoji: "🏈",
    tiebreakerTotalLabel: "Closest to Total Points",
    namePlaceholder: "e.g., Sunday Funday",
    descPlaceholder: "A fun league for NFL fans.",
    maxWeeks: 22,
  },
  [SPORT_MLB]: {
    label: "MLB",
    groupLabel: "Divisions",
    groupEmoji: "⚾",
    tiebreakerTotalLabel: "Closest to Total Runs",
    namePlaceholder: "e.g., Ninth Inning",
    descPlaceholder: "A fun league for MLB fans.",
    maxWeeks: 26,
  },
};

const DURATION_FULL = "full";
const DURATION_WEEKS = "weeks";
const DURATION_DATES = "dates";

const LeagueCreatePage = () => {
  const { userDto, refreshUserDto } = useUserDto();
  const [sport, setSport] = useState(SPORT_NCAA);
  const [leagueName, setLeagueName] = useState("");
  const [description, setDescription] = useState("");
  const [pickType, setPickType] = useState("");
  const [tiebreaker, setTiebreaker] = useState("");
  const [useConfidencePoints, setUseConfidencePoints] = useState(false);
  const [teamFilter, setTeamFilter] = useState([]);
  const [rankingFilter, setRankingFilter] = useState("");
  const [showConfirmDialog, setShowConfirmDialog] = useState(false);
  const [isPublic, setIsPublic] = useState(false);
  const [dropLowWeeksCount, setDropLowWeeksCount] = useState(0);
  const [allConferences, setAllConferences] = useState([]);
  const [fbsOnly, setFbsOnly] = useState(true);
  const [durationMode, setDurationMode] = useState(DURATION_FULL);
  const [startWeek, setStartWeek] = useState(1);
  const [endWeek, setEndWeek] = useState(1);
  const [startsOn, setStartsOn] = useState("");
  const [endsOn, setEndsOn] = useState("");

  const navigate = useNavigate();

  const isNcaa = sport === SPORT_NCAA;
  const isMlbAvailable = userDto?.isAdmin === true;
  const copy = SPORT_COPY[sport];

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
  useEffect(() => {
    setTeamFilter([]);
    if (!isNcaa) {
      setRankingFilter("");
    }
    // Clamp week selection if switching to a sport with fewer weeks.
    const max = SPORT_COPY[sport].maxWeeks;
    setStartWeek((w) => Math.min(w, max));
    setEndWeek((w) => Math.min(w, max));
  }, [sport, isNcaa]);

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
    setShowConfirmDialog(true);
  };

  const finalizeLeagueCreation = async () => {
    if (durationMode === DURATION_WEEKS) {
      alert(
        "Week Range isn't wired up yet — it needs the season calendar endpoint. Use Full Season or Date Range for now."
      );
      return;
    }

    const formState = {
      leagueName,
      description,
      pickType,
      tiebreaker,
      useConfidencePoints,
      rankingFilter,
      teamFilter,
      isPublic,
      dropLowWeeksCount,
      durationMode,
      startsOn,
      endsOn,
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
      alert("An error occurred while creating the league.");
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
            className={`segmented-tab${sport === SPORT_NCAA ? " active" : ""}`}
            onClick={() => setSport(SPORT_NCAA)}
          >
            NCAA
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={sport === SPORT_NFL}
            className={`segmented-tab${sport === SPORT_NFL ? " active" : ""}`}
            onClick={() => setSport(SPORT_NFL)}
          >
            NFL
          </button>
          {isMlbAvailable && (
            <button
              type="button"
              role="tab"
              aria-selected={sport === SPORT_MLB}
              className={`segmented-tab${sport === SPORT_MLB ? " active" : ""}`}
              onClick={() => setSport(SPORT_MLB)}
            >
              MLB
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

          <div className="form-group">
            <label htmlFor="description">Description (optional)</label>
            <textarea
              id="description"
              name="description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder={copy.descPlaceholder}
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

            <div className="form-group">
              <label htmlFor="dropLowWeeksCount">Drop Low Weeks</label>
              <select
                id="dropLowWeeksCount"
                name="dropLowWeeksCount"
                value={dropLowWeeksCount}
                onChange={(e) => setDropLowWeeksCount(Number(e.target.value))}
              >
                <option value={0}>None. Use All Weeks</option>
                <option value={1}>1</option>
                <option value={2}>2</option>
                <option value={3}>3</option>
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
                    value={startWeek}
                    onChange={(e) => setStartWeek(Number(e.target.value))}
                  >
                    {Array.from(
                      { length: copy.maxWeeks },
                      (_, i) => i + 1
                    ).map((w) => (
                      <option key={w} value={w}>
                        Week {w}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="form-group">
                  <label htmlFor="endWeek">End Week</label>
                  <select
                    id="endWeek"
                    value={endWeek}
                    onChange={(e) => setEndWeek(Number(e.target.value))}
                  >
                    {Array.from(
                      { length: copy.maxWeeks },
                      (_, i) => i + 1
                    ).map((w) => (
                      <option key={w} value={w}>
                        Week {w}
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
                    onChange={(e) => setStartsOn(e.target.value)}
                  />
                </div>
                <div className="form-group">
                  <label htmlFor="endsOn">End Date</label>
                  <input
                    type="date"
                    id="endsOn"
                    value={endsOn}
                    onChange={(e) => setEndsOn(e.target.value)}
                  />
                </div>
              </div>
            )}
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
            </div>
          </div>

          <button type="submit" className="submit-button">
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
                <strong>Description:</strong> {description || "None"}
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
                  `Weeks ${startWeek}–${endWeek}`}
                {durationMode === DURATION_DATES &&
                  `${startsOn || "—"} to ${endsOn || "—"}`}
              </li>
              <li>
                <strong>Visibility:</strong> {isPublic ? "Public" : "Private"}
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
