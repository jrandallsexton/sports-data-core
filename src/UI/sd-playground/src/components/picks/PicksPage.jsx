import "./PicksPage.css";

import { useState, useEffect } from "react";
import InsightDialog from "../insights/InsightDialog.jsx";
import toast from "react-hot-toast";
import apiWrapper from "../../api/apiWrapper.js";
import GroupWeekSelector from "./GroupWeekSelector.jsx";
import MatchupList from "../matchups/MatchupList.jsx";
import MatchupGrid from "../matchups/MatchupGrid.jsx"; // ✅ add this
import SubmitButton from "./SubmitButton.jsx";

function PicksPage() {
  const [userPicks, setUserPicks] = useState({});
  const [submitted, setSubmitted] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSubscribed, setIsSubscribed] = useState(false);
  const [selectedMatchup, setSelectedMatchup] = useState(null);
  const [isInsightDialogOpen, setIsInsightDialogOpen] = useState(false);
  const [loadingInsight, setLoadingInsight] = useState(false);

  const [matchups, setMatchups] = useState([]);
  const [loadingMatchups, setLoadingMatchups] = useState(true);
  const [selectedGroup, setSelectedGroup] = useState("Friends League");
  const [selectedWeek, setSelectedWeek] = useState(7);

  const [viewMode, setViewMode] = useState("card"); // ✅ add this

  const weekStartDate = "October 19, 2025";
  const weekEndDate = "October 21, 2025";

  useEffect(() => {
    async function fetchMatchups() {
      setLoadingMatchups(true);
      try {
        const response = await apiWrapper.Matchups.getByGroupAndWeek(
          selectedGroup,
          selectedWeek
        );
        setMatchups(response.data);
      } catch (error) {
        console.error("Failed to load matchups:", error);
        toast.error("Failed to load matchups!");
      } finally {
        setLoadingMatchups(false);
      }
    }

    fetchMatchups();
  }, [selectedGroup, selectedWeek]);

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

  return (
    <div className="picks-page-container">
      <div>
        {/* Group and Week Dropdowns */}
        <div style={{ display: "flex", gap: "20px", marginBottom: "20px" }}>
          <GroupWeekSelector
            selectedGroup={selectedGroup}
            setSelectedGroup={setSelectedGroup}
            selectedWeek={selectedWeek}
            setSelectedWeek={setSelectedWeek}
          />
        </div>

        {/* Heading + View Toggle aligned nicely */}
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            marginBottom: "20px",
          }}
        >
          <h2 style={{ margin: 0 }}>
            Week {selectedWeek} Picks
            <div className="week-dates">
              {weekStartDate} - {weekEndDate}
            </div>
          </h2>

          <button className="toggle-view-button" onClick={toggleViewMode}>
            {viewMode === "card"
              ? "Switch to Grid View"
              : "Switch to Card View"}
          </button>
        </div>

        {/* 🔥 Conditional Rendering */}
        {viewMode === "card" ? (
          <MatchupList
            matchups={matchups}
            loading={loadingMatchups}
            userPicks={userPicks}
            onPick={handlePick}
            onViewInsight={handleViewInsight}
            isSubscribed={isSubscribed}
          />
        ) : (
          <MatchupGrid
            matchups={matchups}
            loading={loadingMatchups}
            userPicks={userPicks}
            onPick={handlePick}
            onViewInsight={handleViewInsight}
            isSubscribed={isSubscribed}
          />
        )}

        <div style={{ marginTop: "30px", textAlign: "center" }}>
          <SubmitButton
            submitted={submitted}
            isSubmitting={isSubmitting}
            onSubmit={handleSubmit}
          />
        </div>
      </div>

      <InsightDialog
        isOpen={isInsightDialogOpen}
        onClose={() => setIsInsightDialogOpen(false)}
        matchup={selectedMatchup}
        bullets={selectedMatchup ? selectedMatchup.bullets : []}
        prediction={selectedMatchup ? selectedMatchup.prediction : ""}
        loading={loadingInsight}
      />
    </div>
  );
}

export default PicksPage;
