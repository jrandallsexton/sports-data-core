#nullable enable

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Picks;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.Clients.Notification.Dtos
{
    /// <summary>
    /// Wire contracts for the SmackBot Lab, shared by Notification's
    /// SmackAdminController (server) and <see cref="NotificationClient"/>
    /// (API-side caller) so the two cannot drift — the same pattern as
    /// SeasonContestDto shared by Producer and ContestClient.
    /// See docs/features/smackbot-lab.md.
    /// </summary>
    public class SmackPreviewPickDto
    {
        public Guid PickId { get; set; }
        public Guid ContestId { get; set; }
        public Guid LeagueId { get; set; }
        public Guid UserId { get; set; }
        public string? AwayAbbreviation { get; set; }
        public string? HomeAbbreviation { get; set; }
        public int AwayScore { get; set; }
        public int HomeScore { get; set; }
        public bool? IsCorrect { get; set; }
        public bool? PickedIsHome { get; set; }
        public double? PickedSpread { get; set; }
        public double? MarketSpread { get; set; }
        public string? LeagueName { get; set; }
        public Sport Sport { get; set; }

        /// <summary>
        /// The catalog takes the event shape; identifiers that only matter for
        /// dispatch (correlation/causation) are synthesized. PickId is REAL —
        /// deterministic phrase selection hashes it, so a preview picks the
        /// same line a live send would.
        /// </summary>
        public UserPickScored ToEvent() => new(
            UserId, null, ContestId, PickId,
            null, null, AwayAbbreviation, HomeAbbreviation,
            AwayScore, HomeScore, IsCorrect, PickedIsHome,
            PickedSpread, MarketSpread,
            LeagueId, LeagueName ?? "your league", Sport, null,
            Guid.NewGuid(), Guid.NewGuid());
    }

    public class SmackPreviewRequestDto
    {
        /// <summary>Wire voice name; unknown values preview as Standard.</summary>
        public string? Voice { get; set; }

        public List<SmackPreviewPickDto>? Picks { get; set; }
    }

    public record SmackPreviewResultDto(
        Guid PickId,
        string Situation,
        Guid? PhraseId,
        string? Text,
        bool UsedStandardFallback);

    public record SmackPhraseDto(
        Guid Id,
        string Voice,
        string Situation,
        string? Sport,
        string Text,
        bool IsActive,
        bool RequiresGamblingContent,
        int Weight,
        string? Description,
        uint RowVersion);

    public class SmackPhraseUpsertDto
    {
        public string Voice { get; set; } = "Smack";
        public string? Situation { get; set; }
        public string? Sport { get; set; }
        public string? Text { get; set; }
        public bool IsActive { get; set; } = true;
        public bool RequiresGamblingContent { get; set; }
        public int Weight { get; set; } = 1;
        public string? Description { get; set; }

        /// <summary>
        /// xmin echo for optimistic concurrency. Ignored on create; REQUIRED
        /// on update — a stale value earns 409 rather than clobbering a newer
        /// edit.
        /// </summary>
        public uint? RowVersion { get; set; }
    }

    /// <summary>
    /// A stored rating, read back so the Lab can re-hydrate stars on reload.
    /// RenderedText travels so the client can refuse to show a rating against
    /// a line that has since changed — the rating graded THAT text.
    /// </summary>
    public record SmackRatingDto(
        Guid PickId,
        string Voice,
        string Situation,
        Guid? PhraseId,
        string RenderedText,
        int Stars);

    public class SmackRatingRequestDto
    {
        public Guid PickId { get; set; }
        public Guid ContestId { get; set; }
        public Guid LeagueId { get; set; }
        public Guid PickerUserId { get; set; }
        public string Voice { get; set; } = "Smack";
        public string? Situation { get; set; }
        public Guid? PhraseId { get; set; }
        public string? RenderedText { get; set; }
        public int Stars { get; set; }

        /// <summary>Training features — serialize the preview pick payload here.</summary>
        public string? FactsJson { get; set; }
    }
}
