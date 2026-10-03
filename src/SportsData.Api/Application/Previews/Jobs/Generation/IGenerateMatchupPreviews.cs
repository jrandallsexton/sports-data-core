using SportsData.Api.Application.Previews.Commands.GenerateMatchupPreviews;

namespace SportsData.Api.Application.Previews.Jobs.Generation;

public interface IGenerateMatchupPreviews
{
    Task Process(GenerateMatchupPreviewsCommand command);
}