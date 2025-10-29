import React, { useEffect, useState } from "react";
import { useUserDto } from "../../contexts/UserContext";
import ContestApi from "../../api/contestApi";
import toast from "react-hot-toast";
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

export default function ContestOverview() {
  const { contestId } = useParams();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const { userDto } = useUserDto();
  const isAdmin = userDto?.isAdmin;
  const [refreshing, setRefreshing] = useState(false);
  const [refreshError, setRefreshError] = useState(null);

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

  const { header, info, leaders, playLog, winProbability, homeMetrics, awayMetrics, mediaItems } = dto;
  const { homeTeam, awayTeam, quarterScores } = header;

  const handleRefresh = async () => {
    setRefreshing(true);
    setRefreshError(null);
    try {
      await ContestApi.refresh(contestId);
  toast.success("Contest refresh request submitted.");
    } catch (err) {
      setRefreshError("Failed to refresh contest.");
  toast.error("Failed to submit refresh request.");
    }
    setRefreshing(false);
  };

  return (
    <div className="contest-overview-container">
      <ContestOverviewHeader homeTeam={homeTeam} awayTeam={awayTeam} quarterScores={quarterScores} />
      <div className="contest-overview-grid">
        <div className="contest-overview-col">
          <ContestOverviewLeaders homeTeam={homeTeam} awayTeam={awayTeam} leaders={leaders} />
        </div>
        <div className="contest-overview-col">
          {/* Video component above Win Probability */}
          <ContestOverviewVideo mediaItems={mediaItems} />
          {/* Win probability moved up until TeamStats is available */}
          <ContestOverviewWinProb winProbability={winProbability} homeTeam={homeTeam} awayTeam={awayTeam} />
          <ContestOverviewMetrics homeMetrics={homeMetrics} awayMetrics={awayMetrics} homeName={homeTeam?.displayName} awayName={awayTeam?.displayName} />
          <ContestOverviewInfo info={info} />
        </div>
        <div className="contest-overview-col">
          <ContestOverviewPlaylog playLog={playLog} />
        </div>
      </div>
      {isAdmin && (
        <div style={{ marginTop: 32, textAlign: "center" }}>
          <button
            onClick={handleRefresh}
            disabled={refreshing}
            style={{ padding: "10px 24px", fontSize: 16, fontWeight: 600, borderRadius: 6, background: "#23272f", color: "#fff", border: "none", cursor: "pointer" }}
          >
            {refreshing ? "Refreshing..." : "Refresh Contest"}
          </button>
          {refreshError && <div style={{ color: "#d32f2f", marginTop: 8 }}>{refreshError}</div>}
        </div>
      )}
    </div>
  );
}
