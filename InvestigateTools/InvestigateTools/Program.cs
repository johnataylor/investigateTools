
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using System.Numerics;
using System.Text.Json;

internal class Program
{
    private static async Task Test0Async()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        var openAIKey = config.GetSection("OpenAIKey").Value ?? throw new Exception("OpenAIKey is required in configuration");

        var client = new OpenAIClient(openAIKey);

        var toolDefinition = new ChatCompletionsFunctionToolDefinition();

        var schema = await File.ReadAllTextAsync("..\\..\\..\\schema.json");

        toolDefinition.Name = "GetWeather";
        toolDefinition.Description = "Provides the current weather for a specific location.";
        toolDefinition.Parameters = BinaryData.FromString(schema);

        var options = new ChatCompletionsOptions();
        options.DeploymentName = "gpt-4-1106-preview";
        options.Tools.Add(toolDefinition);
        options.Messages.Add(new ChatRequestUserMessage("what is the weather in SEattle, San Francisco and London?"));

        var functions = new Dictionary<string, Func<string, string>>
        {
            { nameof(GetWeather), GetWeather }
        };

        while (true)
        {
            var chatComplations = await client.GetChatCompletionsAsync(options);

            var chatChoice = chatComplations.Value.Choices[0] ?? throw new Exception("no choice!");

            options.Messages.Add(new ChatRequestAssistantMessage(chatChoice.Message));

            if (chatChoice.FinishReason == CompletionsFinishReason.Stopped)
            {
                await Console.Out.WriteLineAsync(chatChoice.Message.Content);
                break;
            }
            else if (chatChoice.FinishReason == CompletionsFinishReason.ToolCalls)
            {
                foreach (var toolCall in chatChoice.Message.ToolCalls)
                {
                    if (toolCall is ChatCompletionsFunctionToolCall functionToolCall)
                    {
                        var output = functions[functionToolCall.Name](functionToolCall.Arguments);
                        options.Messages.Add(new ChatRequestToolMessage(output, toolCall.Id));
                    }
                }
            }
        }
    }

    private static string GetWeather(string arguments)
    {
        var obj = JsonDocument.Parse(arguments).RootElement;

        var location = obj.GetProperty("location").GetString() ?? throw new Exception("expected property 'location'");

        if (location.Contains("San Francisco", StringComparison.InvariantCultureIgnoreCase))
        {
            return "sunny";
        }
        if (location.Contains("London", StringComparison.InvariantCultureIgnoreCase))
        {
            return "rainy";
        }
        if (location.Contains("Seattle", StringComparison.InvariantCultureIgnoreCase))
        {
            return "snowing";
        }

        return "unknown";
    }

    private static void Main(string[] args)
    {
        Test0Async().Wait();
    }
}