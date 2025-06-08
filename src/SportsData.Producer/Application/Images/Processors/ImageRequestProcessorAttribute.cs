using SportsData.Core.Common;

namespace SportsData.Producer.Application.Images.Processors;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ImageRequestProcessorAttribute : Attribute
{
    public SourceDataProvider Source { get; }

    public Sport Sport { get; }

    public DocumentType DocumentType { get; }

    public ImageRequestProcessorAttribute(SourceDataProvider source, Sport sport, DocumentType documentType)
    {
        Source = source;
        Sport = sport;
        DocumentType = documentType;
    }
}