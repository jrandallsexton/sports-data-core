import "./PicksPage.css";

import { useState, useEffect, useMemo } from "react";
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
  const [loadingMatchups, setLoadingMatchups] = useState(true);

  const [selectedLeagueId, setSelectedLeagueId] = useState(null);
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

  const leagues = Object.values(userDto?.leagues || {});

  // Get contest updates for live game data
  const { getContestUpdate } = useContestUpdates();

  const usedConfidencePoints = useMemo(() => {
    return Object.values(userPicks)
      .map(p => p.confidencePoints)
      .filter(p => p !== null && p !== undefined);
  }, [userPicks]);

  // Merge live updates with matchups
  const enrichedMatchups = useMemo(() => {
    return matchups.map(matchup => {
      const liveUpdate = getContestUpdate(matchup.contestId);
      if (liveUpdate) {
        // Merge live data, overriding static matchup data
        return {
          ...matchup,
          status: liveUpdate.status,
          awayScore: liveUpdate.awayScore,
          homeScore: liveUpdate.homeScore,
          period: liveUpdate.period,
          clock: liveUpdate.clock,
          possessionFranchiseSeasonId: liveUpdate.possessionFranchiseSeasonId,
          isScoringPlay: liveUpdate.isScoringPlay
        };
      }
      return matchup;
    });
  }, [matchups, getContestUpdate]);

  // Select default league on load or when leagues change
  useEffect(() => {
    if (!userLoading && leagues.length > 0) {
      // Initialize global context
      initializeLeagueSelection(leagues);

      const isRouteValid =
        routeLeagueId && leagues.some((l) => l.id === routeLeagueId);
      if (isRouteValid) {
        setSelectedLeagueId(routeLeagueId);
        setGlobalLeagueId(routeLeagueId); // Sync with global context
      } else if (!selectedLeagueId) {
        // Use global context if available, otherwise default to first league
        const leagueToUse =
          globalLeagueId && leagues.some((l) => l.id === globalLeagueId)
            ? globalLeagueId
            : leagues[0].id;
        setSelectedLeagueId(leagueToUse);
        setGlobalLeagueId(leagueToUse);
      }
    }
  }, [
    userLoading,
    leagues,
    routeLeagueId,
    selectedLeagueId,
    globalLeagueId,
    setGlobalLeagueId,
    initializeLeagueSelection,
  ]);

  // Keep URL in sync with selectedLeagueId and update global context
  useEffect(
    () => {
      if (selectedLeagueId && selectedLeagueId !== routeLeagueId) {
        navigate(`/app/picks/${selectedLeagueId}`, { replace: true });
        setGlobalLeagueId(selectedLeagueId); // Update global context
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [selectedLeagueId] // Intentionally omitting routeLeagueId and navigate
  );

  useEffect(() => {
    async function fetchMatchups() {
      if (!selectedLeagueId || selectedWeek === null) return;
      setLoadingMatchups(true);
      try {
        const response = await apiWrapper.Matchups.getByLeagueAndWeek(
          selectedLeagueId,
          selectedWeek
        );
        setMatchups(response.data.matchups || []);
        setPickType(response.data.pickType);
        setUseConfidencePoints(response.data.useConfidencePoints);
      } catch (error) {
        console.error("Failed to fetch matchups:", error);
      } finally {
        setLoadingMatchups(false);
      }
    }

    fetchMatchups();
  }, [selectedLeagueId, selectedWeek]);

  useEffect(() => {
    async function fetchPicks() {
      if (!selectedLeagueId || selectedWeek === null) return;

      try {
        const response = await apiWrapper.Picks.getUserPicksByWeek(
          selectedLeagueId,
          selectedWeek
        );

        const picksByContest = {};
        for (const pick of response.data) {
          picksByContest[pick.contestId] = pick; // Store full pick object
        }

        setUserPicks(picksByContest);
      } catch (error) {
        console.error("Failed to fetch user picks:", error);
      }
    }

    fetchPicks();
  }, [selectedLeagueId, selectedWeek]);

  async function handlePick(matchup, selectedFranchiseSeasonId, confidencePoints) {
    try {
      const pickPayload = {
        pickemGroupId: selectedLeagueId,
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

  // Find the selected league's maxSeasonWeek
  const selectedLeague = leagues.find((l) => l.id === selectedLeagueId) ?? null;
  const maxSeasonWeek = selectedLeague?.maxSeasonWeek ?? null;

  // When selectedLeagueId or maxSeasonWeek changes, default selectedWeek to maxSeasonWeek
  useEffect(() => {
    if (selectedLeagueId && maxSeasonWeek && selectedWeek !== maxSeasonWeek) {
      setSelectedWeek(maxSeasonWeek);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedLeagueId, maxSeasonWeek, selectedLeague]);

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
            selectedLeagueId={selectedLeagueId}
            setSelectedLeagueId={setSelectedLeagueId}
            selectedWeek={selectedWeek}
            setSelectedWeek={setSelectedWeek}
            maxSeasonWeek={maxSeasonWeek}
          />
          <div className="pick-status-toggle-row">
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
