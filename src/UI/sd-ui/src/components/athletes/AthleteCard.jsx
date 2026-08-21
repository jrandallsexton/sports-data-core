import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import apiWrapper from "../../api/apiWrapper";
import { useUserDto } from "../../contexts/UserContext";
import { teamLink } from "../../utils/sportLinks";
import "./AthleteCard.css";

/**
 * Athlete drill-down: the Athlete record, every AthleteSeason, and each
 * season's sourced statistic documents. Deliberately a research surface —
 * provenance fields (doc created timestamps, split identifiers, record
 * ids) are shown on purpose so sourced data can be spot-checked without
 * opening the database. Duplicate docs and stale vintages are the thing
 * this page exists to make visible.
 */
function AthleteCard() {
  const { sport = "football", league = "ncaa", athleteId } = useParams();
  const { userDto } = useUserDto();
  const [athlete, setAthlete] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Provenance (record ids, created/modified, doc identity + vintage,
  // duplicate docs) is the admin spot-check surface. Regular users get a
  // clean player page: no internal ids, and only the newest statistic doc
  // per split — duplicates would read as a rendering bug, but for admins
  // they ARE the signal.
  const isAdmin = userDto?.isAdmin === true;

  useEffect(() => {
    const fetchAthlete = async () => {
      setLoading(true);
      setError(null);
      try {
        const response = await apiWrapper.Athletes.getDetails(sport, league, athleteId);
        setAthlete(response.data?.value ?? response.data ?? null);
      } catch (err) {
        console.error("Failed to fetch athlete details:", err);
        setError(
          err?.response?.status === 404
            ? "Athlete not found."
            : "Failed to load athlete details."
        );
      } finally {
        setLoading(false);
      }
    };

    if (athleteId) {
      fetchAthlete();
    } else {
      setLoading(false);
    }
  }, [sport, league, athleteId]);

  if (loading) return <div className="athlete-card">Loading athlete...</div>;
  if (error) return <div className="athlete-card error">{error}</div>;
  if (!athlete) return <div className="athlete-card">No athlete data available.</div>;

  const currentSeason = athlete.seasons?.[0];

  const formatDate = (value) => (value ? new Date(value).toLocaleDateString() : "-");
  const formatUtc = (value) => (value ? new Date(value).toISOString().replace("T", " ").slice(0, 19) + "Z" : "-");

  // Admins see every sourced doc; users see the newest per split (the API
  // returns docs newest-first, so first-seen wins).
  const visibleStatDocs = (statistics) => {
    if (!statistics) return [];
    if (isAdmin) return statistics;
    const newestBySplit = new Map();
    for (const doc of statistics) {
      const key = doc.splitId ?? doc.splitName ?? "";
      if (!newestBySplit.has(key)) newestBySplit.set(key, doc);
    }
    return [...newestBySplit.values()];
  };

  return (
    <div className="athlete-card">
      <header className="athlete-header">
        <div className="athlete-title">
          <h1>{athlete.displayName ?? athlete.shortName ?? "Unknown Athlete"}</h1>
          <span className={`status-badge ${athlete.isActive ? "active" : "inactive"}`}>
            {athlete.statusName ?? (athlete.isActive ? "Active" : "Inactive")}
          </span>
        </div>
        <div className="athlete-subtitle">
          {currentSeason?.positionAbbreviation && <span>{currentSeason.positionAbbreviation}</span>}
          {athlete.jersey && <span>#{athlete.jersey}</span>}
          {currentSeason?.teamSlug && (
            <Link to={teamLink(currentSeason.teamSlug, currentSeason.seasonYear, sport, league)}>
              {currentSeason.teamName ?? currentSeason.teamSlug}
            </Link>
          )}
        </div>
      </header>

      <section className="athlete-section">
        <h2>Athlete Record</h2>
        <dl className="athlete-facts">
          <div><dt>Name</dt><dd>{athlete.firstName ?? "-"} {athlete.lastName ?? ""}</dd></div>
          <div><dt>Height</dt><dd>{athlete.heightDisplay ?? "-"}</dd></div>
          <div><dt>Weight</dt><dd>{athlete.weightDisplay ?? "-"}</dd></div>
          <div><dt>Born</dt><dd>{formatDate(athlete.doB)}</dd></div>
          <div><dt>Birthplace</dt><dd>{athlete.birthLocation ?? "-"}</dd></div>
          <div><dt>Experience</dt><dd>{athlete.experienceDisplayValue ?? "-"}</dd></div>
          <div><dt>Debut</dt><dd>{athlete.debutYear ?? "-"}</dd></div>
          <div><dt>Draft</dt><dd>{athlete.draftDisplayText ?? "-"}</dd></div>
          {isAdmin && (
            <>
              <div><dt>Slug</dt><dd className="mono">{athlete.slug ?? "-"}</dd></div>
              <div><dt>Athlete Id</dt><dd className="mono">{athlete.id}</dd></div>
              <div><dt>Created</dt><dd className="mono">{formatUtc(athlete.createdUtc)}</dd></div>
              <div><dt>Modified</dt><dd className="mono">{formatUtc(athlete.modifiedUtc)}</dd></div>
            </>
          )}
        </dl>
      </section>

      <section className="athlete-section">
        <h2>Seasons ({athlete.seasons?.length ?? 0})</h2>
        {(!athlete.seasons || athlete.seasons.length === 0) && (
          <p className="athlete-empty">No season records.</p>
        )}
        {athlete.seasons?.map((season, index) => (
          <details
            key={season.athleteSeasonId}
            className="season-block"
            open={index === 0}
          >
            <summary>
              <span className="season-year">{season.seasonYear ?? "—"}</span>
              <span className="season-team">
                {season.teamName ?? "(no team)"}
              </span>
              <span className="season-meta">
                {season.positionAbbreviation ?? season.position ?? "-"}
                {season.jersey ? ` · #${season.jersey}` : ""}
                {season.experienceDisplayValue ? ` · ${season.experienceDisplayValue}` : ""}
              </span>
              <span className={`status-badge ${season.isActive ? "active" : "inactive"}`}>
                {season.statusName ?? (season.isActive ? "Active" : "Inactive")}
              </span>
              {isAdmin && (
                <span className="season-doc-count">
                  {season.statistics?.length ?? 0} stat doc{(season.statistics?.length ?? 0) === 1 ? "" : "s"}
                </span>
              )}
            </summary>

            <div className="season-body">
              <dl className="athlete-facts compact">
                {season.teamSlug && (
                  <div>
                    <dt>Team</dt>
                    <dd>
                      <Link to={teamLink(season.teamSlug, season.seasonYear, sport, league)}>
                        {season.teamName ?? season.teamSlug}
                      </Link>
                    </dd>
                  </div>
                )}
                {isAdmin && (
                  <>
                    <div><dt>Season Row Id</dt><dd className="mono">{season.athleteSeasonId}</dd></div>
                    <div><dt>Created</dt><dd className="mono">{formatUtc(season.createdUtc)}</dd></div>
                    <div><dt>Modified</dt><dd className="mono">{formatUtc(season.modifiedUtc)}</dd></div>
                  </>
                )}
              </dl>

              {visibleStatDocs(season.statistics).length === 0 && (
                <p className="athlete-empty">
                  {isAdmin ? "No statistics sourced for this season." : "No statistics for this season."}
                </p>
              )}

              {visibleStatDocs(season.statistics).map((doc) => (
                <div key={doc.id} className="stat-doc">
                  <div className="stat-doc-header">
                    <span className="stat-doc-split">
                      {doc.splitName || doc.splitType || "Season"}
                      {isAdmin && doc.splitId ? ` (split ${doc.splitId})` : ""}
                    </span>
                    {isAdmin && (
                      <span className="stat-doc-provenance mono">
                        doc {doc.id?.slice(0, 8)} · sourced {formatUtc(doc.createdUtc)}
                      </span>
                    )}
                  </div>
                  {doc.categories?.map((category) => (
                    <div key={category.name} className="stat-category">
                      <h4>
                        {category.displayName ?? category.name}
                        {category.summary ? <span className="stat-summary"> — {category.summary}</span> : null}
                      </h4>
                      <div className="stat-table-wrap">
                        <table className="stat-table">
                          <thead>
                            <tr>
                              <th>Stat</th>
                              <th>Abbr</th>
                              <th>Total</th>
                              <th>Per Game</th>
                            </tr>
                          </thead>
                          <tbody>
                            {category.stats?.map((stat) => (
                              <tr key={`${category.name}-${stat.abbreviation}-${stat.displayName}`}>
                                <td>{stat.displayName}</td>
                                <td className="mono">{stat.abbreviation}</td>
                                <td className="stat-value">{stat.displayValue ?? "-"}</td>
                                <td className="stat-value">{stat.perGameDisplayValue ?? "-"}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  ))}
                </div>
              ))}
            </div>
          </details>
        ))}
      </section>
    </div>
  );
}

export default AthleteCard;
