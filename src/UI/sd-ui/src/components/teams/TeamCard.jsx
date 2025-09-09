import { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import apiWrapper from "../../api/apiWrapper";
import "./TeamCard.css";
import TeamSchedule from "./TeamSchedule";
//import TeamScheduleMUI from "./TeamScheduleMUI";
import TeamNews from "./TeamNews";
import TeamStatistics from "./TeamStatistics";

function TeamCard() {
  const { slug, seasonYear } = useParams();
  const navigate = useNavigate();
  const [team, setTeam] = useState(null);
  const [loading, setLoading] = useState(true);
  const [selectedTab, setSelectedTab] = useState("schedule");
  const [teamStats, setTeamStats] = useState(null);
  const [statsLoading, setStatsLoading] = useState(false);
  const [statsError, setStatsError] = useState(null);

  const resolvedSeason = seasonYear || new Date().getFullYear();

  useEffect(() => {
    const fetchTeam = async () => {
      try {
        const response = await apiWrapper.TeamCard.getBySlugAndSeason(slug, resolvedSeason);
        setTeam(response.data);
      } catch (error) {
        console.error("Failed to fetch team data:", error);
      } finally {
        setLoading(false);
      }
    };

    fetchTeam();
  }, [slug, resolvedSeason]);

  useEffect(() => {
    if (selectedTab === "statistics" && team && team.franchiseSeasonId) {
      setStatsLoading(true);
      setStatsError(null);
      apiWrapper.TeamCard.getStatistics(slug, resolvedSeason, team.franchiseSeasonId)
        .then((response) => {
          setTeamStats(response.data);
        })
        .catch((error) => {
          setStatsError("Failed to load statistics");
        })
        .finally(() => {
          setStatsLoading(false);
        });
    }
  }, [selectedTab, slug, resolvedSeason, team]);

  if (loading) return <div className="team-card">Loading team data…</div>;
  if (!team) return <div className="team-card">Team not found.</div>;

  return (
    <div
      className="team-card"
      style={{
        "--accent-color": team.colorPrimary || "#6c63ff",
        "--highlight-color": team.colorSecondary || "#ffc107",
      }}
    >
      <div className="team-header">
        <img
          src={team.logoUrl}
          alt={`${team.name} logo`}
          className="team-logo"
        />
        <div>
          <h2 className="team-name">{team.name}</h2>
          <p className="team-conference">
            {team.conferenceName} ({team.conferenceShortName})
          </p>
          <p className="team-record">
            {team.overallRecord} ({team.conferenceRecord})
          </p>
          <br/>
          <p className="team-stadium">
            {team.stadiumName}
            {team.stadiumCapacity > 0 && (
              <>
                {" – "}{team.stadiumCapacity.toLocaleString()} capacity
              </>
            )}
          </p>
        </div>

        {/* Season selector */}
        <div style={{ marginLeft: "auto" }}>
          <label htmlFor="seasonYear" style={{ marginRight: 8 }}>
            Season:
          </label>
          <select
            id="seasonYear"
            value={String(resolvedSeason)}
            onChange={(e) =>
              navigate(`/app/sport/football/ncaa/team/${slug}/${e.target.value}`)
            }
          >
            {team.seasonYears?.map((yr) => (
              <option key={yr} value={yr}>
                {yr}
              </option>
            ))}
          </select>
        </div>
      </div>

      {team.news && team.news.length > 0 && (
        <TeamNews news={team.news} />
      )}

      <div className="team-tabs">
        <button
          className={selectedTab === "schedule" ? "active" : ""}
          onClick={() => setSelectedTab("schedule")}
        >
          Schedule
        </button>
        <button
          className={selectedTab === "statistics" ? "active" : ""}
          onClick={() => setSelectedTab("statistics")}
        >
          Statistics
        </button>
      </div>

      <div className="team-card-content">
        {selectedTab === "schedule" && (
          <TeamSchedule schedule={team.schedule} seasonYear={resolvedSeason} />
        )}
        {selectedTab === "statistics" && (
          statsLoading ? (
            <div>Loading statistics…</div>
          ) : statsError ? (
            <div className="error">{statsError}</div>
          ) : (
            <TeamStatistics team={team} seasonYear={resolvedSeason} stats={teamStats} />
          )
        )}
      </div>
      {/* <TeamScheduleMUI schedule={team.schedule} seasonYear={resolvedSeason} /> */}
    </div>
  );
}

export default TeamCard;
