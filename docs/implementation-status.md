# Phase 1A implementation and verification

Hardening reviewed 2026-10-18 against local commit `b9c5e6ca3a1f6e86d9fc9d8ff750e828085bf221` on main. A read-only `git ls-remote` confirmed origin/main at the same commit during this review. The repository already contained the four-project Phase 1A implementation; it was not empty. This hardening is an uncommitted working-tree change. No commits or pushes were made.

## Scope and changed files

| Files | Change |
|---|---|
| `Directory.Build.props` | Assembly/package version 1.1.0 |
| `LocalModelLab.Core/Definitions.cs` | Validated scheduling/residency enums with legacy defaults |
| `LocalModelLab.Core/Execution.cs` | Recorded schedule, preflight journal/selection, classified failures, explicit residency orchestration, attempt metadata |
| `LocalModelLab.Infrastructure/OllamaProvider.cs` | Buffered answer/reasoning, unload/verify residency, timestamped runtime samples |
| `LocalModelLab.Infrastructure/MachineMetadata.cs` | Distinguish static inventory from uncollected utilization/peak/energy telemetry |
| `LocalModelLab.Infrastructure/Reports.cs` | Report preflight failures without manifest and display policies/failure evidence |
| `LocalModelLab.Cli/Program.cs` | Inspect missing-manifest runs, preserve service validation evidence, distinguish provider timeout from user cancellation, harness help version |
| `LocalModelLab.Tests/HardeningTests.cs` | New regression coverage for the corrected behaviors |
| `LocalModelLab.Tests/BenchmarkServiceTests.cs` | Exact unavailable-model exception assertion |
| `LocalModelLab.Tests/TestSupport.cs` | Mock request hook and correct partial reads in interruption fixture |
| `.github/workflows/ci.yml` | .NET 8 build/test on Ubuntu and Windows, TRX artifacts, no Ollama |
| `benchmarks/comparison.v1.json` | Separate opt-in four-model comparison profile; existing initial definition unchanged |
| `README.md`, `docs/architecture.md`, `docs/operations.fa.md`, `docs/comparison.md`, this file | Current behavior, limits and controlled comparison instructions |

.NET 8, existing CLI -> application service -> infrastructure architecture, no new production dependency. Still Phase 1A pure inference. No agents, RAG, Jev, generated-code execution, scoring or ranking were introduced.

## Verified during this review

```powershell
dotnet build LocalModelLab.sln -c Release
dotnet test LocalModelLab.sln -c Release --no-build --logger "console;verbosity=minimal"
```

Build succeeded with 0 warnings and 0 errors, using SDK 10.0.402 and targeting net8.0; tests ran on .NET 8.0.32. No .NET 8 SDK is installed locally; CI is configured to compile using SDK 8.0.x. Tests: 55 passed, 0 failed, 0 skipped. All HTTP tests use mocked HttpMessageHandler; no Ollama is required. Existing tests continue to verify legacy model-first order, exact requests/raw bytes, metrics, timeout, digest checks, context clamps, cancellation and reports. New cases verify balanced deterministic rotation for four models/multiple cases, persisted plan matching executed attempts, unchanged legacy profile defaults, invalid policy rejection, preflight invalid configuration/missing model/provider failure/provider timeout/cancellation evidence, preflight reports without manifest, unload failure, residency unavailability/contamination/malformed observations, preserved load metrics and residency snapshots, and buffered partial answer/reasoning with an incomplete raw tail on cancellation.

CLI help, comparison-definition validation, and invalid-configuration run/inspect/report were also executed successfully. Verification artifacts are under ignored `results/hardening-verification/`.

No live inference was run during hardening. The previous document described an earlier Granite-only smoke run; it is historical evidence for the old implementation, not verification of these changes. The four-model comparison remains to be performed on real hardware. GitHub-hosted CI has been configured but cannot be claimed successful until it runs after an approved push.

## Behavior and compatibility

- Harness version 1.1.0; artifact schema remains 1. Existing profiles default to modelFirst/providerManaged. New policies are explicit; exact schedule is written before inference. No retries or changes to prompts/output limits/scoring.
- New reader accepts older artifacts. Existing fingerprints are retained when reading old runs; new fingerprints include added policy fields. Older strict readers may reject new additive artifact fields.
- Configuration/model preflight after run creation leaves definitions, selection and final state. A manifest/schedule exists only after successful preflight. Invalid CLI syntax, input read/JSON parsing, endpoint validation or machine discovery cancellation can fail before creating a directory.
- singleModel unloads other digests, including unselected models, then verifies snapshots. Failure to query/enforce is an attempt failure. Keep-alive is unchanged; target load is timed/preserved. Empty post-inference residency is unknown verification (null), not evidence of a resident target. Defaults remain provider-managed and make no exclusivity claim.
- TimeoutSeconds now bounds residency preparation, inference and postflight checks. ClientElapsedMs still covers only inference including raw recording; policy/metadata work is outside that metric. User cancellation is interrupted/cancelled; provider timeout is a failure, not exit code 130.
- Static hardware metadata and timestamped residency snapshots are distinguished. Utilization, peak VRAM, energy and precise CPU/GPU attribution are not collected or inferred.

## Limits and remaining Phase 1A work

Run the documented four-model comparison and inspect real unload timing, context observations, load/cache metrics, truncation and digest stability. The comparison uses provider-default thinking, not equal reasoning budgets. Complete rotations balance positions but cannot eliminate thermal drift, internal prompt caches, OS page caches or external clients. Dedicated-server settings are operator-controlled, not verified by the harness. Runtime observations are snapshots, not continuous isolation evidence.

No periodic NVIDIA sampler, process attribution, automated cooling, cache flushing, statistical significance testing or cross-run aggregation is implemented. Add these only for a concrete measurement need. Disk failure/forced termination can leave incomplete evidence; atomic writes are per file. No resume/regeneration. Discovery currently fetches show metadata for all installed models, so an unrelated broken model can prevent preflight. Keep the dedicated model store healthy and stable.

Any Phase 1B evaluation or generated-code execution needs separate scope and approval; it is not remaining work for this Phase 1A hardening.
