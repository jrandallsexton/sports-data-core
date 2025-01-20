using MassTransit;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Infrastructure.Blobs;
using SportsData.Producer.Application.Images;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Images
{
    public class ImageRequestedProcessorTests : UnitTestBase<ImageRequestedProcessor>
    {
        [Fact]
        public async Task WhenFoo_DoesBar()
        {
            // arrange
            var bus = Mocker.GetMock<IBus>();
            Mocker.Use<IProvideBlobStorage>(new BlobStorageProvider());

            var sut = Mocker.CreateInstance<ImageRequestedProcessor>();

            var message = new ProcessImageRequest(
                "https://a.espncdn.com/i/teamlogos/ncaa/500/99.png",
                Guid.NewGuid(),
                Guid.NewGuid(),
                "99.png",
                Sport.FootballNcaa,
                2024,
                DocumentType.FranchiseLogo,
                SourceDataProvider.Espn,
                500,
                500,
                null);

            // act
            await sut.Process(message);

            // assert
            bus.Verify(b => b.Publish(It.IsAny<ProcessImageResponse>(), CancellationToken.None),
                Times.Exactly(1));
        }
    }
}
