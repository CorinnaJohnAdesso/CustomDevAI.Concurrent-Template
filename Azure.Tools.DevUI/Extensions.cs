using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CustomDevAI.AgentWithDI;

public static class Extensions
{
    public static WebApplicationBuilder AddAgents(this WebApplicationBuilder builder)
    {
        builder.AddServices();

        builder.Services.AddSingleton<AIProjectClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AgentSettings>>().Value;
            var endpoint = settings.ProjectEndpoint;

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new InvalidOperationException(
                    $"Bitte '{AgentSettings.SectionName}:ProjectEndpoint' in appsettings setzen.");
            }

            return new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential());
        });
        builder.Services.AddAIAgent("Weather Agent", (provider, name) =>
        {
            var client = provider.GetRequiredService<AIProjectClient>();
            var settings = provider.GetRequiredService<IOptions<AgentSettings>>().Value;
            var agent = client.AsAIAgent(
                settings.ModelDeployment,
                settings.Instructions,
                name

                // TODO
                // provide system time
                // provice OpenMeteo weather

                );
            return agent;
        });
        return builder;
    }

    public static WebApplicationBuilder AddServices(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<AgentSettings>()
            .Bind(builder.Configuration.GetSection(AgentSettings.SectionName));

        builder.Services.AddSingleton(TimeProvider.System);
        
        // TODO
        // add weather tool here
        
        return builder;
    }
}