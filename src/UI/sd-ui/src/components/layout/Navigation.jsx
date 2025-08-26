// ./layout/Navigation.jsx
import { NavLink } from 'react-router-dom';
import {
  FaHome,
  FaFootballBall,
  FaTrophy,
  FaComments,
  FaCog,
  FaSignOutAlt,
  FaBars,
  FaTimes
} from "react-icons/fa";
import './Navigation.css';

function Navigation({ isSideNav, onToggle, onSignOut }) {
  if (isSideNav) {
    return (
      <>
        <nav className="navigation side-nav">
          <div className="nav-header">
            <NavLink to="/app/" className="logo" end>sportDeets</NavLink>
          </div>
          <div className="nav-links">
            <NavLink to="/app/" className="nav-link" end>
              <FaHome className="nav-icon" />
              <span>Home</span>
            </NavLink>
            <NavLink to="/app/picks" className="nav-link">
              <FaFootballBall className="nav-icon" />
              <span>Picks</span>
            </NavLink>
            <NavLink to="/app/leaderboard" className="nav-link">
              <FaTrophy className="nav-icon" />
              <span>Leaderboard</span>
            </NavLink>
            <NavLink to="/app/messageboard" className="nav-link">
              <FaComments className="nav-icon" />
              <span>Message Board</span>
            </NavLink>
            <NavLink to="/app/settings" className="nav-link">
              <FaCog className="nav-icon" />
              <span>Settings</span>
            </NavLink>
          </div>
          <div className="nav-actions">
            <button 
              className="nav-toggle"
              onClick={onToggle}
              title="Switch to Top Navigation"
            >
              <FaTimes />
            </button>
            <button className="nav-link logout-button" onClick={onSignOut}>
              <FaSignOutAlt className="nav-icon" />
              <span>Sign Out</span>
            </button>
          </div>
        </nav>
        <div className="side-nav-spacer"></div>
      </>
    );
  }

  return (
    <nav className="navigation top-nav">
      <div className="nav-header">
        <NavLink to="/app/" className="logo" end>sportDeets</NavLink>
      </div>
      <div className="nav-links">
        <table>
          <tbody>
            <tr>
              <td>
                <NavLink to="/app/" className="nav-link" end>
                  <FaHome className="nav-icon" />
                  <span>Home</span>
                </NavLink>
              </td>
              <td>
                <NavLink to="/app/picks" className="nav-link">
                  <FaFootballBall className="nav-icon" />
                  <span>Picks</span>
                </NavLink>
              </td>
              <td>
                <NavLink to="/app/leaderboard" className="nav-link">
                  <FaTrophy className="nav-icon" />
                  <span>Leaderboard</span>
                </NavLink>
              </td>
              <td>
                <NavLink to="/app/messageboard" className="nav-link">
                  <FaComments className="nav-icon" />
                  <span>Message Board</span>
                </NavLink>
              </td>
              <td>
                <NavLink to="/app/settings" className="nav-link">
                  <FaCog className="nav-icon" />
                  <span>Settings</span>
                </NavLink>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <div className="nav-actions">
        <button 
          className="nav-toggle"
          onClick={onToggle}
          title="Switch to Side Navigation"
        >
          <FaBars />
        </button>
        <button className="nav-link logout-button" onClick={onSignOut}>
          <FaSignOutAlt className="nav-icon" />
          <span>Sign Out</span>
        </button>
      </div>
    </nav>
  );
}

export default Navigation; 