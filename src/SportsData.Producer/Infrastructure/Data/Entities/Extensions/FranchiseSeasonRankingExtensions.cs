using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class FranchiseSeasonRankingExtensions
{
    /// <summary>
    /// Converts an <see cref="EspnTeamSeasonRankDto"/> to a <see cref="FranchiseSeasonRanking"/>.
    /// </summary>
    /// <param name="dto">The DTO to convert.</param>
    /// <param name="externalRefIdentityGenerator">The generator for external reference identities.</param>
    /// <param name="franchiseId">The ID of the franchise.</param>
    /// <param name="franchiseSeasonId">The ID of the franchise season.</param>
    /// <param name="seasonYear">The year of the season.</param>
    /// <param name="correlationId">The correlation ID for tracking.</param>
    /// <returns>A new instance of <see cref="FranchiseSeasonRanking"/>.</returns>
    /// <exception cref="ArgumentException">Thrown if the DTO is missing its $ref property.</exception>
    public static FranchiseSeasonRanking AsEntity(
        this EspnTeamSeasonRankDto dto,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid franchiseId,
        Guid franchiseSeasonId,
        int seasonYear,
        Guid correlationId)
    {
        if (dto.Ref == null)
            throw new ArgumentException($"{nameof(EspnTeamSeasonRankDto)} is missing its $ref property.");

        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        return new FranchiseSeasonRanking
        {
            Id = identity.CanonicalId,
            FranchiseId = franchiseId,
            FranchiseSeasonId = franchiseSeasonId,
            SeasonYear = seasonYear,
            Name = dto.Name,
            ShortName = dto.ShortName,
            Type = dto.Type,
            Headline = dto.Headline,
            ShortHeadline = dto.ShortHeadline,
            Date = DateTime.Parse(dto.Date).ToUniversalTime(),
            LastUpdated = DateTime.Parse(dto.LastUpdated).ToUniversalTime(),
            DefaultRanking = dto.DefaultRanking,
            Occurrence = new FranchiseSeasonRankingOccurrence
            {
                Id = Guid.NewGuid(),
                Number = dto.Occurrence.Number,
                Type = dto.Occurrence.Type,
                Last = dto.Occurrence.Last,
                Value = dto.Occurrence.Value,
                DisplayValue = dto.Occurrence.DisplayValue
            },
            Rank = new FranchiseSeasonRankingDetail
            {
                Id = Guid.NewGuid(),
                Current = dto.Rank.Current,
                Previous = dto.Rank.Previous,
                Points = dto.Rank.Points,
                FirstPlaceVotes = dto.Rank.FirstPlaceVotes,
                Trend = dto.Rank.Trend,
                Date = DateTime.Parse(dto.Rank.Date).ToUniversalTime(),
                LastUpdated = DateTime.Parse(dto.Rank.LastUpdated).ToUniversalTime(),
                Record = new FranchiseSeasonRankingDetailRecord
                {
                    Id = Guid.NewGuid(),
                    Summary = dto.Rank.Record.Summary,
                    Stats = dto.Rank.Record.Stats.Select(s => new FranchiseSeasonRankingDetailRecordStat
                    {
                        Id = Guid.NewGuid(),
                        Name = s.Name,
                        DisplayName = s.DisplayName,
                        ShortDisplayName = s.ShortDisplayName,
                        Description = s.Description,
                        Abbreviation = s.Abbreviation,
                        Type = s.Type,
                        Value = s.Value,
                        DisplayValue = s.DisplayValue
                    }).ToList()
                }
            },
            Notes = dto.Notes?.Select(n => new FranchiseSeasonRankingNote
            {
                Id = Guid.NewGuid(),
                Text = n.Text
            }).ToList() ?? [],
            ExternalIds =
            [
                new FranchiseSeasonRankingExternalId
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    Value = identity.UrlHash,
                    SourceUrlHash = identity.UrlHash,
                    SourceUrl = identity.CleanUrl
                }
            ],
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = correlationId
        };
    }
}