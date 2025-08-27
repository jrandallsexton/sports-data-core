import { Link } from "react-router-dom";
import "./LandingHero.css";

function LandingHero() {
  return (
    <div className="landing-hero">
      <div className="hero-content">
        {/* <img src={Logo} alt="sportDeets Logo" className="hero-logo" /> */}
        <h1>Win Your Picks. Crush Your Friends.<span className="tm-symbol">™</span></h1>
        <p>Data-driven insights for <i>every</i> NCAA football matchup.</p>
        <div className="hero-buttons">
          <Link to="/signup" className="primary-button">
            Get Started Free
          </Link>
          <a href="#features" className="secondary-button">
            Learn More →
          </a>
        </div>
      </div>
    </div>
  );
}

export default LandingHero;
