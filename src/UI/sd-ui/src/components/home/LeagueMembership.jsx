import { Link } from "react-router-dom";
import { useUserDto } from "../../contexts/UserContext";

/**
 * LeagueMembership component - displays league creation/joining options
 */
function LeagueMembership() {
  const { userDto } = useUserDto();

  return (
    <div className="card">
      <h2>Pick'em Leagues</h2>
      <p>Create or join a private league to compete with friends.</p>
      <div style={{ display: "flex", gap: "1rem", marginTop: "1rem" }}>
        <Link to="/app/league/create" className="card-link">
          Create a League
        </Link>
        <Link to="/app/league/discover" className="card-link">
          Join a League
        </Link>
        {userDto?.leagues?.length > 0 && (
          <Link to="/app/league" className="card-link">
            My Leagues
          </Link>
        )}
      </div>
    </div>
  );
}

export default LeagueMembership;
