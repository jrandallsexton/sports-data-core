using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Picks;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Dispatching;

public interface ISmackPhraseCatalog
{
    /// <summary>
    /// Resolves the line for a scored pick, or null when the catalog can't
    /// serve this slot — the caller then uses the standard copy.
    /// </summary>
    Task<string> TryResolveAsync(
        UserPickScored msg,
        NotificationVoice voice,
        bool allowGamblingContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The full resolution: situation, chosen phrase, rendered text, and
    /// whether the send path would fall back to standard copy. This is what
    /// the SmackBot Lab previews — it MUST stay the same code path as
    /// <see cref="TryResolveAsync"/> so a rating grades exactly what a user
    /// would have received. See docs/features/smackbot-lab.md.
    /// </summary>
    Task<SmackResolution> ResolveDetailedAsync(
        UserPickScored msg,
        NotificationVoice voice,
        bool allowGamblingContent,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a catalog resolution. <see cref="Text"/> null (and
/// <see cref="UsedStandardFallback"/> true) means the dispatch would send
/// the standard copy — an empty catalog, an unfilled slot, or a gambling
/// filter that emptied the bucket. Situation is always resolved, even for
/// the Standard voice, so the Lab can label every pick.
/// </summary>
public record SmackResolution(
    PickSituation Situation,
    Guid? PhraseId,
    string Text,
    bool UsedStandardFallback);

/// <summary>
/// Picks a phrase for a scored pick. See docs/features/smackbot-voice.md.
///
/// <para>
/// Returning null is a FIRST-CLASS outcome, not an error: an empty catalog,
/// a slot with no active rows, or a gambling filter that empties a bucket all
/// fall back to the standard copy. That's what lets the schema ship before
/// the content exists.
/// </para>
/// </summary>
public class SmackPhraseCatalog : ISmackPhraseCatalog
{
    private readonly AppDataContext _dataContext;
    private readonly ILogger<SmackPhraseCatalog> _logger;

    public SmackPhraseCatalog(AppDataContext dataContext, ILogger<SmackPhraseCatalog> logger)
    {
        _dataContext = dataContext;
        _logger = logger;
    }

    public async Task<string> TryResolveAsync(
        UserPickScored msg,
        NotificationVoice voice,
        bool allowGamblingContent,
        CancellationToken cancellationToken = default)
        => (await ResolveDetailedAsync(msg, voice, allowGamblingContent, cancellationToken)).Text;

    public async Task<SmackResolution> ResolveDetailedAsync(
        UserPickScored msg,
        NotificationVoice voice,
        bool allowGamblingContent,
        CancellationToken cancellationToken = default)
    {
        // Situation resolves for every voice — the Lab labels Standard-voice
        // previews too — but only a non-Standard voice consults the catalog.
        var situation = PickSituationResolver.Resolve(msg);

        if (voice == NotificationVoice.Standard)
            return new SmackResolution(situation, null, null, UsedStandardFallback: true);

        // Projected to a DTO in the query rather than materializing entities
        // (house rule); selection needs only these four fields.
        var candidates = await _dataContext.SmackPhrases
            .AsNoTracking()
            .Where(p => p.Voice == voice
                        && p.Situation == situation
                        && p.IsActive
                        && (p.Sport == null || p.Sport == msg.Sport)
                        && (allowGamblingContent || !p.RequiresGamblingContent))
            .Select(p => new PhraseCandidate(p.Id, p.Text, p.Weight, p.Sport))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            // Expected while the catalog is being filled; the caller degrades
            // to standard copy. Logged at Information so an operator can see
            // which slots still need lines without it reading as a fault.
            _logger.LogInformation(
                "No {Voice} phrase for situation {Situation} (Sport={Sport}, GamblingAllowed={GamblingAllowed}); using standard copy.",
                voice, situation, msg.Sport, allowGamblingContent);
            return new SmackResolution(situation, null, null, UsedStandardFallback: true);
        }

        // Sport precedence, mirroring Prompt: a sport-specific line outranks
        // an any-sport one. Applied in memory over the handful of rows the
        // slot returns rather than as a second query.
        var sportSpecific = candidates.Where(p => p.Sport is not null).ToList();
        var pool = sportSpecific.Count > 0 ? sportSpecific : candidates;

        var chosen = SelectDeterministic(pool, msg.PickId);

        return new SmackResolution(
            situation,
            chosen.Id,
            SmackPhraseFormatter.Format(chosen.Text, msg),
            UsedStandardFallback: false);
    }

    /// <summary>
    /// Chooses by hashing the pick id rather than sampling a random number:
    /// stable under redelivery, trivially unit-testable, and still varied
    /// across picks and users. Weight is honoured by scaling each row's slice
    /// of the hash space rather than by materializing duplicates.
    /// </summary>
    internal static PhraseCandidate SelectDeterministic(IReadOnlyList<PhraseCandidate> pool, Guid pickId)
    {
        // Order defensively: the database gives no ordering guarantee, and an
        // unstable order would make the selection non-deterministic despite
        // the stable hash.
        var ordered = pool.OrderBy(p => p.Id).ToList();

        var totalWeight = ordered.Sum(p => Math.Max(1, p.Weight));
        var bucket = (int)(StableHash(pickId) % (uint)totalWeight);

        foreach (var phrase in ordered)
        {
            bucket -= Math.Max(1, phrase.Weight);
            if (bucket < 0)
                return phrase;
        }

        return ordered[^1]; // unreachable while totalWeight is computed above
    }

    /// <summary>
    /// The slice of a phrase row selection actually needs. Keeps the read off
    /// the entity so the query projects rather than materializes.
    /// </summary>
    internal record PhraseCandidate(Guid Id, string Text, int Weight, Sport? Sport);

    /// <summary>
    /// GetHashCode is randomized per process, which would make selection
    /// differ between pods. MD5 over the id bytes is stable everywhere —
    /// it's a bucket chooser, not a security primitive.
    /// </summary>
    private static uint StableHash(Guid value)
    {
        var hash = MD5.HashData(value.ToByteArray());
        return BitConverter.ToUInt32(hash, 0);
    }
}

/// <summary>
/// Resolves <c>{Token}</c> placeholders in phrase text. Unknown tokens are
/// left as-is rather than throwing — a typo in an operator-authored line
/// should look odd, not drop the notification.
/// </summary>
public static class SmackPhraseFormatter
{
    public static string Format(string text, UserPickScored msg)
    {
        var builder = new StringBuilder(text);

        // Margin and league are attribution-free — |away - home| reads the
        // same from either side — so they always resolve.
        builder
            .Replace("{Margin}", Math.Abs(msg.HomeScore - msg.AwayScore).ToString())
            .Replace("{League}", msg.LeagueName ?? "your league");

        if (msg.PickedIsHome is { } pickedIsHome)
        {
            var picked = (pickedIsHome ? msg.HomeAbbreviation : msg.AwayAbbreviation) ?? "your team";
            var opponent = (pickedIsHome ? msg.AwayAbbreviation : msg.HomeAbbreviation) ?? "the other guys";
            var pickedScore = pickedIsHome ? msg.HomeScore : msg.AwayScore;
            var opponentScore = pickedIsHome ? msg.AwayScore : msg.HomeScore;

            builder
                .Replace("{Team}", picked)
                .Replace("{Opponent}", opponent)
                .Replace("{Score}", pickedScore.ToString())
                .Replace("{OpponentScore}", opponentScore.ToString());
        }
        else
        {
            // Unresolved picked side (Over/Under, or an unmatchable pick).
            // Defaulting to a side would describe the AWAY team as though it
            // were the user's pick — a confident lie. Name the pick neutrally
            // and leave the per-side SCORES unresolved, since attributing them
            // is exactly what we cannot do. Such picks only ever land in the
            // generic buckets, whose lines must avoid score tokens.
            builder
                .Replace("{Team}", "your pick")
                .Replace("{Opponent}", "the other side");
        }

        // Absolute value: lines read "a 14-point dog" / "favoured by 14", so
        // the sign is carried by the wording, not the number. Falls back to
        // MarketSpread so a (non-gambling-gated) line using the token still
        // resolves for straight-up picks.
        if ((msg.PickedSpread ?? msg.MarketSpread) is { } spread)
            builder.Replace("{Line}", Math.Abs(spread).ToString("0.#"));

        return builder.ToString();
    }
}
