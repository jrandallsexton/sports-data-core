import { useEffect, useMemo, useState } from "react";
import { useUserDto } from "../../contexts/UserContext";
import { useLeagueContext } from "../../contexts/LeagueContext";
import LeagueSelector from "../shared/LeagueSelector";
import apiWrapper from "../../api/apiWrapper";
import LeaguesApi from '../../api/leagues/leaguesApi';
import LeaderboardStandingsTable from "./LeaderboardStandingsTable";
import LeagueWeekOverviewTable from "./LeagueWeekOverviewTable";
import WeeklyScoresTable from "./WeeklyScoresTable";
import "./LeaderboardPage.css";

function LeaderboardPage() {
  const { userDto, loading: userLoading } = useUserDto();
  const { selectedLeagueId, setSelectedLeagueId } = useLeagueContext();

  // Source the league list from getUserLeagues (includeDeactivated) rather than
  // userDto.leagues: the latter is active-only, so past-season / recently-ended
  // leagues never appear. This call carries seasonYear + seasonWeeks per league,
  // so the season filter and week selector work for every season.
  const [allLeagues, setAllLeagues] = useState([]);
  const [selectedSeason, setSelectedSeason] = useState(null);
  // Active-only by default to keep the League selector short (important on
  // mobile). The pill reveals ended/deactivated leagues on demand.
  const [showEnded, setShowEnded] = useState(false);

  const [leaderboard, setLeaderboard] = useState([]);
  const [sortBy, setSortBy] = useState("totalPoints");
  const [sortOrder, setSortOrder] = useState("desc");
  const [loading, setLoading] = useState(false);
  const [overview, setOverview] = useState(null);
  const [selectedWeek, setSelectedWeek] = useState(null); // Start with null instead of 1
  const [weeklyScores, setWeeklyScores] = useState(null);
  const [activeTab, setActiveTab] = useState("standings");
  const [showBots, setShowBots] = useState(true);

  const currentUserId = userDto?.id ?? null;

  // Fetch every league the user belongs to, including deactivated ones.
  useEffect(() => {
    let cancelled = false;
    LeaguesApi.getUserLeagues({ includeDeactivated: true })
      .then((list) => {
        if (!cancelled) setAllLeagues(Array.isArray(list) ? list : []);
      })
      .catch((err) => {
        console.error("Failed to load leagues", err);
        if (!cancelled) setAllLeagues([]);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  // Season options, newest-first, from ALL the user's leagues (server-
  // authoritative seasonYear). This is a primary control shown whenever there's
  // cross-season history — independent of the "Show ended" pill.
  const seasons = useMemo(
    () => [...new Set(allLeagues.map((l) => l.seasonYear))].sort((a, b) => b - a),
    [allLeagues]
  );

  // Every league in the selected season (active + ended).
  // Sorted by name (id tie-breaker) so the League dropdown order and the
  // reconciliation effect's seasonLeagues[0] snap target are deterministic —
  // getUserLeagues returns no guaranteed order.
  const seasonAllLeagues = useMemo(
    () =>
      selectedSeason == null
        ? []
        : allLeagues
            .filter((l) => l.seasonYear === selectedSeason)
            .sort((a, b) => a.name.localeCompare(b.name) || a.id.localeCompare(b.id)),
    [allLeagues, selectedSeason]
  );

  // The "Show ended" pill applies only to the current (newest) season: a prior
  // season is browsed as history, so its toggle is never shown regardless of
  // whether some league there is still marked active. The extra seasonHasActive
  // check also hides the pill (and shows everything) when the current season has
  // no active leagues, avoiding an empty selector.
  const isCurrentSeason = selectedSeason != null && selectedSeason === seasons[0];
  const seasonHasActive = useMemo(
    () => seasonAllLeagues.some((l) => !l.deactivatedUtc),
    [seasonAllLeagues]
  );
  const canFilterEnded = isCurrentSeason && seasonHasActive;

  // League selector options: active-only in the current season unless the pill
  // is on; otherwise (past season, or pill on) show everything.
  const seasonLeagues = useMemo(
    () =>
      canFilterEnded && !showEnded
        ? seasonAllLeagues.filter((l) => !l.deactivatedUtc)
        : seasonAllLeagues,
    [seasonAllLeagues, canFilterEnded, showEnded]
  );

  // Keep selectedSeason valid: unset or no-longer-present snaps to the saved
  // league's season if it's one of theirs, otherwise the newest season.
  useEffect(() => {
    if (seasons.length === 0) return;
    if (selectedSeason != null && seasons.includes(selectedSeason)) return;
    const saved = allLeagues.find((l) => l.id === selectedLeagueId);
    setSelectedSeason(saved ? saved.seasonYear : seasons[0]);
  }, [seasons, selectedSeason, allLeagues, selectedLeagueId]);

  // Keep the selected league consistent with the visible set: if the current
  // selection isn't in it (e.g. after switching seasons or toggling the pill),
  // snap to the first league shown.
  useEffect(() => {
    if (selectedSeason == null || seasonLeagues.length === 0) return;
    const inSeason = seasonLeagues.some((l) => l.id === selectedLeagueId);
    if (!inSeason) setSelectedLeagueId(seasonLeagues[0].id);
  }, [selectedSeason, seasonLeagues, selectedLeagueId, setSelectedLeagueId]);

  useEffect(() => {
    const fetchLeaderboard = async () => {
      if (!selectedLeagueId) return;
      setLoading(true);
      try {
        const data = await apiWrapper.Leaderboard.getByGroupAndWeek(
          selectedLeagueId
        );
        // Extract the actual leaderboard array from the response
        const leaderboardArray = data.data || data || [];
        setLeaderboard(Array.isArray(leaderboardArray) ? leaderboardArray : []);
      } catch (err) {
        console.error("Failed to load leaderboard", err);
        setLeaderboard([]);
      } finally {
        setLoading(false);
      }
    };
    fetchLeaderboard();
  }, [selectedLeagueId]);

  // Find the selected league and its ascending week list. Search across all
  // leagues (not just the season subset) so seasonWeeks still resolves during
  // the brief window before a season switch reconciles the selection.
  const selectedLeague = allLeagues.find(l => l.id === selectedLeagueId);
  const seasonWeeks = selectedLeague?.seasonWeeks ?? [];
  const latestSeasonWeek = seasonWeeks.length > 0 ? seasonWeeks[seasonWeeks.length - 1] : null;

  // Reconcile selectedWeek with the currently-selected league.
  //   (a) no league selected → nothing to reconcile; leave state alone.
  //   (b) league has no weeks (latestSeasonWeek is null) → clear selectedWeek
  //       explicitly, otherwise a stale value from the previous league would
  //       carry over and getLeagueWeekOverview / the "By Week" select would
  //       receive a week number the current league doesn't have.
  //   (c) current selection is a week the league has → leave it alone
  //       (don't clobber the user's explicit choice when new weeks append).
  //   (d) current selection is missing or invalid → snap to the latest.
  useEffect(() => {
    if (!selectedLeagueId) return;
    if (!latestSeasonWeek) {
      if (selectedWeek !== null) setSelectedWeek(null);
      return;
    }
    const isCurrentValid = selectedWeek && seasonWeeks.includes(selectedWeek);
    if (!isCurrentValid) {
      setSelectedWeek(latestSeasonWeek);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedLeagueId, latestSeasonWeek]);

  // Add the API call for week overview using the selectedLeagueId from context
  // Only run when selectedWeek is properly set (not null)
  useEffect(() => {
    if (selectedLeagueId && selectedWeek !== null) {
      LeaguesApi.getLeagueWeekOverview(selectedLeagueId, selectedWeek)
        .then(response => {
          setOverview(response.data);
        })
        .catch(err => {
          // Optionally log error
        });
    }
  }, [selectedLeagueId, selectedWeek]);

  // Fetch weekly scores when league changes
  useEffect(() => {
    const fetchWeeklyScores = async () => {
      if (!selectedLeagueId) {
        setWeeklyScores(null);
        return;
      }
      try {
        const data = await LeaguesApi.getLeagueScores(selectedLeagueId);
        setWeeklyScores(data);
      } catch (err) {
        console.error("Failed to load weekly scores", err);
        setWeeklyScores(null);
      }
    };
    fetchWeeklyScores();
  }, [selectedLeagueId]);

  // Week options = the league's actual week list (custom-window leagues may skip weeks)
  const weekOptions = seasonWeeks;

  // Filter functions for synthetic users
  const filterLeaderboard = (data) => {
    if (showBots) return data;
    return data.filter(user => !user.isSynthetic);
  };

  const filterWeeklyScores = (data) => {
    if (!data || showBots) return data;
    const filteredWeeks = data.weeks.map(week => ({
      ...week,
      userScores: week.userScores.filter(user => !user.isSynthetic)
    }));
    return { ...data, weeks: filteredWeeks };
  };

  const filterOverview = (data) => {
    if (!data || showBots) return data;
    const filteredUserPicks = data.userPicks.filter(pick => !pick.isSynthetic);
    return { ...data, userPicks: filteredUserPicks };
  };

  function handleSort(column) {
    if (sortBy === column) {
      setSortOrder((prev) => (prev === "asc" ? "desc" : "asc"));
    } else {
      setSortBy(column);
      setSortOrder("desc");
    }
  }

  if (userLoading) {
    return <div className="leaderboard-container">Loading user data...</div>;
  }

  return (
    <div className="leaderboard-container">
      <div style={{ display: 'flex', alignItems: 'center', gap: '24px' }}>
        {/* Season filter — only when the user has leagues across multiple
            seasons. Scopes the League selector below it. */}
        {seasons.length > 1 && (
          <div className="league-selector">
            <label htmlFor="seasonSelect">Season:</label>
            <select
              id="seasonSelect"
              value={selectedSeason ?? ""}
              onChange={(e) => setSelectedSeason(Number(e.target.value))}
            >
              {seasons.map((year) => (
                <option key={year} value={year}>{year}</option>
              ))}
            </select>
          </div>
        )}

        <LeagueSelector
          leagues={seasonLeagues}
          selectedLeagueId={selectedLeagueId}
          setSelectedLeagueId={setSelectedLeagueId}
        />

        {/* Reveal ended/deactivated leagues. Current season only — a past
            season is browsed as history and shows all its leagues. */}
        {canFilterEnded && (
          <button
            type="button"
            className={`pill-toggle ${showEnded ? "active" : ""}`}
            aria-pressed={showEnded}
            onClick={() => setShowEnded((v) => !v)}
          >
            Show ended
          </button>
        )}

        <button
          type="button"
          className={`pill-toggle ${showBots ? "active" : ""}`}
          aria-pressed={showBots}
          onClick={() => setShowBots((v) => !v)}
        >
          Show Bots
        </button>
      </div>

      {/* Tab Navigation */}
      <div className="tab-navigation">
        <button
          className={`tab-button ${activeTab === "standings" ? "active" : ""}`}
          onClick={() => setActiveTab("standings")}
        >
          Standings
        </button>
        <button
          className={`tab-button ${activeTab === "allWeeks" ? "active" : ""}`}
          onClick={() => setActiveTab("allWeeks")}
        >
          All Weeks
        </button>
        <button
          className={`tab-button ${activeTab === "byWeek" ? "active" : ""}`}
          onClick={() => setActiveTab("byWeek")}
        >
          By Week
        </button>
      </div>

      {/* Standings Tab */}
      {activeTab === "standings" && (
        <>
          {loading ? (
            <div className="loading">Loading leaderboard...</div>
          ) : (
            <LeaderboardStandingsTable
              leaderboard={filterLeaderboard(leaderboard)}
              sortBy={sortBy}
              sortOrder={sortOrder}
              handleSort={handleSort}
              currentUserId={currentUserId}
              loading={loading}
            />
          )}
        </>
      )}

      {/* All Weeks Tab */}
      {activeTab === "allWeeks" && (
        <WeeklyScoresTable
          scoresData={filterWeeklyScores(weeklyScores)}
          currentUserId={currentUserId}
        />
      )}

      {/* By Week Tab */}
      {activeTab === "byWeek" && (
        <>
          {selectedWeek !== null && (
            <div className="league-selector" style={{ margin: '16px 0' }}>
              <label htmlFor="week-select" style={{ marginRight: 8, fontWeight: 500, color: '#ccc', fontSize: '1rem', lineHeight: '32px' }}>Week:</label>
              <select
                id="week-select"
                value={selectedWeek}
                onChange={e => setSelectedWeek(Number(e.target.value))}
                className="week-selector-select"
              >
                {weekOptions.map(week => (
                  <option key={week} value={week}>Week {week}</option>
                ))}
              </select>
            </div>
          )}

          <LeagueWeekOverviewTable
            overview={filterOverview(overview)}
          />
        </>
      )}
    </div>
  );
}

export default LeaderboardPage;
