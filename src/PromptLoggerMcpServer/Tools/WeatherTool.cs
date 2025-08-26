using System;
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace PromptLoggerMcpServer.Tools
{
    public class WeatherTool
    {
        [McpServerTool]
        [Description("Describes random weather in the provided city.")]
        public string GetCityWeather(
            [Description("Name of the city to return weather for")] string city)
        {
            Console.WriteLine($"[Tool] GetCityWeather invoked with city={city}");

            // Read the environment variable during tool execution.
            // Alternatively, this could be read during startup and passed via IOptions dependency injection
            var weather = Environment.GetEnvironmentVariable("WEATHER_CHOICES");
            if (string.IsNullOrWhiteSpace(weather))
            {
                weather = "balmy,rainy,stormy";
            }

            var weatherChoices = weather.Split(",");
            var selectedWeatherIndex = Random.Shared.Next(0, weatherChoices.Length);

            var result = $"The weather in {city} is {weatherChoices[selectedWeatherIndex]}.";
            Console.WriteLine($"[Tool] GetCityWeather result={result}");
            return result;
        }

        // Add an explicit tool whose description matches common user phrasing so the model can more easily find it.
        [McpServerTool]
        [Description("Answer the question 'what is the weather in {city}' - returns a short weather string for the named city.")]
        public string AskWeather(
            [Description("City name, e.g. 'Sofia' - used when asking 'what is the weather in Sofia'")] string city)
        {
            Console.WriteLine($"[Tool] AskWeather invoked with city={city}");
            var resp = GetCityWeather(city);
            Console.WriteLine($"[Tool] AskWeather returning: {resp}");
            return resp;
        }
    }
}