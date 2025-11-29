import { useState, useEffect, useCallback, useRef } from "react";
import { GoogleMap, useLoadScript, Marker, InfoWindow, OverlayView } from "@react-google-maps/api";
import { useContestUpdates } from "../../contexts/ContestUpdatesContext";
import { useUserDto } from "../../contexts/UserContext";
import apiWrapper from "../../api/apiWrapper";
import LeagueWeekSelector from "../picks/LeagueWeekSelector";
import "./GameMap.css";

const mapContainerStyle = {
  width: "100%",
  height: "calc(100vh - 120px)",
};

const center = {
  lat: 39.8283, // Geographic center of the US
  lng: -98.5795,
};

const mapOptions = {
  disableDefaultUI: false,
  zoomControl: true,
  mapTypeControl: false,
  streetViewControl: false,
  fullscreenControl: true,
  styles: [
    {
      featureType: "all",
      elementType: "geometry",
      stylers: [
        { saturation: -20 },
        { lightness: 10 }
      ]
    }
  ]
};

function GameMap() {
  console.log('=== Google Maps API Key Debug ===');
  console.log('REACT_APP_GOOGLE_MAPS_API_KEY:', process.env.REACT_APP_GOOGLE_MAPS_API_KEY);
  console.log('Key exists:', !!process.env.REACT_APP_GOOGLE_MAPS_API_KEY);
  console.log('Key length:', process.env.REACT_APP_GOOGLE_MAPS_API_KEY?.length);
  console.log('All env vars:', Object.keys(process.env).filter(k => k.startsWith('REACT_APP_')));
  
  const { isLoaded, loadError } = useLoadScript({
    googleMapsApiKey: process.env.REACT_APP_GOOGLE_MAPS_API_KEY,
  });

  console.log('Google Maps isLoaded:', isLoaded);
  console.log('Google Maps loadError:', loadError);

  const { userDto, loading: userLoading } = useUserDto();
  const leagues = Object.values(userDto?.leagues || {});
  
  const [games, setGames] = useState([]);
  const [selectedGame, setSelectedGame] = useState(null);
  const [hoveredGame, setHoveredGame] = useState(null);
  const [loading, setLoading] = useState(true);
  const [conferenceFilter, setConferenceFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("all");
  const [conferences, setConferences] = useState([]);
  const [tooltipMode, setTooltipMode] = useState("off");  // "off", "labels", "details"
  const [selectedLeagueId, setSelectedLeagueId] = useState(null);
  const [selectedWeek, setSelectedWeek] = useState(null);
  const [scoringPlays, setScoringPlays] = useState(new Set()); // Track contests with recent scoring plays
  const mapRef = useRef(null);
  const previousScoresRef = useRef({});
  const hoverTimeoutRef = useRef(null);
  const selectedGameIdRef = useRef(null); // Track selected game ID

  const { getContestUpdate } = useContestUpdates();

  // Find the selected league's maxSeasonWeek (same pattern as PicksPage)
  const selectedLeague = leagues.find((l) => l.id === selectedLeagueId) ?? null;
  const maxSeasonWeek = selectedLeague?.maxSeasonWeek ?? 
    (leagues.length > 0 ? Math.max(...leagues.map(l => l.maxSeasonWeek || 1)) : null);

  // Fetch map data from API - on mount and when league/week changes
  useEffect(() => {
    async function fetchMapData() {
      setLoading(true);
      try {
        // Pass league and week parameters if they are selected
        const response = await apiWrapper.Maps.getMap(selectedLeagueId, selectedWeek);
        const mapData = response.data?.matchups || [];
        
        // Transform API data to component format
        const transformedGames = mapData.map(game => ({
          contestId: game.contestId,
          awayShort: game.awayAbbreviation,
          homeShort: game.homeAbbreviation,
          awayColor: game.awayColor,
          homeColor: game.homeColor,
          awayRank: game.awayRank,
          homeRank: game.homeRank,
          awayScore: null, // Will be updated by SignalR
          homeScore: null, // Will be updated by SignalR
          status: game.status === "STATUS_SCHEDULED" ? "Scheduled" : 
                  game.status === "STATUS_IN_PROGRESS" ? "InProgress" :
                  game.status === "STATUS_FINAL" ? "Final" : "Scheduled",
          homeSpread: game.homeSpread,
          startDateUtc: game.startDateUtc,
          venueName: game.venueName,
          venueCity: game.venueCity,
          venueState: game.venueState,
          latitude: game.venueLatitude,
          longitude: game.venueLongitude,
          awayConferenceSlug: game.awayConferenceSlug,
          homeConferenceSlug: game.homeConferenceSlug,
          awaySlug: game.awaySlug,
          homeSlug: game.homeSlug,
          awayWins: game.awayWins,
          awayLosses: game.awayLosses,
          homeWins: game.homeWins,
          homeLosses: game.homeLosses,
        }));
        
        setGames(transformedGames);
        
        // Extract unique conferences
        const uniqueConferences = [...new Set(transformedGames.flatMap(g => 
          [g.awayConferenceSlug, g.homeConferenceSlug].filter(Boolean)
        ))];
        setConferences(uniqueConferences.sort());
        
      } catch (error) {
        console.error("Failed to fetch map data:", error);
      } finally {
        setLoading(false);
      }
    }
    fetchMapData();
  }, [selectedLeagueId, selectedWeek]); // Refetch when league or week changes

  // Monitor live updates and add visual indicator for scoring plays
  useEffect(() => {
    games.forEach(game => {
      const liveUpdate = getContestUpdate(game.contestId);
      
      if (liveUpdate?.isScoringPlay) {
        const previousScore = previousScoresRef.current[game.contestId];
        const currentScore = {
          away: liveUpdate.awayScore,
          home: liveUpdate.homeScore,
        };

        // Check if score actually changed
        if (previousScore && 
            (previousScore.away !== currentScore.away || 
             previousScore.home !== currentScore.home)) {
          // Add to scoring plays set for visual indicator
          setScoringPlays(prev => new Set(prev).add(game.contestId));
          
          // Remove after animation duration (5 seconds)
          setTimeout(() => {
            setScoringPlays(prev => {
              const newSet = new Set(prev);
              newSet.delete(game.contestId);
              return newSet;
            });
          }, 5000);
        }

        previousScoresRef.current[game.contestId] = currentScore;
      }
    });
  }, [games, getContestUpdate]);

  // Enrich games with live data
  const enrichedGames = games.map(game => {
    const liveUpdate = getContestUpdate(game.contestId);
    if (liveUpdate) {
      return {
        ...game,
        status: liveUpdate.status,
        awayScore: liveUpdate.awayScore,
        homeScore: liveUpdate.homeScore,
        period: liveUpdate.period,
        clock: liveUpdate.clock,
        isScoringPlay: liveUpdate.isScoringPlay,
      };
    }
    return game;
  });

  // Filter games
  const filteredGames = enrichedGames.filter(game => {
    const conferenceMatch = conferenceFilter === "all" || 
      game.awayConferenceSlug === conferenceFilter || 
      game.homeConferenceSlug === conferenceFilter;

    let statusMatch = true;
    if (statusFilter === "upcoming") {
      statusMatch = game.status === "Scheduled" || !game.status;
    } else if (statusFilter === "live") {
      statusMatch = game.status === "InProgress";
    } else if (statusFilter === "final") {
      statusMatch = game.status === "Final";
    }

    return conferenceMatch && statusMatch;
  });

  // Close InfoWindow when filters change
  useEffect(() => {
    setSelectedGame(null);
  }, [conferenceFilter, statusFilter]);

  // Update selectedGame with live data when enrichedGames changes
  useEffect(() => {
    if (selectedGame && selectedGameIdRef.current === selectedGame.contestId) {
      const updatedGame = enrichedGames.find(g => g.contestId === selectedGame.contestId);
      if (updatedGame && JSON.stringify(updatedGame) !== JSON.stringify(selectedGame)) {
        setSelectedGame(updatedGame);
      }
    }
  }, [enrichedGames, selectedGame]);

  const getMarkerColor = (game) => {
    if (game.status === "Final") return "#F44336"; // Red
    if (game.status === "InProgress") return "#4CAF50"; // Green
    return "#9E9E9E"; // Gray
  };

  const onMapLoad = useCallback((map) => {
    mapRef.current = map;
  }, []);

  const handleMarkerClick = (game) => {
    setSelectedGame(game);
    selectedGameIdRef.current = game.contestId; // Track the selected game ID
    setHoveredGame(null);
    if (hoverTimeoutRef.current) {
      clearTimeout(hoverTimeoutRef.current);
    }
  };

  const handleMapClick = () => {
    setSelectedGame(null);
    selectedGameIdRef.current = null; // Clear the tracked ID
  };

  const cycleTooltipMode = () => {
    setTooltipMode(prev => {
      if (prev === "off") return "labels";
      if (prev === "labels") return "details";
      return "off";
    });
    setSelectedGame(null); // Close any open InfoWindow when cycling modes
  };

  const handleTooltipClick = (game, e) => {
    if (e) {
      e.stopPropagation();
    }
    setSelectedGame(game);
    selectedGameIdRef.current = game.contestId; // Track the selected game ID
  };

  const handleMarkerMouseOver = (game) => {
    if (hoverTimeoutRef.current) {
      clearTimeout(hoverTimeoutRef.current);
    }
    hoverTimeoutRef.current = setTimeout(() => {
      setHoveredGame(game);
    }, 300); // Small delay to prevent flickering
  };

  const handleMarkerMouseOut = () => {
    if (hoverTimeoutRef.current) {
      clearTimeout(hoverTimeoutRef.current);
    }
    hoverTimeoutRef.current = setTimeout(() => {
      setHoveredGame(null);
    }, 100);
  };

  // Cleanup timeout on unmount
  useEffect(() => {
    return () => {
      if (hoverTimeoutRef.current) {
        clearTimeout(hoverTimeoutRef.current);
      }
    };
  }, []);

  if (loadError) {
    console.error("Google Maps load error:", loadError);
    return (
      <div className="map-error">
        <h2>Error loading Google Maps</h2>
        <p>Check console for details. This might be an API key restriction.</p>
        <p>Make sure the API key has:</p>
        <ul style={{textAlign: 'left', maxWidth: '500px', margin: '20px auto'}}>
          <li>Maps JavaScript API enabled</li>
          <li>Billing account attached</li>
          <li>No domain restrictions (or localhost allowed)</li>
        </ul>
      </div>
    );
  }
  if (!isLoaded) return <div className="map-loading">Loading map...</div>;

  return (
    <div className="game-map-container">
      <div className="map-controls">
        {/* League and Week Selector */}
        {!userLoading && leagues.length > 0 && (
          <LeagueWeekSelector
            leagues={leagues}
            selectedLeagueId={selectedLeagueId}
            setSelectedLeagueId={setSelectedLeagueId}
            selectedWeek={selectedWeek}
            setSelectedWeek={setSelectedWeek}
            maxSeasonWeek={maxSeasonWeek}
            allowAll={true}
          />
        )}
        
        <div className="filters">
          <select 
            value={conferenceFilter} 
            onChange={(e) => setConferenceFilter(e.target.value)}
            className="filter-select"
          >
            <option value="all">All Conferences</option>
            {conferences.map(conf => (
              <option key={conf} value={conf}>{conf.toUpperCase()}</option>
            ))}
          </select>

          <select 
            value={statusFilter} 
            onChange={(e) => setStatusFilter(e.target.value)}
            className="filter-select"
          >
            <option value="all">All Games</option>
            <option value="upcoming">Upcoming</option>
            <option value="live">Live</option>
            <option value="final">Final</option>
          </select>

          <div className="legend">
            <button 
              className={`tooltip-toggle ${tooltipMode !== 'off' ? 'active' : ''}`}
              onClick={cycleTooltipMode}
              title="Cycle tooltip display mode"
            >
              {tooltipMode === "off" && '🏷️ Show All'}
              {tooltipMode === "labels" && '🏷️ Details'}
              {tooltipMode === "details" && '🏷️ Hide All'}
            </button>
            <span className="legend-item">
              <span className="legend-dot" style={{backgroundColor: "#9E9E9E"}}></span>
              Upcoming
            </span>
            <span className="legend-item">
              <span className="legend-dot" style={{backgroundColor: "#4CAF50"}}></span>
              Live
            </span>
            <span className="legend-item">
              <span className="legend-dot" style={{backgroundColor: "#F44336"}}></span>
              Final
            </span>
          </div>
        </div>
      </div>

      {loading ? (
        <div className="map-loading">Loading games...</div>
      ) : (
        <GoogleMap
          mapContainerStyle={mapContainerStyle}
          zoom={5}
          center={center}
          options={mapOptions}
          onLoad={onMapLoad}
          onClick={handleMapClick}
        >
          {filteredGames.map(game => {
            if (!game.latitude || !game.longitude) return null;

            return (
              <Marker
                key={game.contestId}
                position={{
                  lat: parseFloat(game.latitude),
                  lng: parseFloat(game.longitude),
                }}
                onClick={() => handleMarkerClick(game)}
                onMouseOver={() => handleMarkerMouseOver(game)}
                onMouseOut={handleMarkerMouseOut}
                icon={{
                  path: window.google.maps.SymbolPath.CIRCLE,
                  fillColor: getMarkerColor(game),
                  fillOpacity: 0.9,
                  strokeColor: "#FFFFFF",
                  strokeWeight: 2,
                  scale: game.status === "InProgress" ? 12 : 8,
                }}
              />
            );
          })}

          {/* Hover Tooltip */}
          {hoveredGame && !selectedGame && tooltipMode === "off" && (
            <OverlayView
              position={{
                lat: parseFloat(hoveredGame.latitude),
                lng: parseFloat(hoveredGame.longitude),
              }}
              mapPaneName={OverlayView.FLOAT_PANE}
              getPixelPositionOffset={(width, height) => ({
                x: -(width / 2),
                y: -(height + 20),
              })}
            >
              <div className="hover-tooltip">
                <div className="tooltip-matchup">
                  <div className="tooltip-team">
                    {hoveredGame.awayRank && <span className="tooltip-rank">#{hoveredGame.awayRank}</span>}
                    <span className="tooltip-team-name">{hoveredGame.awayShort}</span>
                  </div>
                  <span className="tooltip-vs">@</span>
                  <div className="tooltip-team">
                    {hoveredGame.homeRank && <span className="tooltip-rank">#{hoveredGame.homeRank}</span>}
                    <span className="tooltip-team-name">{hoveredGame.homeShort}</span>
                  </div>
                </div>
                {hoveredGame.status === "InProgress" && (
                  <div className="tooltip-score">
                    {hoveredGame.awayScore} - {hoveredGame.homeScore}
                  </div>
                )}
              </div>
            </OverlayView>
          )}

          {/* Show All Labels Mode */}
          {tooltipMode === "labels" && filteredGames.map(game => {
            if (!game.latitude || !game.longitude) return null;
            
            return (
              <OverlayView
                key={`tooltip-${game.contestId}`}
                position={{
                  lat: parseFloat(game.latitude),
                  lng: parseFloat(game.longitude),
                }}
                mapPaneName={OverlayView.FLOAT_PANE}
                getPixelPositionOffset={(width, height) => ({
                  x: -(width / 2),
                  y: -(height + 20),
                })}
              >
                <div className="hover-tooltip clickable" onClick={(e) => handleTooltipClick(game, e)}>
                  <div className="tooltip-matchup">
                    <div className="tooltip-team">
                      {game.awayRank && <span className="tooltip-rank">#{game.awayRank}</span>}
                      <span className="tooltip-team-name">{game.awayShort}</span>
                    </div>
                    <span className="tooltip-vs">@</span>
                    <div className="tooltip-team">
                      {game.homeRank && <span className="tooltip-rank">#{game.homeRank}</span>}
                      <span className="tooltip-team-name">{game.homeShort}</span>
                    </div>
                  </div>
                  {game.status === "InProgress" && (
                    <div className="tooltip-score">
                      {game.awayScore} - {game.homeScore}
                    </div>
                  )}
                </div>
              </OverlayView>
            );
          })}

          {/* Show All Details Mode */}
          {tooltipMode === "details" && filteredGames.map(game => {
            if (!game.latitude || !game.longitude) return null;
            
            return (
              <OverlayView
                key={`detail-${game.contestId}`}
                position={{
                  lat: parseFloat(game.latitude),
                  lng: parseFloat(game.longitude),
                }}
                mapPaneName={OverlayView.FLOAT_PANE}
                getPixelPositionOffset={(width, height) => ({
                  x: -(width / 2),
                  y: -(height + 10),
                })}
              >
                <div className="mini-info-window" onClick={(e) => handleTooltipClick(game, e)}>
                  <div className="mini-venue-name">{game.venueName}</div>
                  <div className="mini-matchup">
                    {game.awayRank && <span className="mini-rank">#{game.awayRank}</span>}
                    {game.awayShort} ({game.awayWins}-{game.awayLosses})
                    {' @ '}
                    {game.homeRank && <span className="mini-rank">#{game.homeRank}</span>}
                    {game.homeShort} ({game.homeWins}-{game.homeLosses})
                  </div>
                </div>
              </OverlayView>
            );
          })}

          {/* Click InfoWindow */}
          {selectedGame && (
            <InfoWindow
              position={{
                lat: parseFloat(selectedGame.latitude),
                lng: parseFloat(selectedGame.longitude),
              }}
              onCloseClick={() => setSelectedGame(null)}
            >
              <div className={`info-window ${scoringPlays.has(selectedGame.contestId) ? 'scoring-play' : ''}`}>
                <h1 className="map-venue-name">{selectedGame.venueName}</h1>
                <h2 className="map-venue-location">{selectedGame.venueCity}, {selectedGame.venueState}</h2>
                <div className="map-game-time">
                  {new Date(selectedGame.startDateUtc).toLocaleString('en-US', {
                    weekday: 'short',
                    month: 'short',
                    day: 'numeric',
                    hour: 'numeric',
                    minute: '2-digit',
                  })}
                </div>
                <div className="map-matchup-line">
                  {selectedGame.awayRank && <span className="map-rank-inline">#{selectedGame.awayRank}</span>}
                  {selectedGame.awayShort} ({selectedGame.awayWins}-{selectedGame.awayLosses})
                  {' @ '}
                  {selectedGame.homeRank && <span className="map-rank-inline">#{selectedGame.homeRank}</span>}
                  {selectedGame.homeShort} ({selectedGame.homeWins}-{selectedGame.homeLosses})
                </div>
                
                {/* Live Score Display */}
                {(selectedGame.status === "InProgress" || selectedGame.status === "Final") && 
                 selectedGame.awayScore !== null && selectedGame.homeScore !== null && (
                  <div className="map-score-display">
                    <div className="map-score-line">
                      <span className="map-team-abbr">{selectedGame.awayShort}:</span>
                      <span className="map-score">{selectedGame.awayScore}</span>
                    </div>
                    <div className="map-score-line">
                      <span className="map-team-abbr">{selectedGame.homeShort}:</span>
                      <span className="map-score">{selectedGame.homeScore}</span>
                    </div>
                  </div>
                )}
                
                {/* Game Status Info */}
                {selectedGame.status === "InProgress" && selectedGame.period && (
                  <div className="map-game-status">
                    {selectedGame.period}
                    {selectedGame.clock && ` - ${selectedGame.clock}`}
                  </div>
                )}
                {selectedGame.status === "Final" && (
                  <div className="map-game-status map-final-status">
                    Final
                  </div>
                )}
                
                {selectedGame.homeSpread && (
                  <div className="map-spread-line">
                    Spread: {selectedGame.homeShort} {selectedGame.homeSpread > 0 ? '+' : ''}{selectedGame.homeSpread}
                  </div>
                )}
              </div>
            </InfoWindow>
          )}
        </GoogleMap>
      )}
    </div>
  );
}

export default GameMap;
