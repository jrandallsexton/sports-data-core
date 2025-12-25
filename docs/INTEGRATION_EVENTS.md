# Integration Events

This document outlines the integration events used within the SportsData system. The system follows an event-driven architecture where the **Producer** service publishes events to a message bus (MassTransit/RabbitMQ), and the **API** service consumes them. Some events are also forwarded to the frontend via **Azure SignalR**.

## Architecture Overview

1.  **Producer**: Sources data from external providers, processes it, and publishes integration events.
2.  **Message Bus**: Transports events between services.
3.  **API**: Consumes events to update its read models or trigger other actions.
4.  **SignalR**: The API forwards specific real-time events to connected clients (web frontend).

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
*   [`CompetitionPlayCompleted`](events/contests/CompetitionPlayCompleted.md)
*   [`CompetitionStatusChanged`](events/contests/CompetitionStatusChanged.md)
*   [`CompetitionWinProbabilityChanged`](events/contests/CompetitionWinProbabilityChanged.md)
*   [`ContestCreated`](events/contests/ContestCreated.md)
*   [`ContestEnrichmentCompleted`](events/contests/ContestEnrichmentCompleted.md)
*   [`ContestOddsCreated`](events/contests/ContestOddsCreated.md)
*   [`ContestOddsUpdated`](events/contests/ContestOddsUpdated.md)
*   [`ContestRecapArticlePublished`](events/contests/ContestRecapArticlePublished.md)
*   [`ContestStartTimeUpdated`](events/contests/ContestStartTimeUpdated.md)

### Core
*   [`Heartbeat`](events/core/Heartbeat.md)
*   [`OutboxTestEvent`](events/core/OutboxTestEvent.md)

### Documents
*   [`DocumentCreated`](events/documents/DocumentCreated.md)
*   [`DocumentRequested`](events/documents/DocumentRequested.md)
*   [`DocumentSourcingStarted`](events/documents/DocumentSourcingStarted.md)
*   [`DocumentUpdated`](events/documents/DocumentUpdated.md)

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
*   [`PickemGroupWeekMatchupsGenerated`](events/pickem-groups/PickemGroupWeekMatchupsGenerated.md)

### Positions
*   [`PositionCreated`](events/positions/PositionCreated.md)

### Previews
*   [`PreviewGenerated`](events/previews/PreviewGenerated.md)

### Venues
*   [`VenueCreated`](events/venues/VenueCreated.md)
*   [`VenueUpdated`](events/venues/VenueUpdated.md)
