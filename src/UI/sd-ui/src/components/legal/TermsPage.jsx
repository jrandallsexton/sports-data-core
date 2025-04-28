import "./LegalPages.css";
import { Link } from "react-router-dom";

function TermsPage() {
  return (
    <div className="legal-page">
      <h2>Terms of Service</h2>
      <p>
        These are the placeholder Terms of Service for sportDeets. Actual terms
        will be provided before public launch.
      </p>
      <p>
        By using sportDeets, you agree to abide by all applicable laws and
        regulations.
      </p>
      <p>For now, just enjoy the picks and bragging rights!</p>

      <div className="back-home-link">
        <Link to="/">← Back to Home</Link>
      </div>
    </div>
  );
}

export default TermsPage;
