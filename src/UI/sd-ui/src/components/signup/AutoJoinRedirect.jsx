import { useEffect, useRef } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { toast } from "react-hot-toast";
import LeaguesApi from "../../api/leagues/leaguesApi";
import { useUserDto } from "../../contexts/UserContext";

function AutoJoinRedirect() {
  const { leagueId } = useParams();
  const navigate = useNavigate();
  const { refreshUserDto } = useUserDto();
  const hasAttemptedJoin = useRef(false);

  useEffect(() => {
    // Prevent double execution in StrictMode or due to re-renders
    if (hasAttemptedJoin.current) return;
    hasAttemptedJoin.current = true;

    const joinLeague = async () => {
      try {
        await LeaguesApi.joinLeague(leagueId); // assumes POST /api/leagues/{id}/join
        await refreshUserDto(); // Refresh user DTO to update leagues array
        toast.success("You've joined the league!");
        navigate(`/app/league/${leagueId}`);
      } catch (error) {
        console.error("Join failed:", error);
        
        // Extract error message from server response
        let errorMessage = "Could not join the league.";
        if (error.response?.data?.errors && error.response.data.errors.length > 0) {
          errorMessage = error.response.data.errors[0].errorMessage;
        }
        
        toast.error(errorMessage);
        navigate("/app/league");
      }
    };

    joinLeague();
  }, [leagueId, navigate, refreshUserDto]);

  return <div className="route-loading">Joining league...</div>;
}

export default AutoJoinRedirect;
