import { Link } from "react-router-dom";
import { useState, useEffect } from "react";
import PickAccuracyWidget from "../widgets/PickAccuracyWidget";
import AiAccuracyWidget from "../widgets/AiAccuracyWidget";
import FeaturedArticleCard from "./FeaturedArticleCard";

import TipWeekWidget from "../widgets/TipWeekWidget";
import NewsWidget from "../widgets/NewsWidget";
import LeaderboardWidget from "../widgets/LeaderboardWidget";
import PickRecordWidget from "../widgets/PickRecordWidget";
import AiRecordWidget from "../widgets/AiRecordWidget";
import "./HomePage.css";
import { useUserDto } from "../../contexts/UserContext";
import apiWrapper from "../../api/apiWrapper.js";
import SystemNews from "./SystemNews";
import RankingsWidget from "../widgets/RankingsWidget";

function HomePage() {
  const [pickGroups, setPickGroups] = useState([]); // Array of league DTOs
  const [syntheticDto, setSyntheticDto] = useState(null);
  const { userDto, loading: userLoading } = useUserDto();
  const [loadingPickAccuracy, setLoadingPickAccuracy] = useState(true);

  useEffect(() => {
    async function fetchPickAccuracy() {
      setLoadingPickAccuracy(true);
      try {
        const response = await apiWrapper.Picks.getAccuracyChartForUser();
        setPickGroups(response.data || []);
  // No need to set selectedGroup; selection is now handled in PickAccuracyWidget
      } catch (e) {
        setPickGroups([]);
      } finally {
        setLoadingPickAccuracy(false);
      }
    }
    fetchPickAccuracy();
  }, []);


  useEffect(() => {
    async function fetchSyntheticAccuracy() {
      try {
        const response = await apiWrapper.Picks.getAccuracyChartForSynthetic();
        setSyntheticDto(response.data || null);
      } catch (e) {
        setSyntheticDto(null);
      }
    }
    fetchSyntheticAccuracy();
  }, []);

  if (userLoading) {
    return <div>Loading your dashboard...</div>;
  }

  console.log("userDto:", userDto);

  return (
    <div className="home-page">
      <SystemNews />

      <section className="card-section">
        <div className="card">
          <RankingsWidget />
        </div>
      </section>
      {/* Leaderboard + Your Stats */}
      <section className="card-section">
        <LeaderboardWidget />
        <PickRecordWidget />
        <AiRecordWidget />
      </section>

      {/* Tips and News */}
      <section className="card-section">
        <div className="card">
          <h2>Pick'em Leagues</h2>
          <p>Create or join a private league to compete with friends.</p>
          <div style={{ display: "flex", gap: "1rem", marginTop: "1rem" }}>
            <Link to="/app/league/create" className="card-link">
              Create a League
            </Link>
            <Link to="/app/league/discover" className="card-link">
              Join a League
            </Link>
            {userDto?.leagues?.length > 0 && (
              <Link to="/app/league" className="card-link">
                My Leagues
              </Link>
            )}
          </div>
        </div>
        <div className="card">
          <TipWeekWidget />
        </div>
        <div className="card">
          <NewsWidget />
        </div>
      </section>

      <section className="chart-section">

        <div className="card">
          <PickAccuracyWidget leagues={pickGroups} />
          {loadingPickAccuracy && <div style={{color:'#ffc107',textAlign:'center'}}>Loading pick accuracy...</div>}
        </div>

        <div className="card">
          <AiAccuracyWidget syntheticDto={syntheticDto} />
        </div>
      </section>

      <div className="card">
        <FeaturedArticleCard />
      </div>
    </div>
  );
}

export default HomePage;
