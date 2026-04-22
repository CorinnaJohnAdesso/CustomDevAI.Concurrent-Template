namespace CustomDevAI.AgentWithDI;

public sealed class AgentSettings
{
    public const string SectionName = "Agent";

    public string? ProjectEndpoint { get; init; }
    public string ModelDeployment { get; init; } = "gpt-5.4-nano";
    public string AgentName { get; init; } = "Simple Agent";
    public string Instructions { get; init; } = "You are a simple agent that responds to user messages.";
}