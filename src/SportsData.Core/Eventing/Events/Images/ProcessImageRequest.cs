using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Images
{
    public class ProcessImageRequest(
        string url,
        string id,
        string name,
        Sport sport,
        int? seasonYear,
        DocumentType documentType,
        SourceDataProvider sourceDataProvider)
        : EventBase
    {
        public string Url { get; set; } = url;

        public string Id { get; init; } = id;

        public string Name { get; init; } = name;

        public Sport Sport { get; init; } = sport;

        public int? SeasonYear { get; init; } = seasonYear;

        public DocumentType DocumentType { get; init; } = documentType;

        public SourceDataProvider SourceDataProvider { get; set; } = sourceDataProvider;
    }
}
