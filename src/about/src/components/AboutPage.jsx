import React, { useState, useEffect, useMemo } from 'react';
import './AboutPage.css';
import OverviewSection from './sections/OverviewSection';
import ArchitectureSection from './sections/ArchitectureSection';
import AISection from './sections/AISection';
import ObservabilitySection from './sections/ObservabilitySection';
import DataQualitySection from './sections/DataQualitySection';
import DevOpsSection from './sections/DevOpsSection';
import GallerySection from './sections/GallerySection';
import FutureEnhancementsSection from './sections/FutureEnhancementsSection';

const AboutPage = () => {
  const [activeSection, setActiveSection] = useState('overview');

  const sections = useMemo(() => [
    { id: 'overview', label: 'Overview' },
    { id: 'gallery', label: 'Gallery' },
    { id: 'architecture', label: 'Architecture' },
    { id: 'ai', label: 'AI & Predictive Insights' },
    { id: 'observability', label: 'Observability' },
    { id: 'data-quality', label: 'Data Quality' },
    { id: 'devops', label: 'DevOps & GitOps' },
    { id: 'future', label: 'Future Enhancements' }
  ], []);

  const scrollToSection = (sectionId) => {
    const element = document.getElementById(sectionId);
    if (element) {
      const navHeight = 70; // Account for sticky nav
      const elementPosition = element.getBoundingClientRect().top + window.pageYOffset;
      window.scrollTo({
        top: elementPosition - navHeight,
        behavior: 'smooth'
      });
      setActiveSection(sectionId);
    }
  };

  // Update active section based on scroll position
  useEffect(() => {
    const handleScroll = () => {
      const navHeight = 70;
      const scrollPosition = window.scrollY + navHeight + 50;

      for (let i = sections.length - 1; i >= 0; i--) {
        const section = document.getElementById(sections[i].id);
        if (section && section.offsetTop <= scrollPosition) {
          setActiveSection(sections[i].id);
          break;
        }
      }
    };

    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, [sections]);

  return (
    <div className="about-page">
      {/* Sticky Navigation */}
      <nav className="about-nav">
        <div className="about-nav-content">
          <h1 className="about-title">
            <a href="https://www.sportdeets.com" target="_blank" rel="noopener noreferrer">
              sportDeets<span className="tm-symbol">™</span>
            </a>
          </h1>
          <div className="about-nav-links">
            {sections.map(section => (
              <button
                key={section.id}
                className={`nav-link ${activeSection === section.id ? 'active' : ''}`}
                onClick={() => scrollToSection(section.id)}
              >
                {section.label}
              </button>
            ))}
          </div>
        </div>
      </nav>

      {/* Main Content */}
      <main className="about-content">
        <OverviewSection id="overview" />
        <GallerySection id="gallery" />
        <ArchitectureSection id="architecture" />
        <AISection id="ai" />
        <ObservabilitySection id="observability" />
        <DataQualitySection id="data-quality" />
        <DevOpsSection id="devops" />
        <FutureEnhancementsSection id="future" />
      </main>

      {/* Footer */}
      <footer className="about-footer">
        <p>
          &copy; 2025 <a href="https://www.sportdeets.com" target="_blank" rel="noopener noreferrer">sportDeets<span className="tm-symbol">™</span></a>. Built with React, .NET, and Azure. {process.env.REACT_APP_VERSION || 'v0.0.0'}
        </p>
        <p className="footer-note">
          This portfolio showcases the technical architecture and capabilities of the <a href="https://www.sportdeets.com" target="_blank" rel="noopener noreferrer">sportDeets<span className="tm-symbol">™</span></a> platform.
        </p>
      </footer>
    </div>
  );
};

export default AboutPage;
