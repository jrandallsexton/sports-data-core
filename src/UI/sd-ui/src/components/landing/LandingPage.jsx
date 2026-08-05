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
      {/* Header + hero own exactly the first viewport: the hero flex-fills
          the remainder under the header, so its content centers on the
          visible screen and the feature sections start below the fold. */}
      <div className="landing-above-fold">
        <LandingHeader />
        <LandingHero />
      </div>
      <FeatureHighlights />
      <HowItWorks />
      <LandingFooter />
      <ScrollToTopButton />
    </div>
  );
}

export default LandingPage;
