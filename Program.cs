using Azure.AI.Projects;
using Azure.Identity;
using CustomDevUI.Concurrent;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using System.ClientModel;
using System.Text;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var settings = configuration.GetSection(AgentsSettings.SectionName).Get<AgentsSettings>() 
    ?? throw new Exception("AgentsSettings konnte nicht geladen werden");

SortedList<string, ChatClientAgent> agents = [];

// prepare all configured agents
foreach (var agentSettings in settings.Agents ?? [])
{
    var client = new AIProjectClient(new Uri(agentSettings.Endpoint), new DefaultAzureCredential());

    var agent = client.AsAIAgent(
        agentSettings.ModelDeployment,
        agentSettings.Instructions,
        agentSettings.AgentName);

    agents.Add(agentSettings.AgentName, agent);
}

// create a workflow that
// - runs all agents (except censor) concurrently with the same prompt
// - waits until finished
// - calls the aggregator function which runs the censor agent
var mainAgent = agents["Censor Agent"];
var otherAgents = agents.Values.Where(x => x != mainAgent).ToList();
var workflow = AgentWorkflowBuilder.BuildConcurrent("Evaluation Workflow", otherAgents, Aggregate);

Console.WriteLine("Worüber suchst du eine Meinung?");
string? userQuestion;

while ((userQuestion = Console.ReadLine())?.Length > 0)
{
    List<ChatMessage> messages = [new(ChatRole.User, userQuestion)];

    // begin streaming of events
    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        if (evt is ExecutorFailedEvent failed)
            Console.WriteLine($"{(failed.Data as ClientResultException)?.Message}");
        else if (evt is WorkflowOutputEvent outputEvent
            && outputEvent.ExecutorId.ToString() == "ConcurrentEnd"
            && outputEvent.Data is List<ChatMessage> responseMessages)
        {
            // show output of the aggregate function
            foreach (var x in responseMessages)
            {
                Console.WriteLine(x.Text);
            }
            break;
        }
        else
        {
            Console.Write(".");
        }
    }

    Console.WriteLine("Was möchtest du noch vergleichen?");
}

List<ChatMessage> Aggregate(IList<List<ChatMessage>> results)
{
    StringBuilder evalPrompt = new($"Wer hat Recht? Die Frage ist <Frage>{results[0].First().Text}</Frage>. Vergleiche folgende Antworten und finde einen Kompromiss.");

    foreach (var x in results)
    {
        evalPrompt.AppendLine($"<Antwort>{x.Last().Text}</Antwort>");
    }

    var response = mainAgent.RunAsync(evalPrompt.ToString()).Result;
    return [.. response.Messages];
}