using System.Diagnostics;

namespace PromptLoggerMcpServer.Services
{
    internal sealed class GitPromptService
    {
        // POC: keep only what we need, do everything inside Save for clarity.
        public PromptSaveResult Save(string promptText)
        {
            if (string.IsNullOrWhiteSpace(promptText))
                return PromptSaveResult.Fail("Prompt is empty");

            var repoPath = Environment.GetEnvironmentVariable("REPO_PATH");
            var promptsFolder = Environment.GetEnvironmentVariable("PROMPTS_FOLDER");
            var authorName = Environment.GetEnvironmentVariable("GIT_AUTHOR_NAME");
            var authorEmail = Environment.GetEnvironmentVariable("GIT_AUTHOR_EMAIL");
            var remote = Environment.GetEnvironmentVariable("GIT_REMOTE");
            var pushFlag = Environment.GetEnvironmentVariable("GIT_PUSH");
            var push = !string.IsNullOrWhiteSpace(pushFlag) && (pushFlag.Equals("true", StringComparison.OrdinalIgnoreCase) || pushFlag == "1");

            try
            {
                // 1. Ensure repository exists locally. Do NOT create or clone a repo here; caller must provide a valid local repo path.
                if (!Directory.Exists(repoPath) || !Directory.Exists(Path.Combine(repoPath, ".git")))
                {
                    return PromptSaveResult.Fail($"No git repository found at REPO_PATH='{repoPath}'. Please clone or provide a local repo path.");
                }

                // 2. Validate prompts folder setting
                if (string.IsNullOrWhiteSpace(promptsFolder))
                    return PromptSaveResult.Fail("PROMPTS_FOLDER environment variable is not set or is empty.");

                // 3. Optional per-repo user config (ignore failures)
                if (!string.IsNullOrWhiteSpace(authorName))
                    RunGit(repoPath, $"config user.name \"{authorName}\"", ignoreErrors: true);
                if (!string.IsNullOrWhiteSpace(authorEmail))
                    RunGit(repoPath, $"config user.email \"{authorEmail}\"", ignoreErrors: true);

                // 4. Check/create prompts folder inside the repo
                var promptsDirFull = Path.Combine(repoPath, promptsFolder);
                if (!Directory.Exists(promptsDirFull))
                    Directory.CreateDirectory(promptsDirFull);

                // 5. Write file (unique timestamp name)
                var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                var fileName = $"prompt-{ts}.md";
                var relPath = Path.Combine(promptsFolder, fileName).Replace('\\', '/');
                var fullPath = Path.Combine(repoPath, relPath);
                var content = $"""---\ncreated_at: {DateTime.UtcNow:O}\n---\n{promptText}\n""";
                File.WriteAllText(fullPath, content);

                // 6. Commit
                RunGit(repoPath, $"add \"{relPath}\"");
                RunGit(repoPath, $"commit -m \"Add prompt {fileName}\"");
                var sha = RunGit(repoPath, "rev-parse HEAD", capture: true).stdout.Trim();

                // 7. Optional push
                bool pushed = false;
                if (push)
                {
                    if (string.IsNullOrWhiteSpace(remote))
                    {
                        // If push requested but no remote configured, return an error-like result but keep the commit local
                        return PromptSaveResult.Fail("GIT_PUSH enabled but no GIT_REMOTE / PROMPT_REPO_URL configured.");
                    }
                    var (exitCode, stdout, stderr) = RunGit(repoPath, "push origin HEAD", capture: true, ignoreErrors: true);
                    pushed = exitCode == 0;
                }

                return PromptSaveResult.Ok(relPath, sha, pushed);
            }
            catch (Exception ex)
            {
                return PromptSaveResult.Fail(ex.Message);
            }
        }

        private static (int exitCode, string stdout, string stderr) RunGit(string cwd, string args, bool capture = false, bool ignoreErrors = false)
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = cwd,
                RedirectStandardOutput = capture,
                RedirectStandardError = capture,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            var stdout = capture ? p.StandardOutput.ReadToEnd() : string.Empty;
            var stderr = capture ? p.StandardError.ReadToEnd() : string.Empty;
            p.WaitForExit();
            if (p.ExitCode != 0 && !ignoreErrors)
                throw new Exception($"git {args} failed: {stderr.Trim()}");
            return (p.ExitCode, stdout, stderr);
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
