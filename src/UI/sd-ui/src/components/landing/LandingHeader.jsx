//import { useState } from "react";
//import { Link } from "react-router-dom";
import "./LandingHeader.css";

function LandingHeader() {
  //const [showLogin, setShowLogin] = useState(false);

  // function toggleLoginPanel(event) {
  //   event.preventDefault(); // <-- Different: prevent router from navigating
  //   event.stopPropagation();
  //   setShowLogin((prev) => !prev);
  // }

  return (
    <header className="landing-header">
      <div className="landing-header-inner">
        <div className="logo">
          <a href="/" className="logo-link">
            sportDeets<span className="tm-symbol">™</span>
          </a>
        </div>

        {/* <div className="header-actions">
          <button
            onClick={toggleLoginPanel}
            className={`signin-button ${showLogin ? "active" : ""}`}
          >
            Sign In
          </button>

          <div className={`login-dropdown ${showLogin ? "show" : ""}`}>
            <p>
              <strong>Sign In</strong>
            </p>
            <input type="email" placeholder="Email" />
            <input type="password" placeholder="Password" />
            <button className="submit-button">Login</button>
            <p className="login-footer-text">
              New here? <Link to="/signup">Get Started Free</Link>
            </p>
          </div>
        </div> */}
      </div>
    </header>
  );
}

export default LandingHeader;
