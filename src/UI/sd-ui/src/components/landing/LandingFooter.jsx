import { Link } from "react-router-dom";
import "./LandingFooter.css";

function LandingFooter() {
  return (
    <footer className="landing-footer">
      <div className="footer-content">
        <p>
          © {new Date().getFullYear()}{" "}
          <span className="footer-brand">
            sportDeets<span className="tm-symbol">™</span>
          </span>
          . All rights reserved. {process.env.REACT_APP_VERSION || 'v0.0.0'}
        </p>

        <div className="footer-links">
          <Link to="/terms">Terms</Link>
          <span>•</span>
          <Link to="/privacy">Privacy</Link>
        </div>
      </div>
    </footer>
  );
}

export default LandingFooter;
