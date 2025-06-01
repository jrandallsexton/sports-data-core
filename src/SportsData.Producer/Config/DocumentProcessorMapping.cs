using SportsData.Producer.Application.Documents.Processors;

namespace SportsData.Producer.Config
{
    public class DocumentProcessorMapping
    {
        public string RoutingKey { get; set; } = default!;
        public DocumentAction Action { get; set; }
        public string ProcessorTypeName { get; set; } = default!;
    }

    public class DocumentProcessorMappings
    {
        public List<DocumentProcessorMapping> Mappings { get; set; } = new();
    }
}
