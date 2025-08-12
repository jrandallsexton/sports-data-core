using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class SeasonRankExtensions
    {
        public static SeasonRanking AsEntity(
            this EspnFootballSeasonTypeWeekRankingsDto dto,
            Guid seasonWeekId,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Dictionary<string, Guid> franchiseDictionary,
            Guid correlationId)
        {
            if (dto.Ref is null)
                throw new ArgumentException("Rankings DTO is missing its $ref property.");

            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            var ranking = new SeasonRanking
            {
                Id = identity.CanonicalId,
                SeasonWeekId = seasonWeekId,

                // poll meta
                ProviderPollId = dto.Id,
                PollName = dto.Name,
                PollShortName = dto.ShortName,
                PollType = dto.Type,

                // occurrence
                OccurrenceNumber = dto.Occurrence?.Number ?? 0,
                OccurrenceType = dto.Occurrence?.Type ?? string.Empty,
                OccurrenceIsLast = dto.Occurrence?.Last ?? false,
                OccurrenceValue = dto.Occurrence?.Value ?? string.Empty,
                OccurrenceDisplay = dto.Occurrence?.DisplayValue ?? string.Empty,

                // timestamps/headlines
                DateUtc = DateTime.Parse(dto.Date).ToUniversalTime(),
                LastUpdatedUtc = DateTime.Parse(dto.LastUpdated).ToUniversalTime(),
                Headline = dto.Headline,
                ShortHeadline = dto.ShortHeadline,

                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,

                ExternalIds =
                {
                    new SeasonRankingExternalId
                    {
                        Id = Guid.NewGuid(),
                        SeasonRankingId = identity.CanonicalId,
                        Value = identity.UrlHash,
                        SourceUrl = identity.CleanUrl,
                        Provider = SourceDataProvider.Espn,
                        SourceUrlHash = identity.UrlHash
                    }
                }
            };

            // Helper local to map a single row (from ranks or others)
            SeasonRankingEntry MapEntry(
                int current, int previous, double points, int fpVotes, string trend,
                string? teamRef, string rowDate, string rowLastUpdated, string? recordSummary,
                IEnumerable<EspnFootballSeasonTypeWeekRankingsRankRecordStat>? stats,
                bool isOther)
            {
                var teamHash = !string.IsNullOrWhiteSpace(teamRef)
                    ? externalRefIdentityGenerator.Generate(new Uri(teamRef)).UrlHash
                    : string.Empty;

                int? wins = null, losses = null;
                if (stats != null)
                {
                    foreach (var s in stats)
                    {
                        if (string.Equals(s.Type, "wins", StringComparison.OrdinalIgnoreCase))
                            wins = (int)s.Value;
                        else if (string.Equals(s.Type, "losses", StringComparison.OrdinalIgnoreCase))
                            losses = (int)s.Value;
                    }
                }

                var entry = new SeasonRankingEntry
                {
                    Id = Guid.NewGuid(),
                    SeasonRankingId = ranking.Id,

                    Current = current,
                    Previous = previous,
                    Points = (decimal)points,
                    FirstPlaceVotes = fpVotes,
                    Trend = trend ?? "-",
                    IsOtherReceivingVotes = isOther,

                    TeamRefUrlHash = teamHash,
                    FranchiseSeasonId = teamRef is not null ? franchiseDictionary[teamRef] : null,

                    RecordSummary = recordSummary,
                    Wins = wins,
                    Losses = losses,

                    RowDateUtc = DateTime.Parse(rowDate).ToUniversalTime(),
                    RowLastUpdatedUtc = DateTime.Parse(rowLastUpdated).ToUniversalTime(),

                    CreatedBy = correlationId,
                    CreatedUtc = DateTime.UtcNow
                };

                // Preserve arbitrary stats (optional but keeps fidelity)
                if (stats != null)
                {
                    foreach (var s in stats.Where(x => x is not null))
                    {
                        entry.Stats.Add(new SeasonRankingEntryStat
                        {
                            Id = Guid.NewGuid(),
                            Abbreviation = s.Abbreviation,
                            CreatedBy = correlationId,
                            CreatedUtc = DateTime.UtcNow,
                            Description = s.Description,
                            DisplayName = s.DisplayName,
                            DisplayValue = s.DisplayValue,
                            Name = s.Name,
                            SeasonRankingEntryId = entry.Id,
                            ShortDisplayName = s.ShortDisplayName,
                            Type = s.Type,
                            Value = (decimal)s.Value
                        });
                    }
                }

                return entry;
            }

            // Top 25
            if (dto.Ranks?.Count > 0)
            {
                foreach (var r in dto.Ranks)
                {
                    var recordStats = r.Record?.Stats ?? new List<EspnFootballSeasonTypeWeekRankingsRankRecordStat>();
                    ranking.Entries.Add(
                        MapEntry(
                            r.Current, r.Previous, r.Points, r.FirstPlaceVotes, r.Trend,
                            r.Team?.Ref?.ToCleanUrl(), r.Date, r.LastUpdated,
                            r.Record?.Summary, recordStats, isOther: false));
                }
            }

            // Others receiving votes
            if (dto.Others?.Count > 0)
            {
                foreach (var o in dto.Others)
                {
                    ranking.Entries.Add(
                        MapEntry(
                            0, o.Previous, o.Points, o.FirstPlaceVotes, o.Trend,
                            o.Team?.Ref?.ToCleanUrl(), o.Date, o.LastUpdated,
                            o.Record?.Ref?.ToString(),            // summary not expanded here; keep raw if needed later
                            null,                     // no expanded stats in "others" payload
                            isOther: true));
                }
            }

            return ranking;
        }
    }
}
