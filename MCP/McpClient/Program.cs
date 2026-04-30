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
#endregion Read settings

// Create OpenAI-compatible client against a custom endpoint
var openAIClient = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) }
).GetChatClient(model);

#region Initialize MCP tools

// TODO

// Create sampling client

// Create MCP client

// Read the available tools

#endregion Initialize MCP tools

// Create an IChatClient that can use the tools.
using IChatClient chatClient = openAIClient.AsIChatClient()
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

#region Process user questions

// Have a conversation, making all tools available to the LLM.
List<ChatMessage> messages = [];
while (true)
{
    Console.Write("Any questions about your own plans? ");
    messages.Add(new(ChatRole.User, Console.ReadLine()));

    var response = chatClient.GetStreamingResponseAsync(messages, new() { Tools = [.. tools] });
    List<ChatResponseUpdate> updates = [];
    
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
        }
        
        await Console.Out.FlushAsync();
    }
    Console.WriteLine();

    messages.AddMessages(updates);
}

#endregion Process user questions