using Microsoft.Extensions.AI;
using OllamaSharp;

namespace SportsData.ProcessorGen
{
    public class DtoGenerator
    {
        private readonly Uri _baseUri;
        private readonly string _model;
        private readonly IChatClient _chatClient;

        public DtoGenerator(string baseUri, string model)
        {
            _chatClient = new OllamaApiClient(new Uri(baseUri), _model); ;
            _baseUri = new Uri(baseUri);
            _model = model;
        }

        public async Task<string> GenerateDtoFromJsonAsync(string json)
        {
            var client = new OllamaApiClient(_baseUri, _model); // << Set model at construction time

            var prompt = $"""
                          You are an expert in C# and System.Text.Json.

                          Based on the following JSON payload, generate a C# DTO class compatible with System.Text.Json serialization and deserialization.

                          JSON:
                          {json}
                          """;

            Console.WriteLine("Prompting model to generate DTO...");

            var chat = new Chat(client);

            string response = "";

            await foreach (var token in chat.SendAsync(prompt))
            {
                Console.Write(token);
                response += token;
            }

            return response;
        }


        public async Task<string> GenerateDtoFromJsonAsyncV2(string json)
        {
            //var client = new OllamaApiClient(_baseUri, _model);

            var role = "You are an expert in C# and System.Text.Json. Based on the following JSON payload, generate a C# DTO class compatible with System.Text.Json serialization and deserialization.";

            var prompt =
                $"You are an expert in C# and System.Text.Json. Based on the following JSON payload, generate a C# DTO class compatible with System.Text.Json serialization and deserialization.  JSON: {json}";

            Console.WriteLine("Prompting model to generate DTO...");

            //var chat = new Chat(client);
            var cm = new ChatMessage(new ChatRole(role), json);

            var result = _chatClient.GetResponseAsync(cm);
            return result.Result.Text;
            //var result = await _chatClient.GetResponseAsync(cm);
            //var chatCompletion = await _chatClient.CompleteAsync(prompt);


            //var chatCompletion = await _chatClient.CompleteAsync(new List<ChatMessage>() { cm });
            //string response = "";

            //await foreach (var token in chat.SendAsync(prompt))
            //{
            //    Console.Write(token);
            //    response += token;
            //}

            //IEmbeddingGenerator<string, Embedding<float>> generator =
            //    new OllamaEmbeddingGenerator(new Uri("http://localhost:11434/"), "deepseek-coder-v2");

            //var embedding = await generator.GenerateAsync(new List<string>()
            //{
            //    role,
            //    json
            //});

            //var foo = string.Join(", ", embedding[0].Vector.ToArray());

            //return chatCompletion.Message.Text;
        }
    }

}