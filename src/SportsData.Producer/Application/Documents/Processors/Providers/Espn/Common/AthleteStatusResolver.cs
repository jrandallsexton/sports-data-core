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

        // Invariant (culture-independent) so this .NET-side normalization can't
        // diverge from the DB's lower() for culture cases (e.g. Turkish 'I'). The
        // query-side ToLower below runs in Postgres (EF translates it to lower()),
        // matching the NameNormalized computed column; it must stay ToLower because
        // Npgsql can't translate ToLowerInvariant.
        var nameLower = name.ToLowerInvariant();

        var existing = await dataContext.AthleteStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => (x.Name ?? "").ToLower() == nameLower, cancellationToken);

        if (existing is not null)
            return existing.Id;

        var created = new AthleteStatus
        {
            Id = Guid.NewGuid(),
            Name = name,
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
            // A concurrent caller inserted the same status name first (the unique
            // index on the computed lower(Name) enforces this case-insensitively).
            // Only this expected conflict is handled here; any other DbUpdateException
            // propagates. Detach our losing row and return the winner's id.
            dataContext.Entry(created).State = EntityState.Detached;

            var winner = await dataContext.AthleteStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => (x.Name ?? "").ToLower() == nameLower, cancellationToken);

            return winner?.Id;
        }
    }
}
