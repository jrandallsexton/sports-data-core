import React, { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import apiWrapper from "../../api/apiWrapper";
import "./ContestOverview.css";
import ContestOverviewHeader from "./ContestOverviewHeader";
import ContestOverviewLeaders from "./ContestOverviewLeaders";
import ContestOverviewPlaylog from "./ContestOverviewPlaylog";
import ContestOverviewTeamStats from "./ContestOverviewTeamStats";
import ContestOverviewSummary from "./ContestOverviewSummary";
import ContestOverviewWinProb from "./ContestOverviewWinProb";
import ContestOverviewInfo from "./ContestOverviewInfo";
import ContestOverviewAnalysis from "./ContestOverviewAnalysis";

export default function ContestOverview() {
  const { contestId } = useParams();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    setLoading(true);
    apiWrapper.Contest.getContestOverview(contestId)
      .then((result) => {
        setData(result);
        setLoading(false);
      })
      .catch((err) => {
        setError(err);
        setLoading(false);
      });
  }, [contestId]);

  if (loading) return <div>Loading contest overview...</div>;
  if (error) return <div>Error loading contest overview.</div>;
  if (!data || !data.data || !data.data.header) {
    return <div>No contest data available. (Debug: {JSON.stringify(data)})</div>;
  }

  const { header } = data.data;
  const { homeTeam, awayTeam, venueName, location, weekLabel, startTimeUtc, status, quarterScores } = header;
  const { leaders, scoringSummary, teamStats, matchupAnalysis, summary, winProbability, info } = data.data;

  // Helper: get total scores from quarterScores
  const awayTotal = quarterScores.reduce((sum, q) => sum + q.awayScore, 0);
  const homeTotal = quarterScores.reduce((sum, q) => sum + q.homeScore, 0);

  return (
    <div className="contest-overview-container">
      <ContestOverviewHeader homeTeam={homeTeam} awayTeam={awayTeam} quarterScores={quarterScores} />
      <div className="contest-section-separator"></div>
      <ContestOverviewLeaders homeTeam={homeTeam} awayTeam={awayTeam} leaders={leaders} />
      <div className="contest-section-separator"></div>
      <ContestOverviewPlaylog scoringSummary={scoringSummary} />
      <div className="contest-section-separator"></div>
      <ContestOverviewTeamStats homeTeam={homeTeam} awayTeam={awayTeam} teamStats={teamStats} />
      <div className="contest-section-separator"></div>
      <ContestOverviewSummary summary={summary} />
      <div className="contest-section-separator"></div>
      <ContestOverviewWinProb winProbability={winProbability} />
      <div className="contest-section-separator"></div>
      <ContestOverviewInfo info={info} />
      <div className="contest-section-separator"></div>
      <ContestOverviewAnalysis matchupAnalysis={matchupAnalysis} />
    </div>
  );
}
