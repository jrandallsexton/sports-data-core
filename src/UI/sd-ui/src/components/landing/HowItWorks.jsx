import { useEffect, useState, useRef } from "react";
import { FaUsers, FaFootballBall, FaBullseye } from "react-icons/fa";
import "./HowItWorks.css";

function HowItWorks() {
  const [isVisible, setIsVisible] = useState(false);
  const sectionRef = useRef(null);

  useEffect(() => {
    const currentSection = sectionRef.current; // Capture ref immediately
  
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
      id="how-it-works"
      ref={sectionRef}
      className={`how-it-works ${isVisible ? "visible" : ""}`}
    >
      <h2>How sportDeets Works</h2>

      <div className="steps-grid">
        <div className="step-card">
          <FaUsers className="step-icon icon-users" />
          <h3>Join a Group</h3>
          <p>Create a private group for your friends or join a public fan group.</p>
        </div>

        <div className="step-card">
          <FaFootballBall className="step-icon icon-football" />
          <h3>Make Your Picks</h3>
          <p>Pick winners straight up or against the spread each week.</p>
        </div>

        <div className="step-card">
          <FaBullseye className="step-icon icon-bullseye" />
          <h3>Dominate the Leaderboard</h3>
          <p>Crush the competition and earn ultimate bragging rights.</p>
        </div>
      </div>
    </div>
  );
}

export default HowItWorks;
