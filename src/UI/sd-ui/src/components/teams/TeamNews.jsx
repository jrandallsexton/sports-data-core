import "./TeamNews.css";

function TeamNews({ news }) {
  return (
    <div className="team-news">
      <h3>Latest News</h3>
      {news?.length ? (
        <ul>
          {news.map((item, idx) => (
            <li key={idx}>
              <a href={item.link} target="_blank" rel="noopener noreferrer">
                {item.title}
              </a>
            </li>
          ))}
        </ul>
      ) : (
        <div>No news available.</div>
      )}
    </div>
  );
}

export default TeamNews;
