import React, { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import apiWrapper from "../../api/apiWrapper";
import "./ContestOverview.css";
import ContestOverviewHeader from "./ContestOverviewHeader";
import ContestOverviewLeaders from "./ContestOverviewLeaders";
import ContestOverviewPlaylog from "./ContestOverviewPlaylog";
import ContestOverviewTeamStats from "./ContestOverviewTeamStats";
import ContestOverviewWinProb from "./ContestOverviewWinProb";
import ContestOverviewInfo from "./ContestOverviewInfo";

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
  const dto = data?.data || data;
  if (!dto || !dto.header) {
    return <div>No contest data available. (Debug: {JSON.stringify(data)})</div>;
  }

  const { header, info, leaders, playLog, teamStats, winProbability } = dto;
  const { homeTeam, awayTeam, quarterScores } = header;

  return (
    <div className="contest-overview-container">
      <ContestOverviewHeader homeTeam={homeTeam} awayTeam={awayTeam} quarterScores={quarterScores} />
      <div className="contest-overview-grid">
        <div className="contest-overview-col">
          <ContestOverviewLeaders homeTeam={homeTeam} awayTeam={awayTeam} leaders={leaders} />
          <ContestOverviewPlaylog playLog={playLog} />
        </div>
        <div className="contest-overview-col">
          <ContestOverviewTeamStats homeTeam={homeTeam} awayTeam={awayTeam} teamStats={teamStats} />
          {/* If summary and matchupAnalysis are still present in the new DTO, pass them here. Otherwise, remove these lines. */}
          {/* <ContestOverviewSummary summary={summary} /> */}
          <ContestOverviewWinProb winProbability={winProbability} homeTeam={homeTeam} awayTeam={awayTeam} />
        </div>
        <div className="contest-overview-col">
          <ContestOverviewInfo info={info} />
          {/* <ContestOverviewAnalysis matchupAnalysis={matchupAnalysis} /> */}
        </div>
      </div>
    </div>
  );
}
