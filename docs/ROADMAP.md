# LocalModelLab roadmap

Last updated: 2026-10-19. Committed milestones and local corrections are distinguished below; no unverified hosted CI success is claimed.

## Final goal

An offline, repository-aware C#/.NET coding agent integrated into the developer workflow: understand relevant code, propose and apply bounded changes, verify behavior, and expose evidence and resource costs. A completed inference is not a correctness verdict.

## Completed Phase 1A

- .NET 8 CLI -> Core application service -> Infrastructure, with mocked provider tests and no generated-code execution.
- Versioned benchmark cases, suites and profiles; exact requests, raw responses, immutable completed attempt snapshots, fingerprints, model digest/context checks, cancellation and separate reports.
- Sequential rotating scheduling, single-model residency verification and separate warmup/measured evidence. Residency snapshots do not prove continuous exclusivity.
- Non-thinking smoke and the first real four-model full benchmark completed. Full run: **80/80 completed attempts, 16 warmups and 64 measured**, four cases and four models (four measured repetitions per model/case).
- Local full-run reference: `non-thinking-full/20261019T103606112Z-f56b1b2ca7bc4c769c713cf2cfc3d02b`; evidence is the local run's `state.json`, `schedule.json`, `manifest.json` and attempt artifacts. Do not publish raw artifacts without privacy review.
- Manual review reported duplicate declarations, incorrect null behavior/exception identification and inaccurate iterator/async explanations. These observations motivate independent evaluation; no automated correctness score exists for this run.

Historical build/CI evidence and measurement limitations: [implementation status](implementation-status.md), [comparison](comparison.md). Some older status text describes an earlier point in time; the dated full-run update supersedes its pending-run statements. Do not infer a model ranking from completion counts or token rates.

## Phase 1B milestones and gates

The overall architecture is approved. Each implementation/execution milestone remains separately reviewable.

| Milestone | Status | Exit gate |
|---|---|---|
| 1B.1 contracts, ground truth, governance | Committed at c47a92b; hosted CI failed; correction locally verified, hosted gate pending | Stable schema 1 JSON; definition/result validation; hash-bound static package; reviewed expected observations; Phase 1A regressions pass; both Windows/Ubuntu CI pass at the exact corrected SHA; no generated-code execution |
| 1B.2 isolation design/preflight and implementation | Not started; separate approval required | Confirm Windows edition/virtualization/resources; pin offline guest/toolchain; demonstrate containment, limits, trusted verdict ownership and teardown with approved fixtures; no host fallback |
| 1B.3 deterministic compilation and behavioral verifier | Not started; separate approval required | Fixed trusted build inputs; exact required checks; candidate failures vs infrastructure errors; artifact integrity and repeatable outcomes; generated code runs only after isolation approval |
| 1B.4 small retrospective pilot | Not started; separate execution approval required | Freeze selection; four string-counting answers plus four iterator manual reviews; traceable evidence and explicit coverage; expand only after review |

1B.1's independent human ground-truth review remains **pending**, even after its automated tests pass. Run [39247348204](https://github.com/ramtin099/LocalModelLab/actions/runs/39247348204) failed on Ubuntu's committed-byte integrity check; Windows was cancelled before testing. The uncommitted correction passes 118 local Windows tests but requires authorized commit/push and both hosted jobs for that exact corrected SHA. Static fixture consistency is not execution-based oracle verification. See [Phase 1B contracts and gates](phase-1b.md) and [legacy package](../evaluations/string-counting/1.0.0/contract.md).

## Later work (not implemented)

1. Separately versioned stricter coding contracts and structured iterator-prediction cases; bug-detection and Persian reasoning evaluation with explicit rubrics/oracles.
2. Repository-context evaluation: frozen repository revisions, private-code boundaries, retrieval relevance and bounded change tasks.
3. Offline agent workflow: explicit tool permissions, edit/test evidence, failure recovery and first-attempt vs repair lineage.
4. Evidence-based routing across local models and task types; routing decisions must not contaminate benchmark comparability.
5. Optimization of latency, memory, context and inference cost only after correctness gates; compare quality/resource trade-offs on this hardware.

No repair loop, repository retrieval, agent executor, model router, optimization engine or universal ranking is present.

## Decisions, unresolved items and next actions

- Approved: separate inference/evaluation artifacts; explicit application-service flow; independent versioned verification; no host execution; one isolated candidate at a time; first-attempt results separate from repairs.
- Legacy case: null input must be rejected, but no exception type is prescribed. Count ordinal case-insensitively; returned dictionary lookup comparer is not scored. Never retroactively strengthen the prompt.
- Unknown: machine Hyper-V eligibility, available RAM/disk, chosen guest image/distribution and license, exact supported SDK/runtime patch and resource-limit calibration.
- Pending: independent review of observation/fixture expectations and extraction policy, approval of strict future requirements, isolation preflight and output-transfer design.
- Next: review the 1B.1 CI correction; explicitly authorize commit/push before obtaining and verifying both hosted CI results for the corrected SHA. Review ground truth before approving the next bounded milestone. No generated-code execution, commit or push is authorized by 1B.1 alone.

New evidence, verdict-affecting edits and acceptance decisions must be recorded with version/hash and review provenance. Never overwrite a released evaluation package; bump case/verifier versions as appropriate.
