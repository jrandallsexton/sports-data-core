using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class SeasonRankExtensions
    {
        public static SeasonPollWeek AsEntity(
            this EspnFootballSeasonTypeWeekRankingsDto dto,
            Guid seasonPollId,
            Guid? seasonWeekId,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Dictionary<string, Guid> franchiseDictionary,
            Guid correlationId)
        {
            if (dto.Ref is null)
                throw new ArgumentException("Rankings DTO is missing its $ref property.");

            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            var ranking = new SeasonPollWeek
            {
                Id = identity.CanonicalId,
                SeasonPollId = seasonPollId,
                SeasonWeekId = seasonWeekId,

                // poll meta

                // occurrence
                OccurrenceNumber = dto.Occurrence?.Number ?? 0,
                OccurrenceType = dto.Occurrence?.Type ?? string.Empty,
                OccurrenceIsLast = dto.Occurrence?.Last ?? false,
                OccurrenceValue = dto.Occurrence?.Value ?? string.Empty,
                OccurrenceDisplay = dto.Occurrence?.DisplayValue ?? string.Empty,

                // timestamps/headlines
                DateUtc = dto.Date.TryParseUtcNullable(),
                LastUpdatedUtc = dto.LastUpdated.TryParseUtcNullable(),
                Name = dto.Name,
                ShortName = dto.ShortName,
                Headline = dto.Headline,
                ShortHeadline = dto.ShortHeadline,
                Type = dto.Type,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,

                ExternalIds =
                {
                    new SeasonPollWeekExternalId
                    {
                        Id = Guid.NewGuid(),
                        SeasonPollWeekId = identity.CanonicalId,
                        Value = identity.UrlHash,
                        SourceUrl = identity.CleanUrl,
                        Provider = SourceDataProvider.Espn,
                        SourceUrlHash = identity.UrlHash
                    }
                }
            };

            // Helper local to map a single row (from ranks or others)
            SeasonPollWeekEntry MapEntry(
                int current, int previous, double points, int fpVotes, string trend,
                string teamRef, string rowDate, string rowLastUpdated, string? recordSummary,
                IEnumerable<EspnFootballSeasonTypeWeekRankingsRankRecordStat>? stats,
                bool isOther, bool isDroppedOut)
            {
                var teamHash = !string.IsNullOrWhiteSpace(teamRef)
                    ? externalRefIdentityGenerator.Generate(new Uri(teamRef)).UrlHash
                    : string.Empty;

                int? wins = null, losses = null;
                if (stats != null)
                {
                    foreach (var s in stats)
                    {
                        if (string.Equals(s.Type, "wins", StringComparison.OrdinalIgnoreCase) && s.Value is not null)
                            wins = (int)s.Value.Value;
                        else if (string.Equals(s.Type, "losses", StringComparison.OrdinalIgnoreCase) && s.Value is not null)
                            losses = (int)s.Value.Value;
                    }
                }

                var entry = new SeasonPollWeekEntry
                {
                    Id = Guid.NewGuid(),
                    CreatedBy = correlationId,
                    CreatedUtc = DateTime.UtcNow,
                    Current = current,
                    FirstPlaceVotes = fpVotes,
                    FranchiseSeasonId = franchiseDictionary[teamRef],
                    IsOtherReceivingVotes = isOther,
                    IsDroppedOut = isDroppedOut,
                    Losses = losses,
                    Points = points,
                    Previous = previous,
                    RecordSummary = recordSummary,
                    RowDateUtc = DateTime.Parse(rowDate).ToUniversalTime(),
                    RowLastUpdatedUtc = DateTime.Parse(rowLastUpdated).ToUniversalTime(),
                    SeasonPollWeekId = ranking.Id,
                    SourceList = isOther ? "others" : "ranks",
                    Trend = trend ?? "-",
                    Wins = wins,
                };

                // Preserve arbitrary stats (optional but keeps fidelity)
                if (stats != null)
                {
                    foreach (var s in stats.Where(x => x is not null))
                    {
                        entry.Stats.Add(new SeasonPollWeekEntryStat
                        {
                            Id = Guid.NewGuid(),
                            Abbreviation = s.Abbreviation,
                            CreatedBy = correlationId,
                            CreatedUtc = DateTime.UtcNow,
                            Description = s.Description,
                            DisplayName = s.DisplayName,
                            DisplayValue = s.DisplayValue,
                            Name = s.Name,
                            SeasonPollWeekEntryId = entry.Id,
                            ShortDisplayName = s.ShortDisplayName,
                            Type = s.Type,
                            Value = (decimal?)s.Value
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
                            r.Team.Ref.ToCleanUrl(), r.Date, r.LastUpdated,
                            r.Record?.Summary, recordStats, isOther: false, isDroppedOut: false));
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
                            o.Team.Ref.ToCleanUrl(), o.Date, o.LastUpdated,
                            o.Record?.Ref?.ToString(),            // summary not expanded here; keep raw if needed later
                            null,                     // no expanded stats in "others" payload
                            isOther: true,
                            isDroppedOut: false));
                }
            }

            // Teams that dropped from the poll
            if (dto.DroppedOut?.Count > 0)
            {
                foreach (var d in dto.DroppedOut)
                {
                    ranking.Entries.Add(
                        MapEntry(
                            0, d.Previous, d.Points, 0, d.Trend ?? "-",
                            d.Team.Ref.ToCleanUrl(), d.Date, d.LastUpdated,
                            null, null,
                            isOther: false, isDroppedOut: true));
                }
            }

            return ranking;
        }
    }
}
