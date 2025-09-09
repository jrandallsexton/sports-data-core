import React from "react";
import { Link } from "react-router-dom";
import "../home/HomePage.css";

function NewsWidget() {
  return (
    <div className="news-card">
  <h2>Latest News (simulated)</h2>
      <p>
        <strong>🏈 Major upset:</strong> #14 seed upsets #2! Full recap and impact on playoff brackets <Link to="/app/news">here</Link>.
      </p>
      <p>
        <strong>🧠 AI Insights:</strong> SportDeets AI now factoring in player injuries into projections! <Link to="/app/ai-news">Learn more</Link>.
      </p>
    </div>
  );
}

export default NewsWidget;
