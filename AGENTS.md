# LocalModelLab collaboration and safety

## Goal and status

Build an offline, repository-aware C#/.NET coding agent integrated into the developer workflow. Inference benchmarking and independent evaluation are prerequisites, not proof that the agent exists. Read [ROADMAP](docs/ROADMAP.md), [architecture](docs/architecture.md) and [Phase 1B](docs/phase-1b.md) before changing scope.

## Collaboration

- Before implementation, explain affected files, what will change, why, and the expected observable result. Present meaningful trade-offs and acceptance criteria.
- Work in small, independently reviewable milestones. Do not silently expand scope or introduce a new architectural style. Significant architecture and execution-boundary changes require approval.
- Maintain progress, decisions, verification evidence, limitations and next actions in the roadmap/status documents. Distinguish proposed, implemented, locally verified and CI verified. Do not claim independent review until it happened.
- Do not commit or push without explicit authorization. Implementation authorization alone is not commit/push authorization.
- Do not publish raw results containing private repository code, machine identifiers, local paths or secrets. Review and redact artifacts before sharing; do not assume gitignored or already tracked files are safe. Preserve local original evidence.

## Safety and evidence

- Never compile, load or execute model-generated code on the host, including in tests or CI. No host fallback. Generated-code execution requires a separately approved isolated worker and satisfied containment gates.
- Phase 1B.1 implements contracts, static ground truth and governance only: no VM, guest worker, compiler integration, executable-code evaluator or inference changes.
- Never modify finalized inference artifacts. Evaluation links frozen bytes by SHA-256 and writes only to a separate evaluation root when storage is implemented. Never give an evaluator a writable inference store.
- Successful inference is not correctness. Model self-assessment is not a verifier. Keep errors distinct from candidate failures, and lifecycle distinct from correctness.
- Keep hidden observations, tests and negative fixtures out of model-facing prompts and repair feedback. No evaluation package is an inference definition. Public task requirements may be shared; hidden probes may not.
- Preserve first-attempt results separately from future repairs. Independent benchmark repetitions are independent first attempts, not retries. Exclude warmups from correctness summaries and show coverage/errors.

## Coding and architecture

Prefer an explicit, readable Controller/Endpoint -> Application Service -> Domain/Repository/Integration flow. The main use-case method should tell the story from top to bottom. Reuse existing patterns. Clarity outweighs minimum lines or files.

Abstract real complexity or meaningful duplication only. Do not introduce CQRS, mediator, handlers, pipelines, strategies, factories, generic base classes, result hierarchies or wrappers without a concrete current need. Repositories own persistence, not arbitrary orchestration; integrations own provider-specific behavior. Exceptions represent execution failure; legitimate external responses remain data. Preserve validation and error handling.

Use Ponytail as a guard against unnecessary complexity, not as a reason to compress understandable code. Understand the end-to-end flow, inspect a comparable implementation and identify affected callers/tests/configuration before editing. Run focused checks and the required build/tests; report what was not verified.

## Code intelligence

- Activate the repository in Serena and read its manual. Use symbol definitions/references for exact semantic work; fall back to focused source reads when the language server is unavailable.
- For broad architecture/context, use an existing `graphify-out/graph.json` before broad reads. Refresh it once before relying on it after source changes, and once after code changes before finishing. Do not start watch mode without authorization.
- If no graph exists, build a code-only index only for broad cross-file tasks, not tiny edits. Treat graph edges as navigation evidence; verify critical relationships with symbols or source.
- Use `rg` for literals, configuration, non-code content and index gaps. Bound reads; combine index results with source checks for exhaustive/negative claims.
- Delegate only when the user or applicable instructions explicitly authorize it; pass graph/symbol context and uncertainty to delegates.

## Persian responses

Whenever writing Persian prose, use the `persian-rtl` skill. Respond in Persian when the user writes mainly in Persian unless requested otherwise. Keep English terms, numbers, paths, commands and code in left-to-right order.
