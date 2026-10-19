# Implementation and verification status

## Current Phase 1B.1 status: not accepted

Current committed revision: `28417ce290fa560ec349803f0bee284806bff9cb`. [Actions run 39251761172](https://github.com/ramtin099/LocalModelLab/actions/runs/39251761172) completed both builds; [Windows job 118057016721](https://github.com/ramtin099/LocalModelLab/actions/runs/39251761172/job/118057016721) passed **118/118**, while [Ubuntu job 118057016297](https://github.com/ramtin099/LocalModelLab/actions/runs/39251761172/job/118057016297) passed **117/118**. Both had zero skipped tests; Ubuntu failed VersionOnePackageAndSourceBindingAreIntact at line 31. The raw artifact-hash correction was committed, but Phase 1B.1 did not meet its cross-platform acceptance gate.

The new root cause is verified in the actual .NET implementation: indented BenchmarkCase serialization contains OS-dependent structural newlines. The same source case serializes to 414 CRLF bytes hashing to `90ce6abc62003f6fce835c6eced0fcd8573e20b68b803fe8d7996cd01c365eef`, or 409 LF bytes hashing to `ad82ca5fb410ce52d6bd7b5aebe03acdd4e291b6cabb20c82b166c36b0b7273d`. The package's implicit Windows identity was compared to Ubuntu's native identity. This is separate from Git normalization of physical artifact files.

Current correction: **uncommitted and unpushed**. It adds versioned, tagged canonical logical fingerprints and explicit legacy CRLF/LF compatibility for source bindings, retaining the recorded Windows digest. Raw SHA-256 verification and Phase 1A fingerprint generation/frozen evidence are unchanged. The evaluation package and source reference now explicitly name the compatible algorithm. Required labels amend the still-unaccepted evaluation draft; no silent OS guessing or historical migration is performed. Full specification and all case/suite/profile/report/manifest caller findings: [fingerprints.md](fingerprints.md).

Local Windows Release build: **0 warnings, 0 errors**. Full tests: **145 passed, 0 failed, 0 skipped**, including fixed cross-platform canonical/legacy vectors and the existing strict raw-byte tests. Local runtime is .NET 8.0.32; SDK 10.0.402. These are local results, not hosted acceptance for a new commit. After explicit commit/push authorization, verify both hosted jobs for the exact corrected SHA and update this record. Ground-truth review and Phase 1B.2 remain pending; no candidate code execution, inference artifact edits, commit or push occurred during this correction.

## Historical Phase 1B.1 raw-hash correction

Phase 1B.1 was committed as `c47a92b01f80735b92a76bb124f6e1e013b5f62b` (`1b.1`). It contains evaluation contracts/validation, a static string-counting package, synthetic serialization fixtures and governance only. It does not implement Phase 1B.2 or generated-code execution.

[Actions run 39247348204](https://github.com/ramtin099/LocalModelLab/actions/runs/39247348204) is **failed**, not accepted: [Ubuntu job 118042397060](https://github.com/ramtin099/LocalModelLab/actions/runs/39247348204/job/118042397060) built with zero warnings/errors, then ran 108 tests: 107 passed, 1 failed, 0 skipped. `EvaluationContractTests.VersionOnePackageAndSourceBindingAreIntact` found the observations.json SHA-256 mismatch. [Windows job 118042396586](https://github.com/ramtin099/LocalModelLab/actions/runs/39247348204/job/118042396586) was cancelled during build by matrix fail-fast; its tests were skipped. There is no Windows CI pass for this commit.

The confirmed cause was hashes computed over pre-normalization CRLF working-tree JSON while Git committed LF blobs. Existing `.gitattributes` correctly specified `text eol=lf`, but did not rewrite already present working copies. Original observations.json: 4,993 CRLF bytes hash to the recorded `9f1bab6208b5ce4b936de424fa63b5289ab568cca877bcf28512ee44ccd366ff`; its 4,775 committed LF bytes hash to Ubuntu's actual `f43ef22b6a3bb2f517d2397680597bbdfa7efb832bc1175ee701d36eabad16ca`. incorrect-fixtures.json and the synthetic manifest's definition reference had the same defect. Details and the corrected hash chain are in [Phase 1B verification](phase-1b.md#ci-correction-for-c47a92b).

That correction was locally verified on Windows before commit: Release build zero warnings/errors; **118 passed, 0 failed, 0 skipped**. Package/golden files use exact UTF-8/LF bytes; raw-byte integrity checks remain enabled. Additional tests cover byte format, hash sensitivity, actual synthetic-manifest binding, cross-reference consistency and declared expected groups. Fresh checkouts from a temporary Git index with core.autocrlf=false and true produced identical bytes for all eight artifact files. This was a Git checkout test on Windows, not an Ubuntu test run. CI matrix fail-fast was disabled so both jobs could finish independently. It was subsequently committed as 28417ce; the newer CI state is recorded above.

No benchmark code was compiled/loaded/executed and no inference artifacts or prompts were modified by that correction. Its subsequent hosted results did not satisfy acceptance; see the current status above. Independent ground-truth review and all Phase 1B.2 execution gates also remain pending.

## Current milestone update: 2026-10-19

The non-thinking smoke and full four-model hardware benchmark have now completed. Full run `non-thinking-full/20261019T103606112Z-f56b1b2ca7bc4c769c713cf2cfc3d02b` records completed execution with 80 finalized attempts: 16 warmups and 64 measured, four cases and four models. Local `state.json`, `schedule.json`, manifest and per-attempt artifacts are the evidence. Inference completion is not correctness. Manual review reported C# and contract errors; no automated correctness verdict is claimed.

This update supersedes the pending-run statements in the historical correction record below; its earlier build/test/CI claims remain scoped to the revisions they describe. Phase 1B.1 contracts, static reference specifications and governance are documented in [Phase 1B](phase-1b.md) and [ROADMAP](ROADMAP.md). No VM, worker or generated-code execution has been added. Current verification is recorded there, not attributed to historical CI.

## Historical Phase 1A committed baseline

The hardening implementation is committed on main at 0a1a7131ae3ca34ca3ffa409d40557fc7f589f7e (parent b9c5e6ca3a1f6e86d9fc9d8ff750e828085bf221). It includes harness 1.1.0, deterministic rotating schedules, singleModel residency enforcement, structured preflight evidence, context/digest checks, buffered partial output preservation, runtime snapshots, reports and the separate comparison.v1.json definition.

GitHub API verification confirmed [build-and-test run 39162228502](https://github.com/ramtin099/LocalModelLab/actions/runs/39162228502) succeeded for that exact commit: both [Windows](https://github.com/ramtin099/LocalModelLab/actions/runs/39162228502/job/117777744785) and [Ubuntu](https://github.com/ramtin099/LocalModelLab/actions/runs/39162228502/job/117777745036) jobs passed. The workflow uses .NET SDK 8.0.x for restore, Release build and tests. These CI results describe the committed baseline, not the changes below.

## Historical correction record (before full-run completion)

Reviewed across the 2026-10-18/19 local session. No commit or push was made.

- Existing initial.v1.json and comparison.v1.json remain unchanged.
- New comparison.non-thinking.v1.json preserves all original cases/suites and adds version-1.0.0 non-thinking-smoke (512 tokens) and non-thinking-full (2048 tokens). Both use explicit thinking=false, context 4096, rotating scheduling and singleModel residency.
- Both tested Qwen/Gemma digests advertise false/true in saved /api/show metadata; no invented declarations. Explicit false also validates for models advertising thinking=unsupported, matching Ollama 0.40.5 behavior. Missing/unknown capability remains rejected; thinking-capable models still require advertised allowed values. Requests retain explicit think=false.
- Reports retain completed execution status independently of answer usability. Empty final answers, reasoning-only attempts and missing first-answer timing count completed measured attempts; existing truncated count covers all measured attempts explicitly marked truncated. No scoring or status rewriting.
- Markdown exposes diagnostics; summary.csv appends three columns while preserving existing columns/order. Attempt JSON schema 1 and attempts.csv are unchanged. Old artifacts can be reported without migration.
- Regression tests cover null/empty/whitespace answers, reasoning-only versus mixed output, absent reasoning, missing/zero timing, unknown/false/true truncation, warmup and unsuccessful-attempt exclusions, unchanged serialized results, CSV/Markdown rendering, profiles, validation and request options.
- Persian operations and comparison instructions document exact operator tags qwen3.5:4b-q4_K_M and gemma4, installed-name/digest resolution, exact two-model smoke and four-model full commands, and memory-field limitations.

## Historical local verification of that correction

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
