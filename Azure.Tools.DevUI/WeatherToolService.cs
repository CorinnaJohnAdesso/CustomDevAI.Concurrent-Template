using Microsoft.Extensions.AI;
using OpenMeteo;
using System.ComponentModel;

namespace CustomDevAI.AgentWithDI;

public sealed class WeatherToolService(TimeProvider timeProvider) : AITool
{
    [Description("Returns the current UTC date and time.")]
    public DateTimeOffset GetTime()
    {
        return timeProvider.GetUtcNow();
    }

    [Description("Returns the weather for the given location and day from OpenMeteo in WeatherForecast format.")]
    public async Task<WeatherForecast?> GetWeather(
        [Description("Location of the weather forecast")] string location,
        [Description("Date of the weather forecast in ISO date format yyyy-mm-dd")] string iso_start_date)
    {
        if (string.IsNullOrWhiteSpace(iso_start_date))
        {
            iso_start_date = timeProvider.GetLocalNow().ToString("yyyy-MM-dd");
        }

        var options = new WeatherForecastOptions
        { 
            Start_date = iso_start_date, 
            End_date = iso_start_date, 
            Current = CurrentOptions.All 
        };

        OpenMeteoClient client = new();
        var weatherData = await client.QueryAsync(location, options);

        return weatherData;
    }
}