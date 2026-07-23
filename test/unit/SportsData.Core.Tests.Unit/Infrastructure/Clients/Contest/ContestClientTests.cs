using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Contest.Queries;
using Xunit;

namespace SportsData.Core.Tests.Unit.Infrastructure.Clients.Contest;

public class ContestClientTests
{
    private readonly HttpClient _httpClient;
    private readonly TestHttpMessageHandler _handler;
    private readonly ContestClient _sut;

    public ContestClientTests()
    {
        _handler = new TestHttpMessageHandler();
        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/api/") };
        var logger = NullLogger<ContestClient>.Instance;
        _sut = new ContestClient(logger, _httpClient);
    }

    [Fact]
    public async Task GetSeasonContests_WithValidResponse_ReturnsSuccess()
    {
        // Arrange
        var franchiseId = Guid.NewGuid();
        var contests = new List<SeasonContestDto>
        {
            new() { Id = Guid.NewGuid(), Slug = "test-contest", Name = "Test Contest" }
        };
        _handler.SetResponse(HttpStatusCode.OK, contests.ToJson());

        // Act
        var result = await _sut.GetSeasonContests(franchiseId, 2025);

        // Assert
        result.Should().BeOfType<Success<GetSeasonContestsResponse>>();
        var success = (Success<GetSeasonContestsResponse>)result;
        success.Value.Contests.Should().HaveCount(1);
        success.Value.Contests[0].Slug.Should().Be("test-contest");
    }

    [Fact]
    public async Task GetSeasonContests_With404_ReturnsNotFound()
    {
        // Arrange
        var franchiseId = Guid.NewGuid();
        _handler.SetResponse(HttpStatusCode.NotFound, "Not found");

        // Act
        var result = await _sut.GetSeasonContests(franchiseId, 2025);

        // Assert
        result.Should().BeOfType<Failure<GetSeasonContestsResponse>>();
        var failure = (Failure<GetSeasonContestsResponse>)result;
        failure.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetSeasonContests_With500_ReturnsError()
    {
        // Arrange
        var franchiseId = Guid.NewGuid();
        _handler.SetResponse(HttpStatusCode.InternalServerError, "Server error");

        // Act
        var result = await _sut.GetSeasonContests(franchiseId, 2025);

        // Assert
        result.Should().BeOfType<Failure<GetSeasonContestsResponse>>();
        var failure = (Failure<GetSeasonContestsResponse>)result;
        failure.Status.Should().Be(ResultStatus.Error);
    }

    [Fact]
    public async Task GetSeasonContests_WithInvalidJson_ReturnsBadRequest()
    {
        // Arrange
        var franchiseId = Guid.NewGuid();
        _handler.SetResponse(HttpStatusCode.OK, "invalid json");

        // Act
        var result = await _sut.GetSeasonContests(franchiseId, 2025);

        // Assert
        result.Should().BeOfType<Failure<GetSeasonContestsResponse>>();
        var failure = (Failure<GetSeasonContestsResponse>)result;
        failure.Status.Should().Be(ResultStatus.BadRequest);
        failure.Errors.Should().ContainSingle(e => e.PropertyName == "Response");
    }

    [Fact]
    public async Task GetContestById_WithValidResponse_ReturnsSuccess()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var contest = new SeasonContestDto { Id = contestId, Slug = "test-contest", Name = "Test Contest" };
        _handler.SetResponse(HttpStatusCode.OK, contest.ToJson());

        // Act
        var result = await _sut.GetContestById(contestId);

        // Assert
        result.Should().BeOfType<Success<GetContestByIdResponse>>();
        var success = (Success<GetContestByIdResponse>)result;
        success.Value.Contest.Slug.Should().Be("test-contest");
    }

    [Fact]
    public async Task GetContestById_With404_ReturnsNotFound()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        _handler.SetResponse(HttpStatusCode.NotFound, "Not found");

        // Act
        var result = await _sut.GetContestById(contestId);

        // Assert
        result.Should().BeOfType<Failure<GetContestByIdResponse>>();
        var failure = (Failure<GetContestByIdResponse>)result;
        failure.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetContestById_With500_ReturnsError()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        _handler.SetResponse(HttpStatusCode.InternalServerError, "Server error");

        // Act
        var result = await _sut.GetContestById(contestId);

        // Assert
        result.Should().BeOfType<Failure<GetContestByIdResponse>>();
        var failure = (Failure<GetContestByIdResponse>)result;
        failure.Status.Should().Be(ResultStatus.Error);
    }

    [Fact]
    public async Task GetContestById_WithInvalidJson_ReturnsBadRequest()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        _handler.SetResponse(HttpStatusCode.OK, "invalid json");

        // Act
        var result = await _sut.GetContestById(contestId);

        // Assert
        result.Should().BeOfType<Failure<GetContestByIdResponse>>();
        var failure = (Failure<GetContestByIdResponse>)result;
        failure.Status.Should().Be(ResultStatus.BadRequest);
        failure.Errors.Should().ContainSingle(e => e.PropertyName == "Contest");
    }

    [Fact]
    public async Task RefreshContestsBySeasonYear_PostsToContestsRefresh_WithSportSeasonAndCorrelation()
    {
        // Arrange — Producer returns 202 Accepted (enqueues the fan-out).
        _handler.SetResponse(HttpStatusCode.Accepted, string.Empty);
        var correlationId = Guid.NewGuid();

        // Act
        var result = await _sut.RefreshContestsBySeasonYear(Sport.BaseballMlb, 2026, correlationId);

        // Assert — success, correct route, and the Sport enum round-trips in the
        // body (the contract that lets Producer's [FromBody] bind succeed).
        result.Should().BeOfType<Success<bool>>();
        _handler.LastRequestUri.Should().EndWith("/contests/refresh");

        _handler.LastRequestBody.Should().NotBeNullOrEmpty();
        var posted = _handler.LastRequestBody.FromJson<RefreshContestsBySeasonYearRequest>();
        posted.Should().NotBeNull();
        posted!.Sport.Should().Be(Sport.BaseballMlb);
        posted.SeasonYear.Should().Be(2026);
        posted.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task RefreshContestsBySeasonYear_OnBadRequest_MapsFailure()
    {
        // Arrange — Producer rejects (e.g. sport mismatch guard).
        _handler.SetResponse(HttpStatusCode.BadRequest, "Sport mismatch");

        // Act
        var result = await _sut.RefreshContestsBySeasonYear(Sport.FootballNcaa, 2025, Guid.NewGuid());

        // Assert
        result.Should().BeOfType<Failure<bool>>();
        ((Failure<bool>)result).Status.Should().Be(ResultStatus.BadRequest);
    }

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private HttpStatusCode _statusCode = HttpStatusCode.OK;
        private string _content = string.Empty;

        public string LastRequestUri { get; private set; } = string.Empty;
        public string LastRequestBody { get; private set; } = string.Empty;

        public void SetResponse(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            };
            return response;
        }
    }
}
