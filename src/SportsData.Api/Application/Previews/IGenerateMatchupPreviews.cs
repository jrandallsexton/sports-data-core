namespace SportsData.Api.Application.Previews;

public interface IGenerateMatchupPreviews
{
    Task Process(GenerateMatchupPreviewsCommand command);
}