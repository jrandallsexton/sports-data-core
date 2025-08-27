import { Link } from "react-router-dom";

function InfoSection() {
  return (
    <section className="info-section">
      <div className="info-block">
        <h2>Tip of the Week (simulated)</h2>
        <div className="tip-card">
          <p>
            <strong>💡 Pro Strategy:</strong> Balance underdogs and favorites!
            Most users win more by mixing picks based on matchup strength, not
            just spreads.
          </p>
          <p className="tip-note">
            Check this week's spread info before making final picks.
          </p>
        </div>
      </div>

      <div className="info-block">
        <h2>Latest News (simulated)</h2>
        <div className="news-card">
          <p>
            <strong>🏈 Major upset:</strong> #14 seed upsets #2! Full recap and
            impact on playoff brackets <Link to="/app/news">here</Link>.
          </p>
          <p>
            <strong>🧠 AI Insights:</strong> SportDeets AI now factoring in
            player injuries into projections!{" "}
            <Link to="/app/ai-news">Learn more</Link>.
          </p>
        </div>
      </div>
    </section>
  );
}

export default InfoSection; 