import React, { createContext, useContext, useState, useEffect } from 'react';

const LeagueContext = createContext();

export function useLeagueContext() {
  const context = useContext(LeagueContext);
  if (!context) {
    throw new Error('useLeagueContext must be used within a LeagueProvider');
  }
  return context;
}

export function LeagueProvider({ children }) {
  const [selectedLeagueId, setSelectedLeagueIdState] = useState(null);
  
  // Load from localStorage on initialization
  useEffect(() => {
    const savedLeagueId = localStorage.getItem('selectedLeagueId');
    if (savedLeagueId) {
      setSelectedLeagueIdState(savedLeagueId);
    }
  }, []);

  // Wrapper function that also saves to localStorage
  const setSelectedLeagueId = (leagueId) => {
    setSelectedLeagueIdState(leagueId);
    if (leagueId) {
      localStorage.setItem('selectedLeagueId', leagueId);
    } else {
      localStorage.removeItem('selectedLeagueId');
    }
  };

  // Function to initialize with available leagues if no selection exists
  const initializeLeagueSelection = (availableLeagues) => {
    if (!selectedLeagueId && availableLeagues.length > 0) {
      // Check if saved league still exists in user's leagues
      const savedLeagueId = localStorage.getItem('selectedLeagueId');
      const isValidLeague = savedLeagueId && availableLeagues.some(league => league.id === savedLeagueId);
      
      if (isValidLeague) {
        setSelectedLeagueIdState(savedLeagueId);
      } else {
        // Default to first league if saved league is no longer valid
        setSelectedLeagueId(availableLeagues[0].id);
      }
    }
  };

  const value = {
    selectedLeagueId,
    setSelectedLeagueId,
    initializeLeagueSelection
  };

  return (
    <LeagueContext.Provider value={value}>
      {children}
    </LeagueContext.Provider>
  );
}
