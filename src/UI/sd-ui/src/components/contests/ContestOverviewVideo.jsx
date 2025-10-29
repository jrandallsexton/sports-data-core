import React, { useState } from 'react';
import './ContestOverviewVideo.css';

function ContestOverviewVideo({ mediaItems }) {
  const [selectedVideoIndex, setSelectedVideoIndex] = useState(0);
  const [userHasSelected, setUserHasSelected] = useState(false);

  if (!mediaItems || mediaItems.length === 0) {
    return null; // Don't render anything if no videos are available
  }

  const currentVideo = mediaItems[selectedVideoIndex];
  //const playlistItems = mediaItems.slice(1, 5); // Items 1-4 for playlist
  const showPlaylist = mediaItems.length > 1; // Show playlist if more than 1 video

  const handleVideoSelect = (index) => {
    setSelectedVideoIndex(index);
    setUserHasSelected(true); // Mark that user has actively selected a video
  };

  // Only add autoplay if user has actively selected a video
  const embedUrl = userHasSelected 
    ? `${currentVideo.embedUrl}?autoplay=1&origin=${window.location.origin}&enablejsapi=1`
    : `${currentVideo.embedUrl}?origin=${window.location.origin}&enablejsapi=1`;

  return (
    <div className="contest-overview-video">
      <h3>Game Highlights</h3>
      <div className="video-container">
        <iframe
          key={currentVideo.videoId} // Force iframe reload when video changes
          src={embedUrl}
          title={currentVideo.title}
          frameBorder="0"
          allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
          allowFullScreen
          className="video-iframe"
          referrerPolicy="strict-origin-when-cross-origin"
        ></iframe>
      </div>
      <div className="video-info">
        <h4 className="video-title">{currentVideo.title}</h4>
        <p className="video-channel">{currentVideo.channelTitle}</p>
        {currentVideo.description && (
          <p className="video-description">{currentVideo.description}</p>
        )}
      </div>
      
      {/* Playlist - show all videos when multiple exist */}
      {showPlaylist && (
        <div className="video-playlist">
          <h4 className="playlist-title">All Videos</h4>
          <div className="playlist-items">
            {mediaItems.slice(0, 5).map((video, index) => {
              const isSelected = selectedVideoIndex === index;
              
              return (
                <div
                  key={video.videoId}
                  className={`playlist-item ${isSelected ? 'active' : ''}`}
                  onClick={() => handleVideoSelect(index)}
                >
                  <img
                    src={video.thumbnailMediumUrl}
                    alt={video.title}
                    className="playlist-thumbnail"
                  />
                  <div className="playlist-info">
                    <h5 className="playlist-video-title">{video.title}</h5>
                    <p className="playlist-video-channel">{video.channelTitle}</p>
                  </div>
                  {isSelected && <div className="playing-indicator">▶</div>}
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}

export default ContestOverviewVideo;