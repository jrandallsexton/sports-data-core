import React, { useState, useEffect, useCallback } from 'react';
import './Gallery.css';

function Gallery() {
  const [selectedImageIndex, setSelectedImageIndex] = useState(null);
  const [touchStart, setTouchStart] = useState(null);
  const [touchEnd, setTouchEnd] = useState(null);

  // Array of images and videos from /public/media
  const media = [
    {
      src: '/media/Recording 2025-10-30 181420.mp4',
      type: 'video',
      alt: 'Platform Demo',
      title: 'Platform Demo Video'
    },
    {
      src: '/media/Screenshot 2025-10-30 181509.png',
      type: 'image',
      alt: 'Screenshot 1',
      title: 'Pick Analytics'
    },
    {
      src: '/media/Screenshot 2025-10-30 181521.png',
      type: 'image',
      alt: 'Screenshot 2',
      title: 'Team Comparison'
    },
    {
      src: '/media/Screenshot 2025-10-30 181530.png',
      type: 'image',
      alt: 'Screenshot 3',
      title: 'Matchup Details'
    },
    {
      src: '/media/Screenshot 2025-10-30 181549.png',
      type: 'image',
      alt: 'Screenshot 4',
      title: 'Leaderboard'
    },
    {
      src: '/media/Screenshot 2025-10-30 181558.png',
      type: 'image',
      alt: 'Screenshot 5',
      title: 'Team Statistics'
    },
    {
      src: '/media/Screenshot 2025-10-30 181614.png',
      type: 'image',
      alt: 'Screenshot 6',
      title: 'Metrics Dashboard'
    },
    {
      src: '/media/Screenshot 2025-10-30 181629.png',
      type: 'image',
      alt: 'Screenshot 7',
      title: 'League Overview'
    },
    {
      src: '/media/Screenshot 2025-10-30 181638.png',
      type: 'image',
      alt: 'Screenshot 8',
      title: 'User Settings'
    },
    {
      src: '/media/Screenshot 2025-10-30 181647.png',
      type: 'image',
      alt: 'Screenshot 9',
      title: 'War Room'
    },
    {
      src: '/media/Screenshot 2025-10-30 181706.png',
      type: 'image',
      alt: 'Screenshot 10',
      title: 'Message Board'
    },
    {
      src: '/media/Screenshot 2025-10-30 181717.png',
      type: 'image',
      alt: 'Screenshot 11',
      title: 'Venue Information'
    }
    // Add videos like this:
    // {
    //   src: '/media/demo-video.mp4',
    //   type: 'video',
    //   alt: 'Demo video',
    //   title: 'Feature Demo'
    // }
  ];

  const openLightbox = (index) => {
    setSelectedImageIndex(index);
  };

  const closeLightbox = () => {
    setSelectedImageIndex(null);
  };

  const isVideo = (item) => item.type === 'video';

  const goToNext = useCallback(() => {
    if (selectedImageIndex !== null) {
      setSelectedImageIndex((selectedImageIndex + 1) % media.length);
    }
  }, [selectedImageIndex, media.length]);

  const goToPrevious = useCallback(() => {
    if (selectedImageIndex !== null) {
      setSelectedImageIndex((selectedImageIndex - 1 + media.length) % media.length);
    }
  }, [selectedImageIndex, media.length]);

  // Touch handlers for mobile swipe
  const minSwipeDistance = 50;

  const onTouchStart = (e) => {
    setTouchEnd(null);
    setTouchStart(e.targetTouches[0].clientX);
  };

  const onTouchMove = (e) => {
    setTouchEnd(e.targetTouches[0].clientX);
  };

  const onTouchEnd = () => {
    if (!touchStart || !touchEnd) return;
    const distance = touchStart - touchEnd;
    const isLeftSwipe = distance > minSwipeDistance;
    const isRightSwipe = distance < -minSwipeDistance;
    
    if (isLeftSwipe) {
      goToNext();
    } else if (isRightSwipe) {
      goToPrevious();
    }
  };

  // Keyboard navigation effect
  useEffect(() => {
    if (selectedImageIndex !== null) {
      const handleGlobalKeyDown = (e) => {
        if (e.key === 'Escape') {
          closeLightbox();
        } else if (e.key === 'ArrowRight') {
          goToNext();
        } else if (e.key === 'ArrowLeft') {
          goToPrevious();
        }
      };
      window.addEventListener('keydown', handleGlobalKeyDown);
      return () => window.removeEventListener('keydown', handleGlobalKeyDown);
    }
  }, [selectedImageIndex, goToNext, goToPrevious]);

  return (
    <div className="gallery-page">
      <div className="gallery-header">
        <h1>sportDeets<span className="tm-symbol">™</span> Gallery</h1>
        <p>A showcase of features and screenshots from the platform</p>
      </div>

      <div className="gallery-grid">
        {media.map((item, index) => (
          <div 
            key={index} 
            className="gallery-item"
            onClick={() => openLightbox(index)}
          >
            {isVideo(item) ? (
              <video 
                src={item.src} 
                muted
                loop
                playsInline
                onMouseEnter={(e) => e.target.play()}
                onMouseLeave={(e) => e.target.pause()}
              />
            ) : (
              <img 
                src={item.src} 
                alt={item.alt}
                loading="lazy"
              />
            )}
            <div className="gallery-item-overlay">
              <span className="gallery-item-title">{item.title}</span>
              {isVideo(item) && <span className="video-badge">▶</span>}
            </div>
          </div>
        ))}
      </div>

      {selectedImageIndex !== null && (
        <div 
          className="lightbox"
          onClick={closeLightbox}
          onTouchStart={onTouchStart}
          onTouchMove={onTouchMove}
          onTouchEnd={onTouchEnd}
          tabIndex={0}
          role="dialog"
          aria-modal="true"
        >
          <button 
            className="lightbox-nav lightbox-nav-left"
            onClick={(e) => { e.stopPropagation(); goToPrevious(); }}
            aria-label="Previous image"
          >
            ‹
          </button>
          
          <div className="lightbox-content" onClick={(e) => e.stopPropagation()}>
            <button 
              className="lightbox-close"
              onClick={closeLightbox}
              aria-label="Close"
            >
              ×
            </button>
            {isVideo(media[selectedImageIndex]) ? (
              <video 
                src={media[selectedImageIndex].src} 
                controls
                autoPlay
                loop
                style={{ maxWidth: '100%', maxHeight: '80vh', borderRadius: '8px' }}
              />
            ) : (
              <img 
                src={media[selectedImageIndex].src} 
                alt={media[selectedImageIndex].alt}
              />
            )}
            <div className="lightbox-caption">
              {media[selectedImageIndex].title}
              <span className="lightbox-counter">
                {selectedImageIndex + 1} / {media.length}
              </span>
            </div>
          </div>

          <button 
            className="lightbox-nav lightbox-nav-right"
            onClick={(e) => { e.stopPropagation(); goToNext(); }}
            aria-label="Next image"
          >
            ›
          </button>
        </div>
      )}
    </div>
  );
}

export default Gallery;
