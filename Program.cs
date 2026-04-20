using Azure.AI.Projects;
using CustomDevAI.Concurrent;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.ClientModel;
using System.Text;

var agents = Utils.GetAgents();

// TODO
// create a workflow that
// - runs all agents (except censor) concurrently with the same prompt
// - waits until finished
// - calls the aggregator function which runs the censor agent

Console.WriteLine("Worüber suchst du eine Meinung?");
string? userQuestion;

while ((userQuestion = Console.ReadLine())?.Length > 0)
{
    List<ChatMessage> messages = [new(ChatRole.User, userQuestion)];

    // TODO
    // begin streaming of events

    // TODO
    // wait for final output event
    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        //
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