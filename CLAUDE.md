# CLAUDE.md

This file provides Claude Code compatibility guidance for this repository.

## Shared Project Instructions

Use `.github/copilot-instructions.md` as the single source of truth for:

- project purpose and architecture boundaries
- build/run workflow
- service ownership and conventions
- source-of-truth files to consult before edits
- maintenance rules for instruction updates

Keep this file minimal to avoid drift between agent-specific instruction files.

## Claude-Specific Notes

- Follow the same build and validation flow documented in `.github/copilot-instructions.md`.
- Prefer source-of-truth files over duplicated prose when uncertain.
- Do not include Claude attribution text in commit messages.
- Never commit secrets, local settings, or service-account keys.
