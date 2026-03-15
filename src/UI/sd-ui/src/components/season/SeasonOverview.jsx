import React, { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import apiWrapper from "../../api/apiWrapper";
import "./SeasonOverview.css";

export default function SeasonOverview() {
  const { seasonYear } = useParams();
  const navigate = useNavigate();

  const [overviewData, setOverviewData] = useState(null);
  const [overviewLoading, setOverviewLoading] = useState(true);
  const [overviewError, setOverviewError] = useState(null);

  const [selectedYear, setSelectedYear] = useState(seasonYear);
  const [selectedWeek, setSelectedWeek] = useState(null);
  const [selectedPoll, setSelectedPoll] = useState(null);

  const [rankings, setRankings] = useState(null);
  const [rankingsLoading, setRankingsLoading] = useState(false);
  const [rankingsError, setRankingsError] = useState(null);

  // Fetch season overview when seasonYear changes
  useEffect(() => {
    setOverviewLoading(true);
    setOverviewError(null);
    setOverviewData(null);
    setRankings(null);

    apiWrapper.Season.getSeasonOverview(seasonYear)
      .then((result) => {
        const dto = result?.data || result;
        setOverviewData(dto);

        // Default to first week and first poll (use week id for unique identification)
        if (dto?.weeks?.length > 0) {
          setSelectedWeek(dto.weeks[0].id);
        } else {
          setSelectedWeek(null);
        }
        if (dto?.polls?.length > 0) {
          setSelectedPoll(dto.polls[0].slug ?? dto.polls[0].shortName ?? dto.polls[0]);
        } else {
          setSelectedPoll(null);
        }

        setOverviewLoading(false);
      })
      .catch((err) => {
        setOverviewError(err);
        setOverviewLoading(false);
      });
  }, [seasonYear]);

  // Fetch rankings when week or poll changes
  useEffect(() => {
    if (selectedWeek == null || selectedPoll == null) return;

    setRankingsLoading(true);
    setRankingsError(null);

    apiWrapper.Rankings.getRankingsByWeekId(selectedWeek, selectedPoll)
      .then((result) => {
        const dto = result?.data || result;
        setRankings(dto);
        setRankingsLoading(false);
      })
      .catch((err) => {
        setRankingsError(err);
        setRankingsLoading(false);
      });
  }, [selectedWeek, selectedPoll]);

  // Handle year change - navigate to new route
  const handleYearChange = (e) => {
    const newYear = e.target.value;
    setSelectedYear(newYear);
    navigate(`/app/football/${newYear}`);
  };

  const handleWeekChange = (e) => {
    setSelectedWeek(e.target.value);
  };

  const handlePollChange = (e) => {
    setSelectedPoll(e.target.value);
  };

  // Generate year options (current year back to 2000)
  const currentYear = new Date().getFullYear();
  const yearOptions = [];
  for (let y = currentYear; y >= 2000; y--) {
    yearOptions.push(y);
  }

  const formatTrend = (trend) => {
    if (trend == null || trend === "" || trend === "0") {
      return <span className="season-overview-trend-none">--</span>;
    }
    const num = Number(trend);
    if (isNaN(num) || num === 0) {
      return <span className="season-overview-trend-none">--</span>;
    }
    if (num > 0) {
      return <span className="season-overview-trend-up">+{num}</span>;
    }
    return <span className="season-overview-trend-down">{num}</span>;
  };

  if (overviewLoading) {
    return (
      <div className="season-overview-container">
        <div className="season-overview-loading">Loading season overview...</div>
      </div>
    );
  }

  if (overviewError) {
    return (
      <div className="season-overview-container">
        <div className="season-overview-error">
          Error loading season overview. Please try again.
        </div>
      </div>
    );
  }

  const weeks = overviewData?.weeks || [];
  const polls = overviewData?.polls || [];
  const rankingItems = rankings?.entries || [];

  return (
    <div className="season-overview-container">
      <h1 className="season-overview-title">Season Overview - {seasonYear}</h1>

      <div className="season-overview-selectors">
        <div className="season-overview-selector-group">
          <label className="season-overview-selector-label" htmlFor="year-select">
            Year
          </label>
          <select
            id="year-select"
            className="season-overview-select"
            value={selectedYear}
            onChange={handleYearChange}
          >
            {yearOptions.map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
        </div>

        <div className="season-overview-selector-group">
          <label className="season-overview-selector-label" htmlFor="week-select">
            Week
          </label>
          <select
            id="week-select"
            className="season-overview-select"
            value={selectedWeek ?? ""}
            onChange={handleWeekChange}
            disabled={weeks.length === 0}
          >
            {weeks.length === 0 && <option value="">No weeks available</option>}
            {weeks.map((w) => {
              const weekLabel = w.label ?? `Week ${w.number ?? w}`;
              return (
                <option key={w.id} value={w.id}>
                  {weekLabel}
                </option>
              );
            })}
          </select>
        </div>

        <div className="season-overview-selector-group">
          <label className="season-overview-selector-label" htmlFor="poll-select">
            Poll
          </label>
          <select
            id="poll-select"
            className="season-overview-select"
            value={selectedPoll ?? ""}
            onChange={handlePollChange}
            disabled={polls.length === 0}
          >
            {polls.length === 0 && <option value="">No polls available</option>}
            {polls.map((p) => {
              const pollValue = p.slug ?? p.shortName ?? p;
              const pollLabel = p.name ?? p.shortName ?? pollValue;
              return (
                <option key={p.id ?? pollValue} value={pollValue}>
                  {pollLabel}
                </option>
              );
            })}
          </select>
        </div>
      </div>

      {weeks.length === 0 && polls.length === 0 && (
        <div className="season-overview-empty">
          No season data available for {seasonYear}.
        </div>
      )}

      {rankingsLoading && (
        <div className="season-overview-loading">Loading rankings...</div>
      )}

      {rankingsError && (
        <div className="season-overview-error">
          Error loading rankings. Please try again.
        </div>
      )}

      {!rankingsLoading && !rankingsError && rankingItems.length > 0 && (
        <div className="season-overview-table-wrapper">
          <table className="season-overview-table">
            <thead>
              <tr>
                <th className="col-rank">#</th>
                <th>Team</th>
                <th>Record</th>
                <th className="col-points">Points</th>
                <th className="col-first">1st</th>
                <th>Trend</th>
              </tr>
            </thead>
            <tbody>
              {rankingItems.map((item) => (
                <tr key={item.franchiseSeasonId}>
                  <td className="col-rank">
                    <span className="season-overview-rank">
                      {item.rank}
                    </span>
                  </td>
                  <td>
                    <div className="season-overview-team-cell">
                      {item.franchiseLogoUrl && (
                        <span className="season-overview-team-logo-wrap">
                          <img
                            className="season-overview-team-logo"
                            src={item.franchiseLogoUrl}
                            alt={item.franchiseName || ""}
                          />
                        </span>
                      )}
                      <span className="season-overview-team-name">
                        {item.franchiseName}
                      </span>
                    </div>
                  </td>
                  <td>
                    <span className="season-overview-record">
                      {item.wins != null && item.losses != null
                        ? `${item.wins}-${item.losses}`
                        : "--"}
                    </span>
                  </td>
                  <td className="col-points">{item.points ?? "--"}</td>
                  <td className="col-first">
                    {item.firstPlaceVotes ?? "--"}
                  </td>
                  <td>{formatTrend(item.trend)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {!rankingsLoading &&
        !rankingsError &&
        rankingItems.length === 0 &&
        selectedWeek != null &&
        selectedPoll != null && (
          <div className="season-overview-empty">
            No rankings available for this week and poll.
          </div>
        )}
    </div>
  );
}
