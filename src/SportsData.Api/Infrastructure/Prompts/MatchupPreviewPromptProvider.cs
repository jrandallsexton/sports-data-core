using System.Reflection;

namespace SportsData.Api.Infrastructure.Prompts;

public class MatchupPreviewPromptProvider
{
    public string PromptTemplate { get; }

    public MatchupPreviewPromptProvider()
    {
        var assembly = typeof(MatchupPreviewPromptProvider).Assembly;

        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("MatchupPreviewPromptTemplate.txt", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            throw new InvalidOperationException("Embedded prompt template not found: MatchupPreviewPromptTemplate.txt");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        PromptTemplate = reader.ReadToEnd();
    }
}