import { Link } from "react-router-dom";
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from "recharts";
import "./HomePage.css";

function HomePage() {
  const pickAccuracyData = [
    { week: "1", accuracy: 62 },
    { week: "2", accuracy: 68 },
    { week: "3", accuracy: 71 },
    { week: "4", accuracy: 65 },
    { week: "5", accuracy: 70 },
    { week: "6", accuracy: 74 },
    { week: "7", accuracy: 77 },
  ];

  // ✨ NEW: AI Accuracy Data
  const aiAccuracyData = [
    { week: "1", aiAccuracy: 70 },
    { week: "2", aiAccuracy: 72 },
    { week: "3", aiAccuracy: 75 },
    { week: "4", aiAccuracy: 73 },
    { week: "5", aiAccuracy: 76 },
    { week: "6", aiAccuracy: 78 },
    { week: "7", aiAccuracy: 80 },
  ];

  const articleCardStyle = {
    backgroundColor: "#2a2a2a",
    padding: "30px",
    borderRadius: "12px",
    boxShadow: "0 6px 12px rgba(0,0,0,0.4)",
    color: "#eee",
    lineHeight: "1.6",
    maxWidth: "800px",
    margin: "20px auto",
    fontSize: "1rem",
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
          <Link to="/leaderboard" className="card-link">
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
          <Link to="/your-picks" className="card-link">
            View Your Weekly Picks
          </Link>
        </div>

        <div className="card">
          <h2>sportDeets AI Accuracy</h2>
          <p>
            Overall Correct Picks: <strong>74%</strong>
          </p>
          <Link to="/ai-performance" className="card-link">
            See Full AI Stats
          </Link>
        </div>
      </section>

      <section className="chart-section">
        <div className="chart-block">
          <h2>Pick Accuracy by Week</h2>
          <div className="chart-container">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart
                data={pickAccuracyData}
                margin={{ top: 20, right: 30, left: 0, bottom: 5 }}
              >
                <CartesianGrid strokeDasharray="3 3" stroke="#444" />
                <XAxis dataKey="week" stroke="#ccc" />
                <YAxis stroke="#ccc" />
                <Tooltip />
                <Bar dataKey="accuracy" fill="#61dafb" />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>

        <div className="chart-block">
          <h2>sportDeets AI Accuracy by Week</h2>
          <div className="chart-container">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart
                data={aiAccuracyData}
                margin={{ top: 20, right: 30, left: 0, bottom: 5 }}
              >
                <CartesianGrid strokeDasharray="3 3" stroke="#444" />
                <XAxis dataKey="week" stroke="#ccc" />
                <YAxis stroke="#ccc" />
                <Tooltip />
                <Bar dataKey="aiAccuracy" fill="#4ea0d9" />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>
      </section>

      {/* Tip of the Week */}
      <section className="section">
        <h2>Tip of the Week</h2>
        <div className="tip-card">
          <p>
            <strong>💡 Pro Strategy:</strong> Balance underdogs and favorites!
            Most users win more by mixing picks based on matchup strength, not
            just spreads.
          </p>
          <p className="tip-note">
            Check this week's spread info before making final picks.
          </p>
        </div>
      </section>

      {/* News Section */}
      <section className="section">
        <h2>Latest News</h2>
        <div className="news-card">
          <p>
            <strong>🏈 Major upset:</strong> #14 seed upsets #2! Full recap and
            impact on playoff brackets <Link to="/news">here</Link>.
          </p>
          <p>
            <strong>🧠 AI Insights:</strong> SportDeets AI now factoring in
            player injuries into projections!{" "}
            <Link to="/ai-news">Learn more</Link>.
          </p>
        </div>
      </section>

      {/* Featured Article */}
      <section style={{ marginTop: "60px" }}>
        <h2>Featured Article: Mid-Season Pick Strategies</h2>
        <div style={articleCardStyle}>
          <p>
            <strong>🏆 Building a Winning Pick Strategy:</strong>
          </p>
          <p>
            As we reach the midpoint of the season, it's important to reevaluate
            your pick strategy. Early trends are helpful, but teams evolve.
            Injuries, morale, and home-field advantages start playing bigger
            roles. Don't just trust team records — look deeper into strength of
            schedule and player news.
          </p>
          <p>
            One of the best ways to gain an edge is by identifying overhyped
            favorites. Upsets tend to cluster late in the season when playoff
            pressure builds. Look for games where the spread seems too generous,
            and consider a calculated risk on the underdog.
          </p>
          <p>
            SportDeets AI models are also updating weekly now with more weight
            on injury reports, coaching changes, and even weather conditions —
            so be sure to review the AI recommendations in your dashboard before
            locking your picks!
          </p>
          <p style={{ fontStyle: "italic", marginTop: "20px" }}>
            🔥 Pro Tip: Balance 75% logical picks with 25% high-upside gambles
            late in the year!
          </p>
        </div>
      </section>
    </div>
  );
}

export default HomePage;
