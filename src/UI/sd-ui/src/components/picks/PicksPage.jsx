import "./PicksPage.css";

import { useState, useEffect, useMemo, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useUserDto } from "../../contexts/UserContext";
import { useLeagueContext } from "../../contexts/LeagueContext";
import { useContestUpdates } from "../../contexts/ContestUpdatesContext";
import InsightDialog from "../insights/InsightDialog.jsx";
import toast from "react-hot-toast";
import apiWrapper from "../../api/apiWrapper.js";
import LeagueWeekSelector from "./LeagueWeekSelector.jsx";
import MatchupList from "../matchups/MatchupList.jsx";
import MatchupGrid from "../matchups/MatchupGrid.jsx";

function PicksPage() {
  const { userDto, loading: userLoading } = useUserDto();
  const {
    selectedLeagueId: globalLeagueId,
    setSelectedLeagueId: setGlobalLeagueId,
    initializeLeagueSelection,
  } = useLeagueContext();
  const { leagueId: routeLeagueId } = useParams(); // optional route param
  const navigate = useNavigate();

  const [userPicks, setUserPicks] = useState({});
  const [isSubscribed] = useState(false);
  const [selectedMatchup, setSelectedMatchup] = useState(null);
  const [isInsightDialogOpen, setIsInsightDialogOpen] = useState(false);
  const [loadingInsight, setLoadingInsight] = useState(false);

  const [matchups, setMatchups] = useState([]);
  const [pickType, setPickType] = useState(null);
  const [useConfidencePoints, setUseConfidencePoints] = useState(false);
  const [leagueSport, setLeagueSport] = useState(null);
  const [leagueSeasonYear, setLeagueSeasonYear] = useState(null);
  // SeasonWeek.EndDate of the displayed week (ISO 8601). Threaded down to
  // MiniSchedule's fetch as an inclusive FinalizedUtc upper bound — see
  // docs/team-schedule-endpoint.md for why numeric week filtering doesn't work
  // (MLB same-week games + football postseason Week-N reuse).
  const [leagueAsOfDate, setLeagueAsOfDate] = useState(null);
  const [loadingMatchups, setLoadingMatchups] = useState(true);

  // Source of truth for the selected league is the route param
  // (/app/picks/:leagueId?). The dropdown navigates instead of holding
  // local state — this kills the prior fight between `selectedLeagueId`
  // state, the URL sync effect, and the week-snap effect that was
  // emitting 5 matchups XHRs per league switch.
  const [selectedWeek, setSelectedWeek] = useState(null);
  const viewMode = "card";

  const [hidePicked, setHidePicked] = useState(false);
  const [fadingOut, setFadingOut] = useState([]);
  const [now, setNow] = useState(new Date());

  // Update 'now' every 15 seconds to keep lock status in sync with MatchupCard
  useEffect(() => {
    const interval = setInterval(() => setNow(new Date()), 15000);
    return () => clearInterval(interval);
  }, []);

  // Memoized so the init effect's dep check doesn't fire on every render
  // (a fresh `Object.values` ref every render previously triggered re-init
  // and stomped the user's dropdown selection).
  const leagues = useMemo(
    () => Object.values(userDto?.leagues || {}),
    [userDto]
  );

  // Derive the selected league from the route param. With `leagues`
  // memoized, `selectedLeague` is a stable reference across renders
  // (same routeLeagueId → same league object) so downstream effect deps
  // don't churn.
  const selectedLeague = useMemo(
    () => leagues.find((l) => l.id === routeLeagueId) ?? null,
    [leagues, routeLeagueId]
  );
  // Memoized so the `?? []` fallback doesn't hand a fresh array to the
  // fetch effects' dep arrays on every render.
  const seasonWeeks = useMemo(
    () => selectedLeague?.seasonWeeks ?? [],
    [selectedLeague]
  );
  // Default to the latest week the league has (last element of the
  // ascending list). Custom-window leagues may only have a single entry.
  const latestSeasonWeek =
    seasonWeeks.length > 0 ? seasonWeeks[seasonWeeks.length - 1] : null;

  // Get contest updates for live game data
  const { getContestUpdate, _instanceId: contestCtxInstanceId } = useContestUpdates();

  const usedConfidencePoints = useMemo(() => {
    return Object.values(userPicks)
      .map(p => p.confidencePoints)
      .filter(p => p !== null && p !== undefined);
  }, [userPicks]);

  // Merge live updates with matchups. Live state arrives via
  // ContestUpdatesContext from the merged *PlayCompleted SignalR
  // events (FootballPlayCompleted / BaseballPlayCompleted). Football
  // and baseball write their own field sets onto the same context
  // record; the union is included here so MatchupCard / GameStatus
  // can pick out whichever fields its sport branch needs.
  const enrichedMatchups = useMemo(() => {
    // DIAG (refresh-loses-updates investigation): how many matchups
    // have a live update merged in, AND which ContestCtx instance
    // we're reading from. If setContests fires on #1 but PicksPage
    // reads from #2, withLive stays 0 — the cross-instance bug.
    const withLive = matchups.filter(m => getContestUpdate(m.contestId)).length;
    console.log(`[PicksPage] enrichedMatchups recompute (reading ContestCtx#${contestCtxInstanceId})`, { total: matchups.length, withLive });
    return matchups.map(matchup => {
      const liveUpdate = getContestUpdate(matchup.contestId);
      if (liveUpdate) {
        return {
          ...matchup,
          // Nullish-fallback so a partial context state (e.g. a
          // ContestStatusChanged that landed before any *PlayCompleted —
          // ContestUpdatesContext's status handler writes only `status` /
          // `lastUpdated`) can't undefine real canonical fields like
          // awayScore / homeScore.
          status: liveUpdate.status ?? matchup.status,
          statusDescription: liveUpdate.statusDescription ?? matchup.statusDescription,
          awayScore: liveUpdate.awayScore ?? matchup.awayScore,
          homeScore: liveUpdate.homeScore ?? matchup.homeScore,
          // Football-shaped
          period: liveUpdate.period ?? matchup.period,
          clock: liveUpdate.clock ?? matchup.clock,
          possessionFranchiseSeasonId: liveUpdate.possessionFranchiseSeasonId ?? matchup.possessionFranchiseSeasonId,
          isScoringPlay: liveUpdate.isScoringPlay ?? matchup.isScoringPlay,
          // Baseball-shaped — nullish-fallback so a future partial-update
          // handler that doesn't carry the full set can't silently undefine
          // a previously-populated field (mirrors the score/status pattern).
          inning: liveUpdate.inning ?? matchup.inning,
          halfInning: liveUpdate.halfInning ?? matchup.halfInning,
          balls: liveUpdate.balls ?? matchup.balls,
          strikes: liveUpdate.strikes ?? matchup.strikes,
          outs: liveUpdate.outs ?? matchup.outs,
          runnerOnFirst: liveUpdate.runnerOnFirst ?? matchup.runnerOnFirst,
          runnerOnSecond: liveUpdate.runnerOnSecond ?? matchup.runnerOnSecond,
          runnerOnThird: liveUpdate.runnerOnThird ?? matchup.runnerOnThird,
          // At-bat header display fields (baseball live)
          atBatShortName: liveUpdate.atBatShortName ?? matchup.atBatShortName,
          atBatPositionAbbreviation: liveUpdate.atBatPositionAbbreviation ?? matchup.atBatPositionAbbreviation,
          atBatHeadshotUrl: liveUpdate.atBatHeadshotUrl ?? matchup.atBatHeadshotUrl,
          pitchingShortName: liveUpdate.pitchingShortName ?? matchup.pitchingShortName,
          pitchingPositionAbbreviation: liveUpdate.pitchingPositionAbbreviation ?? matchup.pitchingPositionAbbreviation,
          pitchingHeadshotUrl: liveUpdate.pitchingHeadshotUrl ?? matchup.pitchingHeadshotUrl,
          // Sport-neutral last-play (written by both *PlayCompleted handlers)
          lastPlayDescription: liveUpdate.lastPlayDescription ?? matchup.lastPlayDescription
        };
      }
      return matchup;
    });
    // contestCtxInstanceId is intentionally NOT in deps — it's stable
    // per Provider mount, and re-running the memo just to update the
    // log tag is pointless. Tracked in console output.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [matchups, getContestUpdate]);

  // One-shot init: ensure the URL carries a valid league. When the user
  // lands on /app/picks with no param (or a stale id) redirect to the
  // remembered league (LeagueContext, localStorage-backed) or the first
  // available. Once the URL has a valid league id, subsequent league
  // switches happen through `handleLeagueChange` (selector → navigate).
  useEffect(() => {
    if (userLoading || leagues.length === 0) return;

    initializeLeagueSelection(leagues);

    const isRouteValid =
      routeLeagueId && leagues.some((l) => l.id === routeLeagueId);
    if (isRouteValid) {
      // Keep cross-page memory current with the URL-driven selection so
      // Leaderboard/Messageboard remember the user's league after a tab
      // switch.
      setGlobalLeagueId(routeLeagueId);
      return;
    }

    const fallback =
      globalLeagueId && leagues.some((l) => l.id === globalLeagueId)
        ? globalLeagueId
        : leagues[0].id;
    setGlobalLeagueId(fallback);
    navigate(`/app/picks/${fallback}`, { replace: true });
  }, [
    userLoading,
    leagues,
    routeLeagueId,
    globalLeagueId,
    setGlobalLeagueId,
    initializeLeagueSelection,
    navigate,
  ]);

  // Clear league-scoped state immediately on league change so the UI
  // doesn't render the previous league's matchups during the brief
  // window between navigate() and the new matchups fetch resolving.
  // Also prevents handlePick from racing a click against stale
  // (newLeagueId, oldContestId) ids — MatchupList reads from `matchups`,
  // which would otherwise hold the prior league's contests for one or
  // two renders (and for inbound-link switches the route changes
  // without remounting PicksPage, so the state survives the transition).
  useEffect(() => {
    setMatchups([]);
    setUserPicks({});
    setLoadingMatchups(true);
  }, [routeLeagueId]);

  useEffect(() => {
    // `cancelled` flips in the cleanup so a late response from the
    // previous (routeLeagueId, selectedWeek) pair can't stomp newer
    // state. Without this, a fast A→B switch where A's XHR resolves
    // after B's clearing effect (or after B's own fetch) would briefly
    // render A's matchups under B's identity.
    let cancelled = false;
    async function fetchMatchups() {
      if (!routeLeagueId || selectedWeek === null) return;
      // Skip the request when the current week isn't in the selected
      // league's week list — happens for one render right after a
      // league switch, before the week-snap effect below updates
      // selectedWeek to the new league's latest. Without this gate we
      // emit a (newLeagueId, oldWeek) XHR that the user never sees.
      if (!seasonWeeks.includes(selectedWeek)) return;
      setLoadingMatchups(true);
      try {
        const response = await apiWrapper.Matchups.getByLeagueAndWeek(
          routeLeagueId,
          selectedWeek
        );
        if (cancelled) return;
        setMatchups(response.data.matchups || []);
        setPickType(response.data.pickType);
        setUseConfidencePoints(response.data.useConfidencePoints);
        setLeagueSport(response.data.sport);
        setLeagueSeasonYear(response.data.seasonYear);
        setLeagueAsOfDate(response.data.asOfDate ?? null);
      } catch (error) {
        if (cancelled) return;
        console.error("Failed to fetch matchups:", error);
      } finally {
        if (!cancelled) setLoadingMatchups(false);
      }
    }

    fetchMatchups();
    return () => {
      cancelled = true;
    };
  }, [routeLeagueId, selectedWeek, seasonWeeks]);

  useEffect(() => {
    // Same race guard as fetchMatchups — see comment above.
    let cancelled = false;
    async function fetchPicks() {
      if (!routeLeagueId || selectedWeek === null) return;
      if (!seasonWeeks.includes(selectedWeek)) return;

      try {
        const response = await apiWrapper.Picks.getUserPicksByWeek(
          routeLeagueId,
          selectedWeek
        );
        if (cancelled) return;

        const picksByContest = {};
        for (const pick of response.data) {
          picksByContest[pick.contestId] = pick; // Store full pick object
        }

        setUserPicks(picksByContest);
      } catch (error) {
        if (cancelled) return;
        console.error("Failed to fetch user picks:", error);
      }
    }

    fetchPicks();
    return () => {
      cancelled = true;
    };
  }, [routeLeagueId, selectedWeek, seasonWeeks]);

  async function handlePick(matchup, selectedFranchiseSeasonId, confidencePoints) {
    try {
      const pickPayload = {
        pickemGroupId: routeLeagueId,
        contestId: matchup.contestId,
        pickType: pickType || "StraightUp",
        franchiseSeasonId: selectedFranchiseSeasonId,
        week: selectedWeek,
      };

      if (confidencePoints !== undefined) {
        pickPayload.confidencePoints = confidencePoints;
      }

      await apiWrapper.Picks.submitPick(pickPayload);

      const updatedPick = {
        ...userPicks[matchup.contestId],
        contestId: matchup.contestId,
        franchiseId: selectedFranchiseSeasonId,
        confidencePoints: confidencePoints !== undefined ? confidencePoints : userPicks[matchup.contestId]?.confidencePoints
      };

      if (hidePicked) {
        setFadingOut((prev) => [...prev, matchup.contestId]);

        setTimeout(() => {
          setUserPicks((prev) => ({
            ...prev,
            [matchup.contestId]: updatedPick,
          }));
          setFadingOut((prev) => prev.filter((id) => id !== matchup.contestId));
        }, 500);
      } else {
        setUserPicks((prev) => ({
          ...prev,
          [matchup.contestId]: updatedPick,
        }));
      }

      toast.success("Pick saved!");
    } catch (error) {
      console.error("Error submitting pick:", error);

      if (
        error.response?.status === 500 &&
        error.response?.data?.includes?.(
          "duplicate key value violates unique constraint"
        )
      ) {
        toast.error("You already picked this game. Refresh to view.");
      } else {
        toast.error("Failed to save pick. Please try again.");
      }
    }
  }

  async function handleViewInsight(matchup) {
    // If no preview available and user is admin, trigger preview generation
    if (!matchup.isPreviewAvailable && userDto?.isAdmin) {
      try {
        await apiWrapper.Admin.resetPreview(matchup.contestId);
        toast.success("Preview generation initiated. Please refresh in a moment.");
      } catch (error) {
        console.error("Error resetting preview:", error);
        toast.error("Failed to initiate preview generation.");
      }
      return;
    }

    setSelectedMatchup({
      ...matchup,
      insightText: "",
      bullets: [],
      prediction: "",
    });

    setIsInsightDialogOpen(true);
    setLoadingInsight(true);

    try {
      const response = await apiWrapper.Matchups.getPreviewByContestId(
        matchup.contestId
      );
      const preview = response.data;

      setSelectedMatchup((prev) => ({
        ...prev,
        id: preview.id, // Ensure preview id is available for rejection
        insightText: preview.overview,
        analysis: preview.analysis,
        prediction: preview.prediction,
        straightUpWinner: preview.straightUpWinner,
        atsWinner: preview.atsWinner,
        awayScore: preview.awayScore,
        homeScore: preview.homeScore,
        vegasImpliedScore: preview.vegasImpliedScore,
        generatedUtc: preview.generatedUtc
      }));
    } catch (error) {
      console.error("Error fetching insight preview:", error);
      toast.error("Failed to load insight preview.");
    } finally {
      setLoadingInsight(false);
    }
  }

  async function handleRejectPreview({ PreviewId, ContestId, RejectionNote }) {
    try {
      console.log("Reject Preview Payload:", {
        PreviewId,
        ContestId,
        RejectionNote,
      });
      await apiWrapper.Previews.rejectPreviewByContestId(PreviewId, {
        PreviewId,
        ContestId,
        RejectionNote,
      });
      toast.success("Preview rejection sent.");
    } catch (error) {
      console.error("Error rejecting preview:", error);
      toast.error("Failed to reject preview.");
    }
  }

  async function handleApprovePreview(contestId, previewId) {
    try {
      await apiWrapper.Previews.approvePreviewByContestId(previewId);
      toast.success("Preview approved.");
    } catch (error) {
      console.error("Error approving preview:", error);
      toast.error("Failed to approve preview.");
    }
  }

  // Switch league → ensure the URL changes (route is the source of
  // truth) and persist the choice for cross-page memory. The week-snap
  // effect below adjusts selectedWeek on the next render; the matchups
  // fetch is gated on the new league's seasonWeeks so the brief
  // (newLeagueId, oldWeek) window doesn't emit an XHR.
  const handleLeagueChange = useCallback(
    (newLeagueId) => {
      if (!newLeagueId || newLeagueId === routeLeagueId) return;
      setGlobalLeagueId(newLeagueId);
      navigate(`/app/picks/${newLeagueId}`, { replace: true });
    },
    [routeLeagueId, navigate, setGlobalLeagueId]
  );

  // Snap selectedWeek when the URL-driven league changes, or when the
  // current week falls outside the new league's week list. Respects a
  // user's manual mid-week selection (deps don't change on week
  // selection alone, and we only re-snap when the current week is no
  // longer valid for this league).
  useEffect(() => {
    if (!routeLeagueId) return;
    if (!latestSeasonWeek) {
      if (selectedWeek !== null) setSelectedWeek(null);
      return;
    }
    if (selectedWeek === null || !seasonWeeks.includes(selectedWeek)) {
      setSelectedWeek(latestSeasonWeek);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [routeLeagueId, latestSeasonWeek, selectedLeague]);

  if (userLoading) return <div>Loading user info...</div>;

  if (!leagues.length) {
    return <p>You are not part of any leagues yet.</p>;
  }

  const totalGames = enrichedMatchups.length;
  const picksMade = Object.keys(userPicks).filter(
    (id) => userPicks[id] !== null && userPicks[id] !== undefined
  ).length;
  const allPicked = totalGames > 0 && picksMade === totalGames;

  const visibleMatchups = hidePicked
    ? enrichedMatchups.filter((m) => {
        const isPicked = !!userPicks[m.contestId];
        
        // Replicate locking logic from usePickLocking (5 min buffer)
        // We do NOT hide based on isReadOnly, only based on time
        const startTime = new Date(m.startDateUtc);
        const lockTime = new Date(startTime.getTime() - 5 * 60 * 1000);
        const isLocked = now > lockTime;

        return (!isPicked && !isLocked) || fadingOut.includes(m.contestId);
      })
    : enrichedMatchups;

  return (
    <div className="picks-page-container">
      <div className="picks-content-wrapper">
        <div className="picks-page-header">
          <LeagueWeekSelector
            leagues={leagues}
            selectedLeagueId={routeLeagueId}
            setSelectedLeagueId={handleLeagueChange}
            selectedWeek={selectedWeek}
            setSelectedWeek={setSelectedWeek}
            seasonWeeks={seasonWeeks}
          />
          <div className="pick-status-toggle-row">
            {(() => {
              // Pick-mode badge. PickType enum on the wire serializes as a
              // string ("StraightUp" / "AgainstTheSpread" / "OverUnder").
              // Anything else (None / unknown) suppresses the badge so a
              // misconfigured league doesn't render a stray "?".
              const label = {
                StraightUp: { short: "SU", full: "Straight Up" },
                AgainstTheSpread: { short: "ATS", full: "Against The Spread" },
                OverUnder: { short: "O/U", full: "Over / Under" },
              }[pickType];
              if (!label) return null;
              return (
                <span className="pick-mode-badge" title={label.full}>
                  {label.short}
                </span>
              );
            })()}
            <span className="pick-status">
              {allPicked
                ? "All Picks Made"
                : `${picksMade} / ${totalGames} Picks Made`}
            </span>
            {!allPicked && (
              <label className="hide-picked-toggle">
                <input
                  type="checkbox"
                  checked={hidePicked}
                  onChange={() => setHidePicked(!hidePicked)}
                />
                Hide Picked Games
              </label>
            )}
          </div>
        </div>

        {loadingMatchups ? (
          <div>Loading matchups...</div>
        ) : (
          <>
            {viewMode === "card" ? (
              <MatchupList
                matchups={visibleMatchups}
                pickType={pickType}
                userPicks={userPicks}
                onPick={handlePick}
                onViewInsight={handleViewInsight}
                isSubscribed={isSubscribed}
                fadingOut={fadingOut}
                useConfidencePoints={useConfidencePoints}
                usedConfidencePoints={usedConfidencePoints}
                totalGames={enrichedMatchups.length}
                leagueSport={leagueSport}
                leagueAsOfDate={leagueAsOfDate}
                leagueSeasonYear={leagueSeasonYear}
              />
            ) : (
              <MatchupGrid
                matchups={visibleMatchups}
                userPicks={userPicks}
                onPick={handlePick}
                onViewInsight={handleViewInsight}
                isSubscribed={isSubscribed}
                fadingOut={fadingOut}
              />
            )}
          </>
        )}

        {isInsightDialogOpen && (
          <InsightDialog
            isOpen={isInsightDialogOpen}
            matchup={selectedMatchup}
            onClose={() => setIsInsightDialogOpen(false)}
            overview={selectedMatchup?.insightText ?? ""}
            analysis={selectedMatchup?.analysis ?? ""}
            prediction={selectedMatchup?.prediction ?? ""}
            loading={loadingInsight}
            onRejectPreview={handleRejectPreview}
            onApprovePreview={handleApprovePreview}
          />
        )}
      </div>
    </div>
  );
}

export default PicksPage;
