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
| 1B.1 contracts, ground truth, governance | Implementation verification complete at 0ecf21a: Windows 145/145 and Ubuntu 145/145 | Stable versioned JSON/fingerprint algorithms and raw hash checks verified on both systems; independent ground-truth review remains separate; no generated-code execution |
| 1B.2A read-only isolation/resource preflight | Completed locally; documentation pending review | Evidence and blocked probes recorded; on-demand lifecycle proposed. Current available RAM fails admission; full Hyper-V/Sandbox reported disabled; no host mutations |
| 1B.2B resource policy and mocked lifecycle | Proposed, not approved or implemented | Configurable admission checks, ownership journal, bounded failure/recovery, no shared-workload mutation or idle polling; no live provisioning |
| 1B.2C trusted isolation lifecycle calibration | Proposed, not approved or implemented | Separately approve features/image/resources; demonstrate trusted VM containment, watchdog/teardown and resource release without generated code |
| 1B.3 deterministic compilation and behavioral verifier | Not started; separate approval required | Fixed trusted build inputs; exact required checks; candidate failures vs infrastructure errors; artifact integrity and repeatable outcomes; generated code runs only after isolation approval |
| 1B.4 small retrospective pilot | Not started; separate execution approval required | Freeze selection; four string-counting answers plus four iterator manual reviews; traceable evidence and explicit coverage; expand only after review |

Implementation verification for 1B.1 is now complete at `0ecf21ae4722e8c2867b2cdb2a2ab0eedde911cb`: [run 39262294509](https://github.com/ramtin099/LocalModelLab/actions/runs/39262294509), [Windows job 118092809438](https://github.com/ramtin099/LocalModelLab/actions/runs/39262294509/job/118092809438) and [Ubuntu job 118092809245](https://github.com/ramtin099/LocalModelLab/actions/runs/39262294509/job/118092809245) each built with zero warnings/errors and passed 145 tests, zero failed/skipped. Earlier failures remain historical evidence in implementation-status.md. Independent human ground-truth review is still pending; CI does not establish oracle correctness or runtime isolation. See [on-demand preflight](phase-1b2a-preflight.md), [fingerprint algorithms](fingerprints.md) and [Phase 1B contracts](phase-1b.md).

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
- Observed in 1B.2A: Windows 11 Pro with active hypervisor/WSL platform; full Hyper-V and Sandbox reported disabled; about 1.2 GiB RAM available during inspection. Guest allocation must defer, not evict user workloads. Disk options and blocked probes are in the preflight report.
- Unknown: elevated feature confirmation and firmware exposure, post-enablement coexistence, chosen guest image/license/offline SDK, and measured startup/peak/release costs.
- Pending: independent review of observation/fixture expectations and extraction policy, approval of strict future requirements, isolation preflight and output-transfer design.
- Resource-On-Demand: Normal Development Mode has no running lab guest/model or background polling; Active Lab Mode admits only explicitly requested, configurable budgets after fresh headroom checks. Sequential inference/owned-model release precedes guest allocation. Existing singleModel must not be used destructively on a shared server.
- Next: review 1B.2A and separately approve 1B.2B. Feature changes/reboot, image provisioning, dedicated inference lifecycle and real guest tests each require approval. No such mutation, generated-code execution, commit or push was authorized by this inspection.

New evidence, verdict-affecting edits and acceptance decisions must be recorded with version/hash and review provenance. Never overwrite a released evaluation package; bump case/verifier versions as appropriate.
