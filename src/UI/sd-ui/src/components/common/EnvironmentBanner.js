// src/components/common/EnvironmentBanner.js
import React from 'react';
import './EnvironmentBanner.css';

const EnvironmentBanner = () => {
  const environment = process.env.REACT_APP_ENVIRONMENT;
  const prodUrl = process.env.REACT_APP_PROD_URL || 'https://sportdeets.com';

  // Only show banner in development environment
  if (environment !== 'development') {
    return null;
  }

  return (
    <div className="environment-banner">
      <div className="environment-banner-content">
        <span className="environment-banner-icon">⚠️</span>
        <span className="environment-banner-text">
          You are viewing the <strong>development</strong> environment. 
          Production is now live!
        </span>
        <a 
          href={prodUrl} 
          className="environment-banner-link"
          target="_self"
        >
          Go to Production →
        </a>
      </div>
    </div>
  );
};

export default EnvironmentBanner;
