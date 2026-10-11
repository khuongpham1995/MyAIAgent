# MyAIAgent

@AGENTS.md

Claude starts in native Plan mode with `opusplan`: Opus for planning, Sonnet after
leaving Plan mode. See `.agents/docs/setup.md` for model overrides and stage skills.

Use `/plan` → `/build` → `/verify` → `/commit`.
These stage skills delegate policy to `.agents/docs/workflow.md`; do not invent a second workflow.
Use the two named Sonnet reviewers concurrently during Verify, then wait for both.
Existing domain skills and .NET rules remain available on demand.
