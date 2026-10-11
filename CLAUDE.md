# MyAIAgent

@AGENTS.md

Claude starts in native Plan mode with `opusplan`: Opus for planning, Sonnet after
leaving Plan mode. See `docs/ai/setup.md` for model overrides and commands.

Use `/plan` → `/build` → `/verify` → `/commit`, or `/commit --pr` when a PR is wanted.
Commands delegate policy to `docs/ai/workflow.md`; do not invent a second workflow.
Use the two named Sonnet reviewers concurrently during Verify, then wait for both.
Existing domain skills and .NET rules remain available on demand.
