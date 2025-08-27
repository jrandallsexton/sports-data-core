// src/hooks/useSignalRClient.js
import { useEffect, useRef } from "react";
import * as signalR from "@microsoft/signalr";

export default function useSignalRClient({ userId, leagueId, onPreviewCompleted }) {
  const connectionRef = useRef(null);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/notifications")
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    connection.on("PreviewCompleted", onPreviewCompleted);

    connection.start().then(() => {
      console.log("SignalR connected");
      connection.invoke("JoinLeagueGroup", leagueId);
    });

    connectionRef.current = connection;

    return () => {
      connection.stop();
    };
  }, [userId, leagueId, onPreviewCompleted]);

  return connectionRef.current;
}
