import "./LegalPages.css";
import { Link } from "react-router-dom";

function PrivacyPage() {
  return (
    <div className="legal-page">
      <h2>Privacy Policy</h2>
      <p>
        This is a placeholder Privacy Policy for sportDeets. Final policies
        regarding data collection and usage will be published prior to launch.
      </p>
      <p>
        Your personal information will never be sold, and will only be used to
        enhance your sportDeets experience.
      </p>
      <p>
        Thank you for trusting us to protect your data.
      </p>

<div className="back-home-link">
  <Link to="/">← Back to Home</Link>
</div>
    </div>
  );
}

export default PrivacyPage;
