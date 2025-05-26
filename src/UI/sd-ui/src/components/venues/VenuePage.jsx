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
        setVenue(res.data);
      } catch (err) {
        console.error("Failed to load venue:", err);
        setError("Unable to load venue.");
      }
    };

    loadVenue();
  }, [sport, league, slug]);

  if (error) return <div className="venue-page">{error}</div>;
  if (!venue) return <div className="venue-page">Loading...</div>;

  return (
    <div className="venue-page">
      <div className="venue-header">
        <img src={venue.imageUrl} alt={venue.name} className="venue-image" />
        <div>
          <h2 className="venue-name">{venue.name}</h2>
          <p className="venue-location">{venue.location}</p>
          <p className="venue-details">
            {venue.capacity.toLocaleString()} capacity •{" "}
            {venue.isIndoor ? "Indoor" : "Outdoor"} •{" "}
            {venue.isGrass ? "Grass" : "Artificial Turf"}
          </p>
        </div>
      </div>

      <div className="venue-content">
        <h3>About This Venue</h3>
        <p>
          {venue.name} is a football stadium located in {venue.location}. It
          seats approximately {venue.capacity.toLocaleString()} fans and is{" "}
          {venue.isIndoor ? "an indoor facility" : "an open-air stadium"} with{" "}
          {venue.isGrass ? "natural grass" : "artificial turf"}.
        </p>
      </div>
    </div>
  );
};

export default VenuePage;
