using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Franchise;
using Xunit;

namespace SportsData.Core.Tests.Unit.Infrastructure.Clients.Franchise;

/// <summary>
/// Status mapping of the correlation-id POST helper shared by the admin
/// franchise-season actions (enrich, single-season source). The API handlers
/// pass the client's status straight through to ToActionResult, so whatever
/// this maps to is what the operator sees.
/// </summary>
public class FranchiseClientTests
{
    private readonly TestHttpMessageHandler _handler;
    private readonly FranchiseClient _sut;

    public FranchiseClientTests()
    {
        _handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/api/") };
        _sut = new FranchiseClient(NullLogger<FranchiseClient>.Instance, httpClient);
    }

    [Fact]
    public async Task RequestSingleFranchiseSeasonSourcing_On202_ReturnsTheCorrelationId_AndPostsToTheSourceRoute()
    {
        var franchiseSeasonId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        _handler.SetResponse(HttpStatusCode.Accepted, $"\"{correlationId}\"");

        var result = await _sut.RequestSingleFranchiseSeasonSourcing(franchiseSeasonId);

        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Accepted);
        result.Value.Should().Be(correlationId);
        _handler.LastRequestMethod.Should().Be(HttpMethod.Post);
        _handler.LastRequestUri.Should().Be($"http://localhost/api/franchise-seasons/id/{franchiseSeasonId}/source");
    }

    [Fact]
    public async Task RequestSingleFranchiseSeasonSourcing_On400_ReturnsBadRequest_NotError_WithTheProducersMessage()
    {
        // The Producer's deliberate 400 (no usable ESPN ref) must stay a 4xx
        // through the API; mapped to Error it rendered as a 500 (Vortex, #795).
        _handler.SetResponse(
            HttpStatusCode.BadRequest,
            "{\"errors\":[{\"propertyName\":\"FranchiseSeasonId\",\"errorMessage\":\"has no usable ESPN TeamSeason ref to source from.\"}]}");

        var result = await _sut.RequestSingleFranchiseSeasonSourcing(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.BadRequest);
        ((Failure<Guid>)result).Errors.Single().ErrorMessage.Should().Contain("no usable ESPN TeamSeason ref");
    }

    [Fact]
    public async Task RequestSingleFranchiseSeasonSourcing_On404_ReturnsNotFound()
    {
        _handler.SetResponse(HttpStatusCode.NotFound, "{\"errors\":[]}");

        var result = await _sut.RequestSingleFranchiseSeasonSourcing(Guid.NewGuid());

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task RequestSingleFranchiseSeasonSourcing_On500_ReturnsError()
    {
        _handler.SetResponse(HttpStatusCode.InternalServerError, "{\"errors\":[]}");

        var result = await _sut.RequestSingleFranchiseSeasonSourcing(Guid.NewGuid());

        result.Status.Should().Be(ResultStatus.Error);
    }

    [Fact]
    public async Task EnrichFranchiseSeason_On400_ReturnsBadRequest_SameHelperSameMapping()
    {
        // Enrich shares the helper; its Producer validation 400 used to
        // surface as a 500 as well.
        _handler.SetResponse(HttpStatusCode.BadRequest, "{\"errors\":[]}");

        var result = await _sut.EnrichFranchiseSeason(Guid.NewGuid());

        result.Status.Should().Be(ResultStatus.BadRequest);
    }

    // Copied from ContestClientTests (private there), plus the request method.
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private HttpStatusCode _statusCode = HttpStatusCode.OK;
        private string _content = string.Empty;

        public string LastRequestUri { get; private set; } = string.Empty;
        public HttpMethod LastRequestMethod { get; private set; } = HttpMethod.Get;

        public void SetResponse(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString() ?? string.Empty;
            LastRequestMethod = request.Method;

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            });
        }
    }
}
