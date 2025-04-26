import { useEffect, useState, useRef } from "react";
import { FaBrain, FaBullhorn, FaTrophy } from "react-icons/fa";
import "./FeatureHighlights.css";

function FeatureHighlights() {
  const [isVisible, setIsVisible] = useState(false);
  const sectionRef = useRef(null);

  useEffect(() => {
    const observer = new IntersectionObserver(
      ([entry]) => {
        setIsVisible(entry.isIntersecting);
      },
      { threshold: 0.2 } // 20% visible triggers
    );

    if (sectionRef.current) {
      observer.observe(sectionRef.current);
    }

    return () => {
      if (sectionRef.current) {
        observer.unobserve(sectionRef.current);
      }
    };
  }, []);

  return (
    <div
      id="features"
      ref={sectionRef}
      className={`feature-highlights ${isVisible ? "visible" : ""}`}
    >
      <h2>Why Choose sportDeets?</h2>

      <div className="features-grid">
        <div className="feature-card">
          <FaBrain className="feature-icon" />
          <h3>Pick Smarter</h3>
          <p>Get insider stats, matchup breakdowns, and expert-driven insights.</p>
        </div>

        <div className="feature-card">
          <FaBullhorn className="feature-icon" />
          <h3>Talk Louder</h3>
          <p>Brag confidently after you outsmart your competition every week.</p>
        </div>

        <div className="feature-card">
          <FaTrophy className="feature-icon" />
          <h3>Crush Your Friends</h3>
          <p>Dominate your private leagues and never let them forget it.</p>
        </div>
      </div>
    </div>
  );
}

export default FeatureHighlights;
