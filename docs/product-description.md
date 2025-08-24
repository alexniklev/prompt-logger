# Prompt Logger — MCP Server Product Description

## Summary
A compact MCP (Model Context Protocol) server that saves and retrieves prompts in a Git repository. The server's primary responsibilities are:

- Persist prompts into a git repository as discrete, versioned artifacts.
- Retrieve prompts (single or list) from the repository via a stable API.

This document defines the product behaviour, API contract, data model, architecture, operational concerns, edge cases, tests, and questions to clarify requirements before implementation.

## Goals (explicit)
- Save prompts to a git repository.
- Return prompts from the repository via an API.

## Non-goals (for now)
- Complex full-text search/indexing (beyond simple filename/tag filtering).
- Long-term archival/retention rules beyond git history.
- UI; this is a server/API only.

## High-level architecture

- HTTP API (MCP-compatible) exposes endpoints to store and fetch prompts.
- Local clone of a git repo (or mounted repo path) is used as the authoritative store.
- Each prompt saved becomes a file under a repository folder (e.g., `prompts/`) and is committed with metadata.
- Git operations are performed atomically and surfaced in API responses (commit SHA, file path).

ASCII overview:

  [Client/API] <--HTTP--> [MCP Server]
                                  |
                                  v
                         [Local Git working tree]
                                  |
                                  v
                             [Remote Git]

## API Contract (minimal)

Inputs/Outputs contract — two core endpoints (expandable):

1) Save prompt
- Endpoint: POST /prompts
- Request JSON:
  - `text` (string, required): the prompt text
  - `metadata` (object, optional): free-form metadata (author, tags, model, source, session_id, etc.)
  - `filename` (string, optional): desired filename (server will validate/slugify)
  - `branch` (string, optional): target branch to commit to (default: configured branch)
- Response JSON (201 on success):
  - `id` (string): internal prompt id (e.g., file path or generated id)
  - `path` (string): repo relative file path where the prompt was written
  - `commit` (string): git commit SHA
  - `timestamp` (ISO8601)
  - `message` (string) human readable confirmation
- Error codes:
  - 400 invalid input
  - 409 conflict (e.g., filename already exists if strict)
  - 500 git or server error

2) List prompts
- Endpoint: GET /prompts
- Query params optional:
  - `path` (string) filter prefix (e.g., `prompts/2025-...`)
  - `tag` (string) metadata tag filter
  - `limit` (int), `offset` (int)
- Response JSON (200): array of prompt summaries:
  - `id`, `path`, `commit`, `timestamp`, `metadata` (minimal)

3) Get single prompt
- Endpoint: GET /prompts/{id}
- Response JSON (200):
  - `id`, `path`, `commit`, `timestamp`, `text`, `metadata`
- 404 if not found

Optional extensions (not required now): delete, update, search, raw file download, history (git log for a prompt)

## Data model (on-disk)

Store prompts as files under `prompts/` inside the repository. File formats to consider:
- Markdown (.md) with frontmatter metadata (YAML) + prompt body
- JSON (.json) with fields { id, text, metadata, created_at }

Recommendation: use Markdown or JSON. Markdown is human-friendly; JSON is machine-friendly. We can support both or pick one. By default use Markdown with a small frontmatter block.

Sample file: `prompts/2025-08-24_15-03-12__<slug>.md`

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

Storing commit SHA in the file is optional; `git log` links the file to commits.

## Save flow (detailed)
1. Validate request payload (required fields, size limits).
2. Determine filename (either provided or generated): pattern -> `prompts/{timestamp}__{slug}.md`.
3. Write file to local working copy (create directory if needed).
4. Git add the file.
5. Git commit with a message like `Add prompt: {slug}` and author from config or metadata.
6. Optionally push to remote (configurable: push_on_write boolean).
7. Return response with file path and commit SHA.

Atomicity and concurrency:
- Use file-level locking or an internal mutex to serialize writes to the working tree.
- If push fails, still return success for the local commit but include a warning in the response.

## Retrieve flow (detailed)
- For list: read the `prompts/` folder tree and parse filenames and frontmatter (or use a JSON index if implemented later).
- For single read: locate the file by id/path, read frontmatter and body, return.
- Optionally fetch from remote if local copy is missing or stale (configurable pull_on_read boolean).

## Edge cases and design decisions
- Duplicate prompts: Should the server dedupe identical text? (configurable)
- Filename collisions: If provided filename exists, either reject (409) or append suffix.
- Large prompts: enforce a reasonable maximum size (e.g., 256KB) to avoid git/performance issues.
- Binary data: not supported.
- Non-text metadata: metadata must be JSON-serializable; nested objects allowed.

## Security
- Ensure input sanitization to avoid path traversal attacks (reject filenames with `..` or absolute paths).
- Repo credentials must be provided to the server via environment variables only:
  - `GIT_REMOTE` (URL)
  - `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL` (optional)
  - `GIT_PUSH_ON_WRITE` (true/false)
  - `GIT_PRIVATE_KEY` or use system ssh agent
  - `GIT_TOKEN` if using HTTPS token auth
- API authentication: the server should be placed behind auth (token or API key) — design does not include a full auth system but supports `Authorization: Bearer <token>` header.
- TLS termination should be handled externally (reverse proxy) in production.

## Concurrency and robustness
- Serialize git operations with a lock or queue.
- Timeouts for git network operations.
- Retries on push/pull with exponential backoff (configurable retry count).

## Observability
- Emit structured logs for operations (save, read, git errors) with trace ids.
- Health endpoint: GET /health that checks repository availability and disk space.
- Expose metrics (Prometheus) for prompt counts, commit failures, API latency.

## Testing strategy
- Unit tests for filename generation, payload validation, frontmatter parsing.
- Integration tests using a temporary local git repo (init, start server, call POST, check commit and file content).
- End-to-end smoke test that exercises commit and optionally push to a test remote.

Minimal test cases:
- Happy path: POST valid prompt -> file created, commit exists, GET returns correct text.
- Filename collision: same filename twice -> 409 or suffix behaviour.
- Invalid input: missing `text` -> 400.
- Large prompt: > limit -> 413 or 400.

## Implementation considerations
- Language/framework: chosen by preference. Supported/considered options:
  - .NET (C#) — ASP.NET Core Minimal APIs or Controllers (recommended based on your choice). Use `LibGit2Sharp` or the `git` CLI for repository operations; Docker-friendly and easy to produce a single runnable app.
  - Node.js: quick to prototype, rich git libs (simple-git)
  - Python: FastAPI, pygit2 or GitPython
  - Go: fast, portable, use `git` CLI or go-git

- Git handling: prefer invoking the `git` CLI for broad compatibility, or use a library (for .NET: `LibGit2Sharp`) for tighter integration. The implementation will allow both modes where practical.
- File format: Markdown frontmatter (YAML) with prompt body is the chosen default (per your answer). It's human-friendly and searchable in the repo.

## Deployment & Ops
- Containerize the server (Docker) and mount SSH credentials or pass tokens via secrets.
- Environment variables to configure repo location and behaviour.
- Health checks and log forwarding.

## Minimal config options
- REPO_PATH: local path or where to clone repo
- REPO_REMOTE: remote URL (optional)
- REPO_BRANCH: default branch (e.g., main)
- PUSH_ON_WRITE: boolean
- PULL_ON_READ: boolean
- MAX_PROMPT_SIZE: bytes
- API_KEY or AUTH_STRATEGY
Additional recommended options based on choices:
- REPO_URL / REPO_REMOTE: remote URL to clone or push to (user-provided)
- PROMPTS_FOLDER: the folder (path) under the repo where prompts will be stored (e.g., `prompts/` or `data/prompts/`); client may choose a subfolder if allowed by server policy.
- PUSH_ON_WRITE: boolean (but per preference, pushing should be explicit per-prompt or on-demand rather than automatic for all writes)
- AUTH_CREDENTIALS: config to supply SSH key, token, or reference to secrets manager (to support user-provided credentials)

## Contract (2–4 bullets)
- Inputs: JSON payloads to POST /prompts containing `text` and optional `metadata`.
- Outputs: JSON responses containing `id`, `path`, `commit`, `timestamp` on success; proper HTTP status codes for errors.
- Error modes: malformed payloads (400), name collisions (409), git failures (500 + details), auth failure (401).
- Success: prompt stored in repo and can be retrieved via GET /prompts/{id}.

## Next steps (implementation plan)
1. Clarify unanswered questions below.
2. Pick implementation language and basic framework.
3. Scaffold a minimal server with the two endpoints (POST /prompts, GET /prompts).
4. Add local git working tree support and implement save flow.
5. Add tests and CI.

## Requirements coverage checklist
- [x] saves prompts to git repo (described in Save flow; implementation pending)
- [x] return prompts from the repo (described in Retrieve flow; implementation pending)

## Questions / Clarifications (answers provided)
Below are your responses incorporated into the spec so implementation can proceed without waiting for these items.

1. Preferred implementation language/framework: .NET (C#) using ASP.NET Core (Minimal APIs or MVC).
2. Repo provisioning: the user will provide a git repository URL (HTTPS or SSH). The server will accept a user-provided repo URL and clone/use it as the backing store.
3. Push behavior: do not push automatically for every write — pushing to the remote should be explicit per-prompt (an option in the save request) or done on demand. Local commits are created by default; push happens only when requested.
4. On-disk storage format: Markdown (`.md`) with YAML frontmatter and prompt body.
5. Filenames: allow clients to provide filenames (server will validate and slugify; collisions either rejected or resolved with suffix based on server policy).
6. Authentication and repo credentials: yes — the server will accept whatever auth the user provides to access the repo (SSH key, HTTPS token). We will provide config/options to supply these credentials securely (environment variables, mounted secrets, or per-request credentials where safe). Steps 2 and 6 are related and will be implemented together so a user can supply repo URL + credentials.
7. Prompt size constraints: none required initially (no special limits), but the server will include a configurable MAX_PROMPT_SIZE with a sensible default.
8. Git metadata in responses: yes — API responses will include commit SHA, commit author, and timestamp; history can be exposed later as an optional endpoint.
9. Branching: not supported for the initial version — keep it simple (single configured branch). Branch workflows can be added later.
10. MCP standard: no specific standard required — treat "MCP server" here as a server usable by model-context clients.

Additional choice
- Prompt folder location: the user can choose the folder under which prompts are stored in the repository. This will be configurable via `PROMPTS_FOLDER` and can also be provided per-request (within allowed bounds) if desired.

## Notes / assumptions made
- Prompts are plain text and will be stored as Markdown files with YAML frontmatter under a configurable folder (default `prompts/`) inside the repo. The server will allow the user to specify the repo folder where prompts are stored via `PROMPTS_FOLDER` or per-request folder parameter (subject to validation).
- The server will accept a repo URL supplied by the user and credentials (SSH key or token) to access that repo. It will clone or use the provided repo path and perform local commits; pushes are explicit per prompt or on-demand.
- Assumed we can safely perform local filesystem operations and run `git` from the server or use `LibGit2Sharp` for .NET.

---

If this spec looks good, tell me your preferences for the questions above and I will scaffold the project and implement the minimal MCP server with tests and a README. If you'd like changes to the file format or API contract before implementation, say which parts to adjust.
