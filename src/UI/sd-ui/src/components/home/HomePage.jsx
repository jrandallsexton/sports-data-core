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
import LeagueMembership from "./LeagueMembership";

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

  // Determine if user is new (no leagues joined yet)
  const isNewUser = !userDto?.leagues || userDto.leagues.length === 0;

  return (
    <div className="home-page">
      <SystemNews />

      {/* Show LeagueMembership at top for new users as CTA */}
      {isNewUser && (
        <section className="card-section">
          <LeagueMembership />
        </section>
      )}

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

      {/* Show LeagueMembership in original position for existing users */}
      {!isNewUser && (
        <section className="card-section">
          <LeagueMembership />
        </section>
      )}

      <section className="card-section">
        {/* Tips and News */}
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
