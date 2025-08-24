# Prompt Logger — MCP Server

A compact MCP (Model Context Protocol) server for saving and retrieving prompts in a Git repository. This project provides a minimal HTTP API for prompt storage and retrieval, using Markdown files with YAML frontmatter as the on-disk format.

## Features

- Save prompts to a Git repository as versioned Markdown files.
- Retrieve prompts (single or list) via a stable API.
- Configurable prompt folder location (`PROMPTS_FOLDER`).
- Git commit metadata included in API responses.
- Supports user-provided repo URL and credentials (SSH key or HTTPS token).
- Local commits by default; pushes are explicit per prompt or on demand.
- Configurable maximum prompt size (`MAX_PROMPT_SIZE`).
- Health endpoint and structured logging.

## API Endpoints

### 1. Save Prompt

- **POST /prompts**
- **Request JSON:**
  - `text` (string, required): prompt text
  - `metadata` (object, optional): free-form metadata (author, tags, etc.)
  - `filename` (string, optional): desired filename (validated/slugified)
  - `branch` (string, optional): target branch (default: configured branch)
- **Response JSON:**
  - `id`, `path`, `commit`, `timestamp`, `message`
- **Error codes:** 400 (invalid input), 409 (conflict), 500 (server error)

### 2. List Prompts

- **GET /prompts**
- **Query params:** `path`, `tag`, `limit`, `offset`
- **Response:** Array of prompt summaries

### 3. Get Single Prompt

- **GET /prompts/{id}**
- **Response:** Full prompt details

## Data Model

Prompts are stored as Markdown files with YAML frontmatter under a configurable folder (default: `prompts/`). Example file:

```markdown
---
metadata:
  author: "automated"
  tags: ["chat", "test"]
  model: "gpt-4"
  session: "abc123"
  source: "api"
created_at: 2025-08-24T15:03:12Z
commit: <sha>
---

<prompt text here>
```

## Configuration

- `REPO_PATH`: local repo path or clone location
- `REPO_REMOTE`: remote URL (user-provided)
- `PROMPTS_FOLDER`: folder for prompt files
- `MAX_PROMPT_SIZE`: maximum allowed prompt size
- `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL`: commit author info
- `GIT_PUSH_ON_WRITE`: push on save (optional)
- Credentials via environment variables (`GIT_PRIVATE_KEY`, `GIT_TOKEN`)

## Security

- Input validation and path sanitization
- Credentials via environment variables only
- API authentication via token (optional)
- TLS handled externally in production

## Testing

- Unit tests for filename generation, validation, and parsing
- Integration tests with local git repo
- End-to-end smoke tests for commit and retrieval

## Development

To run locally:

```bash
dotnet run --project src/PromptLoggerMcpServer
```

Configure your MCP server in VS Code with `.vscode/mcp.json`:

```json
{
  "servers": {
    "PromptLoggerMcpServer": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "src/PromptLoggerMcpServer"
      ],
      "env": {
        "WEATHER_CHOICES": "sunny,humid,freezing,perfect"
      }
    }
  }
}
```

## References

- [ModelContextProtocol Documentation](https://modelcontextprotocol.io/)
- [Protocol Specification](https://spec.modelcontextprotocol.io/)
- [GitHub Organization](https://github.com/modelcontextprotocol)
- [VS Code MCP Servers Guide](https://code.visualstudio.com/docs/copilot/chat/mcp-servers)
- [NuGet MCP Guide](https://aka.ms/nuget/mcp/guide)

---

For questions or changes to the API or file format, see `docs/product-description.md` and open an issue or