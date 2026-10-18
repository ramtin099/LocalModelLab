# Phase 1A implementation and verification

## Committed baseline

The hardening implementation is committed on main at 0a1a7131ae3ca34ca3ffa409d40557fc7f589f7e (parent b9c5e6ca3a1f6e86d9fc9d8ff750e828085bf221). It includes harness 1.1.0, deterministic rotating schedules, singleModel residency enforcement, structured preflight evidence, context/digest checks, buffered partial output preservation, runtime snapshots, reports and the separate comparison.v1.json definition.

GitHub API verification confirmed [build-and-test run 39162228502](https://github.com/ramtin099/LocalModelLab/actions/runs/39162228502) succeeded for that exact commit: both [Windows](https://github.com/ramtin099/LocalModelLab/actions/runs/39162228502/job/117777744785) and [Ubuntu](https://github.com/ramtin099/LocalModelLab/actions/runs/39162228502/job/117777745036) jobs passed. The workflow uses .NET SDK 8.0.x for restore, Release build and tests. These CI results describe the committed baseline, not the changes below.

## Current uncommitted correction

Reviewed across the 2026-10-18/19 local session. No commit or push was made.

- Existing initial.v1.json and comparison.v1.json remain unchanged.
- New comparison.non-thinking.v1.json preserves all original cases/suites and adds version-1.0.0 non-thinking-smoke (512 tokens) and non-thinking-full (2048 tokens). Both use explicit thinking=false, context 4096, rotating scheduling and singleModel residency.
- Both tested Qwen/Gemma digests advertise false/true in saved /api/show metadata; no invented declarations. Explicit false also validates for models advertising thinking=unsupported, matching Ollama 0.40.5 behavior. Missing/unknown capability remains rejected; thinking-capable models still require advertised allowed values. Requests retain explicit think=false.
- Reports retain completed execution status independently of answer usability. Empty final answers, reasoning-only attempts and missing first-answer timing count completed measured attempts; existing truncated count covers all measured attempts explicitly marked truncated. No scoring or status rewriting.
- Markdown exposes diagnostics; summary.csv appends three columns while preserving existing columns/order. Attempt JSON schema 1 and attempts.csv are unchanged. Old artifacts can be reported without migration.
- Regression tests cover null/empty/whitespace answers, reasoning-only versus mixed output, absent reasoning, missing/zero timing, unknown/false/true truncation, warmup and unsuccessful-attempt exclusions, unchanged serialized results, CSV/Markdown rendering, profiles, validation and request options.
- Persian operations and comparison instructions document exact operator tags qwen3.5:4b-q4_K_M and gemma4, installed-name/digest resolution, exact two-model smoke and four-model full commands, and memory-field limitations.

## Local verification of this correction

Executed successfully:

```powershell
dotnet build LocalModelLab.sln -c Release
dotnet test LocalModelLab.sln -c Release --no-build --logger "console;verbosity=minimal"
dotnet run --project LocalModelLab.Cli -c Release --no-build -- cases validate --definitions benchmarks/comparison.non-thinking.v1.json
dotnet run --project LocalModelLab.Cli -c Release --no-build -- benchmark report --run results/comparison/20261018T201755261Z-31358b18d0964db4b99d3dd67c4834af
```

Build: 0 warnings/errors. Tests: 65 passed, 0 failed/skipped on Windows, targeting net8.0. Local SDK is 10.0.402; test runtime is .NET 8.0.32. HTTP regression tests are mocked. The corrected definition validates with four cases, two suites and two profiles. Regenerating a report from the existing smoke artifacts succeeded without altering those attempts: each model has four measured completions, four empty finals, four reasoning-only, four truncated and four missing-first-answer attempts (warmups excluded).

The real default-thinking smoke has 10 successful provider executions including two warmups, all length-limited with empty final answers and exposed reasoning. Context/residency checks passed. This validates execution evidence, not answer usefulness or correctness. The revised non-thinking smoke and full four-model hardware run have not been executed. This uncommitted correction has not run on hosted CI; the successful Windows/Ubuntu CI above belongs to the baseline.

## Scope and limitations

Still Phase 1A local pure inference with the existing CLI -> application service -> infrastructure architecture and no new production dependency. No Agent, Jev, RAG, generated-code execution, LLM judging, subjective correctness scores or ranking.

Ollama 0.40.5 runtime size/size_vram are provider accounting, not file size or utilization telemetry. Gemma's small value is preserved and unexplained without runner logs; see [source investigation](comparison.md). No inferred CPU/GPU split, peak VRAM, utilization, energy or hardware efficiency.

Rotating order does not remove thermal/cache effects. Residency checks are snapshots, not continuous isolation. Provider-minus-load does not prove a warm cache. Missing timings are null, never zero. No periodic sampler, cooling automation, cache flush, statistical significance testing, cross-run aggregation, resume or regeneration. Summary consumers requiring a fixed header must handle the appended columns.

Remaining Phase 1A verification: run the revised smoke, inspect final content/reasoning/truncation/timing/context/residency/digests, then run the full four-model benchmark. A larger output budget may still truncate. New hosted CI requires a future approved commit/push; no claim is made that these local changes passed Ubuntu.
