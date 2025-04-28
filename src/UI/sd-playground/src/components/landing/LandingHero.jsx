import { Link } from "react-router-dom";
import "./LandingHero.css";
import { Logo} from "../../logo.svg"; // Adjust the path as needed

function LandingHero() {
  return (
    <div className="landing-hero">
      <div className="hero-content">
        {/* <img src={Logo} alt="sportDeets Logo" className="hero-logo" /> */}
        <h1>Win Your Picks. Crush Your Friends.&#8482;</h1>
        <p>Data-driven insights for every NCAA football matchup.</p>
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
