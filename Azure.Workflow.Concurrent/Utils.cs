using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;

namespace CustomDevAI.Concurrent
{
    internal class Utils
    {
        internal static SortedList<string, ChatClientAgent> GetAgents()
        {
            SortedList<string, ChatClientAgent> agents = [];

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var settings = configuration.GetSection(AgentsSettings.SectionName).Get<AgentsSettings>()
                ?? throw new Exception("AgentsSettings konnte nicht geladen werden");

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

            return agents;
        }
    }
}
