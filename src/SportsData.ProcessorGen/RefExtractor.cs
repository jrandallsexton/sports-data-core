using System.Text.Json;

namespace SportsData.ProcessorGen;

public class RefExtractor
{
    public List<string> ExtractRefs(string json)
    {
        var uniqueRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(json);
        ExtractFromElement(doc.RootElement, uniqueRefs);
        return uniqueRefs.ToList();
    }

    private static void ExtractFromElement(JsonElement element, HashSet<string> results)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("$ref") && property.Value.ValueKind == JsonValueKind.String)
                    {
                        var rawUrl = property.Value.GetString();
                        var normalized = NormalizeUrl(rawUrl);
                        results.Add(normalized);
                    }
                    else
                    {
                        ExtractFromElement(property.Value, results);
                    }
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    ExtractFromElement(item, results);
                break;
        }
    }

    private static string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        try
        {
            var uri = new Uri(url);
            return uri.GetLeftPart(UriPartial.Path); // removes query and fragment
        }
        catch
        {
            return url ?? string.Empty; // fallback to raw string
        }
    }
}