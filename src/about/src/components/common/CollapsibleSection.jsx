import React from 'react';

const CollapsibleSection = ({ title, children, isExpanded, onToggle }) => {
  return (
    <div className="collapsible-section">
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
