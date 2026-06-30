using Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using OpenAI;
using System.ClientModel;

#region Read settings
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

var endpoint = configuration["OpenAI:Endpoint"]!;
var apiKey = configuration["OpenAI:ApiKey"]!;
var model = configuration["OpenAI:Model"]!;
var toolConfigs = configuration.GetSection("Tools").Get<ToolInfo[]>() ?? [];
#endregion Read settings

// Create OpenAI-compatible client against a custom endpoint
var openAIClient = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) }
).GetChatClient(model);

#region Initialize MCP tools

// TODO: Create a sampling client

var tools = toolConfigs.SelectMany(
    toolConfig => InitTool(samplingClient, toolConfig.Name, toolConfig.Command, toolConfig.Args).Result
    ).ToList();

#endregion Initialize MCP tools

// TODO: Create an IChatClient that can use the tools.

#region Process user questions

// Have a conversation, making all tools available to the LLM.
List<ChatMessage> messages = [];

// Add system prompt
messages.Add(new(ChatRole.System, "You are a personal assistant. If you need to search the internet, use a new browser tab."));

while (true)
{
    Console.ForegroundColor = ConsoleColor.White;
    Console.Write("Any questions about your own plans? ");
    messages.Add(new(ChatRole.User, Console.ReadLine()));

    var response = chatClient.GetStreamingResponseAsync(messages, new() { Tools = [.. tools] });
    List<ChatResponseUpdate> updates = [];

    await foreach (var update in response)
    {
        var text = update.Text;
        if (text?.Length > 0)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(text);

            updates.Add(update);
        }
        else
        {
            var content = update.Contents.FirstOrDefault();

            if (content is UsageContent usageContent)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("Token count: {0}", usageContent.Details.TotalTokenCount);
            }
            else if (content is FunctionCallContent functionCallContent)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("Function call: {0}({1})", functionCallContent.Name, string.Join(", ", functionCallContent.Arguments?.Select(x => $"{x.Key}={x.Value}") ?? []));
            }

            else if (content is FunctionResultContent functionResult)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine("Function result: {0}", functionResult.Result);
            }
            else if (content is ToolCallContent toolCallContent)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("Tool call: {0}", toolCallContent);
            }

            else if (content is ToolResultContent toolResult)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine("Tool result: {0}", toolResult);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.Write(update.Contents.FirstOrDefault()?.ToString());
            }
        }

        await Console.Out.FlushAsync();
    }
    Console.WriteLine();

    messages.AddMessages(updates);
}

#endregion Process user questions

#region helper methods

static async Task<IList<McpClientTool>> InitTool(IChatClient samplingClient, string name, string command, string[] arguments)
{
    var mcpClient = await McpClient.CreateAsync(
        new StdioClientTransport(new()
        {
            Name = name,
            Command = command,
            Arguments = arguments,
        }),
        clientOptions: new()
        {
            Handlers = new()
            {
                SamplingHandler = samplingClient.CreateSamplingHandler()
            }
        });

    // TODO: Get all available tools

    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine("Tools available:");
    foreach (var tool in tools)
    {
        Console.WriteLine($"  {tool}");
    }

    return tools;
}

#endregion helper methods