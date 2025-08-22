// src/components/picks/PicksPage.jsx
import "./PicksPage.css";

import { useState, useEffect } from "react";
import { useUserDto } from "../../contexts/UserContext";
import InsightDialog from "../insights/InsightDialog.jsx";
import toast from "react-hot-toast";
import apiWrapper from "../../api/apiWrapper.js";
import LeagueWeekSelector from "./LeagueWeekSelector.jsx";
import MatchupList from "../matchups/MatchupList.jsx";
import MatchupGrid from "../matchups/MatchupGrid.jsx";

function PicksPage() {
  const { userDto, loading: userLoading } = useUserDto();

  const [userPicks, setUserPicks] = useState({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSubscribed] = useState(false);
  const [selectedMatchup, setSelectedMatchup] = useState(null);
  const [isInsightDialogOpen, setIsInsightDialogOpen] = useState(false);
  const [loadingInsight, setLoadingInsight] = useState(false);

  const [matchups, setMatchups] = useState([]);
  const [loadingMatchups, setLoadingMatchups] = useState(true);

  const [selectedLeagueId, setSelectedLeagueId] = useState(null);
  const [selectedWeek, setSelectedWeek] = useState(1); // TODO: Dynamically set current week as default
  const [viewMode, setViewMode] = useState("card");

  const [hidePicked, setHidePicked] = useState(false);
  const [fadingOut, setFadingOut] = useState([]);

  const leagues = Object.values(userDto?.leagues || {});

  // Auto-select first available league if not already selected
  useEffect(() => {
    if (!selectedLeagueId && leagues.length > 0) {
      setSelectedLeagueId(leagues[0].id);
    }
  }, [leagues, selectedLeagueId]);

  useEffect(() => {
    async function fetchMatchups() {
      if (!selectedLeagueId) return;
      setLoadingMatchups(true);
      try {
        const response = await apiWrapper.Matchups.getByLeagueAndWeek(
          selectedLeagueId,
          selectedWeek
        );

        const newMatchups = response.data.matchups || [];

        // Only update after full fetch
        setMatchups(newMatchups);
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
      if (!selectedLeagueId) return;

      try {
        const response = await apiWrapper.Picks.getUserPicksByWeek(
          selectedLeagueId,
          selectedWeek
        );

        const picksByContest = {};
        for (const pick of response.data) {
          picksByContest[pick.contestId] = pick.franchiseId;
        }

        // Update only once all picks are ready
        setUserPicks(picksByContest);
      } catch (error) {
        console.error("Failed to fetch user picks:", error);
      }
    }

    fetchPicks();
  }, [selectedLeagueId, selectedWeek]);

  async function handlePick(matchup, selectedFranchiseSeasonId) {
    try {
      await apiWrapper.Picks.submitPick({
        pickemGroupId: selectedLeagueId,
        contestId: matchup.contestId,
        pickType: "StraightUp", // hardcoded for MVP
        franchiseSeasonId: selectedFranchiseSeasonId,
        week: selectedWeek,
      });

      setUserPicks((prev) => ({
        ...prev,
        [matchup.contestId]: selectedFranchiseSeasonId,
      }));

      toast.success("Pick saved!");
    } catch (error) {
      console.error("Error submitting pick:", error);
      toast.error("Failed to save pick.");
    }
  }

  function handleSubmit() {
    if (isSubmitting) return;
    setIsSubmitting(true);

    setTimeout(() => {
      console.log("User Picks:", userPicks);
      toast.success("Your picks have been submitted!");
      setIsSubmitting(false);
      setSubmitted(true);
    }, 1500);
  }

  async function handleViewInsight(matchup) {
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
        insightText: preview.overview,
        analysis: preview.analysis,
        prediction: preview.prediction,
      }));
    } catch (error) {
      console.error("Error fetching insight preview:", error);
      toast.error("Failed to load insight preview.");
    } finally {
      setLoadingInsight(false);
    }
  }

  function toggleViewMode() {
    setViewMode((prev) => (prev === "card" ? "grid" : "card"));
  }

  if (userLoading) return <div>Loading user info...</div>;

  if (!leagues.length) {
    return <p>You are not part of any leagues yet.</p>;
  }

  const totalGames = matchups.length;

  const picksMade = Object.keys(userPicks).filter(
    (contestId) =>
      userPicks[contestId] !== null && userPicks[contestId] !== undefined
  ).length;

  const allPicked = totalGames > 0 && picksMade === totalGames;

  const visibleMatchups = hidePicked
    ? matchups.filter((m) => !userPicks[m.contestId])
    : matchups;

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
          />
          <div className="pick-status-toggle-row">
            <span className="pick-status">
              {allPicked
                ? "All Picks Made"
                : `${picksMade} / ${totalGames} Picks Made`}
            </span>
            <label className="hide-picked-toggle">
              <input
                type="checkbox"
                checked={hidePicked}
                onChange={() => setHidePicked(!hidePicked)}
              />
              Hide Picked Games
            </label>
          </div>

          {/* <button onClick={toggleViewMode} className="view-mode-toggle">
            {viewMode === "card" ? "Grid View" : "Card View"}
          </button> */}
        </div>

        {loadingMatchups ? (
          <div>Loading matchups...</div>
        ) : (
          <>
            {viewMode === "card" ? (
              <MatchupList
                matchups={visibleMatchups}
                userPicks={userPicks}
                onPick={handlePick}
                onViewInsight={handleViewInsight}
                isSubscribed={isSubscribed}
              />
            ) : (
              <MatchupGrid
                matchups={visibleMatchups}
                userPicks={userPicks}
                onPick={handlePick}
                onViewInsight={handleViewInsight}
                isSubscribed={isSubscribed}
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
          />
        )}
      </div>
    </div>
  );
}

export default PicksPage;
