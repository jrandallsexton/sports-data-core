# DocumentCreated

Fired when a new raw document is saved.

## Flow Diagram

```mermaid
sequenceDiagram
    participant Pr as Provider
    participant B as Message Bus
    participant P_H as Producer (Handler)
    participant HF as Hangfire
    participant P_P as Producer (Processor)
    participant P_F as Processor Factory
    participant P_S as Specific Processor

    Note over Pr, P_S: Document Ingestion
    Pr->>B: Publish DocumentCreated
    B->>P_H: Consume DocumentCreated
    P_H->>HF: Enqueue Job (DocumentCreatedProcessor)
    HF->>P_P: Execute Process()
    P_P->>Pr: Fetch Document Content (if missing)
    P_P->>P_F: Get Specific Processor
    P_F-->>P_P: Return Processor
    P_P->>P_S: ProcessAsync(ProcessDocumentCommand)
```

## Processing Details

1.  **Provider** publishes `DocumentCreated` event.
2.  **Producer** consumes the event via `DocumentCreatedHandler`.
3.  **Handler** enqueues a background job in **Hangfire** to process the document asynchronously.
4.  **DocumentCreatedProcessor** picks up the job:
    *   Retrieves the full document content from the **Provider** if it wasn't included in the event payload.
    *   Uses `IDocumentProcessorFactory` to resolve the correct processor strategy based on:
        *   Source Data Provider (e.g., ESPN)
        *   Sport (e.g., FootballNcaa)
        *   Document Type (e.g., Team, Athlete, Scoreboard)
    *   Delegates execution to the specific processor using `ProcessDocumentCommand`.
