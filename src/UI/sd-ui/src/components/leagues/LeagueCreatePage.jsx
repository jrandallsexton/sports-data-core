import React, { useState } from "react";
import { useNavigate } from "react-router-dom";

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
  const navigate = useNavigate();

  const CONFERENCES = [
    { shortName: "American", slug: "american" },
    { shortName: "ACC", slug: "acc" },
    { shortName: "Big 12", slug: "big-12" },
    { shortName: "CUSA", slug: "cusa" },
    { shortName: "Big Ten", slug: "big-ten" },
    { shortName: "MAC", slug: "mac" },
    { shortName: "FBS Indep.", slug: "fbs-indep" },
    { shortName: "Mountain West", slug: "mountain-west" },
    { shortName: "Pac-12", slug: "pac-12" },
    { shortName: "SEC", slug: "sec" },
    { shortName: "Sun Belt", slug: "sun-belt" },
  ];

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
    });

    console.log("Sending payload:", payload);

    try {
      const response = await LeaguesApi.createLeague(payload);
      console.log("League created!", response);

      navigate(`/app/league/${response.id}`);
    } catch (error) {
      console.error("Failed to create league:", error);
      alert("An error occurred while creating the league.");
    }

    setShowConfirmDialog(false);
  };

  return (
    <div className="page-container">
      <h1>Create a New Pick’em League</h1>
      <p>Let’s set up your custom league so you can compete with friends.</p>

      <div className="card">
        <h2>How It Works</h2>
        <p>
          Pick’em leagues are public or private groups where players compete by
          making weekly picks for college football games.
        </p>
        <ul>
          <li>Choose your league name and rules</li>
          <li>Invite friends with a shareable code</li>
          <li>Track wins, streaks, and weekly leaders</li>
        </ul>
        <p>
          You’re in control — whether you want casual fun or competitive trash
          talk, we’ve got you covered.
        </p>
      </div>

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
              <table className="checkbox-table">
                <tbody>
                  {chunk(CONFERENCES, 3).map((row, rowIndex) => (
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
                        const conf = CONFERENCES.find((c) => c.slug === slug);
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
                <strong>Pick Deadline:</strong> 15 minutes before kickoff
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
