using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Picks;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Infrastructure.Auth;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Controllers
{
    /// <summary>
    /// SmackBot Lab's server side: preview what the voice would send for real
    /// scored picks, manage the phrase catalog, and record 0–4 star ratings as
    /// training data. API relays these server-to-server (the browser never
    /// holds this service's key), same access model as
    /// <see cref="BackfillController"/>. See docs/features/smackbot-lab.md.
    /// </summary>
    [ApiController]
    [Route("admin/smack")]
    [ApiKeyAuth]
    public class SmackAdminController : ControllerBase
    {
        private readonly ILogger<SmackAdminController> _logger;
        private readonly AppDataContext _dataContext;
        private readonly ISmackPhraseCatalog _catalog;
        private readonly IDateTimeProvider _dateTimeProvider;

        public SmackAdminController(
            ILogger<SmackAdminController> logger,
            AppDataContext dataContext,
            ISmackPhraseCatalog catalog,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _catalog = catalog;
            _dateTimeProvider = dateTimeProvider;
        }

        // ─── Preview ──────────────────────────────────────────────────────

        /// <summary>
        /// Runs the send path's exact resolution (situation → deterministic
        /// phrase → formatting) for each pick WITHOUT dispatching. Fidelity is
        /// the contract: allowGamblingContent derives from the pick's ATS-ness
        /// exactly as the consumer does, so a rating grades precisely what a
        /// user would have received.
        /// </summary>
        [HttpPost("preview")]
        public async Task<ActionResult<List<SmackPreviewResultDto>>> Preview(
            [FromBody] SmackPreviewRequestDto request,
            CancellationToken cancellationToken)
        {
            if (request?.Picks is not { Count: > 0 })
                return BadRequest("At least one pick is required.");

            if (request.Picks.Count > 500)
                return BadRequest("At most 500 picks per preview batch.");

            // Wire string → enum with the same Standard fallback the
            // preferences projection applies.
            var voice = Enum.TryParse<NotificationVoice>(request.Voice, ignoreCase: false, out var parsed)
                ? parsed
                : NotificationVoice.Standard;

            var results = new List<SmackPreviewResultDto>(request.Picks.Count);
            foreach (var pick in request.Picks)
            {
                var msg = pick.ToEvent();

                // Same derivation as UserPickScoredConsumer: knowing the line
                // and being allowed to talk about it are different things.
                var allowGamblingContent = msg.PickedSpread is not null;

                var resolution = await _catalog.ResolveDetailedAsync(
                    msg, voice, allowGamblingContent, cancellationToken);

                results.Add(new SmackPreviewResultDto(
                    pick.PickId,
                    resolution.Situation.ToString(),
                    resolution.PhraseId,
                    resolution.Text,
                    resolution.UsedStandardFallback));
            }

            return Ok(results);
        }

        // ─── Phrases ──────────────────────────────────────────────────────

        /// <summary>Full catalog, inactive rows included — the Lab shows both.</summary>
        [HttpGet("phrases")]
        public async Task<ActionResult<List<SmackPhraseDto>>> GetPhrases(CancellationToken cancellationToken)
        {
            var phrases = await _dataContext.SmackPhrases
                .AsNoTracking()
                .OrderBy(p => p.Situation).ThenBy(p => p.CreatedUtc)
                .Select(p => new SmackPhraseDto(
                    p.Id, p.Voice.ToString(), p.Situation.ToString(),
                    p.Sport.HasValue ? p.Sport.Value.ToString() : null,
                    p.Text, p.IsActive, p.RequiresGamblingContent, p.Weight, p.Description))
                .ToListAsync(cancellationToken);

            return Ok(phrases);
        }

        [HttpPost("phrases")]
        public async Task<ActionResult<SmackPhraseDto>> CreatePhrase(
            [FromBody] SmackPhraseUpsertDto request,
            CancellationToken cancellationToken)
        {
            var validationError = ValidateUpsert(request, out var voice, out var situation, out var sport);
            if (validationError is not null)
                return BadRequest(validationError);

            var entity = new SmackPhrase
            {
                Id = Guid.NewGuid(),
                Voice = voice,
                Situation = situation,
                Sport = sport,
                Text = request.Text.Trim(),
                IsActive = request.IsActive,
                RequiresGamblingContent = request.RequiresGamblingContent,
                Weight = request.Weight,
                Description = request.Description,
                CreatedUtc = _dateTimeProvider.UtcNow(),
                CreatedBy = Guid.Empty // operator-authored via admin key; no user identity on this path
            };

            _dataContext.SmackPhrases.Add(entity);
            await _dataContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "SmackPhrase created. PhraseId={PhraseId}, Situation={Situation}, Voice={Voice}",
                entity.Id, entity.Situation, entity.Voice);

            return Ok(ToDto(entity));
        }

        [HttpPut("phrases/{id:guid}")]
        public async Task<ActionResult<SmackPhraseDto>> UpdatePhrase(
            [FromRoute] Guid id,
            [FromBody] SmackPhraseUpsertDto request,
            CancellationToken cancellationToken)
        {
            var validationError = ValidateUpsert(request, out var voice, out var situation, out var sport);
            if (validationError is not null)
                return BadRequest(validationError);

            var entity = await _dataContext.SmackPhrases
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (entity is null)
                return NotFound();

            entity.Voice = voice;
            entity.Situation = situation;
            entity.Sport = sport;
            entity.Text = request.Text.Trim();
            entity.IsActive = request.IsActive;
            entity.RequiresGamblingContent = request.RequiresGamblingContent;
            entity.Weight = request.Weight;
            entity.Description = request.Description;
            entity.ModifiedUtc = _dateTimeProvider.UtcNow();

            await _dataContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("SmackPhrase updated. PhraseId={PhraseId}", id);

            return Ok(ToDto(entity));
        }

        // ─── Ratings ──────────────────────────────────────────────────────

        /// <summary>
        /// Upserts on (PickId, Voice): re-rating after a phrase edit
        /// overwrites rather than duplicates, so the training set holds one
        /// current opinion per previewed pick.
        /// </summary>
        [HttpPost("ratings")]
        public async Task<IActionResult> RatePreview(
            [FromBody] SmackRatingRequestDto request,
            CancellationToken cancellationToken)
        {
            if (request is null)
                return BadRequest("A rating payload is required.");

            if (request.Stars is < 0 or > 4)
                return BadRequest("Stars must be between 0 and 4.");

            if (string.IsNullOrWhiteSpace(request.RenderedText))
                return BadRequest("RenderedText is required — it is the string being rated.");

            var voice = Enum.TryParse<NotificationVoice>(request.Voice, ignoreCase: false, out var parsedVoice)
                ? parsedVoice
                : NotificationVoice.Standard;

            if (!Enum.TryParse<PickSituation>(request.Situation, ignoreCase: false, out var situation))
                return BadRequest($"Unknown situation '{request.Situation}'.");

            var now = _dateTimeProvider.UtcNow();

            var existing = await _dataContext.SmackPreviewRatings
                .FirstOrDefaultAsync(r => r.PickId == request.PickId && r.Voice == voice, cancellationToken);

            if (existing is null)
            {
                _dataContext.SmackPreviewRatings.Add(new SmackPreviewRating
                {
                    Id = Guid.NewGuid(),
                    PickId = request.PickId,
                    ContestId = request.ContestId,
                    LeagueId = request.LeagueId,
                    PickerUserId = request.PickerUserId,
                    Voice = voice,
                    Situation = situation,
                    PhraseId = request.PhraseId,
                    RenderedText = request.RenderedText,
                    Stars = request.Stars,
                    FactsJson = request.FactsJson ?? "{}",
                    CreatedUtc = now,
                    CreatedBy = Guid.Empty
                });
            }
            else
            {
                existing.Situation = situation;
                existing.PhraseId = request.PhraseId;
                existing.RenderedText = request.RenderedText;
                existing.Stars = request.Stars;
                existing.FactsJson = request.FactsJson ?? existing.FactsJson;
                existing.ModifiedUtc = now;
            }

            try
            {
                await _dataContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Race between two concurrent first-time ratings of the same
                // pick — vanishingly rare on an admin surface, and the loser's
                // opinion is equally valid: re-run as an update.
                _dataContext.ChangeTracker.Clear();
                var winner = await _dataContext.SmackPreviewRatings
                    .FirstAsync(r => r.PickId == request.PickId && r.Voice == voice, cancellationToken);
                winner.Situation = situation;
                winner.PhraseId = request.PhraseId;
                winner.RenderedText = request.RenderedText;
                winner.Stars = request.Stars;
                winner.FactsJson = request.FactsJson ?? winner.FactsJson;
                winner.ModifiedUtc = now;
                await _dataContext.SaveChangesAsync(cancellationToken);
            }

            return Ok();
        }

        // ─── Helpers ──────────────────────────────────────────────────────

        private static string ValidateUpsert(
            SmackPhraseUpsertDto request,
            out NotificationVoice voice,
            out PickSituation situation,
            out Sport? sport)
        {
            voice = default;
            situation = default;
            sport = null;

            if (request is null)
                return "A phrase payload is required.";

            if (string.IsNullOrWhiteSpace(request.Text))
                return "Text is required.";

            if (request.Text.Length > 300)
                return "Text must be 300 characters or fewer.";

            if (request.Weight < 1)
                return "Weight must be at least 1.";

            if (!Enum.TryParse(request.Voice, ignoreCase: false, out voice))
                return $"Unknown voice '{request.Voice}'.";

            if (!Enum.TryParse(request.Situation, ignoreCase: false, out situation))
                return $"Unknown situation '{request.Situation}'.";

            if (request.Sport is not null)
            {
                if (!Enum.TryParse<Sport>(request.Sport, ignoreCase: false, out var parsedSport))
                    return $"Unknown sport '{request.Sport}'.";
                sport = parsedSport;
            }

            return null;
        }

        private static SmackPhraseDto ToDto(SmackPhrase p) => new(
            p.Id, p.Voice.ToString(), p.Situation.ToString(), p.Sport?.ToString(),
            p.Text, p.IsActive, p.RequiresGamblingContent, p.Weight, p.Description);

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
            => ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
    }

    // ─── Wire contracts ───────────────────────────────────────────────────

    /// <summary>
    /// The pick facts the resolver and formatter need — the UserPickScored
    /// shape minus event plumbing. API composes these from canonical data
    /// (the same derivation PickScoringProcessor performs for live sends).
    /// </summary>
    public class SmackPreviewPickDto
    {
        public Guid PickId { get; set; }
        public Guid ContestId { get; set; }
        public Guid LeagueId { get; set; }
        public Guid UserId { get; set; }
        public string AwayAbbreviation { get; set; }
        public string HomeAbbreviation { get; set; }
        public int AwayScore { get; set; }
        public int HomeScore { get; set; }
        public bool? IsCorrect { get; set; }
        public bool? PickedIsHome { get; set; }
        public double? PickedSpread { get; set; }
        public double? MarketSpread { get; set; }
        public string LeagueName { get; set; }
        public Sport Sport { get; set; }

        /// <summary>
        /// The catalog takes the event shape; identifiers that only matter for
        /// dispatch (correlation/causation) are synthesized. PickId is REAL —
        /// deterministic phrase selection hashes it, so the preview picks the
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
        public string Voice { get; set; }

        public List<SmackPreviewPickDto> Picks { get; set; }
    }

    public record SmackPreviewResultDto(
        Guid PickId,
        string Situation,
        Guid? PhraseId,
        string Text,
        bool UsedStandardFallback);

    public record SmackPhraseDto(
        Guid Id,
        string Voice,
        string Situation,
        string Sport,
        string Text,
        bool IsActive,
        bool RequiresGamblingContent,
        int Weight,
        string Description);

    public class SmackPhraseUpsertDto
    {
        public string Voice { get; set; } = "Smack";
        public string Situation { get; set; }
        public string Sport { get; set; }
        public string Text { get; set; }
        public bool IsActive { get; set; } = true;
        public bool RequiresGamblingContent { get; set; }
        public int Weight { get; set; } = 1;
        public string Description { get; set; }
    }

    public class SmackRatingRequestDto
    {
        public Guid PickId { get; set; }
        public Guid ContestId { get; set; }
        public Guid LeagueId { get; set; }
        public Guid PickerUserId { get; set; }
        public string Voice { get; set; } = "Smack";
        public string Situation { get; set; }
        public Guid? PhraseId { get; set; }
        public string RenderedText { get; set; }
        public int Stars { get; set; }

        /// <summary>Training features — serialize the preview pick payload here.</summary>
        public string FactsJson { get; set; }
    }
}
