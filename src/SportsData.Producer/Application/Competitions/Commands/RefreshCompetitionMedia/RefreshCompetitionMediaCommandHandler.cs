using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.YouTube;
using SportsData.Core.Infrastructure.Clients.YouTube.Dtos;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMedia;

public interface IRefreshCompetitionMediaCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        RefreshCompetitionMediaCommand command,
        CancellationToken cancellationToken = default);
}

public class RefreshCompetitionMediaCommandHandler : IRefreshCompetitionMediaCommandHandler
{
    private readonly TeamSportDataContext _dataContext;
    private readonly IProvideYouTube _youTubeProvider;
    private readonly ILogger<RefreshCompetitionMediaCommandHandler> _logger;

    public RefreshCompetitionMediaCommandHandler(
        TeamSportDataContext dataContext,
        IProvideYouTube youTubeProvider,
        ILogger<RefreshCompetitionMediaCommandHandler> logger)
    {
        _dataContext = dataContext;
        _youTubeProvider = youTubeProvider;
        _logger = logger;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        RefreshCompetitionMediaCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.RemoveExisting)
        {
            await _dataContext.CompetitionMedia
                .Where(cm => cm.CompetitionId == command.CompetitionId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        var competition = await _dataContext.Competitions
            .Include(x => x.Contest)
            .FirstOrDefaultAsync(c => c.Id == command.CompetitionId, cancellationToken);

        if (competition is null)
        {
            return new Failure<Guid>(
                command.CompetitionId,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.CompetitionId), "Competition not found")]
            );
        }

        var searchTerm = $"{competition.Contest.Name} {competition.Contest.SeasonYear} Highlights";

        var youTubeResults = await _youTubeProvider.Search(searchTerm);

        if (youTubeResults?.Items != null && youTubeResults.Items.Any())
        {
            foreach (var ytResult in youTubeResults.Items)
            {
                var mediaEntity = TryMapYouTubeResultToCompetitionMedia(
                    ytResult,
                    competition.Id,
                    competition.Contest.AwayTeamFranchiseSeasonId,
                    competition.Contest.HomeTeamFranchiseSeasonId);

                if (mediaEntity is not null)
                {
                    _dataContext.CompetitionMedia.Add(mediaEntity);
                }
                else
                {
                    _logger.LogWarning(
                        "Skipped YouTube result for competition {CompetitionId} due to missing required fields. VideoId: {VideoId}",
                        command.CompetitionId,
                        ytResult?.Id?.VideoId ?? "unknown");
                }
            }

            await _dataContext.SaveChangesAsync(cancellationToken);
        }

        return new Success<Guid>(command.CompetitionId);
    }

    private CompetitionMedia? TryMapYouTubeResultToCompetitionMedia(
        Item? ytResult,
        Guid competitionId,
        Guid awayFranchiseSeasonId,
        Guid homeFranchiseSeasonId)
    {
        // Essential fields validation
        if (ytResult?.Id?.VideoId is null || ytResult?.Snippet is null)
        {
            return null;
        }

        return new CompetitionMedia
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            AwayFranchiseSeasonId = awayFranchiseSeasonId,
            HomeFranchiseSeasonId = homeFranchiseSeasonId,
            VideoId = ytResult.Id.VideoId,
            CreatedBy = Guid.Empty,
            CreatedUtc = DateTime.UtcNow,

            // Optional fields with null-coalescing defaults
            ChannelId = ytResult.Snippet.ChannelId ?? string.Empty,
            ChannelTitle = ytResult.Snippet.ChannelTitle ?? string.Empty,
            Description = ytResult.Snippet.Description ?? string.Empty,
            Title = ytResult.Snippet.Title ?? string.Empty,
            PublishedUtc = ytResult.Snippet.PublishedAt,

            // Thumbnail - Default
            ThumbnailDefaultUrl = ytResult.Snippet.Thumbnails?.Default?.Url ?? string.Empty,
            ThumbnailDefaultHeight = ytResult.Snippet.Thumbnails?.Default?.Height ?? 0,
            ThumbnailDefaultWidth = ytResult.Snippet.Thumbnails?.Default?.Width ?? 0,

            // Thumbnail - Medium
            ThumbnailMediumUrl = ytResult.Snippet.Thumbnails?.Medium?.Url ?? string.Empty,
            ThumbnailMediumHeight = ytResult.Snippet.Thumbnails?.Medium?.Height ?? 0,
            ThumbnailMediumWidth = ytResult.Snippet.Thumbnails?.Medium?.Width ?? 0,

            // Thumbnail - High
            ThumbnailHighUrl = ytResult.Snippet.Thumbnails?.High?.Url ?? string.Empty,
            ThumbnailHighHeight = ytResult.Snippet.Thumbnails?.High?.Height ?? 0,
            ThumbnailHighWidth = ytResult.Snippet.Thumbnails?.High?.Width ?? 0
        };
    }
}
