import { Link } from "react-router-dom";
import "./LandingFooter.css";

function LandingFooter() {
  return (
    <footer className="landing-footer">
      <div className="footer-content">
        <p>© {new Date().getFullYear()} sportDeets. All rights reserved.</p>
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
