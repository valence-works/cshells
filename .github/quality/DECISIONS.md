# Quality decisions

Proposals that were considered and declined, so the review job does not raise them again. Re-raise one only with new evidence, and say what changed.

Issues closed as *not planned* count as declined too; the review job checks them directly. Add an entry here when the reasoning is worth keeping longer than the issue.

## Declined during the baseline audit (2026-10-10)

The audit's verifier rejected these findings on their merits. Findings it merged into a filed issue are not listed.

| Proposal | Why it was declined |
|---|---|
| Stop recording release-qualification evidence in `specs/` | A deliberate maintainer release process. Not a code-quality concern. |
| Remove the "Agent Orchestration" section from `AGENTS.md` and delete `.agents/skills` | Deliberate maintainer choices (model routing, Codex use). Only stale or generated parts should go (#192). |
| Merge `IDrainHandler` into `IShellTerminator` | Different timing: drain handlers run while the generation is Draining and scopes may still be active. Terminators run just before disposal. |
| Drop the unused `IHostEnvironment?` parameter from web feature callbacks | Breaks every `IWebShellFeature` and `IMiddlewareShellFeature` implementer for little gain. |
| Move `ShellConfiguration` / `GetConfigurationRoot` out of `CShells.Abstractions` | A documented feature-author helper; it belongs in the abstractions. |
| Reorganize tests from `Unit/`/`Integration/` into feature-area folders | Churn with little payoff; the targeted consolidations (#205, #206, #207) fix most misfiling. |
| Let a database-backed provider return `ShellSettings` directly instead of implementing the blueprint contracts | Large redesign of the central provider contract for modest ergonomic gain. |
| Replace the string-keyed resolution context with a typed, protocol-agnostic record | Superseded by passing `HttpContext` directly (#172). HTTP is the only consumer. |
