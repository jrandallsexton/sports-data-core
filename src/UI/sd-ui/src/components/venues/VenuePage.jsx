import { useParams } from "react-router-dom";
import { useEffect, useState } from "react";
import apiWrapper from "../../api/apiWrapper";
import "./VenuePage.css";

const VenuePage = () => {
  const { sport, league, slug } = useParams();
  const [venue, setVenue] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    const loadVenue = async () => {
      try {
        const res = await apiWrapper.Venues.getBySlug(sport, league, slug);
        setVenue(res.data.value.venue);
      } catch (err) {
        console.error("Failed to load venue:", err);
        setError("Unable to load venue.");
      }
    };

    loadVenue();
  }, [sport, league, slug]);

  if (error) return <div className="venue-page">{error}</div>;
  if (!venue) return <div className="venue-page">Loading...</div>;

  const formattedCapacity = isFinite(Number(venue.capacity))
    ? Number(venue.capacity).toLocaleString()
    : "unknown";

  // Grab first image URL if available
  const firstImageUrl =
    venue.images && venue.images.length > 0 ? venue.images[0].url : null;

  return (
    <div className="venue-page">
      <div className="venue-header">
        {firstImageUrl ? (
          <img
            src={firstImageUrl}
            alt={venue.name || "Venue image"}
            className="venue-image"
          />
        ) : (
          <div className="venue-image-placeholder">No image available</div>
        )}
        <div>
          <h2 className="venue-name">{venue.name || "Unnamed Venue"}</h2>
          <p className="venue-location">{venue.location || "Location unknown"}</p>
          <p className="venue-details">
            {formattedCapacity} capacity • {venue.isIndoor ? "Indoor" : "Outdoor"} •{" "}
            {venue.isGrass ? "Grass" : "Artificial Turf"}
          </p>
        </div>
      </div>

      <div className="venue-content">
        <h3>About This Venue</h3>
        <p>
          {venue.name || "This venue"} is a football stadium located in{" "}
          {venue.location || "an unknown location"}. It seats approximately{" "}
          {formattedCapacity} fans and is{" "}
          {venue.isIndoor ? "an indoor facility" : "an open-air stadium"} with{" "}
          {venue.isGrass ? "natural grass" : "artificial turf"}.
        </p>
      </div>
    </div>
  );
};

export default VenuePage;
