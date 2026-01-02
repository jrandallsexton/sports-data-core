using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.AI;

namespace SportsData.Api.Application.Admin.Queries.GetAiResponse;

public interface IGetAiResponseQueryHandler
{
    /// <summary>
/// Retrieves an AI-generated response for the provided query prompt.
/// </summary>
/// <param name="query">The query containing the prompt to send to the AI.</param>
/// <param name="cancellationToken">A token to cancel the operation.</param>
/// <returns>A Result containing the AI response string on success, or a failure Result with error information.</returns>
Task<Result<string>> ExecuteAsync(GetAiResponseQuery query, CancellationToken cancellationToken = default);
}

public class GetAiResponseQueryHandler : IGetAiResponseQueryHandler
{
    private readonly IProvideAiCommunication _ai;
    private readonly ILogger<GetAiResponseQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAiResponseQueryHandler"/> with the required dependencies.
    /// </summary>
    /// <param name="ai">Provider used to send prompts and retrieve AI responses.</param>
    /// <param name="logger">Logger for recording operational information and diagnostics.</param>
    public GetAiResponseQueryHandler(
        IProvideAiCommunication ai,
        ILogger<GetAiResponseQueryHandler> logger)
    {
        _ai = ai;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves an AI-generated response for the provided query prompt.
    /// </summary>
    /// <param name="query">The query containing the prompt text to send to the AI.</param>
    /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
    /// <returns>`Result<string>` containing the AI response text when successful; on failure the result contains the error information.</returns>
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