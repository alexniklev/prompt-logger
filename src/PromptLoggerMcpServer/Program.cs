using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PromptLoggerMcpServer.Tools;
using PromptLoggerMcpServer.Services; // added for manual test

// Quick manual test harness: run with --manual-save "your prompt text" to bypass MCP server
// if (args.Contains("--manual-save"))
// {
//     var idx = Array.IndexOf(args, "--manual-save");
//     string text;
//     if (idx >= 0 && idx + 1 < args.Length && !args[idx + 1].StartsWith("--"))
//     {
//         text = args[idx + 1];
//     }
//     else
//     {
//         Console.Error.WriteLine("Enter prompt text, finish with EOF (Ctrl+Z / Ctrl+D):");
//         text = Console.In.ReadToEnd();
//     }

//     var svc = new GitPromptService();
//     var result = svc.Save(text);
//     Console.Error.WriteLine(result.ToString());
//     return; // Do not start MCP server in manual mode
// }

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<WeatherTool>()
    .WithTools<PromptLoggerTool>()
    .WithTools<RandomNumberTools>();

await builder.Build().RunAsync();
