import { Routes, Route, NavLink } from 'react-router-dom';
import { FaHome, FaFootballBall, FaTrophy, FaComments } from 'react-icons/fa';
import './MainApp.css'; // Keep this

import matchups from './data/matchups.js'; // Import matchups data
import MatchupCard from './components/matchups/MatchupCard';
import PicksPage from './components/picks/PicksPage';
import LeaderboardPage from './components/leaderboard/LeaderboardPage'; // <-- NEW
import MessageBoardPage from './components/messageboard/MessageboardPage.js';

function HomePage() { return <div>🏠 Welcome Home</div>; }

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
        <NavLink to="/app/messageboard" className="nav-link">
            <FaComments className="nav-icon" /><span>Message Board</span>
        </NavLink>
        </nav>
      </aside>

      <main className="main-content">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/picks" element={<PicksPage />} />
          <Route path="/leaderboard" element={<LeaderboardPage />} />
          <Route path="/messageboard" element={<MessageBoardPage />} />
        </Routes>
      </main>
    </div>
  );
}

export default MainApp;
