// src/hooks/useSignalRClient.js
import { useEffect, useRef } from "react";
import * as signalR from "@microsoft/signalr";
import { getAuth } from "firebase/auth";

export default function useSignalRClient({
  userId,
  leagueId,
  onPreviewCompleted,
  onContestStatusChanged,
  onFootballContestStateChanged,
  onBaseballContestStateChanged,
}) {
  const connectionRef = useRef(null);

  useEffect(() => {
    // Use separate SignalR URL if provided, otherwise fall back to API base URL
    const signalRUrl = process.env.REACT_APP_SIGNALR_URL || process.env.REACT_APP_API_BASE_URL || "http://localhost:5262";

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${signalRUrl}/hubs/notifications`, {
        accessTokenFactory: async () => {
          // Get Firebase ID token for authentication
          const auth = getAuth();
          const user = auth.currentUser;
          if (user) {
            try {
              return await user.getIdToken();
            } catch (error) {
              console.error("Failed to get Firebase token for SignalR:", error);
              return null;
            }
          }
          return null;
        }
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    connection.on("PreviewGenerated", onPreviewCompleted);

    // Lifecycle (Scheduled→InProgress→Final) — sport-neutral.
    if (onContestStatusChanged) {
      connection.on("ContestStatusChanged", onContestStatusChanged);
    }

    // Per-play scoreboard ticks — sport-specific shapes.
    if (onFootballContestStateChanged) {
      connection.on("FootballContestStateChanged", onFootballContestStateChanged);
    }

    if (onBaseballContestStateChanged) {
      connection.on("BaseballContestStateChanged", onBaseballContestStateChanged);
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
  }, [
    userId,
    leagueId,
    onPreviewCompleted,
    onContestStatusChanged,
    onFootballContestStateChanged,
    onBaseballContestStateChanged,
  ]);

  return connectionRef.current;
}
