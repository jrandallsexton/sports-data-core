import { Link } from "react-router-dom";
import { useState } from "react";
import PickAccuracyChart from "./PickAccuracyChart";
import AIAccuracyChart from "./AIAccuracyChart";
import FeaturedArticleCard from "./FeaturedArticleCard";
import InfoSection from "./InfoSection";
import "./HomePage.css";

function HomePage() {
  const [selectedGroup, setSelectedGroup] = useState("all");
  const [selectedAIGroup, setSelectedAIGroup] = useState("all");

  // Sample data structure for multiple groups
  const pickGroups = {
    all: {
      name: "All Groups",
      data: [
        { week: "1", accuracy: 45, correctPicks: 9, totalPicks: 20 },
        { week: "2", accuracy: 65, correctPicks: 13, totalPicks: 20 },
        { week: "3", accuracy: 52, correctPicks: 10, totalPicks: 19 },
        { week: "4", accuracy: 78, correctPicks: 16, totalPicks: 20 },
        { week: "5", accuracy: 60, correctPicks: 12, totalPicks: 20 },
        { week: "6", accuracy: 85, correctPicks: 17, totalPicks: 20 },
        { week: "7", accuracy: 72, correctPicks: 14, totalPicks: 19 },
      ]
    },
    group1: {
      name: "Fantasy Football League",
      data: [
        { week: "1", accuracy: 50, correctPicks: 8, totalPicks: 16 },
        { week: "2", accuracy: 62, correctPicks: 10, totalPicks: 16 },
        { week: "3", accuracy: 56, correctPicks: 9, totalPicks: 16 },
        { week: "4", accuracy: 75, correctPicks: 12, totalPicks: 16 },
        { week: "5", accuracy: 68, correctPicks: 11, totalPicks: 16 },
        { week: "6", accuracy: 81, correctPicks: 13, totalPicks: 16 },
        { week: "7", accuracy: 75, correctPicks: 12, totalPicks: 16 },
      ]
    },
    group2: {
      name: "Office Pool",
      data: [
        { week: "1", accuracy: 40, correctPicks: 6, totalPicks: 15 },
        { week: "2", accuracy: 66, correctPicks: 10, totalPicks: 15 },
        { week: "3", accuracy: 46, correctPicks: 7, totalPicks: 15 },
        { week: "4", accuracy: 80, correctPicks: 12, totalPicks: 15 },
        { week: "5", accuracy: 53, correctPicks: 8, totalPicks: 15 },
        { week: "6", accuracy: 86, correctPicks: 13, totalPicks: 15 },
        { week: "7", accuracy: 66, correctPicks: 10, totalPicks: 15 },
      ]
    }
  };

  // AI Accuracy Data
  const aiAccuracyData = {
    all: [
      { week: "1", aiAccuracy: 55, correctPicks: 11, totalPicks: 20 },
      { week: "2", aiAccuracy: 70, correctPicks: 14, totalPicks: 20 },
      { week: "3", aiAccuracy: 65, correctPicks: 13, totalPicks: 20 },
      { week: "4", aiAccuracy: 82, correctPicks: 16, totalPicks: 19 },
      { week: "5", aiAccuracy: 75, correctPicks: 15, totalPicks: 20 },
      { week: "6", aiAccuracy: 88, correctPicks: 18, totalPicks: 20 },
      { week: "7", aiAccuracy: 80, correctPicks: 16, totalPicks: 20 },
    ],
    group1: [
      { week: "1", aiAccuracy: 56, correctPicks: 9, totalPicks: 16 },
      { week: "2", aiAccuracy: 68, correctPicks: 11, totalPicks: 16 },
      { week: "3", aiAccuracy: 62, correctPicks: 10, totalPicks: 16 },
      { week: "4", aiAccuracy: 81, correctPicks: 13, totalPicks: 16 },
      { week: "5", aiAccuracy: 75, correctPicks: 12, totalPicks: 16 },
      { week: "6", aiAccuracy: 87, correctPicks: 14, totalPicks: 16 },
      { week: "7", aiAccuracy: 81, correctPicks: 13, totalPicks: 16 },
    ],
    group2: [
      { week: "1", aiAccuracy: 53, correctPicks: 8, totalPicks: 15 },
      { week: "2", aiAccuracy: 73, correctPicks: 11, totalPicks: 15 },
      { week: "3", aiAccuracy: 66, correctPicks: 10, totalPicks: 15 },
      { week: "4", aiAccuracy: 80, correctPicks: 12, totalPicks: 15 },
      { week: "5", aiAccuracy: 73, correctPicks: 11, totalPicks: 15 },
      { week: "6", aiAccuracy: 86, correctPicks: 13, totalPicks: 15 },
      { week: "7", aiAccuracy: 80, correctPicks: 12, totalPicks: 15 },
    ]
  };

  return (
    <div className="home-page">
      {/* Leaderboard + Your Stats */}
      <section className="card-section">
        <div className="card">
          <h2>Current Leaderboard</h2>
          <p className="highlight-text">
            You are ranked <span className="highlight-number">12th</span>
          </p>
          <Link to="/app/leaderboard" className="card-link">
            View Full Leaderboard
          </Link>
        </div>

        <div className="card">
          <h2>Your Pick Record</h2>
          <ul className="pick-record">
            <li>
              Wins: <strong>55</strong>
            </li>
            <li>
              Losses: <strong>25</strong>
            </li>
            <li>
              Win %: <strong>68%</strong>
            </li>
          </ul>
          <Link to="/app/your-picks" className="card-link">
            View Your Weekly Picks
          </Link>
        </div>

        <div className="card">
          <h2>sportDeets AI Accuracy</h2>
          <p>
            Overall Correct Picks: <strong>74%</strong>
          </p>
          <Link to="/app/ai-performance" className="card-link">
            See Full AI Stats
          </Link>
        </div>
      </section>

      <section className="chart-section">
        <PickAccuracyChart 
          selectedGroup={selectedGroup}
          onGroupChange={setSelectedGroup}
          groups={pickGroups}
        />

        <AIAccuracyChart 
          selectedGroup={selectedAIGroup}
          onGroupChange={setSelectedAIGroup}
          groups={pickGroups}
          aiAccuracyData={aiAccuracyData}
        />
      </section>

      <InfoSection />
      <FeaturedArticleCard />
    </div>
  );
}

export default HomePage;
