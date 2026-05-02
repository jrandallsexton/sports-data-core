import React, { useState } from "react";
import Dialog from '@mui/material/Dialog';
import { FaTimes } from 'react-icons/fa';
import { formatToUserTime, getZoneAbbreviation } from "../../utils/timeUtils";
import { useUserTimeZone } from "../../hooks/useUserTimeZone";
import "./ContestOverview.css";

export default function ContestOverviewInfo({ info }) {
  const userTz = useUserTimeZone();
  // Pass the game's date so the abbreviation matches its DST window
  // (e.g. "EST" for an October NCAAFB game even when viewed in May).
  const zoneAbbrev = getZoneAbbreviation(userTz, info?.startDateUtc);
  const [lightboxOpen, setLightboxOpen] = useState(false);
  const handleOpenLightbox = () => setLightboxOpen(true);
  const handleCloseLightbox = () => setLightboxOpen(false);

  if (!info) {
    return (
      <div className="contest-section">
        <div className="contest-section-title">Info</div>
        <div className="contest-info-section">
          <div className="contest-info-list">No info available.</div>
        </div>
      </div>
    );
  }
  return (
    <div className="contest-section">
      <div className="contest-section-title">Info</div>
      <div className="contest-info-section">
        <ul className="contest-info-list">
          {info.venue && (
            <li className="contest-info-item"><span className="contest-info-label">Venue:</span> {' '}<span className="contest-info-value">{info.venue}</span></li>
          )}
          {info.venueCity && (
            <li className="contest-info-item"><span className="contest-info-label">City:</span> {' '}<span className="contest-info-value">{info.venueCity}</span></li>
          )}
          {info.venueState && (
            <li className="contest-info-item"><span className="contest-info-label">State:</span> {' '}<span className="contest-info-value">{info.venueState}</span></li>
          )}
          {/* Venue image will be shown after the list */}
          {info.startDateUtc && (
            <li className="contest-info-item">
              <span className="contest-info-label">Start Time ({zoneAbbrev}):</span> {' '}
              <span className="contest-info-value">{formatToUserTime(info.startDateUtc, userTz)}</span>
            </li>
          )}
          {info.attendance !== undefined && (
            <li className="contest-info-item">
              <span className="contest-info-label">Attendance:</span> {' '}
              <span className="contest-info-value">{Number(info.attendance).toLocaleString("en-US")}</span>
            </li>
          )}
          {info.broadcast && (
            <li className="contest-info-item"><span className="contest-info-label">Broadcast:</span> {' '}<span className="contest-info-value">{info.broadcast}</span></li>
          )}
        </ul>
        {info.venueImageUrl && (
          <div style={{ marginTop: 8, textAlign: 'center', width: '100%' }}>
            <img 
              src={info.venueImageUrl} 
              alt="Venue" 
              style={{ width: '100%', height: 'auto', borderRadius: 6, objectFit: 'cover', maxHeight: 220, marginBottom: 0, paddingBottom: 0, cursor: 'pointer' }} 
              onClick={handleOpenLightbox}
            />
            <Dialog open={lightboxOpen} onClose={handleCloseLightbox} maxWidth="md" fullWidth>
              <div style={{ position: 'relative', background: 'var(--bg-input)', padding: 0 }}>
                <button
                  aria-label="close"
                  onClick={handleCloseLightbox}
                  style={{ position: 'absolute', top: 8, right: 8, color: 'var(--text-primary)', zIndex: 2, background: 'none', border: 'none', cursor: 'pointer', fontSize: 28 }}
                >
                  <FaTimes />
                </button>
                <img
                  src={info.venueImageUrl}
                  alt="Venue Large"
                  style={{ width: '100%', height: 'auto', borderRadius: 6, objectFit: 'contain', maxHeight: '80vh', display: 'block', margin: '0 auto', background: 'var(--bg-input)' }}
                />
              </div>
            </Dialog>
          </div>
        )}
      </div>
    </div>
  );
}
