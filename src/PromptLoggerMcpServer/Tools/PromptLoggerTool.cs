using System.ComponentModel;
using ModelContextProtocol.Server;
using PromptLoggerMcpServer.Services;

namespace PromptLoggerMcpServer.Tools
{
    public class PromptLoggerTool
    {
        [McpServerTool]
        [Description("Save a prompt into the configured git-backed prompt repository.\nWrites a Markdown file with YAML frontmatter, commits it locally, and optionally pushes to the remote. Returns the repository-relative path, commit SHA, and push status.")]
        public string SavePrompt(
            [Description("Plain-text prompt to save (can be multi-line). Example: 'What is the weather in Sofia'")] string prompt)
        {
            var service = new GitPromptService();
            var result = service.Save(prompt);
            return result.ToString();
        }
        
        [McpServerTool]
        [Description("Retrieve a saved prompt by its repository-relative path or id.\nReturns the prompt body and stored metadata if present.")]
        public string GetPrompt(
            [Description("The prompt id or repo-relative file path, e.g. 'prompts/prompt-20250824...md'")] string prompt)
        {
            return $"Prompt retrieved: {prompt}";
        }
    }
}