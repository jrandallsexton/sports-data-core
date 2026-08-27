// ./layout/Navigation.jsx
import { NavLink, useLocation } from 'react-router-dom';
import {
  FaHome,
  FaClipboardCheck,
  FaTrophy,
  FaComments,
  FaCog,
  FaSignOutAlt,
  FaBars,
  FaTimes,
  FaRocket,
  FaMapMarkedAlt
} from "react-icons/fa";
import Wordmark from '../brand/Wordmark';
import { useUserDto } from '../../contexts/UserContext';
import './Navigation.css';

function Navigation({ isSideNav, onToggle, onSignOut }) {
  // The Picks entry points at the league-less landing (/app/picks), but
  // the canonical URLs it redirects to are league-rooted
  // (/app/league/:id/picks...), which NavLink's own matching can't see —
  // mark it active by path shape instead.
  const location = useLocation();
  const picksLinkClass = /\/picks(\/|$)/.test(location.pathname)
    ? "nav-link active"
    : "nav-link";
  // Game Map is admin-only until the feature is launch-ready — hidden from
  // regular users in BOTH nav variants (the /app/map route is AdminRoute-
  // gated too, so a typed URL bounces).
  const { userDto } = useUserDto();
  const isAdmin = userDto?.isAdmin === true;

  // Auto-close menu on mobile when navigation link is clicked
  const handleNavLinkClick = () => {
    // Only auto-close on mobile/small screens when in side nav mode
    if (isSideNav && window.innerWidth <= 768) {
      onToggle();
    }
  };

  if (isSideNav) {
    return (
      <>
        <nav className="navigation side-nav">
          <div className="nav-header">
            <NavLink to="/app/" className="logo" end><Wordmark /></NavLink>
          </div>
          <div className="nav-links">
            <NavLink to="/app/" className="nav-link" end onClick={handleNavLinkClick}>
              <FaHome className="nav-icon" />
              <span>Home</span>
            </NavLink>
            <NavLink to="/app/warroom" className="nav-link" onClick={handleNavLinkClick}>
              <FaRocket className="nav-icon" />
              <span>War Room</span>
            </NavLink>
            <NavLink to="/app/picks" className={picksLinkClass} onClick={handleNavLinkClick}>
              <FaClipboardCheck className="nav-icon" />
              <span>Picks</span>
            </NavLink>
            <NavLink to="/app/leaderboard" className="nav-link" onClick={handleNavLinkClick}>
              <FaTrophy className="nav-icon" />
              <span>Leaderboard</span>
            </NavLink>
            {isAdmin && (
              <NavLink to="/app/map" className="nav-link" onClick={handleNavLinkClick}>
                <FaMapMarkedAlt className="nav-icon" />
                <span>Game Map</span>
              </NavLink>
            )}
            <NavLink to="/app/messageboard" className="nav-link" onClick={handleNavLinkClick}>
              <FaComments className="nav-icon" />
              <span>Locker Room</span>
            </NavLink>
            <NavLink to="/app/settings" className="nav-link" onClick={handleNavLinkClick}>
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
      <button 
        className="nav-toggle"
        onClick={onToggle}
        title="Switch to Side Navigation"
      >
        <FaBars />
      </button>
      <div className="nav-header">
        <NavLink to="/app/" className="logo" end><Wordmark /></NavLink>
      </div>
      <div className="nav-links">
        <table>
          <tbody>
            <tr>
              <td>
                <NavLink to="/app/" className="nav-link home-nav-link" end>
                  <FaHome className="nav-icon" />
                  <span>Home</span>
                </NavLink>
              </td>
              <td>
                <NavLink to="/app/warroom" className="nav-link">
                  <FaRocket className="nav-icon" />
                  <span>War Room</span>
                </NavLink>
              </td>
              <td>
                <NavLink to="/app/picks" className={picksLinkClass}>
                  <FaClipboardCheck className="nav-icon" />
                  <span>Picks</span>
                </NavLink>
              </td>
              <td>
                <NavLink to="/app/leaderboard" className="nav-link">
                  <FaTrophy className="nav-icon" />
                  <span>Leaderboard</span>
                </NavLink>
              </td>
              {isAdmin && (
                <td>
                  <NavLink to="/app/map" className="nav-link">
                    <FaMapMarkedAlt className="nav-icon" />
                    <span>Map</span>
                  </NavLink>
                </td>
              )}
              <td>
                <NavLink to="/app/messageboard" className="nav-link">
                  <FaComments className="nav-icon" />
                  <span>Locker Room</span>
                </NavLink>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <div className="nav-actions">
        <NavLink to="/app/settings" className="nav-link">
          <FaCog className="nav-icon" />
          <span>Settings</span>
        </NavLink>
        <button className="nav-link logout-button" onClick={onSignOut}>
          <FaSignOutAlt className="nav-icon" />
          <span>Sign Out</span>
        </button>
      </div>
    </nav>
  );
}

export default Navigation; 