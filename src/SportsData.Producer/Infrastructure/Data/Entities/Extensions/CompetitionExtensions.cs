using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class CompetitionExtensions
    {
        public static Competition AsEntity(
            this EspnEventCompetitionDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid contestId,
            Guid correlationId)
        {
            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            var entity = new Competition
            {
                Id = identity.CanonicalId,
                Attendance = dto.Attendance,
                ContestId = contestId,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                Date = DateTime.TryParse(dto.Date, out var date) ? date.ToUniversalTime() : DateTime.MinValue.ToUniversalTime(),
                DateValid = dto.DateValid,
                FormatOvertimeDisplayName = dto.Format?.Overtime?.DisplayName,
                FormatOvertimePeriods = dto.Format?.Overtime?.Periods,
                FormatOvertimeSlug = dto.Format?.Overtime?.Slug,
                FormatRegulationClock = dto.Format?.Regulation?.Clock,
                FormatRegulationDisplayName = dto.Format?.Regulation?.DisplayName,
                FormatRegulationPeriods = dto.Format?.Regulation?.Periods,
                FormatRegulationSlug = dto.Format?.Regulation?.Slug,
                HasDefensiveStats = dto.HasDefensiveStats,
                IsBoxscoreAvailable = dto.BoxscoreAvailable,
                IsBracketAvailable = dto.BracketAvailable,
                IsCommentaryAvailable = dto.CommentaryAvailable,
                IsConferenceCompetition = dto.ConferenceCompetition,
                IsConversationAvailable = dto.ConversationAvailable,
                IsDivisionCompetition = dto.DivisionCompetition,
                IsGamecastAvailable = dto.GamecastAvailable,
                IsHighlightsAvailable = dto.HighlightsAvailable,
                IsLineupAvailable = dto.LineupAvailable,
                IsLiveAvailable = dto.LiveAvailable,
                IsNeutralSite = dto.NeutralSite,
                IsOnWatchEspn = dto.OnWatchESPN,
                IsPickCenterAvailable = dto.PickcenterAvailable,
                IsPlayByPlayAvailable = dto.PlayByPlayAvailable,
                IsPossessionArrowAvailable = dto.PossessionArrowAvailable,
                IsPreviewAvailable = dto.PreviewAvailable,
                IsRecapAvailable = dto.RecapAvailable,
                IsRecent = dto.Recent,
                IsShotChartAvailable = dto.ShotChartAvailable,
                IsSummaryAvailable = dto.SummaryAvailable,
                IsTicketsAvailable = dto.TicketsAvailable,
                IsTimeoutsAvailable = dto.TimeoutsAvailable,
                IsWallClockAvailable = dto.WallclockAvailable,
                TimeValid = dto.TimeValid,
                TypeAbbreviation = dto.Type?.Abbreviation,
                TypeId = dto.Type?.Id,
                TypeName = dto.Type?.TypeName,
                TypeSlug = dto.Type?.Slug,
                TypeText = dto.Type?.Text,
                ExternalIds = new List<CompetitionExternalId>()
                {
                    new()
                    {
                        Id = identity.CanonicalId,
                        Value = identity.UrlHash,
                        Provider = SourceDataProvider.Espn,
                        SourceUrlHash = identity.UrlHash,
                        SourceUrl = identity.CleanUrl
                    }
                }
            };

            return entity;
        }
    }
}
