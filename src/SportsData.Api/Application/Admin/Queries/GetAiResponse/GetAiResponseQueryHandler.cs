using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.AI;

namespace SportsData.Api.Application.Admin.Queries.GetAiResponse;

public interface IGetAiResponseQueryHandler
{
    Task<Result<string>> ExecuteAsync(GetAiResponseQuery query, CancellationToken cancellationToken = default);
}

public class GetAiResponseQueryHandler : IGetAiResponseQueryHandler
{
    private readonly IProvideAiCommunication _ai;
    private readonly ILogger<GetAiResponseQueryHandler> _logger;

    public GetAiResponseQueryHandler(
        IProvideAiCommunication ai,
        ILogger<GetAiResponseQueryHandler> logger)
    {
        _ai = ai;
        _logger = logger;
    }

    public async Task<Result<string>> ExecuteAsync(GetAiResponseQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AI chat test request received. Prompt length: {Length} chars", query.Prompt.Length);

        var response = await _ai.GetResponseAsync(query.Prompt, cancellationToken);

        if (response.IsSuccess)
        {
            _logger.LogInformation(
                "AI chat test completed. Response length: {Length} chars, Model: {Model}",
                response.Value.Length,
                _ai.GetModelName());
        }

        return response;
    }
}
