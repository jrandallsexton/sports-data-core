import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import apiWrapper from "../../api/apiWrapper";
import "./VenuesPage.css";

// 🧠 Simple module-scoped cache (clears on full page reload)
let cachedVenues = null;

const VenuesPage = () => {
  const { sport, league } = useParams();
  const [venues, setVenues] = useState(cachedVenues || []);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (cachedVenues) return; // 👈 Already cached

    const loadVenues = async () => {
      try {
        const res = await apiWrapper.Venues.getAll(sport, league);
        cachedVenues = res.data.venues;
        setVenues(cachedVenues);
      } catch (err) {
        console.error("Failed to load venues:", err);
        setError("Unable to fetch venues.");
      }
    };

    loadVenues();
  }, [sport, league]);

  if (error) return <div className="venues-page">{error}</div>;

  return (
    <div className="venues-page">
      <h2>Venues</h2>
      <ul className="venue-list">
        {venues.map((venue) => (
          <li key={venue.id} className="venue-list-item">
            <Link to={`/app/sport/${sport}/${league}/venue/${venue.slug}`}>
              <strong>{venue.name}</strong>
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
};

export default VenuesPage;
