using SportsData.Api.Infrastructure.Data.Entities;

using System.Text.Json;

namespace SportsData.Api.Application.MiniGames.Commands.SubmitMiniGameResults;

/// <summary>
/// A batch of finished games, because an offline device may have several
/// queued. Each item carries its own client-generated <see cref="MiniGameResultSubmission.Id"/>.
/// </summary>
public record SubmitMiniGameResultsCommand
{
    public List<MiniGameResultSubmission> Results { get; init; } = [];
}

public record MiniGameResultSubmission
{
    public Guid Id { get; init; }

    public string GameKey { get; init; } = null!;

    public MiniGameOutcome Outcome { get; init; }

    public int Score { get; init; }

    public int OpponentScore { get; init; }

    public int DurationSeconds { get; init; }

    public string GameVersion { get; init; } = null!;

    public string? Seed { get; init; }

    public JsonElement? Detail { get; init; }

    public DateTime PlayedUtc { get; init; }
}

/// <summary>
/// Every submitted id lands in exactly one list. The client drops all three
/// from its queue: accepted and duplicate are already stored, and a rejected
/// item would only fail again.
/// </summary>
public record SubmitMiniGameResultsResponse
{
    public List<Guid> Accepted { get; init; } = [];

    public List<Guid> Duplicate { get; init; } = [];

    public List<Guid> Rejected { get; init; } = [];
}
