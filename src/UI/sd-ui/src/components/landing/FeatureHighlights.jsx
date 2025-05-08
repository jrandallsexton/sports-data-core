import { useEffect, useState, useRef } from "react";
import { FaBrain, FaBullhorn, FaTrophy } from "react-icons/fa";
import "./FeatureHighlights.css";

function FeatureHighlights() {
  const [isVisible, setIsVisible] = useState(false);
  const sectionRef = useRef(null);

  useEffect(() => {
    const currentSection = sectionRef.current; // Capture ref at the time effect runs
  
    const observer = new IntersectionObserver(
      ([entry]) => {
        setIsVisible(entry.isIntersecting);
      },
      { threshold: 0.2 }
    );
  
    if (currentSection) {
      observer.observe(currentSection);
    }
  
    return () => {
      if (currentSection) {
        observer.unobserve(currentSection);
      }
    };
  }, []);
  

  return (
    <div
      id="features"
      ref={sectionRef}
      className={`feature-highlights ${isVisible ? "visible" : ""}`}
    >
      <h2>Why sportDeets?</h2>

      <div className="features-grid">
        <div className="feature-card">
          <FaBrain className="feature-icon icon-brain" />
          <h3>Pick Smarter</h3>
          <p>Get insider stats, matchup breakdowns, and expert-driven insights.</p>
        </div>

        <div className="feature-card">
          <FaBullhorn className="feature-icon icon-bullhorn" />
          <h3>Talk Louder</h3>
          <p>Brag confidently after you outsmart your competition every week.</p>
        </div>

        <div className="feature-card">
          <FaTrophy className="feature-icon icon-trophy" />
          <h3>Crush Your Friends</h3>
          <p>Dominate your private leagues and never let them forget it.</p>
        </div>
      </div>
    </div>
  );
}

export default FeatureHighlights;
