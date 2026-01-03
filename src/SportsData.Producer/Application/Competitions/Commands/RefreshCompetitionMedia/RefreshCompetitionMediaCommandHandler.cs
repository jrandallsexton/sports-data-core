using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.YouTube;
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

    public RefreshCompetitionMediaCommandHandler(
        TeamSportDataContext dataContext,
        IProvideYouTube youTubeProvider)
    {
        _dataContext = dataContext;
        _youTubeProvider = youTubeProvider;
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

        if (youTubeResults != null && youTubeResults.Items.Any())
        {
            foreach (var mediaEntity in youTubeResults.Items.Select(ytResult => new CompetitionMedia()
                     {
                         Id = Guid.NewGuid(),
                         AwayFranchiseSeasonId = competition.Contest.AwayTeamFranchiseSeasonId,
                         ChannelId = ytResult.Snippet.ChannelId,
                         ChannelTitle = ytResult.Snippet.ChannelTitle,
                         CompetitionId = competition.Id,
                         CreatedBy = Guid.Empty,
                         CreatedUtc = DateTime.UtcNow,
                         Description = ytResult.Snippet.Description,
                         HomeFranchiseSeasonId = competition.Contest.HomeTeamFranchiseSeasonId,
                         PublishedUtc = ytResult.Snippet.PublishedAt,
                         ThumbnailDefaultHeight = ytResult.Snippet.Thumbnails.Default.Height,
                         ThumbnailDefaultUrl = ytResult.Snippet.Thumbnails.Default.Url,
                         ThumbnailDefaultWidth = ytResult.Snippet.Thumbnails.Default.Width,
                         ThumbnailHighHeight = ytResult.Snippet.Thumbnails.High.Height,
                         ThumbnailHighUrl = ytResult.Snippet.Thumbnails.High.Url,
                         ThumbnailHighWidth = ytResult.Snippet.Thumbnails.High.Width,
                         ThumbnailMediumHeight = ytResult.Snippet.Thumbnails.Medium.Height,
                         ThumbnailMediumUrl = ytResult.Snippet.Thumbnails.Medium.Url,
                         ThumbnailMediumWidth = ytResult.Snippet.Thumbnails.Medium.Width,
                         Title = ytResult.Snippet.Title,
                         VideoId = ytResult.Id.VideoId
                     }))
            {
                _dataContext.CompetitionMedia.Add(mediaEntity);
            }

            await _dataContext.SaveChangesAsync(cancellationToken);
        }

        return new Success<Guid>(command.CompetitionId);
    }
}
