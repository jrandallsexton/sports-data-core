import { Link } from 'react-router-dom';
import "./LandingPage.css"; // (You can create this or reuse App.css styles for now)
import LandingHero from './LandingHero'; // Import the Hero we discussed
import FeatureHighlights from "./FeatureHighlights";
import HowItWorks from "./HowItWorks";
import LandingFooter from "./LandingFooter";

function LandingPage() {
  return (
    <div className="landing-page">
      <LandingHero />
      <FeatureHighlights />
      <HowItWorks />
      {/* Later add FeatureHighlights, Testimonials, Footer, etc. */}

      <div className="landing-links">
        <Link to="/app" className="App-link">
          Sign In
        </Link>

        <Link to="/signup" className="App-link secondary-link">
          Sign Up
        </Link>
      </div>

      <LandingFooter />
    </div>
  );
}

export default LandingPage;
