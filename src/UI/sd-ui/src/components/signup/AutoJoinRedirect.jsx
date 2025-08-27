import { useEffect } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { toast } from "react-hot-toast";
import LeaguesApi from "../../api/leagues/leaguesApi";

function AutoJoinRedirect() {
  const { leagueId } = useParams();
  const navigate = useNavigate();

  useEffect(() => {
    const joinLeague = async () => {
      try {
        await LeaguesApi.joinLeague(leagueId); // assumes POST /api/leagues/{id}/join
        toast.success("You’ve joined the league!");
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
  }, [leagueId, navigate]);

  return <div className="route-loading">Joining league...</div>;
}

export default AutoJoinRedirect;
