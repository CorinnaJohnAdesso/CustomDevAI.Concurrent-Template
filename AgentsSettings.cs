namespace CustomDevAI.Concurrent;

public class AgentsSettings
{
    public class AgentSettings
    {
        public string Endpoint { get; init; } = "";
        public string? ApiKey { get; init; }
        public string ModelDeployment { get; init; } = "gpt-5.4-nano";
        public string AgentName { get; init; } = "Simple Agent";
        public string Instructions { get; init; } = "You are a simple agent that responds to user messages.";
    }

    public const string SectionName = "Crowd";

    public AgentSettings[]? Agents { get; init; }
}