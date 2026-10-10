using System;
using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.Clients.Contest.Queries;

/// <summary>One instant-analysis lab replay run of a game (Producer <c>PlayAnalysisRun</c>).</summary>
public record PlayAnalysisRunSummaryDto(
    Guid RunId,
    Guid ContestId,
    Guid CompetitionId,
    int PlayCount,
    int FactCount,
    int DroppedCount,
    int TemplateVersion,
    DateTime CreatedUtc,
    DateTime? CompletedUtc);

/// <summary>A lab run with every play of the game, in order, and what the run said or dropped for each.</summary>
public record PlayAnalysisRunDetailDto(
    PlayAnalysisRunSummaryDto Run,
    IReadOnlyList<PlayAnalysisLabPlayDto> Plays);

public record PlayAnalysisLabPlayDto(
    Guid PlayId,
    string SequenceNumber,
    string TypeId,
    string Text,
    IReadOnlyList<PlayAnalysisLabFactDto> Facts,
    IReadOnlyList<PlayAnalysisLabDropDto> Dropped);

/// <summary>A generated fact. <see cref="Facts"/> is the persisted JSON (numbers and provenance).</summary>
public record PlayAnalysisLabFactDto(
    Guid Id,
    int Ordinal,
    string FactType,
    string Text,
    int Priority,
    string Facts,
    int? Stars);

/// <summary>A fact the run considered and didn't produce, and why.</summary>
public record PlayAnalysisLabDropDto(
    string FactType,
    string Kind,
    Guid? AthleteSeasonId,
    string Reason);

public record RatePlayAnalysisRequest(int Stars);

public record PlayAnalysisRatingDto(Guid PlayAnalysisId, int Stars);
