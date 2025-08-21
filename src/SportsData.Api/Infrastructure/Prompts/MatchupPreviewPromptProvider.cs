namespace SportsData.Api.Infrastructure.Prompts;

public class MatchupPreviewPromptProvider
{
    public string PromptTemplate { get; }

    public MatchupPreviewPromptProvider()
    {
        // TODO: Fix the path to the prompt template file.
        //var path = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Prompts", "MatchupPreviewPromptTemplate.txt");
        var path =
            "C:\\Projects\\sports-data\\src\\SportsData.Api\\Infrastructure\\Prompts\\MatchupPreviewPromptTemplate.txt";
        PromptTemplate = File.ReadAllText(path);
    }
}