import React from "react";
import "./HelmetLogo.css";

function HelmetLogo({ logoUrl, flip = false }) {
  return (
    <div className={`helmet-logo-container ${flip ? "flip" : ""}`}>
      <img src="../../helmet.svg" alt="helmet-svg" className="helmet-base" />
      <img src={logoUrl} alt="Team Logo" className="helmet-logo-overlay" />
    </div>
  );
}

export default HelmetLogo;
