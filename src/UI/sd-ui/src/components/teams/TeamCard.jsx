// src\components\teams\TeamCard.jsx
import "./TeamCard.css";
import { useParams } from "react-router-dom";
import teams from "../../data/teams"; // or wherever your merged file lives
import { Link } from "react-router-dom";

function TeamCard() {
  //if (!team) return <div className="team-card">Loading team data…</div>;

  const { slug } = useParams();
  const team = teams[slug];

//   const renderResult = (result) => {
//     if (!result) return "";

//     const normalized = result.trim().toUpperCase();
//     const isWin = normalized.startsWith("W");
//     const emoji = isWin ? "✅" : "✖️";

//     return (
//       <span>
//         <span className={isWin ? "result-win" : "result-loss"}>{emoji}</span>{" "}
//         {result}
//       </span>
//     );
//   };

  return (
    <div
      className="team-card"
      style={{
        "--accent-color": team.colorPrimary || "#6c63ff",
        "--highlight-color": team.colorSecondary || "#ffc107",
      }}
    >
      <div className="team-header">
        <img
          src={team.logoUrl}
          alt={`${team.name} logo`}
          className="team-logo"
        />
        <div>
          <h2 className="team-name">{team.name}</h2>
          <p className="team-location">{team.location}</p>
          <p className="team-stadium">
            {team.stadiumName} – {team.stadiumCapacity.toLocaleString()}{" "}
            capacity
          </p>
        </div>
      </div>

      <div className="team-news">
        <h3>Latest News</h3>
        <ul>
          {team.news?.map((item, idx) => (
            <li key={idx}>
              <a href={item.link} target="_blank" rel="noopener noreferrer">
                {item.title}
              </a>
            </li>
          )) || <p>No news available.</p>}
        </ul>
      </div>

      <div className="team-schedule">
        <h3>Current Season</h3>
        <table>
          <thead>
            <tr>
              <th>Date</th>
              <th>Opponent</th>
              <th>Result</th>
            </tr>
          </thead>
          <tbody>
            {team.schedule?.map((game, idx) => (
              <tr key={idx}>
                <td>{game.date}</td>
                <td>
                  <Link
                    to={`/app/sport/football/ncaa/team/${
                      Object.values(teams).find((t) => t.name === game.opponent)
                        ?.slug
                    }`}
                    className="team-link"
                  >
                    {game.opponent}
                  </Link>
                </td>

                <td
                  className={
                    game.result.trim().toUpperCase().startsWith("W")
                      ? "result-win"
                      : "result-loss"
                  }
                >
                  {game.result}
                </td>
              </tr>
            )) || (
              <tr>
                <td colSpan="3">No games scheduled.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default TeamCard;
