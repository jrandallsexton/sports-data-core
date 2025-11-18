import React, { useState, useEffect, useCallback } from "react";
import CollapsibleSection from "../common/CollapsibleSection";

const GallerySection = ({ id }) => {
  const [expandedSection, setExpandedSection] = useState(null);
  const [lightboxImage, setLightboxImage] = useState(null);

  const handleToggle = (sectionName) => {
    setExpandedSection(expandedSection === sectionName ? null : sectionName);
  };

  const openLightbox = (imageSrc, alt, caption, galleryKey, imageIndex) => {
    setLightboxImage({ src: imageSrc, alt, caption, galleryKey, imageIndex });
  };

  const closeLightbox = () => {
    setLightboxImage(null);
  };

  const navigateImage = useCallback((direction) => {
    if (!lightboxImage) return;
    
    const currentGallery = galleries[lightboxImage.galleryKey];
    const currentIndex = lightboxImage.imageIndex;
    let newIndex;

    if (direction === 'next') {
      newIndex = (currentIndex + 1) % currentGallery.images.length;
    } else {
      newIndex = (currentIndex - 1 + currentGallery.images.length) % currentGallery.images.length;
    }

    const newImage = currentGallery.images[newIndex];
    setLightboxImage({
      src: newImage.src,
      alt: newImage.alt,
      caption: newImage.caption,
      galleryKey: lightboxImage.galleryKey,
      imageIndex: newIndex,
    });
  }, [lightboxImage]);

  useEffect(() => {
    const handleKeyDown = (e) => {
      if (!lightboxImage) return;

      switch (e.key) {
        case 'Escape':
          closeLightbox();
          break;
        case 'ArrowRight':
          navigateImage('next');
          break;
        case 'ArrowLeft':
          navigateImage('prev');
          break;
        default:
          break;
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [lightboxImage, navigateImage]);

  const galleries = {
    application: {
      title: "Application Screenshots",
      description: "User-facing features and interfaces",
      images: [
        {
          src: "/gallery/application/dashboard.jpg",
          alt: "Main Dashboard",
          caption: "User dashboard with weekly contests and standings",
        },
        {
          src: "/gallery/application/dashboard-ApPoll.jpg",
          alt: "AP Poll Dashboard",
          caption: "Current AP Poll rankings and details",
        },
        {
          src: "/gallery/application/dashboard-CoachesPoll.jpg",
          alt: "Coaches Poll Dashboard",
          caption: "Current Coaches Poll rankings and details",
        },
        {
          src: "/gallery/application/franchise-season-metrics.jpg",
          alt: "Franchise Season Metrics",
          caption: "Detailed franchise statistics and season performance metrics",
        },
        {
          src: "/gallery/application/leaderboard.jpg",
          alt: "Leaderboard",
          caption: "Contest leaderboard showing user rankings and scores",
        },
        {
          src: "/gallery/application/widgets-accuracy.jpg",
          alt: "Accuracy Widgets",
          caption: "Pick accuracy tracking and performance widgets",
        },
        {
          src: "/gallery/application/scoring-weekly.jpg",
          alt: "Weekly Scoring",
          caption: "Weekly contest scoring and point distribution",
        },
        {
          src: "/gallery/application/pick-correct.jpg",
          alt: "Correct Pick",
          caption: "Correct pick indication with visual feedback",
        },
        {
          src: "/gallery/application/pick-incorrect.jpg",
          alt: "Incorrect Pick",
          caption: "Incorrect pick indication with visual feedback",
        },
        {
          src: "/gallery/application/messageboard.jpg",
          alt: "Message Board",
          caption: "Community message board for user discussions",
        },
        {
          src: "/gallery/application/matchup-preview-AI.jpg",
          alt: "AI Matchup Preview",
          caption: "AI-generated matchup preview with predictions and insights",
        },
        {
          src: "/gallery/application/matchup-stats-compare.jpg",
          alt: "Matchup Stats Comparison",
          caption: "Side-by-side statistical comparison for matchups",
        },
        {
          src: "/gallery/application/matchup-metrics-compare.jpg",
          alt: "Matchup Metrics Comparison",
          caption: "Advanced metrics comparison between teams",
        },
      ],
    },
    observability: {
      title: "Observability, Monitoring, & Logging",
      description: "Grafana dashboards, Prometheus metrics, Seq structured logs, and distributed tracing",
      images: [
        {
          src: "/gallery/observability/grafana-cluster.jpg",
          alt: "Grafana Cluster Dashboard",
          caption: "K3s cluster metrics and health monitoring",
        },
        {
          src: "/gallery/observability/prometheus.jpg",
          alt: "Prometheus Metrics",
          caption: "Time-series metrics collection and alerts",
        },
        {
          src: "/gallery/observability/seq.jpg",
          alt: "Seq Structured Logs",
          caption: "Centralized structured logging with search and filtering",
        },
      ],
    },
    infrastructure: {
      title: "Infrastructure & DevOps",
      description: "Kubernetes, Flux, and deployment pipelines",
      images: [
        {
          src: "/gallery/infra/flux-dashboard.jpg",
          alt: "Flux GitOps",
          caption: "GitOps deployment status and reconciliation",
        },
        {
          src: "/gallery/infra/k8s-pods.jpg",
          alt: "Kubernetes Pods",
          caption: "Running pods and service health in K3s cluster",
        },
      ],
    },
  };

  return (
    <>
      <section id={id} className="about-section">
        <div className="section-header">
          <h2 className="section-title">Gallery</h2>
          <p className="section-subtitle">
            Screenshots of the Application and Admin Tools
          </p>
        </div>

        <div className="section-content">
          {Object.entries(galleries).map(([key, gallery]) => (
            <CollapsibleSection
              key={key}
              title={gallery.title}
              isExpanded={expandedSection === key}
              onToggle={() => handleToggle(key)}
            >
              <p className="gallery-description">{gallery.description}</p>
              <div className="gallery-grid">
                {gallery.images.map((image, index) => (
                  <div
                    key={index}
                    className="gallery-item"
                    onClick={() => openLightbox(image.src, image.alt, image.caption, key, index)}
                  >
                    <img
                      src={image.src}
                      alt={image.alt}
                      className="gallery-thumbnail"
                      loading="lazy"
                    />
                    <p className="gallery-caption">{image.caption}</p>
                  </div>
                ))}
              </div>
            </CollapsibleSection>
          ))}
        </div>
      </section>

      {/* Lightbox Modal */}
      {lightboxImage && (
        <div className="lightbox-overlay" onClick={closeLightbox}>
          <div className="lightbox-content" onClick={(e) => e.stopPropagation()}>
            <button className="lightbox-close" onClick={closeLightbox}>
              ✕
            </button>
            <p className="lightbox-caption">{lightboxImage.caption}</p>
            <img
              src={lightboxImage.src}
              alt={lightboxImage.alt}
              className="lightbox-image"
            />
          </div>
        </div>
      )}
    </>
  );
};

export default GallerySection;
