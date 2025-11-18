import React, { useEffect, useRef } from 'react';

const CollapsibleSection = ({ title, children, isExpanded, onToggle }) => {
  const sectionRef = useRef(null);

  useEffect(() => {
    if (isExpanded && sectionRef.current) {
      const navHeight = 170; // Height of sticky nav + padding
      const elementPosition = sectionRef.current.getBoundingClientRect().top + window.pageYOffset;
      const offsetPosition = elementPosition - navHeight;

      window.scrollTo({
        top: offsetPosition,
        behavior: 'smooth'
      });
    }
  }, [isExpanded]);

  return (
    <div className="collapsible-section" ref={sectionRef}>
      <div 
        className="collapsible-header"
        onClick={onToggle}
      >
        <h4 className="collapsible-title">{title}</h4>
        <span className={`collapsible-icon ${isExpanded ? 'expanded' : ''}`}>
          ▼
        </span>
      </div>
      {isExpanded && (
        <div className="collapsible-content">
          {children}
        </div>
      )}
    </div>
  );
};

export default CollapsibleSection;
