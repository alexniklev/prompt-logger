using System;
using System.Diagnostics;
using System.IO;

namespace PromptLoggerMcpServer.Services
{
    internal sealed class GitPromptService
    {
        // POC: keep only what we need, do everything inside Save for clarity.
        public PromptSaveResult Save(string promptText)
        {
            if (string.IsNullOrWhiteSpace(promptText))
                return PromptSaveResult.Fail("Prompt is empty");

            // Determine working repo path (default: separate hidden folder, not the project repo)
            var defaultRepoDir = Path.Combine(Directory.GetCurrentDirectory(), ".prompt-logger-repo");
            var repoPath = EnvOr("REPO_PATH", defaultRepoDir);
            var promptsFolder = EnvOr("PROMPTS_FOLDER", "prompts");
            var authorName = Environment.GetEnvironmentVariable("GIT_AUTHOR_NAME");
            var authorEmail = Environment.GetEnvironmentVariable("GIT_AUTHOR_EMAIL");
            // Allow PROMPT_REPO_URL as alias to GIT_REMOTE
            var remote = Environment.GetEnvironmentVariable("PROMPT_REPO_URL") ?? Environment.GetEnvironmentVariable("GIT_REMOTE");
            var pushFlag = Environment.GetEnvironmentVariable("GIT_PUSH");
            var push = !string.IsNullOrWhiteSpace(pushFlag) && (pushFlag.Equals("true", StringComparison.OrdinalIgnoreCase) || pushFlag == "1");

            try
            {
                // 1. Ensure repository: clone if remote provided & repo not present; else init
                if (!Directory.Exists(Path.Combine(repoPath, ".git")))
                {
                    if (!Directory.Exists(repoPath))
                        Directory.CreateDirectory(repoPath);

                    // If directory is empty (or only contains system files) and remote set -> clone
                    var dirEmpty = Directory.GetFileSystemEntries(repoPath).Length == 0;
                    if (!string.IsNullOrWhiteSpace(remote) && dirEmpty)
                    {
                        // Clone into repoPath (git clone <remote> <path>)
                        RunGit(Directory.GetCurrentDirectory(), $"clone {remote} \"{repoPath}\"");
                    }

                    if (!Directory.Exists(Path.Combine(repoPath, ".git")))
                    {
                        RunGit(repoPath, "init");
                        if (!string.IsNullOrWhiteSpace(remote))
                            RunGit(repoPath, $"remote add origin {remote}", ignoreErrors: true);
                    }
                }

                // 2. Optional per-repo user config (ignore failures)
                if (!string.IsNullOrWhiteSpace(authorName))
                    RunGit(repoPath, $"config user.name \"{authorName}\"", ignoreErrors: true);
                if (!string.IsNullOrWhiteSpace(authorEmail))
                    RunGit(repoPath, $"config user.email \"{authorEmail}\"", ignoreErrors: true);

                // 3. Ensure prompts directory
                var promptsDirFull = Path.Combine(repoPath, promptsFolder);
                Directory.CreateDirectory(promptsDirFull);

                // 4. Write file (unique timestamp name)
                var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                var fileName = $"prompt-{ts}.md";
                var relPath = Path.Combine(promptsFolder, fileName).Replace('\\', '/');
                var fullPath = Path.Combine(repoPath, relPath);
                var content = $"""---\ncreated_at: {DateTime.UtcNow:O}\n---\n{promptText}\n""";
                File.WriteAllText(fullPath, content);

                // 5. Commit
                RunGit(repoPath, $"add \"{relPath}\"");
                RunGit(repoPath, $"commit -m \"Add prompt {fileName}\"");
                var sha = RunGit(repoPath, "rev-parse HEAD", capture: true).stdout.Trim();

                // 6. Optional push
                bool pushed = false;
                if (push && !string.IsNullOrWhiteSpace(remote))
                {
                    var pushRes = RunGit(repoPath, "push origin HEAD", capture: true, ignoreErrors: true);
                    pushed = pushRes.exitCode == 0;
                }

                return PromptSaveResult.Ok(relPath, sha, pushed);
            }
            catch (Exception ex)
            {
                return PromptSaveResult.Fail(ex.Message);
            }
        }

        private static string EnvOr(string key, string fallback)
        {
            var v = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(v) ? fallback : v;
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
