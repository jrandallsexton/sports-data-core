// src/hooks/useSignalRClient.js
import { useEffect, useRef } from "react";
import * as signalR from "@microsoft/signalr";

export default function useSignalRClient({
  userId,
  leagueId,
  onPreviewCompleted,
  onContestStatusUpdated,
}) {
  const connectionRef = useRef(null);

  useEffect(() => {
    // Use separate SignalR URL if provided, otherwise fall back to API base URL
    const signalRUrl = process.env.REACT_APP_SIGNALR_URL || process.env.REACT_APP_API_BASE_URL || "http://localhost:5262";
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${signalRUrl}/hubs/notifications`, {
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    connection.on("PreviewGenerated", onPreviewCompleted);
    
    // Register handler for contest status updates (live game data)
    if (onContestStatusUpdated) {
      connection.on("ContestStatusChanged", onContestStatusUpdated);
    }

    connection
      .start()
      .then(() => {
        console.log("SignalR connected");
        // Optional: you can invoke group join here later
        // connection.invoke("JoinLeagueGroup", leagueId);
      })
      .catch((err) => {
        console.error("SignalR connection error:", err);
      });

    connectionRef.current = connection;

    return () => {
      connection.stop();
    };
  }, [userId, leagueId, onPreviewCompleted, onContestStatusUpdated]);

  return connectionRef.current;
}
