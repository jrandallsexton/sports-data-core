using System;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueSingleFranchiseSeasonEnrichment;

/// <summary>
/// "Make this ONE franchise season current" — the single-team twin of the
/// weekly <see cref="Franchises.FranchiseSeasonEnrichmentJob"/>. Same three
/// legs (record enrichment, scoped statistics re-source, metrics), scoped to
/// one FranchiseSeason so an operator can repair a team from its team page.
/// </summary>
public record EnqueueSingleFranchiseSeasonEnrichmentCommand(
    Guid FranchiseSeasonId,
    // Resolved by the controller from X-Correlation-Id (or the current activity); shared by every leg.
    Guid CorrelationId);
