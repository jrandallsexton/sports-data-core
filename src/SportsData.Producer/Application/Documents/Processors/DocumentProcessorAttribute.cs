using SportsData.Core.Common;

namespace SportsData.Producer.Application.Documents.Processors;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class DocumentProcessorAttribute : Attribute
{
    public SourceDataProvider Source { get; }

    public Sport Sport { get; }

    public DocumentType DocumentType { get; }

    public DocumentProcessorAttribute(
        SourceDataProvider source,
        Sport sport,
        DocumentType documentType)
    {
        Source = source;
        Sport = sport;
        DocumentType = documentType;
    }
}