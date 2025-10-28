# LLM-Guidelines for SportsData Solution

## Purpose
This document provides context, conventions, and best practices for working with the SportsData solution, especially when collaborating with AI assistants or onboarding new developers.

## Project Overview
- Multi-project .NET 9 solution for sports data ingestion, processing, and canonicalization.
- Key domains: Franchise, Venue, Coach, Athlete, TeamSeason, etc.
- Data sources: ESPN, SportsDataIO, and others.

## Entity & Data Patterns
- Canonical entities inherit from `CanonicalEntityBase<Guid>`.
- Each canonical entity (e.g., Coach, Venue, Franchise) has a corresponding `ExternalId` entity for provider-specific IDs.
- Entity configurations should specify string length constraints to suppress Roslyn warnings.
- Use navigation properties and `ICollection<T>` for related entities.

## Document Processors
- Implement `IProcessDocuments` for all document processors.
- Use `[DocumentProcessor]` attribute to register processors for specific (provider, sport, document type) tuples.
- Follow patterns from `VenueDocumentProcessor`, `FranchiseDocumentProcessor`, `CoachDocumentProcessor`, etc.
- Use `ProcessDocumentCommand` for all processor entry points.

## Testing
- Unit tests are located in `SportsData.Producer.Tests.Unit` and follow the `ProducerTestBase<T>` pattern.
- Use AutoFixture for test data, FluentAssertions for assertions, and Moq for mocking dependencies.
- Test data JSON files are stored in the `Data` subfolder.

## Naming & Conventions
- Use PascalCase for types and properties.
- Use singular names for entities (e.g., `Coach`, not `Coaches`).
- Use plural for `DbSet` properties (e.g., `Coaches`).
- Keep DTOs, entities, and extension methods in their respective folders.

## AI/LLM Usage Tips
- Always enumerate the workspace for context before making changes.
- Reference this file for architectural and naming guidance.
- When in doubt, look at similar entities or processors for patterns.
- Document any new conventions or patterns here for future reference.

## Miscellaneous
- All projects target .NET 9 and use C# 13 features.
- Use `HashProvider.UrlHash` for consistent external ID hashing.
- Register all new entity configurations in the appropriate `DbContext`.
