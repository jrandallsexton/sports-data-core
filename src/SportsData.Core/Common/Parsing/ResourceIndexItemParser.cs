using System;
using System.Collections.Generic;
using System.Text.Json;

namespace SportsData.Core.Common.Parsing
{
    public interface IResourceIndexItemParser
    {
        List<Uri> ExtractEmbeddedLinks(string indexItemJson);
    }

    public class ResourceIndexItemParser : IResourceIndexItemParser
    {
        public List<Uri> ExtractEmbeddedLinks(string indexItemJson)
        {
            using var jsonDoc = JsonDocument.Parse(indexItemJson);
            var refValues = ExtractRefs(jsonDoc.RootElement);

            var uris = new List<Uri>();

            foreach (var value in refValues)
            {
                if (Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var uri))
                {
                    uris.Add(uri);
                }
            }

            return uris;
        }

        List<string> ExtractRefs(JsonElement element)
        {
            var refs = new List<string>();

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (property.Name == "$ref")
                        {
                            refs.Add(property.Value.GetString()!);
                        }
                        else
                        {
                            refs.AddRange(ExtractRefs(property.Value));
                        }
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        refs.AddRange(ExtractRefs(item));
                    }
                    break;
            }

            return refs;
        }
    }
}
