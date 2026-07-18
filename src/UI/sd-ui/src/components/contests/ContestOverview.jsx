import React, { useEffect, useState } from "react";
import { useUserDto } from "../../contexts/UserContext";
import { useParams } from "react-router-dom";
import apiWrapper from "../../api/apiWrapper";
import "./ContestOverview.css";
import ContestOverviewHeader from "./ContestOverviewHeader";
import ContestOverviewLeaders from "./ContestOverviewLeaders";
import ContestOverviewPlaylog from "./ContestOverviewPlaylog";
// ContestOverviewTeamStats temporarily removed until teamStats DTO is available
import ContestOverviewWinProb from "./ContestOverviewWinProb";
import ContestOverviewVideo from "./ContestOverviewVideo";
import ContestOverviewInfo from "./ContestOverviewInfo";
import ContestOverviewMetrics from "./ContestOverviewMetrics";
import ContestOverviewAdmin from "./ContestOverviewAdmin";

export default function ContestOverview() {
  const { sport, league, contestId } = useParams();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const { userDto } = useUserDto();
  const isAdmin = userDto?.isAdmin;

  useEffect(() => {
    setLoading(true);
    apiWrapper.Contest.getContestOverview(contestId, sport, league)
      .then((result) => {
        setData(result);
        setLoading(false);
      })
      .catch((err) => {
        setError(err);
        setLoading(false);
      });
  }, [contestId, sport, league]);

  if (loading) return <div>Loading contest overview...</div>;
  if (error) return <div>Error loading contest overview.</div>;
  const dto = data?.data || data;
  if (!dto || !dto.header) {
    return <div>No contest data available. (Debug: {JSON.stringify(data)})</div>;
  }

  const { header, info, leaders, playLog, winProbability, homeMetrics, awayMetrics, mediaItems } = dto;
  const { homeTeam, awayTeam, quarterScores } = header;

  return (
    <div className="contest-overview-container">
      <ContestOverviewHeader homeTeam={homeTeam} awayTeam={awayTeam} quarterScores={quarterScores} seasonYear={header.seasonYear} sport={sport} league={league} status={header.statusDescription ?? header.status} />
      <div className="contest-overview-grid">
        <div className="contest-overview-col">
          <ContestOverviewLeaders homeTeam={homeTeam} awayTeam={awayTeam} leaders={leaders} />
        </div>
        <div className="contest-overview-col">
          {/* Video component above Win Probability */}
          <ContestOverviewVideo mediaItems={mediaItems} />
          {/* Win probability moved up until TeamStats is available */}
          <ContestOverviewWinProb winProbability={winProbability} homeTeam={homeTeam} awayTeam={awayTeam} sport={sport} />
          <ContestOverviewMetrics homeMetrics={homeMetrics} awayMetrics={awayMetrics} homeName={homeTeam?.displayName} awayName={awayTeam?.displayName} />
        </div>
        <div className="contest-overview-col">
          <ContestOverviewPlaylog playLog={playLog} sport={sport} contestId={contestId} league={league} />
          <ContestOverviewInfo info={info} />
          {/* Admin section sits in the third column below Play Log and Info.
              Hidden from non-admins via the isAdmin gate. */}
          {isAdmin && (
            <ContestOverviewAdmin contestId={contestId} sport={sport} league={league} />
          )}
        </div>
      </div>
    </div>
  );
}
