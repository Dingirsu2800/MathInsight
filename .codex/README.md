# Codex project configuration

This folder is reserved for optional project-level Codex configuration.

Use the repository-level `AGENTS.md` file as the main shared instruction file for agent behavior.

Do not commit personal Codex files, including:

- `auth.json`
- session logs
- local thread data
- local API keys
- personal MCP credentials
- machine-specific paths

Avoid adding `.codex/config.toml` until the team agrees on shared Codex behavior, because project-level Codex config can override personal settings for users who trust this repository.
