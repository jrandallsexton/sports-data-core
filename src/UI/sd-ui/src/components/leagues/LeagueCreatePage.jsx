import React, { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import apiWrapper from "../../api/apiWrapper.js";
import { useUserDto } from "../../contexts/UserContext";

import "./LeagueCreatePage.css";

import { buildCreateLeagueRequest } from "api/leagues/requests/CreateLeagueRequest";
import LeaguesApi from "api/leagues/leaguesApi";

const LeagueCreatePage = () => {
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

  const navigate = useNavigate();
  const { refreshUserDto } = useUserDto();

  useEffect(() => {
    const fetchConferences = async () => {
      try {
        const result =
          await apiWrapper.Conferences.getConferenceNamesAndSlugs();
        setAllConferences(result.data); // raw data
      } catch (error) {
        console.error("Failed to load conferences", error);
      }
    };

    fetchConferences();
  }, []);

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
    const payload = buildCreateLeagueRequest({
      leagueName,
      description,
      pickType,
      tiebreaker,
      useConfidencePoints,
      rankingFilter,
      teamFilter,
      isPublic,
      dropLowWeeksCount, // NEW
    });

    try {
      const response = await LeaguesApi.createLeague(payload);
      await refreshUserDto(); // Refresh user DTO to update leagues array
      navigate(`/app/league/${response.id}`);
    } catch (error) {
      console.error("Failed to create league:", error);
      alert("An error occurred while creating the league.");
    }

    setShowConfirmDialog(false);
  };

  const conferences = fbsOnly
    ? allConferences.filter((c) => c.division === "FBS (I-A)")
    : allConferences;

  return (
    <div className="page-container">
      <h1>Create a New Pick’em League</h1>
      <p>
        Let’s set up your custom league so you can compete with friends - or
        publish it for others to join!
      </p>

      <div className="card">
        <form className="league-form" onSubmit={handleFormSubmit}>
          <div className="form-group">
            <label htmlFor="leagueName">League Name</label>
            <input
              type="text"
              id="leagueName"
              name="leagueName"
              value={leagueName}
              onChange={(e) => setLeagueName(e.target.value)}
              placeholder="e.g., Saturday Showdown"
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
              placeholder="A fun league for SEC fans."
            />
          </div>

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
            <label>Teams Included</label>

            <div className="checkbox-section">
              <h4>🏆 Rankings</h4>
              <div className="radio-group">
                {[
                  { label: "None", value: "" },
                  { label: "AP Top 25", value: "AP_TOP_25" },
                  { label: "AP Top 20", value: "AP_TOP_20" },
                  { label: "AP Top 15", value: "AP_TOP_15" },
                  { label: "AP Top 10", value: "AP_TOP_10" },
                  { label: "AP Top 5", value: "AP_TOP_5" },
                ].map((option) => (
                  <label key={option.value}>
                    <input
                      type="radio"
                      name="rankingFilter"
                      value={option.value}
                      checked={rankingFilter === option.value}
                      onChange={(e) => setRankingFilter(e.target.value)}
                    />
                    {option.label}
                  </label>
                ))}
              </div>

              <h4>🏈 Conferences</h4>
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

              <table className="checkbox-table">
                <tbody>
                  {chunk(conferences, 3).map((row, rowIndex) => (
                    <tr key={rowIndex}>
                      {row.map((conf) => (
                        <td key={conf.slug}>
                          <label className="table-checkbox">
                            <input
                              type="checkbox"
                              value={conf.slug}
                              checked={teamFilter.includes(conf.slug)}
                              onChange={handleCheckboxChange}
                            />
                            {conf.shortName}
                          </label>
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>

              <h4>🌐 Other</h4>
              <div className="form-group">
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

          {/* NEW: Drop Low Weeks */}
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
              <option value="closest">Closest to Total Points</option>
            </select>
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
                <strong>Conferences:</strong>{" "}
                {teamFilter.length
                  ? teamFilter
                      .map((slug) => {
                        const conf = conferences.find((c) => c.slug === slug);
                        return conf?.shortName || slug;
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
              <li>
                <strong>Ranking Filter:</strong> {rankingFilter || "None"}
              </li>
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
