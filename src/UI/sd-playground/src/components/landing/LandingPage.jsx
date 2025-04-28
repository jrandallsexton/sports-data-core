import { Link } from 'react-router-dom';
import "./LandingPage.css"; // (You can create this or reuse App.css styles for now)
import LandingHero from './LandingHero'; // Import the Hero we discussed
import FeatureHighlights from "./FeatureHighlights";
import HowItWorks from "./HowItWorks";
import LandingFooter from "./LandingFooter";
import ScrollToTopButton from '../shared/ScrollToTopButton';
import LandingHeader from "./LandingHeader";

function LandingPage() {
  return (
    <div className="landing-page">
      <LandingHeader />
      <LandingHero />
      <FeatureHighlights />
      <HowItWorks />
      <LandingFooter />
      <ScrollToTopButton />
    </div>
  );
}

export default LandingPage;
