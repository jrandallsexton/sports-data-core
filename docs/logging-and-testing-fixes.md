# Logging and Event Testing Fixes

## Summary

This document describes the fixes applied to resolve logging duplication issues and enhancements to event testing in the Provider service.

## Issue 1: Double Logging in API (and all services)

### Root Cause
The `AppConfiguration.UseCommon()` method was calling `configuration.ReadFrom.Configuration(context.Configuration)` which read Serilog sinks from `appsettings.Development.json`, AND THEN it was also adding the same sinks programmatically. This caused duplicate log entries to Seq and potentially to File sinks.

**Example of duplication:**
1. `appsettings.Development.json` defined a Seq sink pointing to `http://localhost:5341/`
2. `AppConfiguration.cs` called `ReadFrom.Configuration()` which added that Seq sink
3. `AppConfiguration.cs` then ALSO added a Seq sink programmatically if `CommonConfig:SeqUri` was set
4. Result: **Every log message was written twice to Seq**

### Solution
1. **Commented out** `configuration.ReadFrom.Configuration()` in `AppConfiguration.cs`
2. **Removed** all `Serilog` sections from `appsettings.Development.json` files in all services
3. **Added** Console sink programmatically for better development experience
4. **All sinks now configured in one place**: `AppConfiguration.cs`

### Files Modified (Logging Fix)
- `src/SportsData.Core/DependencyInjection/AppConfiguration.cs`
- `src/SportsData.Api/appsettings.Development.json`
- `src/SportsData.Contest/appsettings.Development.json`
- `src/SportsData.Franchise/appsettings.Development.json`
- `src/SportsData.Notification/appsettings.Development.json`
- `src/SportsData.Player/appsettings.Development.json`
- `src/SportsData.Season/appsettings.Development.json`
- `src/SportsData.Venue/appsettings.Development.json`

### Current Logging Configuration (Programmatic)
All sinks are now configured in `AppConfiguration.UseCommon()`:

```csharp
// File sink (always enabled)
configuration.WriteTo.File(
    path: logPath,
    rollingInterval: RollingInterval.Day,
    retainedFileCountLimit: 3,
    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
);

// Console sink (always enabled)
configuration.WriteTo.Console(
    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
);

// Seq sink (optional - enabled if CommonConfig:SeqUri is set)
if (!string.IsNullOrWhiteSpace(seqUri))
{
    var seqLevel = ParseLevel(loggingConfig.SeqMinimumLevel, globalLevel);
    configuration.WriteTo.Seq(seqUri, restrictedToMinimumLevel: seqLevel);
}
```

## Issue 2: Provider Event Testing

### Enhancement
Created a comprehensive `OutboxTestController` in the Provider service to validate that events are being published correctly.

### Why This Matters
Provider is the **source** of truth for documents. It:
1. Fetches data from external APIs (ESPN, CBS, etc.)
2. Stores raw JSON in Azure Blob Storage
3. **Publishes `DocumentCreated` events** to Azure Service Bus
4. Producer listens for these events and processes the documents

If events aren't being published from Provider, the entire downstream pipeline fails.

### New Provider Test Endpoints

#### 1. `POST /api/test/outbox/comprehensive-test`
**Purpose:** Mimics the actual Provider workflow - publishes a `DocumentCreated` event like Provider does in production.

**What it does:**
- Creates a `DocumentCreated` event for an ESPN Event document
- Publishes directly to Azure Service Bus (NO outbox in Provider)
- Returns detailed information about the event

**Example Response:**
```json
{
  "testId": "guid",
  "correlationId": "guid",
  "success": true,
  "verdict": "? Event published to Azure Service Bus",
  "eventDetails": {
    "eventType": "DocumentCreated",
    "documentType": "Event",
    "sport": "FootballNcaa",
    "provider": "Espn",
    "seasonYear": 2025
  },
  "flow": "Provider ? Azure Service Bus ? Producer (listens for DocumentCreated)",
  "expectedDownstream": [
    "1. Producer DocumentCreatedHandler receives event",
    "2. Producer queues Hangfire job to process document",
    "3. Producer DocumentProcessorFactory creates appropriate processor",
    "4. Processor transforms and persists data to database"
  ],
  "troubleshooting": {
    "checkProducerLogs": "Search for 'DocumentCreated' in Producer logs",
    "checkHangfireDashboard": "Verify job was enqueued in Producer's Hangfire dashboard",
    "checkServiceBus": "Verify message was sent to Azure Service Bus topic/queue"
  }
}
```

#### 2. `POST /api/test/outbox/publish-batch?count=5`
**Purpose:** Simulates bulk document sourcing by publishing multiple `DocumentCreated` events.

**Parameters:**
- `count` (query param): Number of events to publish (1-100)

**Use case:** Test how Producer handles bursts of events (like during historical data import).

#### 3. `POST /api/test/outbox/publish-direct`
**Purpose:** Simple test - publishes an `OutboxTestEvent` directly.

**Use case:** Verify basic event bus connectivity.

#### 4. `GET /api/test/outbox/info`
**Purpose:** Returns information about Provider's event publishing architecture.

**Response:**
```json
{
  "service": "Provider",
  "outboxEnabled": false,
  "publishingStrategy": "Direct publish to Azure Service Bus",
  "workflow": [
    "1. Provider sources documents from external APIs (ESPN, CBS, Yahoo, etc.)",
    "2. Provider stores raw JSON in Azure Blob Storage",
    "3. Provider publishes DocumentCreated event to Azure Service Bus",
    "4. Producer receives event and processes document asynchronously"
  ],
  "architecture": {
    "dataStore": "Azure Blob Storage (JSON documents)",
    "messaging": "Azure Service Bus (direct publish)",
    "consumers": ["Producer", "Other downstream services"]
  }
}
```

### Files Modified (Provider Testing Enhancement)
- `src/SportsData.Provider/Controllers/OutboxTestController.cs`

## Testing Instructions

### 1. Verify Logging Fix
1. Start any service (e.g., API)
2. Check logs in Seq (`http://localhost:5341`)
3. Verify each log message appears **only once** (not duplicated)

### 2. Test Provider Event Publishing
1. Start Provider service
2. Start Producer service (to receive events)
3. Call Provider test endpoint:
   ```bash
   curl -X POST http://localhost:5000/api/test/outbox/comprehensive-test
   ```
4. Check Producer logs for:
   ```
   DocumentCreatedHandler: Received DocumentCreated event
   ```
5. Check Producer's Hangfire dashboard for the queued job
6. Verify Producer processes the document successfully

### 3. Test End-to-End Flow
1. Use the batch endpoint to simulate bulk sourcing:
   ```bash
   curl -X POST "http://localhost:5000/api/test/outbox/publish-batch?count=10"
   ```
2. Watch Producer logs to confirm all 10 events are received
3. Check Producer's database to verify processing completed

## Troubleshooting

### If events aren't reaching Producer from Provider:

1. **Check Azure Service Bus connection:**
   - Verify `CommonConfig:ServiceBusConnectionString` is set correctly
   - Check Azure portal for Service Bus activity

2. **Check Producer is listening:**
   - Look for `MassTransit` startup logs in Producer
   - Verify consumer is registered: `DocumentCreatedHandler`

3. **Check event serialization:**
   - Events must be serializable to JSON
   - Check for circular references or incompatible types

4. **Enable MassTransit diagnostics:**
   - Add to Producer's `appsettings.Development.json`:
     ```json
     {
       "Logging": {
         "LogLevel": {
           "MassTransit": "Debug"
         }
       }
     }
     ```

### If logs are still duplicated:

1. Verify `ReadFrom.Configuration()` is commented out in `AppConfiguration.cs`
2. Verify NO `Serilog` section exists in any `appsettings.Development.json`
3. Restart the service (changes require restart)

## Benefits

### Logging Fix
- ? **No more duplicate logs** in Seq, files, or console
- ? **Single source of truth** for logging configuration
- ? **Consistent logging** across all services
- ? **Better performance** (no redundant log writes)

### Provider Testing Enhancement
- ? **Validate event publishing** without running full integration tests
- ? **Test Producer integration** end-to-end
- ? **Simulate production scenarios** (bulk sourcing, etc.)
- ? **Debug event flow** issues quickly
- ? **Comprehensive troubleshooting guidance** in API responses

## Related Documentation
- [MassTransit Documentation](https://masstransit.io/)
- [Serilog Documentation](https://serilog.net/)
- [Azure Service Bus Documentation](https://docs.microsoft.com/en-us/azure/service-bus-messaging/)
