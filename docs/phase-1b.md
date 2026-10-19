# Phase 1B.1: contracts and ground truth

Scope: domain data and validation, static verification specifications, governance and trusted serialization tests. There is no EvaluationService, CLI evaluation command, storage writer, candidate compiler/loader, VM or worker. Phase 1A collection, prompts and reports remain unchanged.

## Contracts and versioning

`LocalModelLab.Core/Evaluation.cs` uses existing `LabJson.Options`: camelCase JSON properties and enum strings, rejected unknown fields and numeric enums. All contract properties are required, including explicit schemaVersion, lifecycle and outcome; missing properties are rejected on deserialization. Empty evidence is permitted only for notRun checks. Collections use ImmutableArray and records have init-only properties. No contract contains an AttemptResult, BenchmarkDefinitions, provider, mutable inference store or generated executable.

- EvaluationDefinition owns versioned cases and verifiers. EvaluationCase binds exact source case ID/version/fingerprint to a public contract and verifier. VerifierDefinition identifies its kind, required check IDs and private artifact hashes. An unknown kind is data, not permission to run it; any future dispatcher must return unsupported/notRun.
- EvaluationManifest is one source attempt evaluated against one case/verifier. Its definition reference hashes exact package JSON bytes, its source reference hashes exact finalized inference-result bytes, and provenance records engine/package/extraction/environment evidence. Run ID plus attempt ID identifies the source globally within the local collection. The frozen source result carries model digest, profile fingerprint, prompt binding and answer. Include source run manifest/request/raw hashes in provenance when creating real manifests later.
- EvaluationResult links the immutable manifest by hash; CheckResult links diagnostics/observations by hash. Artifact paths use `/`, are relative to their declared root, and cannot traverse. Definition artifacts resolve relative to the package; source result relative to the source run; manifest definition/provenance and result evidence relative to the separate evaluation root.
- Major.minor.patch versions identify case, verifier and engine semantics; schemaVersion identifies the wire format. Hashes identify exact bytes, not normalized JSON. Never compare unlike case/verifier versions as if equivalent. Any verdict-affecting change needs a verifier version/hash change; changed public requirements need a new case version.

References sharing an artifact root must agree on exact path spelling and SHA-256, including references across different checks and manifest provenance. Windows device names are rejected, as are case-only path aliases. This is lexical validation, not filesystem/symlink validation. Source references use a different declared root and are not conflated with evaluation artifacts that happen to have the same relative name.

Validation is in-memory structural validation, not a filesystem reader or proof that bytes match references. Package tests verify actual file hashes and exact source-case fingerprint. A future reader must validate source finalization and fingerprint against the frozen source run, check every referenced byte hash, reject conflicting identities, and enforce root containment including symlink/reparse-point escapes before dispatch. A future store must reject inference/evaluation-root overlap, use create-new evaluation directories, and never expose a writable inference store. There is no artifact mutation API in 1B.1; storage isolation remains an explicit 1B.2/1B.3 gate, not a claimed implemented sandbox.

## Outcomes and lifecycle

Check outcomes: pass (observed contract compliance), fail (candidate violated a requirement), error (evaluator could not establish the observation), notRun (blocked, unsupported or unfinished). Each has a reasonCode; non-notRun checks require evidence references. A compiler diagnostic rejecting candidate source is fail; unavailable SDK or broken test discovery is error. Logs are evidence, not authority to invent a pass.

Lifecycle: planned, running, finalized, cancelled, interrupted. Every result enumerates every required check once, using notRun for unperformed checks. Non-finalized lifecycle has overall notRun and may retain completed check evidence. Finalized overall outcome precedence is fail > error > notRun > pass. Thus proven failure survives a later infrastructure error, while missing/unsupported checks never become pass. Individual error checks remain visible even if overall fail. Validation throws ArgumentException for malformed contracts; that is not a candidate verdict.

Schema 1 is for independent Phase 1A first attempts only. Each measured repetition is independent; warmups remain traceable but excluded from correctness reports. No best-of-repetitions or repair success may replace first-attempt correctness. A future repair schema needs explicit lineage, parent, feedback provenance and attempt index. No repair executor is implied here.

## Ground truth and extraction

The [string-counting package](../evaluations/string-counting/1.0.0/contract.md) contains public legacy requirements and separate verifier-only observations, negative fixture descriptions and extraction policy. Those private files must never be passed to BuildRequest or copied into benchmark prompt definitions. The new package has no connection to the inference provider or CLI. Unit tests verify unchanged source prompt binding.

Fixture JSON contains trusted static specifications authored during implementation, not independently human-reviewed ground truth. It contains no benchmark model output and no executable C# fixture files. The duplicate-declaration example is an inert string in JSON. Tests inspect JSON and mocked worker results only; they neither compile nor load candidate code. Tests independently cross-check declared group counts with their static input values using .NET's ordinal comparer. The fixture specifications still need independent human review before being released as an oracle. There is no claim that candidate behavior was dynamically verified in 1B.1.

The synthetic result fixtures hash the actual manifest fixture, which hashes the actual definition file. Their remaining all-zero source/provenance/evidence hashes are explicitly placeholders for structural/mock tests; no corresponding source/worker evidence is present or authenticated. They must never be imported as real evaluation evidence. A real reader must verify those referenced bytes and reject missing/mismatched artifacts.

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

## Historical local verification before commit

Local verification on 2026-10-19:

```powershell
dotnet build LocalModelLab.sln -c Release
dotnet test LocalModelLab.sln -c Release --no-build --logger "console;verbosity=minimal"
```

Release build: zero warnings/errors. Tests: 108 passed, zero failed/skipped on Windows targeting net8.0, including the existing Phase 1A tests. New tests cover package file hashes/source binding, stable schema-1 JSON shape and enum names, rejected missing/unknown wire fields, malformed versions/references, incomplete/contradictory verdicts, mocked candidate failure vs worker error, value-only inference links, static fixture coverage and the original prompt-only request boundary. Scoped `.gitattributes` rules preserve LF bytes for package/golden artifacts across checkouts. No production dependency or CI workflow change was added. No benchmark-generated code was compiled, loaded or executed, and no commit/push was made.

Those 108 local passes described a CRLF working tree, not the exact committed LF artifacts. They did not establish committed-package integrity. The milestone was subsequently committed as c47a92b and failed hosted CI as described below. Independent ground-truth review, isolation preflight, real worker-result authentication, byte-preserving artifact persistence and generated-code verification remain pending.

## CI correction for c47a92b

[Run 39247348204](https://github.com/ramtin099/LocalModelLab/actions/runs/39247348204) checked out `c47a92b01f80735b92a76bb124f6e1e013b5f62b`. Ubuntu built successfully but reported 107 passed, 1 failed, 0 skipped; the failure was VersionOnePackageAndSourceBindingAreIntact. Windows was cancelled during build and did not execute tests. Neither a Windows pass nor overall hosted acceptance is claimed.

Byte investigation used binary `git cat-file blob HEAD:<path>` output captured directly through Python subprocess.check_output, not a text-shell pipeline. Git configuration showed core.autocrlf=true on the authoring Windows machine. `git ls-files --eol` showed LF index blobs but CRLF working copies for the JSON files, with effective `text eol=lf` attributes. Normalizing just CRLF to LF made every investigated working copy byte-identical to its committed blob; no BOM or JSON-value difference was involved.

| Artifact in c47a92b | Bytes hashed by the old reference | Exact committed bytes |
|---|---|---|
| observations.json | 4,993 bytes, 218 CRLF; SHA-256 `9f1bab6208b5ce4b936de424fa63b5289ab568cca877bcf28512ee44ccd366ff` | 4,775 bytes, 218 LF; SHA-256 `f43ef22b6a3bb2f517d2397680597bbdfa7efb832bc1175ee701d36eabad16ca` |
| incorrect-fixtures.json | 3,817 bytes, 109 CRLF; SHA-256 `bc079f3eab5e54c9df906f89ee060862bca6b4c1d494f267b166d067a03e5207` | 3,708 bytes, 109 LF; SHA-256 `a0cb9b6e4ca66de51eeb846b521a958d99c46bcbb982263a3cdfafe22f1d24b2` |
| original definition.json | 1,635 bytes, 58 CRLF; SHA-256 `7466518093bc79bd53be9db65f4484fd68a2f456b1091c98b3cb53377747bee9` | 1,577 bytes, 58 LF; SHA-256 `53339bb6e0befa976e4cdcaee270af254d6d11c7d49582f8f22cdacadd47602b` |

The original definition recorded CRLF hashes, and the original synthetic manifest similarly hashed the CRLF definition. Git's text clean filter normalized those files on staging, leaving the references stale. Attributes control staging/checkout; adding LF rules does not retroactively rewrite existing working files. A clean Git status can therefore coexist with a working copy whose bytes differ from the committed text blob. Ubuntu's observed `f43ef22b...` exactly matches the committed observation bytes; it is not a serializer/compiler/culture discrepancy.

Correction: retain the existing LF attributes, write package/golden files as UTF-8 without BOM and LF before hashing, then repair only the definition's two leaf hashes and the synthetic manifest/result hash chain. The contract, extraction, observations and negative-fixture files remain byte-identical to c47a92b's committed blobs; no requirements, expected groups, case versions or verifier checks changed. This repairs invalid integrity metadata in a still-unreleased oracle specification, not evaluation semantics. Corrected definition SHA-256: `541cb7cd83895c0b2008b68b31b1b40505055afc05be1c84d3e3d594d4a58fdf`; corrected synthetic manifest SHA-256: `beb26b7ac6218de8c85350c84470851f16178d71d95936e55b60d636a68bbc6c`.

Authoring rule: use binary writes or an explicit LF newline setting, inspect effective attributes, and hash exact file bytes only. Do not strip BOMs, replace newlines or reserialize JSON during verification. New tests reject CR/BOM/invalid UTF-8/missing final LF and prove line-ending/BOM/one-byte changes alter the hash. Existing raw-byte hash checks remain strict and now identify the offending path and full hashes. Synthetic result-to-manifest links are also checked. Run these checks before staging; after staging, verify the staged blobs and a fresh checkout rather than trusting a pre-existing build-output copy.

Local correction verification on Windows (SDK 10.0.402, .NET 8.0.32 runtime): the full Release build produced zero warnings/errors; the full test suite reported **118 passed, 0 failed, 0 skipped** using the build/test commands above. A separate temporary Git index and fresh checkout under core.autocrlf=false and true produced byte-identical copies of all eight package/golden files, including definition/manifest hash references. No commits were made for that check. It tests Git's two checkout configurations on Windows; it is not an Ubuntu runtime/CI pass.

Review also added same-root hash/path consistency checks and Windows device-name rejection, without changing the schema-1 wire shape. CI fail-fast is disabled so both matrix jobs can finish independently. No candidate compiler/loader/executor, new production dependency or Phase 1B.2 code was added; inference artifacts and prompts remain unchanged.

This correction is uncommitted and unpushed. Acceptance remains **pending**: obtain explicit commit/push authorization, run both CI jobs for the exact corrected SHA, verify their completed test results, and record that evidence. Ground-truth review and all execution/isolation gates remain pending independently.
