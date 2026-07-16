# CLAUDE.md

## Purpose

This file contains Claude Code specific instructions.

Project-wide rules are defined in AGENTS.md.

Always follow AGENTS.md first.

---

## Startup workflow

When starting a new conversation:

1. Read AGENTS.md.
2. Read the relevant specification under `/specs`.
3. Read the related documents under `/docs`.
4. Read the existing implementation before making changes.

Never assume business rules.

---

## Implementation workflow

For every task:

1. Understand the requirement.
2. Locate related modules.
3. Explain the implementation plan.
4. Wait for confirmation if the change is large.
5. Implement.
6. Verify.
7. Summarize modified files.

---

## Scope control

Never modify unrelated modules.

Never refactor the whole project unless explicitly requested.

Prefer minimal changes.

---

## Working with Spec Kit

When a specification exists under `/specs`:

- Treat it as the primary implementation source.
- Follow the implementation plan.
- Follow the generated task list.

If no specification exists:

Explain that one should be created first instead of guessing requirements.

---

## Documentation

When business logic is unclear:

Read documents under `/docs`.

Do not invent missing requirements.

---

## Git

Never:

- commit automatically
- push automatically
- resolve merge conflicts automatically

---

## Code generation

Before generating code:

- Reuse existing services.
- Reuse existing DTOs.
- Reuse existing repositories.

Avoid duplicate implementations.

---

## Database

Never:

- generate migrations
- modify schema
- rename tables
- rename columns

unless explicitly requested.

---

## Response style

When making changes:

Explain:

- why
- affected files
- impact

Keep responses concise.

Do not rewrite unrelated code.