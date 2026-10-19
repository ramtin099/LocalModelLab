# Phase 1B.1: contracts and ground truth

Scope: domain data and validation, static verification specifications, governance and trusted serialization tests. There is no EvaluationService, CLI evaluation command, storage writer, candidate compiler/loader, VM or worker. Phase 1A collection, prompts and reports remain unchanged.

## Contracts and versioning

`LocalModelLab.Core/Evaluation.cs` uses existing `LabJson.Options`: camelCase JSON properties and enum strings, rejected unknown fields and numeric enums. All contract properties are required, including explicit schemaVersion, lifecycle and outcome; missing properties are rejected on deserialization. Empty evidence is permitted only for notRun checks. Collections use ImmutableArray and records have init-only properties. No contract contains an AttemptResult, BenchmarkDefinitions, provider, mutable inference store or generated executable.

- EvaluationDefinition owns versioned cases and verifiers. EvaluationCase binds exact source case ID/version/fingerprint to a public contract and verifier. VerifierDefinition identifies its kind, required check IDs and private artifact hashes. An unknown kind is data, not permission to run it; any future dispatcher must return unsupported/notRun.
- EvaluationManifest is one source attempt evaluated against one case/verifier. Its definition reference hashes exact package JSON bytes, its source reference hashes exact finalized inference-result bytes, and provenance records engine/package/extraction/environment evidence. Run ID plus attempt ID identifies the source globally within the local collection. The frozen source result carries model digest, profile fingerprint, prompt binding and answer. Include source run manifest/request/raw hashes in provenance when creating real manifests later.
- EvaluationResult links the immutable manifest by hash; CheckResult links diagnostics/observations by hash. Artifact paths use `/`, are relative to their declared root, and cannot traverse. Definition artifacts resolve relative to the package; source result relative to the source run; manifest definition/provenance and result evidence relative to the separate evaluation root.
- Major.minor.patch versions identify case, verifier and engine semantics; schemaVersion identifies the wire format. Hashes identify exact bytes, not normalized JSON. Never compare unlike case/verifier versions as if equivalent. Any verdict-affecting change needs a verifier version/hash change; changed public requirements need a new case version.

Validation is in-memory structural validation, not a filesystem reader or proof that bytes match references. Package tests verify actual file hashes and exact source-case fingerprint. A future reader must validate source finalization and fingerprint against the frozen source run, check every referenced byte hash, reject conflicting identities, and enforce root containment including symlink/reparse-point escapes before dispatch. A future store must reject inference/evaluation-root overlap, use create-new evaluation directories, and never expose a writable inference store. There is no artifact mutation API in 1B.1; storage isolation remains an explicit 1B.2/1B.3 gate, not a claimed implemented sandbox.

## Outcomes and lifecycle

Check outcomes: pass (observed contract compliance), fail (candidate violated a requirement), error (evaluator could not establish the observation), notRun (blocked, unsupported or unfinished). Each has a reasonCode; non-notRun checks require evidence references. A compiler diagnostic rejecting candidate source is fail; unavailable SDK or broken test discovery is error. Logs are evidence, not authority to invent a pass.

Lifecycle: planned, running, finalized, cancelled, interrupted. Every result enumerates every required check once, using notRun for unperformed checks. Non-finalized lifecycle has overall notRun and may retain completed check evidence. Finalized overall outcome precedence is fail > error > notRun > pass. Thus proven failure survives a later infrastructure error, while missing/unsupported checks never become pass. Individual error checks remain visible even if overall fail. Validation throws ArgumentException for malformed contracts; that is not a candidate verdict.

Schema 1 is for independent Phase 1A first attempts only. Each measured repetition is independent; warmups remain traceable but excluded from correctness reports. No best-of-repetitions or repair success may replace first-attempt correctness. A future repair schema needs explicit lineage, parent, feedback provenance and attempt index. No repair executor is implied here.

## Ground truth and extraction

The [string-counting package](../evaluations/string-counting/1.0.0/contract.md) contains public legacy requirements and separate verifier-only observations, negative fixture descriptions and extraction policy. Those private files must never be passed to BuildRequest or copied into benchmark prompt definitions. The new package has no connection to the inference provider or CLI. Unit tests verify unchanged source prompt binding.

Fixture JSON describes trusted, human-authored inputs and expected observations. It contains no model output and no executable C# fixture files. The duplicate-declaration example is an inert string in JSON. Tests inspect JSON and mocked worker results only; they neither compile nor load candidate code. The fixture specifications still need independent human review before being released as an oracle. There is no claim that their described behavior was dynamically verified in 1B.1.

The legacy iterator case remains free-text/manual evaluation. Its independent reference observations are: Positive(source) returns an iterator without enumerating; MoveNext starts enumeration; positive values preserve order and duplicates; Positive(null) does not throw at invocation, but advancing its iterator throws NullReferenceException for the supplied unguarded foreach. Running that supplied snippet in a future isolated oracle would not prove a model explanation correct. Do not implement a keyword score for prose. A future structured prediction task needs a new version and fresh inference.

## Phase 1B.2 isolation requirements (not implemented)

Preferred design: one disposable Hyper-V VM, initially a minimal Linux guest, with a pinned offline .NET 8 SDK/runtime/reference set. This proves behavior on that recorded environment, not Windows-specific semantics. Windows Sandbox is an alternative requiring review of reproducibility and transfer limitations. Never use a host subprocess, AssemblyLoadContext, CAS or ordinary shared-kernel container as the host safety boundary.

Preflight questions before implementation/execution approval:

- Does the Windows 11 edition support Hyper-V? Are SLAT/firmware virtualization enabled, and are other hypervisors compatible? Do not change system features without authorization.
- Can 4GB guest RAM, two vCPUs and bounded disposable disk fit while preserving host headroom on 16GB? Run no inference concurrently; fail preflight if resources are insufficient.
- Which guest image/license, patched SDK 8/runtime, compiler options, globalization libraries and reference hashes are pinned? How are offline dependencies acquired and verified without exposing candidate execution to a network?
- How are inputs delivered read-only with no network adapter, shared host folders, clipboard, secrets or checkout? How are bounded output files retrieved as untrusted data without host execution or automatic preview?
- How do guest accounts/permissions isolate the trusted verifier from the candidate? Candidate code must not load in the supervisor process, read hidden tests, alter verdict files or forge success via stdout.
- How do process-tree limits, memory/PID/output/workspace quotas, per-invocation timeouts, cancellation and a host VM watchdog work? Starting design budgets: 512MiB candidate memory, 32 PIDs, 60s compilation, 5s invocation, 30s test phase, 180s whole job, 256KiB source, 1MiB per log stream, 128MiB candidate workspace. Calibrate with approved trusted fixtures before generated-code execution.
- How is a fresh disposable disk created per candidate and destroyed on success, failure, cancellation or controller crash? How are failed teardown and partial evidence recorded?
- What approved containment fixtures demonstrate denied host/network/test access, bounded resource use, tamper detection and watchdog teardown? No automatic host fallback on any failure.

VM lifecycle implementation, verifier implementation and generated-code pilot execution each require their own review. CI must continue to build/test trusted project code only. Exact version and image selections, preflight findings and approval evidence belong in the manifest and roadmap before dispatch exists.

## Verification record

Local verification on 2026-10-19:

```powershell
dotnet build LocalModelLab.sln -c Release
dotnet test LocalModelLab.sln -c Release --no-build --logger "console;verbosity=minimal"
```

Release build: zero warnings/errors. Tests: 108 passed, zero failed/skipped on Windows targeting net8.0, including the existing Phase 1A tests. New tests cover package file hashes/source binding, stable schema-1 JSON shape and enum names, rejected missing/unknown wire fields, malformed versions/references, incomplete/contradictory verdicts, mocked candidate failure vs worker error, value-only inference links, static fixture coverage and the original prompt-only request boundary. Scoped `.gitattributes` rules preserve LF bytes for package/golden artifacts across checkouts. No production dependency or CI workflow change was added. No benchmark-generated code was compiled, loaded or executed, and no commit/push was made.

Hosted CI is not claimed. Independent ground-truth review, isolation preflight, real worker-result authentication, byte-preserving artifact persistence and generated-code verification remain pending. Next action: review this milestone and its expected observations, then separately approve the bounded 1B.2 isolation work.
