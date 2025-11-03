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
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("https://api-dev.sportdeets.com/hubs/notifications", {
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
