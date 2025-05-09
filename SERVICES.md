# SportDeets Services Overview

| **Service**    | **Purpose** |
|----------------|-------------|
| `core`         | Shared services, components, and middleware to be consumed by the various services that compose the entire application. |
| `api`          | API Gateway that orchestrates access to underlying services and enforces authentication, authorization, and routing. |
| `contest`      | Manages matchups, results, pick logic, and consensus calculations for contests (e.g., weekly pick’em games). |
| `franchise`    | Stores metadata and branding information for teams/franchises across supported sports and leagues. |
| `notification` | Responsible for sending notifications to users (e.g., email, in-app messages) triggered by key events. |
| `player`       | Maintains canonical player information including stats, position, bio, and affiliations. |
| `producer`     | Converts raw external JSON documents from the Provider into structured domain models and emits internal domain/integration events. |
| `provider`     | Gathers and stores external sports data (e.g., ESPN, Yahoo!, sportsData.io), persisting raw JSON and emitting acquisition events. |
| `season`       | Tracks metadata for sports seasons including start/end dates, registration windows, and pick deadlines. |
| `venue`        | Centralized repository for stadium and location data shared across multiple sports and services. |
