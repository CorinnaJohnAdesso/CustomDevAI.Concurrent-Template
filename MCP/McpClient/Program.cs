using Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using OpenAI;
using System.ClientModel;
using System.Diagnostics;

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


// TODO: Create sampling client

var tools = toolConfigs.SelectMany(
    toolConfig => InitTool(samplingClient, toolConfig.Name, toolConfig.Command, toolConfig.Args).Result
    ).ToList();

#endregion Initialize MCP tools

// Create an IChatClient that can use the tools.
using IChatClient chatClient = openAIClient.AsIChatClient()
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

#region Process user questions

// Have a conversation, making all tools available to the LLM.
List<ChatMessage> messages = [];

// Add system prompt
messages.Add(new(ChatRole.System, "You are a personal assistant. If you need to search the internet, use a new browser tab."));

while (true)
{
    Console.Write("Any questions about your own plans? ");
    messages.Add(new(ChatRole.User, Console.ReadLine()));

    // TODO: Pass the tools to the LLM and get a response

    await foreach (var update in response)
    {
        var text = update.Text;
        if (text?.Length > 0)
        {
            Console.Write(text);
            updates.Add(update);
        }
        else
        {
            Console.Write(".");
            Debug.Write(update.Contents.FirstOrDefault()?.ToString());
        }

        await Console.Out.FlushAsync();
    }
    Console.WriteLine();

    messages.AddMessages(updates);
}

#endregion Process user questions

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
    
    Console.WriteLine("Tools available:");
    foreach (var tool in tools)
    {
        Console.WriteLine($"  {tool}");
    }

    return tools;
}