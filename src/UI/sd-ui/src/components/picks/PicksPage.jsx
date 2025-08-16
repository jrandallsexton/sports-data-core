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
import SubmitButton from "./SubmitButton.jsx";
import mockMatchups from "../../data/matchups.js";

function PicksPage() {
  const { userDto, loading: userLoading } = useUserDto();

  const [userPicks, setUserPicks] = useState({});
  const [submitted, setSubmitted] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSubscribed] = useState(false);
  const [selectedMatchup, setSelectedMatchup] = useState(null);
  const [isInsightDialogOpen, setIsInsightDialogOpen] = useState(false);
  const [loadingInsight, setLoadingInsight] = useState(false);

  const [matchups, setMatchups] = useState([]);
  const [loadingMatchups, setLoadingMatchups] = useState(true);

  const [selectedLeagueId, setSelectedLeagueId] = useState(null);
  const [selectedWeek, setSelectedWeek] = useState(7);
  const [viewMode, setViewMode] = useState("card");

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
        setMatchups(response.data.matchups || []);
      } catch (error) {
        console.error("Failed to fetch matchups:", error);
        setMatchups([]);
      } finally {
        setLoadingMatchups(false);
      }
    }

    fetchMatchups();
  }, [selectedLeagueId, selectedWeek]);

  function handlePick(matchupId, teamPicked) {
    setUserPicks((prev) => ({
      ...prev,
      [matchupId]: teamPicked,
    }));
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

  function handleViewInsight(matchup) {
    setSelectedMatchup({
      ...matchup,
      insightText: "",
    });
    setIsInsightDialogOpen(true);
    setLoadingInsight(true);

    setTimeout(() => {
      setSelectedMatchup((prev) => ({
        ...prev,
        bullets: ["Mock bullet 1", "Mock bullet 2"],
        prediction: "Mock prediction",
      }));
      setLoadingInsight(false);
    }, 500);
  }

  function toggleViewMode() {
    setViewMode((prev) => (prev === "card" ? "grid" : "card"));
  }

  if (userLoading) return <div>Loading user info...</div>;

  if (!leagues.length) {
    return <p>You are not part of any leagues yet.</p>;
  }

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
          <button onClick={toggleViewMode} className="view-mode-toggle">
            {viewMode === "card" ? "Grid View" : "Card View"}
          </button>
        </div>

        {loadingMatchups ? (
          <div>Loading matchups...</div>
        ) : (
          <>
            {viewMode === "card" ? (
              <MatchupList
                matchups={matchups}
                userPicks={userPicks}
                onPick={handlePick}
                onViewInsight={handleViewInsight}
                isSubscribed={isSubscribed}
              />
            ) : (
              <MatchupGrid
                matchups={matchups}
                userPicks={userPicks}
                onPick={handlePick}
                onViewInsight={handleViewInsight}
                isSubscribed={isSubscribed}
              />
            )}
            <SubmitButton
              onSubmit={handleSubmit}
              isSubmitting={isSubmitting}
              submitted={submitted}
            />
          </>
        )}

        {isInsightDialogOpen && (
          <InsightDialog
            matchup={selectedMatchup}
            onClose={() => setIsInsightDialogOpen(false)}
            loading={loadingInsight}
          />
        )}
      </div>
    </div>
  );
}

export default PicksPage;
