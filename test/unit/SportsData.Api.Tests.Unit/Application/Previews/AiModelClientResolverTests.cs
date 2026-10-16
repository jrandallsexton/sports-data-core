using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SportsData.Api.Application.Previews;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Infrastructure.Clients.AI;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Previews
{
    /// <summary>
    /// The catalog row's reasoning cap reaches OpenRouter's request body —
    /// asserted on the JSON actually posted, since a mis-mapped cap fails
    /// silently (the model just thinks, and bills, at its default).
    /// </summary>
    public class AiModelClientResolverTests
    {
        private static Model OpenRouterModel(ModelReasoningEffort? effort = null, int? maxTokens = null) => new()
        {
            Id = Guid.NewGuid(),
            Name = "Qwen 3.8 Max (0902)",
            ApiModelId = "qwen/qwen3.8-max-0902",
            Gateway = ModelGateway.OpenRouter,
            ModelProvider = new ModelProvider { Id = Guid.NewGuid(), Name = "Alibaba", Kind = ModelProviderKind.GatewayOnly },
            ReasoningEffort = effort,
            ReasoningMaxTokens = maxTokens
        };

        private static async Task<JsonElement> PostedBodyAsync(Model model)
        {
            var handler = new CapturingHandler();
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(x => x.CreateClient(AiModelClientResolver.OpenRouterHttpClientName))
                .Returns(new HttpClient(handler));

            var resolver = new AiModelClientResolver(
                factory.Object,
                new OpenRouterClientConfig { BaseUrl = "https://openrouter.test/api/v1/chat/completions", ApiKey = "test" },
                NullLoggerFactory.Instance);

            var result = await resolver.Resolve(model).EvaluateAsync("prompt");

            Assert.True(result.IsSuccess);
            return JsonDocument.Parse(handler.Body!).RootElement;
        }

        [Fact]
        public async Task Resolve_NoCap_SendsNoReasoningParameter()
        {
            var body = await PostedBodyAsync(OpenRouterModel());

            Assert.False(body.TryGetProperty("reasoning", out _));
        }

        [Theory]
        [InlineData(ModelReasoningEffort.Off, "none")]
        [InlineData(ModelReasoningEffort.Minimal, "minimal")]
        [InlineData(ModelReasoningEffort.Low, "low")]
        [InlineData(ModelReasoningEffort.High, "high")]
        public async Task Resolve_EffortCap_SendsEffortOnly(ModelReasoningEffort effort, string expected)
        {
            var body = await PostedBodyAsync(OpenRouterModel(effort: effort));

            var reasoning = body.GetProperty("reasoning");
            Assert.Equal(expected, reasoning.GetProperty("effort").GetString());
            Assert.False(reasoning.TryGetProperty("max_tokens", out _));
        }

        [Fact]
        public async Task Resolve_TokenBudget_SendsMaxTokensOnly_BelowTheRequestCeiling()
        {
            var body = await PostedBodyAsync(OpenRouterModel(maxTokens: 4096));

            var reasoning = body.GetProperty("reasoning");
            Assert.Equal(4096, reasoning.GetProperty("max_tokens").GetInt32());
            Assert.False(reasoning.TryGetProperty("effort", out _));
            Assert.True(body.GetProperty("max_tokens").GetInt32() > 4096);
        }

        private sealed class CapturingHandler : HttpMessageHandler
        {
            public string? Body { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Body = await request.Content!.ReadAsStringAsync(cancellationToken);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"choices":[{"message":{"role":"assistant","content":"{}"},"finish_reason":"stop"}],"usage":{"prompt_tokens":10,"completion_tokens":5,"completion_tokens_details":{"reasoning_tokens":3}}}""",
                        Encoding.UTF8,
                        "application/json")
                };
            }
        }
    }
}
