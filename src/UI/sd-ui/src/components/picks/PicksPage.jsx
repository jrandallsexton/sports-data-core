import "./PicksPage.css";

import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useUserDto } from "../../contexts/UserContext";
import InsightDialog from "../insights/InsightDialog.jsx";
import toast from "react-hot-toast";
import apiWrapper from "../../api/apiWrapper.js";
import LeagueWeekSelector from "./LeagueWeekSelector.jsx";
import MatchupList from "../matchups/MatchupList.jsx";
import MatchupGrid from "../matchups/MatchupGrid.jsx";

function PicksPage() {
  const { userDto, loading: userLoading } = useUserDto();
  const { leagueId: routeLeagueId } = useParams(); // optional route param
  const navigate = useNavigate();

  const [userPicks, setUserPicks] = useState({});
  const [isSubscribed] = useState(false);
  const [selectedMatchup, setSelectedMatchup] = useState(null);
  const [isInsightDialogOpen, setIsInsightDialogOpen] = useState(false);
  const [loadingInsight, setLoadingInsight] = useState(false);

  const [matchups, setMatchups] = useState([]);
  const [loadingMatchups, setLoadingMatchups] = useState(true);

  const [selectedLeagueId, setSelectedLeagueId] = useState(null);
  const [selectedWeek, setSelectedWeek] = useState(1);
  const viewMode = "card";

  const [hidePicked, setHidePicked] = useState(false);
  const [fadingOut, setFadingOut] = useState([]);

  const leagues = Object.values(userDto?.leagues || {});

  // Select default league on load or when leagues change
  useEffect(() => {
    if (!userLoading && leagues.length > 0) {
      const isRouteValid =
        routeLeagueId && leagues.some((l) => l.id === routeLeagueId);
      if (isRouteValid) {
        setSelectedLeagueId(routeLeagueId);
      } else if (!selectedLeagueId) {
        setSelectedLeagueId(leagues[0].id);
      }
    }
  }, [userLoading, leagues, routeLeagueId, selectedLeagueId]);

  // Keep URL in sync with selectedLeagueId
  useEffect(
    () => {
      if (selectedLeagueId && selectedLeagueId !== routeLeagueId) {
        navigate(`/app/picks/${selectedLeagueId}`, { replace: true });
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [selectedLeagueId] // Intentionally omitting routeLeagueId and navigate
  );

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
        pickType: "StraightUp",
        franchiseSeasonId: selectedFranchiseSeasonId,
        week: selectedWeek,
      });

      if (hidePicked) {
        setFadingOut((prev) => [...prev, matchup.contestId]);

        setTimeout(() => {
          setUserPicks((prev) => ({
            ...prev,
            [matchup.contestId]: selectedFranchiseSeasonId,
          }));
          setFadingOut((prev) => prev.filter((id) => id !== matchup.contestId));
        }, 500);
      } else {
        setUserPicks((prev) => ({
          ...prev,
          [matchup.contestId]: selectedFranchiseSeasonId,
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

  if (userLoading) return <div>Loading user info...</div>;

  if (!leagues.length) {
    return <p>You are not part of any leagues yet.</p>;
  }

  const totalGames = matchups.length;
  const picksMade = Object.keys(userPicks).filter(
    (id) => userPicks[id] !== null && userPicks[id] !== undefined
  ).length;
  const allPicked = totalGames > 0 && picksMade === totalGames;

  const visibleMatchups = hidePicked
    ? matchups.filter(
        (m) => !userPicks[m.contestId] || fadingOut.includes(m.contestId)
      )
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
                fadingOut={fadingOut}
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
          />
        )}
      </div>
    </div>
  );
}

export default PicksPage;
