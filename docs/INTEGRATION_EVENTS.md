# Integration Events

This document outlines the integration events used within the SportsData system. The system follows an event-driven architecture where both the **Producer** and **API** services publish events to a message bus (MassTransit over RabbitMQ locally / Azure Service Bus in production). Some events are also forwarded to the frontend via **Azure SignalR**.

> **Looking for the canonical map of who publishes/consumes what (with broken-chain flags)?** See [Event Surface Overview](event-surface-overview.md). This catalog lists event names; the overview adds wiring status, file:line references, and recommendations.

## Architecture Overview

1.  **Producer**: Sources data from external providers, processes it, and publishes integration events.
2.  **Message Bus**: Transports events between services.
3.  **API**: Consumes events to update its read models or trigger other actions.
4.  **SignalR**: The API forwards specific real-time events to connected clients (web frontend).

## Event Flows (multi-hop chains)

Some user-visible features traverse multiple events across multiple services and pod roles. These docs show the full chain end-to-end with sequence diagrams and per-hop narrative.

*   [Competitor Score Update → Live UI Push](events/flows/competitor-score-flow.md) — `DocumentCreated` → `CompetitorScoreUpdated` → `ContestScoreChanged` → SignalR push to web clients. Three services, two pod roles, two Hangfire hops.

## Event Catalog

### Athletes
*   [`AthleteCreated`](events/athletes/AthleteCreated.md)
*   [`AthletePositionCreated`](events/athletes/AthletePositionCreated.md)
*   [`AthletePositionUpdated`](events/athletes/AthletePositionUpdated.md)

### Conferences
*   [`ConferenceCreated`](events/conferences/ConferenceCreated.md)
*   [`ConferenceSeasonCreated`](events/conferences/ConferenceSeasonCreated.md)
*   [`ConferenceSeasonUpdated`](events/conferences/ConferenceSeasonUpdated.md)
*   [`ConferenceUpdated`](events/conferences/ConferenceUpdated.md)

### Contests
*   [`ContestCreated`](events/contests/ContestCreated.md)
*   [`ContestEnrichmentCompleted`](events/contests/ContestEnrichmentCompleted.md)
*   [`ContestOddsCreated`](events/contests/ContestOddsCreated.md)
*   [`ContestOddsUpdated`](events/contests/ContestOddsUpdated.md)
*   [`ContestPlayCompleted`](events/contests/ContestPlayCompleted.md)
*   [`ContestRecapArticlePublished`](events/contests/ContestRecapArticlePublished.md)
*   [`ContestScoreChanged`](events/contests/ContestScoreChanged.md)
*   [`ContestStartTimeUpdated`](events/contests/ContestStartTimeUpdated.md)
*   [`ContestStatusChanged`](events/contests/ContestStatusChanged.md) — sport-neutral lifecycle (Scheduled → InProgress → Final)
*   [`ContestWinProbabilityChanged`](events/contests/ContestWinProbabilityChanged.md)
*   [`CompetitorScoreUpdated`](events/contests/CompetitorScoreUpdated.md)
*   Football
    *   [`FootballContestStateChanged`](events/contests/FootballContestStateChanged.md) — per-play scoreboard tick
*   Baseball
    *   [`BaseballContestStateChanged`](events/contests/BaseballContestStateChanged.md) — per-pitch / per-at-bat tick

### Core
*   [`Heartbeat`](events/core/Heartbeat.md)
*   [`OutboxTestEvent`](events/core/OutboxTestEvent.md)

### Documents
*   [`DocumentCreated`](events/documents/DocumentCreated.md)
*   [`DocumentRequested`](events/documents/DocumentRequested.md)
*   [`DocumentSourcingStarted`](events/documents/DocumentSourcingStarted.md)
*   [`DocumentDeadLetter`](events/documents/DocumentDeadLetter.md)
*   [`DocumentProcessingCompleted`](events/documents/DocumentProcessingCompleted.md)
*   [`DocumentUpdated`](events/documents/DocumentUpdated.md)
*   [`RequestedDependency`](events/documents/RequestedDependency.md)

### Franchise
*   [`FranchiseCreated`](events/franchise/FranchiseCreated.md)
*   [`FranchiseSeasonCreated`](events/franchise/FranchiseSeasonCreated.md)
*   [`FranchiseSeasonEnrichmentCompleted`](events/franchise/FranchiseSeasonEnrichmentCompleted.md)
*   [`FranchiseSeasonRecordCreated`](events/franchise/FranchiseSeasonRecordCreated.md)
*   [`FranchiseUpdated`](events/franchise/FranchiseUpdated.md)

### Images
*   [`ProcessImageRequest`](events/images/ProcessImageRequest.md)
*   [`ProcessImageResponse`](events/images/ProcessImageResponse.md)

### PickemGroups
*   [`PickemGroupCreated`](events/pickem-groups/PickemGroupCreated.md)
*   [`PickemGroupMatchupAdded`](events/pickem-groups/PickemGroupMatchupAdded.md)
*   [`PickemGroupWeekMatchupsGenerated`](events/pickem-groups/PickemGroupWeekMatchupsGenerated.md)

### Positions
*   [`PositionCreated`](events/positions/PositionCreated.md)

### Previews
*   [`PreviewGenerated`](events/previews/PreviewGenerated.md)

### Venues
*   [`VenueCreated`](events/venues/VenueCreated.md)
*   [`VenueUpdated`](events/venues/VenueUpdated.md)
