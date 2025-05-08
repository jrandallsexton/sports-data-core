import { useParams } from "react-router-dom";
import mockVenues from "../../data/venues.js";
import "./VenuePage.css";

const VenuePage = () => {
  const { slug } = useParams();
  const venue = mockVenues[slug];

  if (!venue) return <div className="venue-page">Venue not found.</div>;

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
