import { Link } from "react-router-dom";
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Cell,
  ReferenceLine,
  Legend,
  LabelList,
} from "recharts";
import "./HomePage.css";

// Custom label component
const CustomLabel = ({ x, y, value, data, index }) => {
  return (
    <text
      x={x}
      y={y - 5}
      fill="#fff"
      textAnchor="middle"
      dominantBaseline="bottom"
    >
      {`${value}/${data[index].totalPicks}`}
    </text>
  );
};

function HomePage() {
  const pickAccuracyData = [
    { week: "1", accuracy: 45, correctPicks: 9, totalPicks: 20 },
    { week: "2", accuracy: 65, correctPicks: 13, totalPicks: 20 },
    { week: "3", accuracy: 52, correctPicks: 10, totalPicks: 19 },
    { week: "4", accuracy: 78, correctPicks: 16, totalPicks: 20 },
    { week: "5", accuracy: 60, correctPicks: 12, totalPicks: 20 },
    { week: "6", accuracy: 85, correctPicks: 17, totalPicks: 20 },
    { week: "7", accuracy: 72, correctPicks: 14, totalPicks: 19 },
  ];

  // ✨ NEW: AI Accuracy Data
  const aiAccuracyData = [
    { week: "1", aiAccuracy: 55, correctPicks: 11, totalPicks: 20 },
    { week: "2", aiAccuracy: 70, correctPicks: 14, totalPicks: 20 },
    { week: "3", aiAccuracy: 65, correctPicks: 13, totalPicks: 20 },
    { week: "4", aiAccuracy: 82, correctPicks: 16, totalPicks: 19 },
    { week: "5", aiAccuracy: 75, correctPicks: 15, totalPicks: 20 },
    { week: "6", aiAccuracy: 88, correctPicks: 18, totalPicks: 20 },
    { week: "7", aiAccuracy: 80, correctPicks: 16, totalPicks: 20 },
  ];

  // Find the maximum value across both datasets
  const maxValue = Math.max(
    ...pickAccuracyData.map(d => d.accuracy),
    ...aiAccuracyData.map(d => d.aiAccuracy)
  );

  // Calculate mean values
  const pickMean = pickAccuracyData.reduce((sum, item) => sum + item.accuracy, 0) / pickAccuracyData.length;
  const aiMean = aiAccuracyData.reduce((sum, item) => sum + item.aiAccuracy, 0) / aiAccuracyData.length;

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
  
  // Custom gradient component for the bars
  const GradientBar = ({ dataKey, data }) => {
    return (
      <Bar dataKey={dataKey} radius={[4, 4, 0, 0]} barSize={30}>
        {data.map((entry, index) => {
          const value = entry[dataKey];
          let color;
          if (value < 50) {
            // Red to yellow gradient for values below 50%
            const ratio = value / 50;
            color = `rgb(${255}, ${Math.round(255 * ratio)}, 0)`;
          } else {
            // Yellow to green gradient for values above 50%
            const ratio = (value - 50) / 50;
            color = `rgb(${Math.round(255 * (1 - ratio))}, 255, 0)`;
          }
          return <Cell key={`cell-${index}`} fill={color} />;
        })}
      </Bar>
    );
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
                margin={{ top: 20, right: 30, left: 30, bottom: 5 }}
              >
                <defs>
                  <linearGradient id="accuracyGradient" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#00ff00" />
                    <stop offset="50%" stopColor="#ffff00" />
                    <stop offset="100%" stopColor="#ff0000" />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#444" />
                <XAxis 
                  dataKey="week" 
                  stroke="#ccc"
                  tick={{ fill: '#ccc' }}
                />
                <YAxis 
                  stroke="#ccc"
                  tick={{ fill: '#ccc' }}
                  domain={[0, maxValue]}
                  tickFormatter={(value) => `${value}%`}
                  width={40}
                  ticks={[0, 20, 40, 60, 80, 100]}
                />
                <Tooltip 
                  contentStyle={{ 
                    backgroundColor: '#1a1a1a',
                    border: '1px solid #333',
                    borderRadius: '8px',
                    color: '#ddd'
                  }}
                  wrapperStyle={{
                    backgroundColor: 'transparent',
                    border: 'none'
                  }}
                  cursor={{ fill: 'rgba(255, 255, 255, 0.1)' }}
                  formatter={(value, name, props) => [`${props.payload.correctPicks}/${props.payload.totalPicks}`, 'Correct Picks']}
                />
                <ReferenceLine 
                  y={pickMean} 
                  stroke="#61dafb"
                  strokeDasharray="3 3"
                />
                <Legend 
                  content={() => (
                    <div style={{ 
                      display: 'flex', 
                      justifyContent: 'center', 
                      marginTop: '10px',
                      color: '#61dafb'
                    }}>
                      Mean Accuracy: {pickMean.toFixed(1)}%
                    </div>
                  )}
                />
                <Bar 
                  dataKey="accuracy" 
                  fill="url(#accuracyGradient)"
                  radius={[4, 4, 0, 0]}
                  barSize={30}
                >
                  <LabelList 
                    dataKey="accuracy" 
                    position="top" 
                    fill="#fff"
                    formatter={(value) => `${value}%`}
                  />
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>

        <div className="chart-block">
          <h2>AI Accuracy by Week</h2>
          <div className="chart-container">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart
                data={aiAccuracyData}
                margin={{ top: 20, right: 30, left: 30, bottom: 5 }}
              >
                <defs>
                  <linearGradient id="aiAccuracyGradient" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#00ff00" />
                    <stop offset="50%" stopColor="#ffff00" />
                    <stop offset="100%" stopColor="#ff0000" />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#444" />
                <XAxis 
                  dataKey="week" 
                  stroke="#ccc"
                  tick={{ fill: '#ccc' }}
                />
                <YAxis 
                  stroke="#ccc"
                  tick={{ fill: '#ccc' }}
                  domain={[0, maxValue]}
                  tickFormatter={(value) => `${value}%`}
                  width={40}
                  ticks={[0, 20, 40, 60, 80, 100]}
                />
                <Tooltip 
                  contentStyle={{ 
                    backgroundColor: '#1a1a1a',
                    border: '1px solid #333',
                    borderRadius: '8px',
                    color: '#ddd'
                  }}
                  wrapperStyle={{
                    backgroundColor: 'transparent',
                    border: 'none'
                  }}
                  cursor={{ fill: 'rgba(255, 255, 255, 0.1)' }}
                  formatter={(value, name, props) => [`${props.payload.correctPicks}/${props.payload.totalPicks}`, 'Correct Picks']}
                />
                <ReferenceLine 
                  y={aiMean} 
                  stroke="#61dafb"
                  strokeDasharray="3 3"
                />
                <Legend 
                  content={() => (
                    <div style={{ 
                      display: 'flex', 
                      justifyContent: 'center', 
                      marginTop: '10px',
                      color: '#61dafb'
                    }}>
                      Mean Accuracy: {aiMean.toFixed(1)}%
                    </div>
                  )}
                />
                <Bar 
                  dataKey="aiAccuracy" 
                  fill="url(#aiAccuracyGradient)"
                  radius={[4, 4, 0, 0]}
                  barSize={30}
                >
                  <LabelList 
                    dataKey="aiAccuracy" 
                    position="top" 
                    fill="#fff"
                    formatter={(value) => `${value}%`}
                  />
                </Bar>
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
