using Moq;

using SportsData.Api.Application.Models.Commands.UpdateModel;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Models.Commands.UpdateModel
{
    public class UpdateModelCommandHandlerTests : ApiTestBase<UpdateModelCommandHandler>
    {
        private static readonly DateTime Now = new(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);

        public UpdateModelCommandHandlerTests()
        {
            Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
        }

        private async Task<ModelProvider> SeedProviderAsync(string name = "Anthropic", ModelProviderKind kind = ModelProviderKind.Anthropic)
        {
            var provider = new ModelProvider { Id = Guid.NewGuid(), Name = name, Kind = kind };
            await DataContext.ModelProviders.AddAsync(provider);
            await DataContext.SaveChangesAsync();
            return provider;
        }

        [Fact]
        public async Task UpdateModel_EditsMetadata_NotIdentity()
        {
            var provider = await SeedProviderAsync();
            var model = new Model
            {
                Id = Guid.NewGuid(),
                ModelProviderId = provider.Id,
                Name = "Claude Sonnet 4.6",
                ApiModelId = "claude-sonnet-4-6"
            };
            await DataContext.Models.AddAsync(model);
            await DataContext.SaveChangesAsync();

            var sut = Mocker.CreateInstance<UpdateModelCommandHandler>();

            var result = await sut.ExecuteAsync(new UpdateModelCommand
            {
                ModelId = model.Id,
                KnowledgeCutoffUtc = new DateTime(2025, 8, 31, 0, 0, 0, DateTimeKind.Utc),
                CutoffEvidence = "docs.anthropic.com — resolved training-vs-reliable discrepancy",
                CutoffVerifiedUtc = Now,
                IsActive = true
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var updated = DataContext.Models.Single(m => m.Id == model.Id);
            Assert.Equal(new DateTime(2025, 8, 31, 0, 0, 0, DateTimeKind.Utc), updated.KnowledgeCutoffUtc);
            Assert.Equal(Now, updated.CutoffVerifiedUtc);
            // Identity untouched
            Assert.Equal("Claude Sonnet 4.6", updated.Name);
            Assert.Equal("claude-sonnet-4-6", updated.ApiModelId);
        }
    }
}
