# Phase 1A implementation and verification

Completed 2026-10-18. No commits or pushes were made.

## Changed files

This was an empty repository; all application files are new. Template placeholder classes/tests were removed.

| Area | Files |
|---|---|
| Solution/build | `LocalModelLab.sln`, `Directory.Build.props`, `.gitignore` |
| CLI | `LocalModelLab.Cli/LocalModelLab.Cli.csproj`, `LocalModelLab.Cli/Program.cs` |
| Core | `LocalModelLab.Core/LocalModelLab.Core.csproj`, `LocalModelLab.Core/Definitions.cs`, `LocalModelLab.Core/Execution.cs` |
| Infrastructure | `LocalModelLab.Infrastructure/LocalModelLab.Infrastructure.csproj`, `LocalModelLab.Infrastructure/OllamaProvider.cs`, `LocalModelLab.Infrastructure/FileRunStore.cs`, `LocalModelLab.Infrastructure/Reports.cs`, `LocalModelLab.Infrastructure/MachineMetadata.cs` |
| Tests | `LocalModelLab.Tests/LocalModelLab.Tests.csproj`, `LocalModelLab.Tests/TestSupport.cs`, `LocalModelLab.Tests/ValidationTests.cs`, `LocalModelLab.Tests/OllamaProviderTests.cs`, `LocalModelLab.Tests/BenchmarkServiceTests.cs` |
| Definitions | `benchmarks/initial.v1.json` |
| Documentation | `README.md`, `docs/architecture.md`, `docs/operations.fa.md`, `docs/implementation-status.md` |

Local `results/` smoke artifacts and `graphify-out/` navigation index are intentionally gitignored. Existing IDE/Serena directories were preserved.

## Decisions and features

The four projects target net8.0. Cli calls BenchmarkService; Infrastructure implements inference and storage contracts. The application service explicitly validates selections, resolves metadata, snapshots a run, executes independent sequential attempts, persists each outcome and handles cancellation. Provider-specific runtime verification and protocol parsing stay in Infrastructure. There are no new application layers, mediator, CQRS, generic repositories or production package dependencies.

Implemented: versioned JSON cases/suites/profiles; strict field/reference/setting validation; single/multiple models; digest-bound capabilities with distinct missing/unknown/unsupported states; configurable warmups, repetitions, sampling, context/output limits, thinking, streaming, keep-alive and request timeout; raw response recording; separate reasoning/answer output when exposed; client and provider metrics; terminal reasons/truncation; digest checks; model/provider/harness/machine provenance; atomic JSON persistence and partial results; all five CLI workflows; per-model/per-case Markdown and CSV reports; Persian operating instructions.

No generated code was compiled or executed. No evaluation scores, self-assessment scores, automated judge or universal model ranking exist. Future executors have capability names only.

## Automated verification

Executed successfully:

```powershell
dotnet build LocalModelLab.sln -c Release
dotnet test LocalModelLab.sln -c Release --no-build --logger "console;verbosity=minimal"
```

- Build: 0 warnings, 0 errors, using SDK 10.0.402 and targeting .NET 8.
- Tests: 37 passed, 0 failed, 0 skipped, running on .NET 8.0.32.
- All HTTP integration tests use an in-memory HttpMessageHandler; tests do not require an Ollama installation or service.
- Coverage includes configuration/fingerprint validation; capability distinctions and digest-bound declarations; unsupported thinking/remote models; model metadata/version/digest discovery; exact request options and independent messages; exact UTF-8/NDJSON raw capture; answer/reasoning separation; streaming/nonstreaming timing semantics; null/zero metrics; HTTP/provider errors; malformed/incomplete streams; no retries; multiple-model sequential orchestration; timeout and cancellation before/during response streaming; partial result persistence; digest drift; provider context clamp; atomic writes/path traversal protection; reports excluding warmups and failed timing samples.
- Actual CLI help, model discovery, definition validation, run, inspect and report commands were exercised successfully.
- A local code-only Graphify index was built for navigation. Serena activation succeeded but symbol lookup was unavailable because the pre-existing project configuration has no active language server; focused source searches and compilation supplied verification.

## Real Ollama smoke

Executed the supplied smoke suite/profile on installed `granite4:3b`, Ollama 0.40.5, on Windows with i7-12700H, approximately 16 GB RAM and RTX 3060 Laptop GPU. NVIDIA discovery recorded 6144 MiB VRAM and driver 610.88.

Final smoke artifacts:

```text
results/20261018T090219300Z-2fa623c1e1824c1fb7ee466cae504e28/
reports/28db3632226d437dbf0bec143d1a172e/report.md
```

One warmup and one measured attempt completed, with model digest verified after each. The measured attempt recorded 635.6814 ms client time, 27.7252 ms first answer time, 7.1749 ms provider load time, 64 generated tokens and verified context length 2048. Both attempts ended with reason `length` and correctly have `outputTruncated: true`. This is a connectivity/artifact smoke test, not a model-quality conclusion or a statistically useful performance benchmark. Earlier smoke artifacts also preserve a run with approximately 3.44 seconds of model-loading latency.

## Known limitations

- Real execution was verified for Granite chat streaming only. Other models, thinking, nonstreaming, errors and multiple-model orchestration have mocked coverage; no full initial-suite real-model run was performed.
- Sent sampling options, model defaults and observed runtime context are retained. Ollama does not echo every effective internal option; unobservable values remain unverified. Explicit unsupported/unknown thinking is rejected rather than replaced.
- Digest checks detect changes before/after trials but cannot eliminate an external mutation between API calls. Keep model files and provider version stable during a run.
- First output times are client observations and include load/network/capture overhead. Persistent raw capture is inside the client interval; provider metrics are separate. Keep-alive and warmups cannot guarantee model residency or remove provider prompt-cache effects.
- Graceful interruption finalizes the active trial; forced termination can leave an in-progress marker and raw bytes without a parsed partial answer. Report/inspect do not resume or regenerate attempts. Atomicity is per JSON file, not across all files or against disk failure.
- When runtime or hardware discovery is unavailable, nullable data and explicit discovery errors are retained. Win32 GPU AdapterRAM alone is not reliable above 4 GiB; NVIDIA memory metadata is preferred when available.
- Filesystem artifacts are immutable by runner convention after completion, not write-protected or signed. Deliberate external edits remain possible.

## Remaining Phase 1B work

Requires separate approval: independent versioned evaluation contracts/artifacts, deterministic correctness checks and their tests, resource-limit and isolation design, worker contracts and a threat model before any generated-code execution. Preserve inference/evaluation separation. FIM, embeddings, vision/decision execution, agents/tools, RAG, Jev, dashboards and LLM judging remain outside this implementation and need explicit future scoping.
