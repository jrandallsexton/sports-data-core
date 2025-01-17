namespace SportsData.Producer.Application.Documents.Processors.Football.Ncaa
{
    public class TeamBySeasonDocumentProcessor : IProcessDocuments
    {
        public Task ProcessAsync(ProcessDocumentCommand command)
        {
            throw new NotImplementedException();

            //// deserialize the DTO
            //var espnTeamSeason = document.FromJson<EspnTeamSeasonDto>(new JsonSerializerSettings
            //{
            //    MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            //});

            //// TODO: Determine if this entity exists. Do NOT trust that it says it is a new document!

            //// 1. map to the entity and save it
            //// TODO: Move to extension method?
            //var venueEntity = new Venue()
            //{
            //    Id = Guid.NewGuid(),
            //    Name = espnVenue.FullName,
            //    ShortName = espnVenue.ShortName,
            //    IsIndoor = espnVenue.Indoor,
            //    IsGrass = espnVenue.Grass,
            //    CreatedUtc = DateTime.UtcNow,
            //    CreatedBy = context.Message.CorrelationId,
            //    ExternalIds = [new ExternalId() { Id = espnVenue.Id.ToString(), Provider = SourceDataProvider.Espn }],
            //    GlobalId = Guid.NewGuid()
            //};
            //await _dataContext.AddAsync(venueEntity);
            //await _dataContext.SaveChangesAsync();

            //// 2. raise an event
            //// TODO: Determine if I want to publish all data in the event instead of this chatty stuff
            //var evt = new VenueCreated()
            //{
            //    Id = venueEntity.Id.ToString(),
            //    Name = context.Message.Name
            //};
            //await _bus.Publish(evt);
            //_logger.LogInformation("New {@type} event {@evt}", context.Message.DocumentType, evt);
        }
    }
}
