namespace SportsData.Api.Application.Processors;

public class GenerateMatchupPreviewsCommand
{
    public Guid ContestId { get; set; }

    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}