namespace SportsData.Api.Application.Processors;

public interface IGenerateMatchupPreviews
{
    Task Process(GenerateMatchupPreviewsCommand command);
}