using Microsoft.EntityFrameworkCore;

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

        private async Task<ModelFamily> SeedFamilyAsync(string name = "Claude Haiku")
        {
            var family = new ModelFamily { Id = Guid.NewGuid(), Name = name };
            await DataContext.ModelFamilies.AddAsync(family);
            await DataContext.SaveChangesAsync();
            return family;
        }

        private async Task<Model> SeedSeatAsync(ModelProvider provider, ModelFamily family, string name, bool isActive = true, ModelGateway gateway = ModelGateway.OpenRouter)
        {
            var model = new Model
            {
                Id = Guid.NewGuid(),
                ModelProviderId = provider.Id,
                ModelFamilyId = family.Id,
                Name = name,
                ApiModelId = name.ToLowerInvariant().Replace(' ', '-'),
                Gateway = gateway,
                IsActive = isActive
            };
            await DataContext.Models.AddAsync(model);
            await DataContext.SaveChangesAsync();
            return model;
        }

        [Fact]
        public async Task UpdateModel_EditsMetadata_NotIdentity()
        {
            var provider = await SeedProviderAsync();
            var family = await SeedFamilyAsync("Claude Sonnet");
            var model = new Model
            {
                Id = Guid.NewGuid(),
                ModelProviderId = provider.Id,
                ModelFamilyId = family.Id,
                Name = "Claude Sonnet 4.6",
                ApiModelId = "claude-sonnet-4-6"
            };
            await DataContext.Models.AddAsync(model);
            await DataContext.SaveChangesAsync();

            var sut = Mocker.CreateInstance<UpdateModelCommandHandler>();

            var result = await sut.ExecuteAsync(new UpdateModelCommand
            {
                ModelId = model.Id,
                ModelFamilyId = family.Id,
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

        [Fact]
        public async Task UpdateModel_Activating_SwapsTheFamilySeat()
        {
            var provider = await SeedProviderAsync("Alibaba", ModelProviderKind.GatewayOnly);
            var qwen = await SeedFamilyAsync("Qwen");
            var incumbent = await SeedSeatAsync(provider, qwen, "Qwen 3 Max");
            var successor = await SeedSeatAsync(provider, qwen, "Qwen 3.8 Max", isActive: false);
            var sut = Mocker.CreateInstance<UpdateModelCommandHandler>();

            var result = await sut.ExecuteAsync(new UpdateModelCommand
            {
                ModelId = successor.Id,
                ModelFamilyId = qwen.Id,
                IsActive = true
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(DataContext.Models.Single(m => m.Id == successor.Id).IsActive);
            Assert.False(DataContext.Models.Single(m => m.Id == incumbent.Id).IsActive);
        }

        [Fact]
        public async Task UpdateModel_MovingAnActiveVersion_TakesTheTargetFamilySeat()
        {
            var provider = await SeedProviderAsync("Alibaba", ModelProviderKind.GatewayOnly);
            var qwen = await SeedFamilyAsync("Qwen");
            var stray = await SeedFamilyAsync("Qwen 3.8 Max (auto)");
            var incumbent = await SeedSeatAsync(provider, qwen, "Qwen 3 Max");
            var moved = await SeedSeatAsync(provider, stray, "Qwen 3.8 Max");
            var sut = Mocker.CreateInstance<UpdateModelCommandHandler>();

            var result = await sut.ExecuteAsync(new UpdateModelCommand
            {
                ModelId = moved.Id,
                ModelFamilyId = qwen.Id,
                IsActive = true
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(qwen.Id, DataContext.Models.Single(m => m.Id == moved.Id).ModelFamilyId);
            Assert.False(DataContext.Models.Single(m => m.Id == incumbent.Id).IsActive);
        }

        [Fact]
        public async Task UpdateModel_ActivatingDirectRoute_LeavesTheFamilySeatAlone()
        {
            var provider = await SeedProviderAsync("DeepSeek", ModelProviderKind.DeepSeek);
            var deepSeek = await SeedFamilyAsync("DeepSeek");
            var seat = await SeedSeatAsync(provider, deepSeek, "DeepSeek V3.1");
            var direct = await SeedSeatAsync(provider, deepSeek, "DeepSeek Chat", isActive: false, gateway: ModelGateway.None);
            var sut = Mocker.CreateInstance<UpdateModelCommandHandler>();

            var result = await sut.ExecuteAsync(new UpdateModelCommand
            {
                ModelId = direct.Id,
                ModelFamilyId = deepSeek.Id,
                IsActive = true
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(DataContext.Models.Single(m => m.Id == seat.Id).IsActive);
        }

        [Fact]
        public async Task UpdateModel_RejectsUnknownFamily()
        {
            var provider = await SeedProviderAsync();
            var family = await SeedFamilyAsync("Claude Sonnet");
            var model = await SeedSeatAsync(provider, family, "Claude Sonnet 4.6");
            var sut = Mocker.CreateInstance<UpdateModelCommandHandler>();

            var result = await sut.ExecuteAsync(new UpdateModelCommand
            {
                ModelId = model.Id,
                ModelFamilyId = Guid.NewGuid(),
                IsActive = true
            }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(family.Id, DataContext.Models.Single(m => m.Id == model.Id).ModelFamilyId);
        }

        [Fact]
        public async Task UpdateModel_SetsThenClearsReasoningCap()
        {
            var provider = await SeedProviderAsync("Alibaba", ModelProviderKind.GatewayOnly);
            var family = await SeedFamilyAsync("Qwen");
            var model = await SeedSeatAsync(provider, family, "Qwen 3.8 Max");
            var sut = Mocker.CreateInstance<UpdateModelCommandHandler>();

            var capped = await sut.ExecuteAsync(new UpdateModelCommand
            {
                ModelId = model.Id,
                ModelFamilyId = family.Id,
                ReasoningMaxTokens = 4096,
                IsActive = true
            }, CancellationToken.None);

            Assert.True(capped.IsSuccess);
            Assert.Equal(4096, DataContext.Models.AsNoTracking().Single(m => m.Id == model.Id).ReasoningMaxTokens);

            var cleared = await sut.ExecuteAsync(new UpdateModelCommand
            {
                ModelId = model.Id,
                ModelFamilyId = family.Id,
                IsActive = true
            }, CancellationToken.None);

            Assert.True(cleared.IsSuccess);
            var saved = DataContext.Models.AsNoTracking().Single(m => m.Id == model.Id);
            Assert.Null(saved.ReasoningMaxTokens);
            Assert.Null(saved.ReasoningEffort);
        }

        [Fact]
        public async Task UpdateModel_RejectsReasoningCapOnDirectRoute()
        {
            var provider = await SeedProviderAsync("DeepSeek", ModelProviderKind.DeepSeek);
            var family = await SeedFamilyAsync("DeepSeek");
            var model = await SeedSeatAsync(provider, family, "DeepSeek Chat", gateway: ModelGateway.None);
            var sut = Mocker.CreateInstance<UpdateModelCommandHandler>();

            var result = await sut.ExecuteAsync(new UpdateModelCommand
            {
                ModelId = model.Id,
                ModelFamilyId = family.Id,
                ReasoningEffort = ModelReasoningEffort.Low,
                IsActive = true
            }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(ResultStatus.Validation, result.Status);
            Assert.Null(DataContext.Models.AsNoTracking().Single(m => m.Id == model.Id).ReasoningEffort);
        }
    }
}
