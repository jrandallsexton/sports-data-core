using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class FranchiseSeasonRankingExtensions
{
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

        var ranking = new FranchiseSeasonRanking
        {
            Id = identity.CanonicalId,
            FranchiseId = franchiseId,
            FranchiseSeasonId = franchiseSeasonId,
            SeasonWeekId = externalRefIdentityGenerator.Generate(dto.Season.Type.Week.Ref).CanonicalId,
            SeasonYear = seasonYear,
            Name = dto.Name,
            ShortName = dto.ShortName,
            Type = dto.Type,
            Headline = dto.Headline,
            ShortHeadline = dto.ShortHeadline,
            DefaultRanking = dto.DefaultRanking,
            Date = dto.Date.TryParseUtcNullable(),
            LastUpdated = dto.LastUpdated.TryParseUtcNullable(),
            Occurrence = dto.Occurrence != null
                ? new FranchiseSeasonRankingOccurrence
                {
                    Id = Guid.NewGuid(),
                    Number = dto.Occurrence.Number,
                    Type = dto.Occurrence.Type,
                    Last = dto.Occurrence.Last,
                    Value = dto.Occurrence.Value,
                    DisplayValue = dto.Occurrence.DisplayValue
                }
                : new FranchiseSeasonRankingOccurrence
                {
                    Id = Guid.NewGuid(),
                    Number = 0,
                    Type = string.Empty,
                    Last = false,
                    Value = string.Empty,
                    DisplayValue = string.Empty
                },
            Rank = dto.Rank != null
                ? new FranchiseSeasonRankingDetail
                {
                    Id = Guid.NewGuid(),
                    Current = dto.Rank.Current,
                    Previous = dto.Rank.Previous,
                    Points = dto.Rank.Points,
                    FirstPlaceVotes = dto.Rank.FirstPlaceVotes,
                    Trend = dto.Rank.Trend,
                    Date = dto.Rank.Date.TryParseUtcNullable(),
                    LastUpdated = dto.Rank.LastUpdated.TryParseUtcNullable(),
                    Record = dto.Rank.Record != null
                        ? new FranchiseSeasonRankingDetailRecord
                        {
                            Id = Guid.NewGuid(),
                            Summary = dto.Rank.Record.Summary,
                            Stats = dto.Rank.Record.Stats?.Select(s => new FranchiseSeasonRankingDetailRecordStat
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
                            }).ToList() ?? new List<FranchiseSeasonRankingDetailRecordStat>()
                        }
                        : null
                }
                : new FranchiseSeasonRankingDetail
                {
                    Id = Guid.NewGuid(),
                    Current = 0,
                    Previous = 0,
                    Points = 0,
                    FirstPlaceVotes = 0,
                    Trend = "0"
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
                    SourceUrl = identity.CleanUrl,
                    SourceUrlHash = identity.UrlHash
                }
            ],
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = correlationId
        };

        return ranking;
    }
}
