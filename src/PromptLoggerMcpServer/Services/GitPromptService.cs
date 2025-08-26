using System.Diagnostics;

namespace PromptLoggerMcpServer.Services
{
    internal sealed class GitPromptService
    {
        public PromptSaveResult Save(string promptText)
        {
            if (string.IsNullOrWhiteSpace(promptText))
                return PromptSaveResult.Fail("Prompt is empty");

            var repoPath = Environment.GetEnvironmentVariable("REPO_PATH") ?? Directory.GetCurrentDirectory();
            var promptsFolder = Environment.GetEnvironmentVariable("PROMPTS_FOLDER") ?? "prompts";

            try
            {
                // 1. Basic repo check
                if (!Directory.Exists(Path.Combine(repoPath, ".git")))
                    return PromptSaveResult.Fail($"No git repository at '{repoPath}'");

                // 2. Ensure prompts folder exists
                var promptsDirFull = Path.Combine(repoPath, promptsFolder);
                Directory.CreateDirectory(promptsDirFull);

                // 3. Write file
                var fileName = $"prompt-{DateTime.UtcNow:yyyyMMddHHmmssfff}.md";
                var relPath = Path.Combine(promptsFolder, fileName).Replace('\\', '/');
                var fullPath = Path.Combine(repoPath, relPath);
                var content = $"---\ncreated_at: {DateTime.UtcNow:O}\n---\n\n{promptText}\n";
                File.WriteAllText(fullPath, content);

                // 4. Commit (minimal)
                RunGit(repoPath, $"add \"{relPath}\"");
                RunGit(repoPath, $"commit -m \"Add prompt {fileName}\"");

                return PromptSaveResult.Ok(relPath, "committed", false);
            }
            catch (Exception ex)
            {
                return PromptSaveResult.Fail(ex.Message);
            }
        }

        private static void RunGit(string cwd, string args)
        {
            using var p = Process.Start(new ProcessStartInfo("git", args)
            {
                WorkingDirectory = cwd,
                UseShellExecute = false,
                CreateNoWindow = true
            })!;
            
            p.WaitForExit(10000); // 10 second timeout
            if (p.ExitCode != 0)
                throw new Exception($"git {args} failed");
        }
    }

    internal sealed record PromptSaveResult(bool Success, string? RelativePath, string? CommitSha, bool Pushed, string? Error)
    {
        public static PromptSaveResult Ok(string relPath, string sha, bool pushed) => new(true, relPath, sha, pushed, null);
        public static PromptSaveResult Fail(string message) => new(false, null, null, false, message);
        public override string ToString() => Success
            ? $"Saved: {RelativePath}\nCommit: {CommitSha}\nPushed: {Pushed}"
            : $"Error: {Error}";
    }
}
