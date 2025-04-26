import { Routes, Route, NavLink } from 'react-router-dom';
import { FaHome, FaFootballBall, FaTrophy } from 'react-icons/fa';
import './MainApp.css'; // Keep this

import matchups from './data/matchups.js'; // Import matchups data
import MatchupCard from './components/matchups/MatchupCard';
import PicksPage from './components/picks/PicksPage';
import LeaderboardPage from './components/leaderboard/LeaderboardPage'; // <-- NEW

function HomePage() { return <div>🏠 Welcome Home</div>; }
// function PicksPage() {
//     return <div>
//         <h2>🏈 Make your Picks</h2>
//         {matchups.map(m => (
//         <MatchupCard key={m.id} matchup={m} />
//       ))}
//         </div>;
// }
//function LeaderboardPage() { return <div>🏆 See the Leaderboard</div>; }

function MainApp() {
  return (
    <div className="app-container">
      <aside className="sidebar">
        <h1 className="sidebar-title">sportDeets</h1>
        <nav className="nav-links">
        <NavLink to="/app/" end className="nav-link">
            <FaHome className="nav-icon" /><span>Home</span>
        </NavLink>
        <NavLink to="/app/picks" className="nav-link">
            <FaFootballBall className="nav-icon" /><span>Picks</span>
        </NavLink>
        <NavLink to="/app/leaderboard" className="nav-link">
            <FaTrophy className="nav-icon" /><span>Leaderboard</span>
        </NavLink>
        </nav>
      </aside>

      <main className="main-content">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/picks" element={<PicksPage />} />
          <Route path="/leaderboard" element={<LeaderboardPage />} />
        </Routes>
      </main>
    </div>
  );
}

export default MainApp;
