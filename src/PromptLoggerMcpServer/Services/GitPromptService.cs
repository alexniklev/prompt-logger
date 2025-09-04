using System.Diagnostics;
using System.Text.RegularExpressions;

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
                var fileName = CreateFileName(promptText);
                var relPath = Path.Combine(promptsFolder, fileName).Replace('\\', '/');
                var fullPath = Path.Combine(repoPath, relPath);
                var content = $"---\ncreated_at: {DateTime.UtcNow:O}\n---\n\n{promptText}\n";
                File.WriteAllText(fullPath, content);

                // 4. Commit (minimal)
                RunGit(repoPath, $"pull");
                RunGit(repoPath, $"add \"{relPath}\"");
                RunGit(repoPath, $"commit -m \"Add prompt {fileName}\"");
                RunGit(repoPath, $"push");

                return PromptSaveResult.Ok(relPath, "committed", true);
            }
            catch (Exception ex)
            {
                return PromptSaveResult.Fail(ex.Message);
            }
        }

        private static string CreateFileName(string promptText)
        {
            // timestamp - use filesystem-safe sortable UTC format without colons
            var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");

            // short slug from first non-empty line
            var firstLine = promptText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                      .FirstOrDefault() ?? string.Empty;
            var slug = CreateSlug(firstLine, maxLength: 40);

            var slugPart = string.IsNullOrEmpty(slug) ? string.Empty : $"-{slug}";

            return $"prompt-{ts}-{slugPart}.md";
        }

        private static string CreateSlug(string input, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // lower-case, replace whitespace with hyphen
            var lowered = input.Trim().ToLowerInvariant();
            var replaced = Regex.Replace(lowered, @"\s+", "-");

            // remove invalid filename chars and keep a-z0-9-_ 
            var sanitized = Regex.Replace(replaced, @"[^a-z0-9\-_]", string.Empty);

            // collapse multiple hyphens
            sanitized = Regex.Replace(sanitized, "-{2,}", "-").Trim('-');

            if (sanitized.Length <= maxLength)
                return sanitized;

            return sanitized.Substring(0, maxLength).Trim('-');
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
