using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class SeasonPhaseExtensions
{
    public static SeasonPhase AsEntity(
        this EspnFootballSeasonTypeDto dto,
        Guid seasonId,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid correlationId)
    {
        if (dto.Ref == null)
            throw new ArgumentException("Season Type DTO is missing its $ref property.");

        // Generate canonical ID and hash from the season type ref
        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        var phase = new SeasonPhase
        {
            Id = identity.CanonicalId,
            SeasonId = seasonId,
            TypeCode = dto.Type,
            Name = dto.Name,
            Abbreviation = dto.Abbreviation,
            Slug = dto.Slug,
            Year = dto.Year,
            StartDate = DateTime.Parse(dto.StartDate).ToUniversalTime(),
            EndDate = DateTime.Parse(dto.EndDate).ToUniversalTime(),
            HasGroups = dto.HasGroups,
            HasStandings = dto.HasStandings,
            HasLegs = dto.HasLegs,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow,
            ExternalIds =
            {
                new SeasonPhaseExternalId
                {
                    Id = Guid.NewGuid(),
                    Value = identity.UrlHash,
                    SourceUrl = identity.CleanUrl,
                    Provider = SourceDataProvider.Espn,
                    SourceUrlHash = identity.UrlHash
                }
            }
        };
        return phase;
    }
}