using Microsoft.EntityFrameworkCore;

using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;

/// <summary>
/// Find-or-create the <see cref="AthleteStatus"/> for an ESPN status DTO and
/// return its id. AthleteStatus is a small shared lookup (Active / Injured
/// Reserve / …) deduped by name. Lets the AthleteSeason processors populate
/// <c>AthleteSeason.StatusId</c> — previously always null because the mapper
/// can't do the DB lookup. Mirrors the athlete processor's status handling.
/// </summary>
public static class AthleteStatusResolver
{
    public static async Task<Guid?> ResolveIdAsync(
        BaseDataContext dataContext,
        EspnAthleteStatusDto? status,
        CancellationToken cancellationToken = default)
    {
        var name = status?.Name?.Trim();
        if (string.IsNullOrEmpty(name))
            return null;

        // Single canonical normalization rule (culture-independent) used for both
        // the persisted NameNormalized value and every lookup key, so the lookup
        // and the unique constraint can't diverge for culture/Unicode cases.
        var nameNormalized = name.ToLowerInvariant();

        var existing = await dataContext.AthleteStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NameNormalized == nameNormalized, cancellationToken);

        if (existing is not null)
            return existing.Id;

        var created = new AthleteStatus
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameNormalized = nameNormalized,
            Abbreviation = status!.Abbreviation?.Trim(),
            Type = status.Type?.Trim(),
            ExternalId = status.Id.ToString()
        };

        await dataContext.AthleteStatuses.AddAsync(created, cancellationToken);

        try
        {
            await dataContext.SaveChangesAsync(cancellationToken);
            return created.Id;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            // A concurrent caller inserted the same status first (the unique index
            // on NameNormalized enforces this case-insensitively). Only this expected
            // conflict is handled here; any other DbUpdateException propagates.
            // Detach our losing row and return the winner's id.
            dataContext.Entry(created).State = EntityState.Detached;

            var winner = await dataContext.AthleteStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.NameNormalized == nameNormalized, cancellationToken);

            return winner?.Id;
        }
    }
}
