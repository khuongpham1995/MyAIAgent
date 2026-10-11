# Verification and permissions

No project hooks are installed. Use `node scripts/ai/quality.mjs` at the Build gate.
Native permissions control tool execution; instructions and regex scans are not
security boundaries. Review gates and commit authorization are in `docs/ai/workflow.md`.
Do not add mutating per-edit hooks or network-installed hook dependencies.
