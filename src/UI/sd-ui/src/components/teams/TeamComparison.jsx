import React, { useState, useEffect } from "react";
import "./TeamComparison.css";
import "./TeamComparisonTabs.css";

/**
 * TeamComparison Dialog
 * Props:
 * - open: boolean (controls dialog visibility)
 * - onClose: function (called to close dialog)
 * - teamA: { name, logoUri, stats: { data: { statistics: { ... } } } }
 * - teamB: { name, logoUri, stats: { data: { statistics: { ... } } } }
 *
 * The stats prop is the full API response, just like TeamStatistics.
 */
export default function TeamComparison({
  open,
  onClose,
  teamA,
  teamB,
  teamAColor = "#61dafb",
  teamBColor = "#61dafb",
  history = null,
  showGambling = false,
}) {
  // Historical blocks (head-to-head + prior-season form). Present whenever
  // the franchises have played before — including week 1, when it's the only
  // populated tab.
  const headToHead = history?.headToHead ?? [];
  const teamAPriorGames = history?.awayPriorSeasonGames ?? [];
  const teamBPriorGames = history?.homePriorSeasonGames ?? [];
  const teamAPriorSeason = history?.awayPriorSeason ?? null;
  const teamBPriorSeason = history?.homePriorSeason ?? null;
  const hasHistoryData =
    headToHead.length > 0 ||
    teamAPriorGames.length > 0 ||
    teamBPriorGames.length > 0 ||
    teamAPriorSeason != null ||
    teamBPriorSeason != null ||
    // Spread context only counts when it can actually render — it is
    // gambling-gated, and a hidden-only history would open an empty tab.
    (showGambling && history?.spreadContext != null);

  // Main tab state. History is the overview and opens first; Statistics and
  // Metrics are the detail tabs. Statistics only leads when there is no
  // history to show.
  const [activeTab, setActiveTab] = useState(
    hasHistoryData ? "history" : "statistics"
  );

  // Helper: choose light or dark text based on background color
  const getContrastTextColor = (bgColor) => {
    let color = bgColor;
    if (
      typeof color === "string" &&
      color.length === 6 &&
      !color.startsWith("#")
    ) {
      color = `#${color}`;
    }
    if (color.startsWith("#")) {
      let hex = color.replace("#", "");
      if (hex.length === 3) {
        hex = hex
          .split("")
          .map((x) => x + x)
          .join("");
      }
      if (hex.length === 6) {
        const r = parseInt(hex.substring(0, 2), 16);
        const g = parseInt(hex.substring(2, 4), 16);
        const b = parseInt(hex.substring(4, 6), 16);
        // Relative luminance formula
        const luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        return luminance < 128 ? "#fff" : "#23272f";
      }
    }
    // For rgb(a) colors
    if (color.startsWith("rgb")) {
      const vals = color.match(/\d+/g);
      if (vals && vals.length >= 3) {
        const r = parseInt(vals[0], 10);
        const g = parseInt(vals[1], 10);
        const b = parseInt(vals[2], 10);
        const luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        return luminance < 128 ? "#fff" : "#23272f";
      }
    }
    // Default to dark text
    return "#23272f";
  };
  // Normalize color: prepend # if missing for hex
  const normalizeColor = (color) => {
    if (
      typeof color === "string" &&
      color.length === 6 &&
      !color.startsWith("#")
    ) {
      return `#${color}`;
    }
    return color;
  };
  const normAColor = normalizeColor(teamAColor);
  const normBColor = normalizeColor(teamBColor);
  // Debug: log normalized color props
  console.log("TeamComparison colors:", {
    teamAColor,
    teamBColor,
    normAColor,
    normBColor,
  });
  // Prevent background scroll when dialog is open
  useEffect(() => {
    if (open) {
      const original = document.body.style.overflow;
      document.body.style.overflow = "hidden";
      return () => {
        document.body.style.overflow = original;
      };
    }
  }, [open]);
  // Use the same logic as TeamStatistics
  const statisticsA =
    teamA.stats?.data?.statistics || teamA.stats?.statistics || {};
  const statisticsB =
    teamB.stats?.data?.statistics || teamB.stats?.statistics || {};
  const categories = Object.keys(statisticsA);
  const [selectedCategory, setSelectedCategory] = useState(categories[0] || "");

  // Helper to render the Statistics tab content
  const renderStatisticsTab = () => {
    return (
      <>
        <div className="team-comparison-tabs">
          {categories.map((cat) => {
            // Count favored stats for each team in this category
            const statsA = statisticsA[cat] || [];
            const statsB = statisticsB[cat] || [];
            let favoredA = 0,
              favoredB = 0;
            for (let i = 0; i < Math.max(statsA.length, statsB.length); i++) {
              const entryA = statsA[i] || {};
              const entryB = statsB[i] || {};
              const favored = getFavored(
                entryA.displayValue ?? "-",
                entryB.displayValue ?? "-",
                entryA,
                entryB
              );
              if (favored === "A") favoredA++;
              if (favored === "B") favoredB++;
            }
            let tabBg = "";
            let tabColor = "";
            if (favoredA > favoredB) {
              tabBg = /^#|rgb/.test(normAColor)
                ? normAColor
                : getMutedColor(normAColor);
              tabColor = getContrastTextColor(normAColor);
            } else if (favoredB > favoredA) {
              tabBg = /^#|rgb/.test(normBColor)
                ? normBColor
                : getMutedColor(normBColor);
              tabColor = getContrastTextColor(normBColor);
            } else {
              tabBg = "#343a40";
              tabColor = "#fff";
            }
            return (
              <button
                key={cat}
                className={`team-comparison-tab${
                  selectedCategory === cat ? " selected" : ""
                }`}
                onClick={() => setSelectedCategory(cat)}
                style={{
                  background: tabBg,
                  color: tabColor,
                  borderRadius: 6,
                  fontWeight: selectedCategory === cat ? "bold" : undefined,
                  position: "relative",
                  zIndex: selectedCategory === cat ? 2 : 1,
                  display: "flex",
                  flexDirection: "column",
                  alignItems: "center",
                  gap: selectedCategory === cat ? 4 : 3,
                  minHeight: "42px",
                  padding: "0.4rem 0.7rem",
                }}
              >
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: selectedCategory === cat ? 6 : 0,
                  }}
                >
                  {selectedCategory === cat && (
                    <span
                      style={{ fontSize: "1.1em", verticalAlign: "middle" }}
                    >
                      ★
                    </span>
                  )}
                  <span>
                    {cat.charAt(0).toUpperCase() + cat.slice(1)} ({favoredA}:
                    {favoredB})
                  </span>
                </div>
                {(favoredA > 0 || favoredB > 0) && (
                  <div className="category-gradient-bar">
                    <div
                      className="gradient-segment team-a"
                      style={{
                        width: `${(favoredA / (favoredA + favoredB)) * 100}%`,
                        backgroundColor: normAColor,
                      }}
                    ></div>
                    <div
                      className="gradient-segment team-b"
                      style={{
                        width: `${(favoredB / (favoredA + favoredB)) * 100}%`,
                        backgroundColor: normBColor,
                      }}
                    ></div>
                  </div>
                )}
              </button>
            );
          })}
        </div>
        {selectedCategory && (
          <div className="team-comparison-table">
            {(statisticsA[selectedCategory] || []).map((entry, idx) => {
              const bEntry = (statisticsB[selectedCategory] || [])[idx] || {};
              const favored = getFavored(
                entry.displayValue ?? "-",
                bEntry.displayValue ?? "-",
                entry,
                bEntry
              );
              const aRankContent =
                entry.rank && entry.rank > 1 ? (
                  <span className="rank-inline">(#{entry.rank})</span>
                ) : (
                  <span style={{ width: 0, display: "inline-block" }}></span>
                );
              const bRankContent =
                bEntry.rank && bEntry.rank > 1 ? (
                  <span className="rank-inline">(#{bEntry.rank})</span>
                ) : (
                  <span style={{ width: 0, display: "inline-block" }}></span>
                );
              const aValContent = entry.displayValue ?? "-";
              const bValContent = bEntry.displayValue ?? "-";
              const statKey = entry.statisticKey;
              const statLabel = entry.statisticValue;
              return (
                <div className="stat-row" key={statKey}>
                  <div className="stat-rank left-rank">{aRankContent}</div>
                  <div
                    className={`stat-value left${
                      favored === "A" ? " favored" : ""
                    }`}
                    style={
                      favored === "A"
                        ? {
                            background: /^#|rgb/.test(normAColor)
                              ? normAColor
                              : getMutedColor(normAColor),
                            borderRadius: 6,
                            color: getContrastTextColor(normAColor),
                          }
                        : {}
                    }
                  >
                    {aValContent}
                  </div>
                  <div
                    className="stat-category"
                    style={{
                      width: 480,
                      minWidth: 360,
                      maxWidth: 660,
                      textOverflow: "ellipsis",
                      overflow: "hidden",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {statLabel}
                  </div>
                  <div
                    className={`stat-value right${
                      favored === "B" ? " favored" : ""
                    }`}
                    style={
                      favored === "B"
                        ? {
                            background: /^#|rgb/.test(normBColor)
                              ? normBColor
                              : getMutedColor(normBColor),
                            borderRadius: 6,
                            color: getContrastTextColor(normBColor),
                          }
                        : {}
                    }
                  >
                    {bValContent}
                  </div>
                  <div className="stat-rank right-rank">{bRankContent}</div>
                </div>
              );
            })}
          </div>
        )}
      </>
    );
  };

  // Helper to render the Metrics tab content
  const renderMetricsTab = () => {
    // Check if metrics data is available
    if (!teamA?.metrics || !teamB?.metrics) {
      return (
        <div className="metrics-placeholder">
          <p
            style={{
              textAlign: "center",
              color: "#adb5bd",
              fontSize: "1.1rem",
              padding: "2rem",
            }}
          >
            Metrics data is not available for comparison.
          </p>
        </div>
      );
    }

    const metricsData = [
      {
        category: "Offensive Efficiency",
        metrics: [
          { 
            label: "Yards Per Play", 
            keyA: "ypp", 
            keyB: "ypp",
            format: (val) => val?.toFixed(2) || "0.00",
            higherIsBetter: true
          },
          { 
            label: "Success Rate", 
            keyA: "successRate", 
            keyB: "successRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: true
          },
          { 
            label: "Explosive Play Rate", 
            keyA: "explosiveRate", 
            keyB: "explosiveRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: true
          },
          { 
            label: "Points Per Drive", 
            keyA: "pointsPerDrive", 
            keyB: "pointsPerDrive",
            format: (val) => val?.toFixed(2) || "0.00",
            higherIsBetter: true
          },
          { 
            label: "3rd/4th Down Rate", 
            keyA: "thirdFourthRate", 
            keyB: "thirdFourthRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: true
          }
        ]
      },
      {
        category: "Red Zone Efficiency",
        metrics: [
          { 
            label: "Red Zone TD Rate", 
            keyA: "rzTdRate", 
            keyB: "rzTdRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: true
          },
          { 
            label: "Red Zone Score Rate", 
            keyA: "rzScoreRate", 
            keyB: "rzScoreRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: true
          }
        ]
      },
      {
        category: "Defensive Metrics",
        metrics: [
          { 
            label: "Opp Yards Per Play", 
            keyA: "oppYpp", 
            keyB: "oppYpp",
            format: (val) => val?.toFixed(2) || "0.00",
            higherIsBetter: false
          },
          { 
            label: "Opp Success Rate", 
            keyA: "oppSuccessRate", 
            keyB: "oppSuccessRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: false
          },
          { 
            label: "Opp Explosive Rate", 
            keyA: "oppExplosiveRate", 
            keyB: "oppExplosiveRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: false
          },
          { 
            label: "Opp Points Per Drive", 
            keyA: "oppPointsPerDrive", 
            keyB: "oppPointsPerDrive",
            format: (val) => val?.toFixed(2) || "0.00",
            higherIsBetter: false
          },
          { 
            label: "Opp 3rd/4th Down Rate", 
            keyA: "oppThirdFourthRate", 
            keyB: "oppThirdFourthRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: false
          },
          { 
            label: "Opp Red Zone TD Rate", 
            keyA: "oppRzTdRate", 
            keyB: "oppRzTdRate",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: false
          }
        ]
      },
      {
        category: "Game Control",
        metrics: [
          { 
            label: "Time Possession Ratio", 
            keyA: "timePossRatio", 
            keyB: "timePossRatio",
            format: (val) => val ? (val * 100).toFixed(1) + "%" : "0.0%",
            higherIsBetter: true
          },
          { 
            label: "Field Position Differential", 
            keyA: "fieldPosDiff", 
            keyB: "fieldPosDiff",
            format: (val) => val?.toFixed(2) || "0.00",
            higherIsBetter: true
          },
          { 
            label: "Turnover Margin Per Drive", 
            keyA: "turnoverMarginPerDrive", 
            keyB: "turnoverMarginPerDrive",
            format: (val) => val?.toFixed(3) || "0.000",
            higherIsBetter: true
          }
        ]
      },
      {
        category: "Special Teams",
        // netPunt / penaltyYardsPerPlay removed: no longer computed
        // (metrics formula audit M4/H3)
        metrics: [
          {
            label: "Field Goal %",
            keyA: "fgPctShrunk",
            keyB: "fgPctShrunk",
            format: (val) => val != null ? (val * 100).toFixed(1) + "%" : "-",
            higherIsBetter: true
          }
        ]
      }
    ];

    const getMetricFavored = (metric, valA, valB) => {
      if (valA == null || valB == null) return null;
      
      if (metric.higherIsBetter) {
        return valA > valB ? 'A' : valB > valA ? 'B' : null;
      } else {
        return valA < valB ? 'A' : valB < valA ? 'B' : null;
      }
    };

    return (
      <div className="metrics-content">
        <div className="team-comparison-metrics-table">
          {metricsData.map((category, categoryIndex) => 
            category.metrics.map((metric, metricIndex) => {
              const valA = teamA.metrics[metric.keyA];
              const valB = teamB.metrics[metric.keyB];
              const favored = getMetricFavored(metric, valA, valB);

              return (
                <div key={`${categoryIndex}-${metricIndex}`} className="stat-row">
                  <div className="stat-rank left-rank"></div>
                  <div 
                    className={`stat-value left ${favored === 'A' ? 'favored' : ''}`}
                    style={{
                      backgroundColor: favored === 'A' ? normAColor : 'transparent',
                      color: favored === 'A' ? getContrastTextColor(normAColor) : '#f8f9fa'
                    }}
                  >
                    {metric.format(valA)}
                  </div>
                  <div 
                    className="stat-category"
                    style={{
                      width: 480,
                      minWidth: 360,
                      maxWidth: 660,
                      textOverflow: "ellipsis",
                      overflow: "hidden",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {metric.label}
                  </div>
                  <div 
                    className={`stat-value right ${favored === 'B' ? 'favored' : ''}`}
                    style={{
                      backgroundColor: favored === 'B' ? normBColor : 'transparent',
                      color: favored === 'B' ? getContrastTextColor(normBColor) : '#f8f9fa'
                    }}
                  >
                    {metric.format(valB)}
                  </div>
                  <div className="stat-rank right-rank"></div>
                </div>
              );
            })
          )}
        </div>
      </div>
    );
  };

  // ─── History tab ─────────────────────────────────────────────────────────
  // Historical team names come from Franchise.DisplayName — the same source
  // as teamA.name / teamB.name (matchup Away/Home) — so exact string matching
  // identifies "our" side of a historical row.

  const formatGameDate = (iso) =>
    new Date(iso).toLocaleDateString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
    });

  // W/L/T + score + opponent from one team's perspective; null when the team
  // name matches neither side (defensive — shouldn't happen).
  const gameResultForTeam = (game, teamName) => {
    const isHome = game.homeTeam === teamName;
    const isAway = game.awayTeam === teamName;
    if (!isHome && !isAway) return null;
    const ourScore = isHome ? game.homeScore : game.awayScore;
    const theirScore = isHome ? game.awayScore : game.homeScore;
    const outcome =
      game.winner == null ? "T" : game.winner === teamName ? "W" : "L";
    return {
      outcome,
      ourScore,
      theirScore,
      opponent: isHome ? game.awayTeam : game.homeTeam,
      venue: isHome ? "vs" : "@",
    };
  };

  const renderPriorSeasonColumn = (teamName, games, summary) => (
    <div className="history-team-col">
      <div className="history-team-col-header">
        <span className="history-team-name">{teamName}</span>
        {summary && (
          <span className="history-season-record">
            {summary.seasonYear}: {summary.wins}-{summary.losses}
            {summary.conferenceWins != null && summary.conferenceLosses != null
              ? ` (${summary.conferenceWins}-${summary.conferenceLosses} conf)`
              : ""}
          </span>
        )}
      </div>
      {games.length === 0 ? (
        <div className="history-empty-note">No prior-season games on record.</div>
      ) : (
        games.map((game, idx) => {
          const r = gameResultForTeam(game, teamName);
          if (!r) {
            return (
              <div className="history-game-line" key={idx}>
                <span className="history-game-date">{formatGameDate(game.gameDate)}</span>
                <span className="history-game-detail">
                  {game.awayTeam} {game.awayScore ?? "—"} @ {game.homeTeam} {game.homeScore ?? "—"}
                </span>
              </div>
            );
          }
          return (
            <div className="history-game-line" key={idx}>
              <span className={`history-result-badge ${r.outcome === "W" ? "win" : r.outcome === "L" ? "loss" : "tie"}`}>
                {r.outcome}
              </span>
              <span className="history-game-score">
                {r.ourScore ?? "—"}-{r.theirScore ?? "—"}
              </span>
              <span className="history-game-detail">
                {r.venue} {r.opponent}
              </span>
              <span className="history-game-date">{formatGameDate(game.gameDate)}</span>
            </div>
          );
        })
      )}
    </div>
  );

  // ─── Spread-context facts ("The Line") ───────────────────────────────────
  // Deterministic sentences composed from server-computed facts — every
  // number comes from a query, never from prose. The whole block is
  // spread-derived, so it renders only when showGambling allows.

  const spreadContext = history?.spreadContext ?? null;

  const fmtLine = (n) => String(n);

  const marginFactSentence = (teamName, fact, magnitude, won) => {
    if (!fact) return null;
    if (!fact.lastGame) {
      return {
        head: `${teamName} ${won ? "has never won" : "has never lost"} a game by ${fmtLine(magnitude)}+`,
        detail: `in our records (back to ${fact.searchFloorSeason}).`,
      };
    }
    const g = fact.lastGame;
    const isHome = g.homeTeam === teamName;
    const ourScore = isHome ? g.homeScore : g.awayScore;
    const theirScore = isHome ? g.awayScore : g.homeScore;
    const opponent = isHome ? g.awayTeam : g.homeTeam;
    const when = new Date(g.gameDate).toLocaleDateString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
    const quality =
      fact.opponentSeasonRecord || fact.opponentPriorSeasonRecord
        ? ` (they went ${fact.opponentSeasonRecord ?? "?"}${
            fact.opponentPriorSeasonRecord ? `; ${fact.opponentPriorSeasonRecord} the season before` : ""
          })`
        : "";
    const times = fact.countLastFiveSeasons;
    return {
      head: `Last time ${teamName} ${won ? "won" : "lost"} by ${fmtLine(magnitude)}+:`,
      detail: `${when} — ${won ? "beat" : "lost to"} ${opponent} ${ourScore}-${theirScore}${quality}. ${times} such ${won ? "win" : "loss"}${times === 1 ? "" : won ? "s" : "es"} in the last 5 seasons.`,
    };
  };

  const atsFactSentence = (teamName, fact, asFavorite) => {
    if (!fact) return null;
    const role = `${fmtLine(fact.threshold)}+ ${asFavorite ? "favorite" : "underdog"}`;
    if (fact.games === 0) {
      return {
        head: `${teamName} as a ${role}:`,
        detail: `no games with a line that large since ${fact.dataFloorSeason}.`,
      };
    }
    return {
      head: `${teamName} as a ${role}:`,
      detail: `covered ${fact.covers} of ${fact.games} (since ${fact.dataFloorSeason}).`,
    };
  };

  const renderSpreadContext = () => {
    if (!showGambling || !spreadContext) return null;
    const facts = [
      marginFactSentence(spreadContext.favoriteTeam, spreadContext.favoriteWonByMargin, spreadContext.magnitude, true),
      marginFactSentence(spreadContext.underdogTeam, spreadContext.underdogLostByMargin, spreadContext.magnitude, false),
      atsFactSentence(spreadContext.favoriteTeam, spreadContext.favoriteAtsAsBigFavorite, true),
      atsFactSentence(spreadContext.underdogTeam, spreadContext.underdogAtsAsBigUnderdog, false),
    ].filter(Boolean);
    if (facts.length === 0) return null;
    return (
      <div className="history-section">
        <div className="history-section-title">
          The Line{spreadContext.spreadDetails ? ` — ${spreadContext.spreadDetails}` : ""}
        </div>
        {facts.map((f, i) => (
          <div className="line-fact" key={i}>
            <span className="line-fact-head">{f.head}</span>{" "}
            <span className="line-fact-detail">{f.detail}</span>
          </div>
        ))}
      </div>
    );
  };

  const renderHistoryTab = () => {
    if (!hasHistoryData) {
      return (
        <div className="metrics-placeholder">
          <p style={{ textAlign: "center", color: "var(--text-secondary)", fontSize: "1.1rem", padding: "2rem" }}>
            No matchup history is available for these teams.
          </p>
        </div>
      );
    }

    return (
      <div className="history-content">
        {renderSpreadContext()}
        {headToHead.length > 0 && (
          <div className="history-section">
            <div className="history-section-title">
              Head-to-Head — Last {headToHead.length} Meeting{headToHead.length === 1 ? "" : "s"}
            </div>
            {headToHead.map((game, idx) => {
              return (
                <div className="h2h-row" key={idx}>
                  <div className="h2h-meta">
                    <span className="history-game-date">{formatGameDate(game.gameDate)}</span>
                    {game.phase && game.phase !== "Regular Season" && (
                      <span className="h2h-phase">{game.phase}</span>
                    )}
                    {game.note && <span className="h2h-note">{game.note}</span>}
                  </div>
                  {/* Winner is marked by weight alone — team-color text is
                      illegible whenever a team's color sits near the card
                      background (e.g. LSU purple on dark). */}
                  <div className="h2h-line">
                    <span className={`h2h-team${game.winner === game.awayTeam ? " h2h-winner" : ""}`}>
                      {game.awayTeam} {game.awayScore ?? "—"}
                    </span>
                    <span className="h2h-at">@</span>
                    <span className={`h2h-team${game.winner === game.homeTeam ? " h2h-winner" : ""}`}>
                      {game.homeTeam} {game.homeScore ?? "—"}
                    </span>
                  </div>
                  {showGambling && (game.spread || game.spreadWinner || game.overUnderResult) && (
                    <div className="h2h-market">
                      {game.spread && <span>{game.spread}</span>}
                      {game.spreadWinner && <span>ATS: {game.spreadWinner}</span>}
                      {game.overUnderResult && (
                        <span>
                          {game.overUnderResult}
                          {game.overUnder != null ? ` ${game.overUnder}` : ""}
                        </span>
                      )}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}

        <div className="history-section">
          <div className="history-section-title">
            Last Season — Final {Math.max(teamAPriorGames.length, teamBPriorGames.length)} Games
          </div>
          <div className="history-teams-grid">
            {renderPriorSeasonColumn(teamA.name, teamAPriorGames, teamAPriorSeason)}
            {renderPriorSeasonColumn(teamB.name, teamBPriorGames, teamBPriorSeason)}
          </div>
        </div>
      </div>
    );
  };

  if (!open) return null;

  // Helper to determine which team is favored (higher value wins by default)
  // Helper to determine which team is favored (supports isNegativeAttribute)
  const getFavored = (a, b, aEntry = {}, bEntry = {}) => {
    if (a == null || b == null) return null;
    const aNum = parseFloat(a);
    const bNum = parseFloat(b);
    // Use isNegativeAttribute from either entry (prefer A, fallback to B)
    const isNegative =
      aEntry.isNegativeAttribute ?? bEntry.isNegativeAttribute ?? false;
    if (!isNaN(aNum) && !isNaN(bNum)) {
      if (isNegative) {
        if (aNum < bNum) return "A";
        if (bNum < aNum) return "B";
      } else {
        if (aNum > bNum) return "A";
        if (bNum > aNum) return "B";
      }
    }
    return null;
  };

  // Calculate overall team favorability across all statistics for main tab coloring
  const calculateOverallFavorability = () => {
    let totalFavoredA = 0,
      totalFavoredB = 0;

    categories.forEach((cat) => {
      const statsA = statisticsA[cat] || [];
      const statsB = statisticsB[cat] || [];
      for (let i = 0; i < Math.max(statsA.length, statsB.length); i++) {
        const entryA = statsA[i] || {};
        const entryB = statsB[i] || {};
        const favored = getFavored(
          entryA.displayValue ?? "-",
          entryB.displayValue ?? "-",
          entryA,
          entryB
        );
        if (favored === "A") totalFavoredA++;
        if (favored === "B") totalFavoredB++;
      }
    });

    return { totalFavoredA, totalFavoredB };
  };

  const calculateMetricsFavorability = () => {
    if (!teamA?.metrics || !teamB?.metrics) {
      return { metricsFavoredA: 0, metricsFavoredB: 0 };
    }

    let metricsFavoredA = 0,
      metricsFavoredB = 0;

    // Define all metrics with their comparison logic
    const metricsToCompare = [
      // Offensive metrics (higher is better)
      { key: "ypp", higherIsBetter: true },
      { key: "successRate", higherIsBetter: true },
      { key: "explosiveRate", higherIsBetter: true },
      { key: "pointsPerDrive", higherIsBetter: true },
      { key: "thirdFourthRate", higherIsBetter: true },
      { key: "rzTdRate", higherIsBetter: true },
      { key: "rzScoreRate", higherIsBetter: true },
      // Defensive metrics (lower is better for opponent stats)
      { key: "oppYpp", higherIsBetter: false },
      { key: "oppSuccessRate", higherIsBetter: false },
      { key: "oppExplosiveRate", higherIsBetter: false },
      { key: "oppPointsPerDrive", higherIsBetter: false },
      { key: "oppThirdFourthRate", higherIsBetter: false },
      { key: "oppRzTdRate", higherIsBetter: false },
      // Game control metrics
      { key: "timePossRatio", higherIsBetter: true },
      { key: "fieldPosDiff", higherIsBetter: true },
      { key: "turnoverMarginPerDrive", higherIsBetter: true },
      // Special teams metrics (netPunt / penaltyYardsPerPlay removed:
      // no longer computed — metrics formula audit M4/H3)
      { key: "fgPctShrunk", higherIsBetter: true }
    ];

    metricsToCompare.forEach((metric) => {
      const valA = teamA.metrics[metric.key];
      const valB = teamB.metrics[metric.key];
      
      if (valA != null && valB != null) {
        if (metric.higherIsBetter) {
          if (valA > valB) metricsFavoredA++;
          else if (valB > valA) metricsFavoredB++;
        } else {
          if (valA < valB) metricsFavoredA++;
          else if (valB < valA) metricsFavoredB++;
        }
      }
    });

    return { metricsFavoredA, metricsFavoredB };
  };

  const { totalFavoredA, totalFavoredB } = calculateOverallFavorability();
  const { metricsFavoredA, metricsFavoredB } = calculateMetricsFavorability();

  // Head-to-head wins among the displayed meetings (ties count for neither).
  const h2hWinsA = headToHead.filter((g) => g.winner === teamA.name).length;
  const h2hWinsB = headToHead.filter((g) => g.winner === teamB.name).length;

  // Get main tab styling based on overall favorability
  const getMainTabStyling = (tabType, isActive) => {
    if (!isActive) return {}; // Only apply styling to active tabs

    if (tabType === "statistics") {
      if (totalFavoredA > totalFavoredB) {
        return {
          background: /^#|rgb/.test(normAColor)
            ? normAColor
            : getMutedColor(normAColor),
          color: getContrastTextColor(normAColor),
        };
      } else if (totalFavoredB > totalFavoredA) {
        return {
          background: /^#|rgb/.test(normBColor)
            ? normBColor
            : getMutedColor(normBColor),
          color: getContrastTextColor(normBColor),
        };
      }
    } else if (tabType === "metrics") {
      if (metricsFavoredA > metricsFavoredB) {
        return {
          background: /^#|rgb/.test(normAColor)
            ? normAColor
            : getMutedColor(normAColor),
          color: getContrastTextColor(normAColor),
        };
      } else if (metricsFavoredB > metricsFavoredA) {
        return {
          background: /^#|rgb/.test(normBColor)
            ? normBColor
            : getMutedColor(normBColor),
          color: getContrastTextColor(normBColor),
        };
      }
    } else if (tabType === "history") {
      if (h2hWinsA > h2hWinsB) {
        return {
          background: /^#|rgb/.test(normAColor)
            ? normAColor
            : getMutedColor(normAColor),
          color: getContrastTextColor(normAColor),
        };
      } else if (h2hWinsB > h2hWinsA) {
        return {
          background: /^#|rgb/.test(normBColor)
            ? normBColor
            : getMutedColor(normBColor),
          color: getContrastTextColor(normBColor),
        };
      }
    }
    // Default styling for neutral tabs or when teams are tied
    return {
      background: "#61dafb", // Use default active color
      color: "#23272f",
    };
  };

  // Helper to get muted color (simple alpha blend)
  const getMutedColor = (color) => {
    if (!color) return "#61dafb33";
    // If hex, convert to rgba with alpha
    if (color.startsWith("#")) {
      // Support #RRGGBB and #RGB
      let hex = color.replace("#", "");
      if (hex.length === 3) {
        hex = hex
          .split("")
          .map((x) => x + x)
          .join("");
      }
      if (hex.length === 6) {
        const r = parseInt(hex.substring(0, 2), 16);
        const g = parseInt(hex.substring(2, 4), 16);
        const b = parseInt(hex.substring(4, 6), 16);
        return `rgba(${r},${g},${b},0.18)`;
      }
      // Fallback for other hex formats
      return color;
    }
    // If rgb(a), reduce alpha
    if (color.startsWith("rgb")) {
      // If already rgba, replace alpha
      if (color.startsWith("rgba")) {
        return color.replace(
          /rgba\(([^,]+),([^,]+),([^,]+),[^)]+\)/,
          "rgba($1,$2,$3,0.18)"
        );
      }
      // If rgb, add alpha
      return color.replace(/rgb\(([^)]+)\)/, "rgba($1,0.18)");
    }
    // Otherwise, use color as-is
    return color;
  };

  return (
    <div className="team-comparison-dialog-backdrop" onClick={onClose}>
      <div
        className="team-comparison-dialog"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Same close affordance as InsightDialog — X top-right, no footer CTA */}
        <button className="close-x-button" onClick={onClose} aria-label="Close">
          &times;
        </button>
        <div className="team-comparison-content">
          <div className="team-comparison-header">
            <div className="team-col">
              <img src={teamA.logoUri} alt={teamA.name} className="team-logo" />
              <div className="team-name">{teamA.name}</div>
            </div>
            <div className="vs-col">vs</div>
            <div className="team-col">
              <img src={teamB.logoUri} alt={teamB.name} className="team-logo" />
              <div className="team-name">{teamB.name}</div>
            </div>
          </div>

          {/* Main tabs — History is the overview and leads; Statistics and
              Metrics carry the detail. */}
          <div className="main-tabs">
            {hasHistoryData && (
              <button
                className={`main-tab ${activeTab === "history" ? "active" : ""}`}
                onClick={() => setActiveTab("history")}
                style={getMainTabStyling("history", activeTab === "history")}
              >
                <div className="main-tab-content">
                  <div className="tab-text">History ({h2hWinsA}:{h2hWinsB})</div>
                  {(h2hWinsA > 0 || h2hWinsB > 0) && (
                    <div className="tab-gradient-bar">
                      <div
                        className="gradient-segment team-a"
                        style={{
                          width: `${(h2hWinsA / (h2hWinsA + h2hWinsB)) * 100}%`,
                          backgroundColor: normAColor
                        }}
                      ></div>
                      <div
                        className="gradient-segment team-b"
                        style={{
                          width: `${(h2hWinsB / (h2hWinsA + h2hWinsB)) * 100}%`,
                          backgroundColor: normBColor
                        }}
                      ></div>
                    </div>
                  )}
                </div>
              </button>
            )}
            <button
              className={`main-tab ${
                activeTab === "statistics" ? "active" : ""
              }`}
              onClick={() => setActiveTab("statistics")}
              style={getMainTabStyling(
                "statistics",
                activeTab === "statistics"
              )}
            >
              <div className="main-tab-content">
                <div className="tab-text">
                  Statistics ({totalFavoredA}:{totalFavoredB})
                </div>
                {(totalFavoredA > 0 || totalFavoredB > 0) && (
                  <div className="tab-gradient-bar">
                    <div
                      className="gradient-segment team-a"
                      style={{
                        width: `${
                          (totalFavoredA / (totalFavoredA + totalFavoredB)) *
                          100
                        }%`,
                        backgroundColor: normAColor,
                      }}
                    ></div>
                    <div
                      className="gradient-segment team-b"
                      style={{
                        width: `${
                          (totalFavoredB / (totalFavoredA + totalFavoredB)) *
                          100
                        }%`,
                        backgroundColor: normBColor,
                      }}
                    ></div>
                  </div>
                )}
              </div>
            </button>
            <button
              className={`main-tab ${activeTab === "metrics" ? "active" : ""}`}
              onClick={() => setActiveTab("metrics")}
              style={getMainTabStyling("metrics", activeTab === "metrics")}
            >
              <div className="main-tab-content">
                <div className="tab-text">Metrics ({metricsFavoredA}:{metricsFavoredB})</div>
                {(metricsFavoredA > 0 || metricsFavoredB > 0) && (
                  <div className="tab-gradient-bar">
                    <div
                      className="gradient-segment team-a"
                      style={{
                        width: `${(metricsFavoredA / (metricsFavoredA + metricsFavoredB)) * 100}%`,
                        backgroundColor: normAColor
                      }}
                    ></div>
                    <div
                      className="gradient-segment team-b"
                      style={{
                        width: `${(metricsFavoredB / (metricsFavoredA + metricsFavoredB)) * 100}%`,
                        backgroundColor: normBColor
                      }}
                    ></div>
                  </div>
                )}
              </div>
            </button>
          </div>

          {/* Tab content */}
          <div className="tab-content">
            {activeTab === "statistics" && renderStatisticsTab()}
            {activeTab === "metrics" && renderMetricsTab()}
            {activeTab === "history" && renderHistoryTab()}
          </div>
        </div>

      </div>
    </div>
  );
}
