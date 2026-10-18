# Phase 1A decisions (approved)

Status: implemented scope; artifact schema and harness version 1.0.0.

- .NET 8 / C#, four projects at repository root, no src folder.
- Cli owns transport, command parsing and cancellation. Core owns contracts, validation and the explicit sequential BenchmarkService flow. Infrastructure owns Ollama HTTP, filesystem persistence, reports and machine discovery. Tests use mocked HTTP, no Ollama dependency.
- Service orchestration; no CQRS, mediator, generic repositories or stage framework.
- JSON definitions contain independently versioned cases, suites and profiles. A run snapshots the exact input bytes, SHA-256 fingerprints, resolved metadata, binary fingerprints and settings. Existing runs are never reused or overwritten.
- Ollama chat is the only executor. Each attempt sends only the case's system/user messages. Warmups and measured attempts are retained separately. No retries or regeneration.
- Preflight all models before inference. Unsupported, unknown and missing capabilities are distinct and requested capabilities must be supported. Explicit digest-bound declarations can supply knowledge absent from older providers; their provenance remains in the snapshot.
- Provider-advertised metadata, raw HTTP bodies and sent options are authoritative evidence, not proof the provider honored a setting. Effective server-internal defaults are unknown unless exposed; never invent them.
- Persist requests before sending, flush raw bytes while reading, atomically replace the attempt result and run journal. A killed process may leave an in-progress result; inspect/report read attempt files and identify this. Graceful interruption finalizes the active attempt. Atomicity applies to individual JSON files, not a transaction across the whole directory.
- Use monotonic client clocks. Keep provider nanoseconds, load time, prompt evaluation and generation separate. First content timestamps include load and network time. Provider time excluding load is derived only when both values exist. Null means unavailable. No correctness scores or universal ranking, and tokenizer rates cannot be used as universal quality comparisons.
- Capture post-attempt /api/ps metadata and verify the provider context length when exposed. A context clamp is an explicit failed trial. Other internal sampling settings are not echoed by Ollama and remain unverified. Runtime metadata discovery is outside the timed inference interval; unavailable runtime metadata is retained as an explicit error with null observations.
- Model tags are mutable. Check digest before and after every attempt and fail attribution on drift. Do not mutate or pull models; users must keep the model store stable during a run. Checks cannot eliminate an external concurrent mutation between API calls.

## Boundaries

Phase 1A collects responses and inference metrics only. Never compile or execute generated code. No code quality scoring, self-assessment scoring, LLM judge, Linux worker/VM, agents, tools, RAG, Jev, web dashboard, FIM execution or embedding execution.

Capability declarations reserve Chat, FIM, Embedding, Vision, Decision and Thinking names. Reserving names does not implement executors or claim provider support.

## Phase 1B (requires separate approval)

Design independent evaluation artifacts and deterministic checks, resource isolation and threat model before any generated-code execution. Specify worker contracts, cancellation/resource limits, and evaluation provenance. Add future executors only under their own scope. Do not mix evaluation results with inference measurements.
