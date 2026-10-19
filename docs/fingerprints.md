# Raw hashes and logical fingerprints

Status: Phase 1B.1 correction for `28417ce290fa560ec349803f0bee284806bff9cb`; not a Phase 1B.2 executor. Algorithm names are part of an identity. Never compare digest strings across algorithms as if they identify the same representation.

## Raw artifact integrity

`LabJson.Hash(bytes)` is lowercase SHA-256 of the exact supplied bytes. ArtifactReference.Sha256, definition-file SHA-256 and binary hashes use this rule. Verification must not normalize newlines, strip a BOM, reserialize JSON or substitute a logical fingerprint. Different physical bytes must have different hashes even if they parse into equal objects.

Evaluation package/golden source files use UTF-8 without BOM, LF and a final LF, enforced by attributes and tests. That authoring rule does not change the raw verification rule. Other artifact types, including frozen Phase 1A evidence, retain their original bytes.

## New logical algorithm: lab-json-canonical-v1

`EvaluationFingerprints.Create(value)` returns a LogicalFingerprint with explicit Algorithm and Sha256. Version 1 is this project-specific algorithm, not a claim of RFC 8785/JCS conformance:

1. Convert trusted .NET contract values to JSON values with private serializer options: camelCase CLR property names; included null properties; camelCase enum strings; no integer enum fallback. JSON-element object keys and dictionary string keys retain their spelling. Do not use mutable presentation options from LabJson.Options.
2. Recursively sort object properties with StringComparer.Ordinal (UTF-16 ordinal order). Reject duplicate property names. Preserve array order. Preserve string values exactly: no trimming, line-ending conversion, Unicode normalization or case folding.
3. Write compact JSON with Utf8JsonWriter and JavaScriptEncoder.UnsafeRelaxedJsonEscaping, as UTF-8 without BOM, indentation or final newline. Equivalent escape spellings are decoded and rewritten consistently. Escaped CRLF inside a string remains a different value from escaped LF. This is a serialization profile for the supported .NET 8 contracts; runtime upgrades must pass the fixed conformance vectors before reuse of this algorithm ID.
4. Normalize validated JSON number tokens exactly, without double/decimal rounding: concatenate mantissa digits, remove leading zeros, and subtract fractional digit count from the base-10 exponent. Remove trailing coefficient zeros while increasing the exponent. Write sign plus significant digits, followed by lowercase `e` and a signed exponent only when nonzero. All zero representations become `0`. Exponents use invariant BigInteger arithmetic. Examples: `1.0` -> `1`; `1000` -> `1e3`; `1.2340e3` -> `1234`; `-0.00` -> `0`; `1e-999` stays distinct from zero.
5. Hash UTF-8 bytes of the literal `lab-json-canonical-v1` followed by one LF byte, followed by those compact canonical JSON bytes. Return lowercase SHA-256 and the algorithm ID. The prefix separates logical fingerprints from untagged raw hashes.

Object property order, indentation, structural CRLF/LF, escape spelling and equivalent numeric spelling do not change the identity. Changed field values, array order, property-name casing or string newline values do change it. A changed raw artifact reference is a changed field value: canonicalization does not erase provenance differences merely because referenced files parse similarly.

Fixed vectors (executed with the same expectations on Windows and Linux):

| Logical input | Canonical SHA-256 |
|---|---|
| Existing csharp-generation BenchmarkCase, including system=null | `8cd8825520f72d0eae873d82c68597c556580008ec863e6dc7464251ffea20c5` |
| `{"b":2,"a":[true,null,"x"]}` | `93a8948fe39e65ab30bbadd37837323fa0c2380e3ef8b7a939372bb21a804ae3` |
| `{"value":1}` | `66347a9f2f053ddfd1c86c1d7ad140d5d7ab48c562486a9d3cc045abcc55768c` |

Vectors were also calculated independently from the specified prefixed compact bytes with Python's hashlib, then verified by the actual .NET implementation. Regression coverage includes Unicode escapes, alternate presentation serializers, exact large/small numbers, duplicate names, meaningful string/array changes and strict raw-byte hashing.

## Explicit Phase 1A compatibility

The existing `LabJson.Fingerprint` deliberately remains unchanged. It hashes indented System.Text.Json serialization using native structural newlines, so it is a legacy platform-sensitive identity, not the new logical algorithm. .NET default string escaping, declaration order and serialized contract shape are also relevant. Do not silently replace its output in saved or newly written Phase 1A schema-1 artifacts.

`EvaluationFingerprints.ForSourceCase(case, algorithm)` has three explicit choices:

| Algorithm | Meaning |
|---|---|
| `phase1a-stj-indented-crlf-v1` | Reproduce the Phase 1A Windows representation with CRLF indentation |
| `phase1a-stj-indented-lf-v1` | Reproduce the Phase 1A Unix representation with LF indentation |
| `lab-json-canonical-v1` | New logical representation; not interchangeable with either historical representation |

Legacy compatibility fixes the historical .NET 8 serialization profile: camelCase properties, declaration order, null inclusion, default encoder, two-space indentation and camelCase string enums. Only structural newlines in already serialized JSON are set to the named convention; escaped field contents are unchanged. Legacy hashes do not receive the new algorithm prefix because that would change historical identities. Compatibility is currently for source BenchmarkCase bindings; no profile/suite migration or historical report regeneration is performed.

A trusted temporary net8.0 diagnostic referencing the actual Core project reproduced the source case on .NET 8.0.32:

| Actual indented JSON layout | Byte length | SHA-256 |
|---|---|---|
| CRLF | 414 | `90ce6abc62003f6fce835c6eced0fcd8573e20b68b803fe8d7996cd01c365eef` |
| LF | 409 | `ad82ca5fb410ce52d6bd7b5aebe03acdd4e291b6cabb20c82b166c36b0b7273d` |

The five differing bytes are indentation CR characters. Both byte sequences deserialize to the same BenchmarkCase. These exact lengths and hashes are asserted in .NET tests on both platforms, without platform-dependent expected values. No benchmark-generated code is involved.

The legacy string-counting package retains its recorded `90ce6abc...` value and adds SourceCaseFingerprintAlgorithm=`phase1a-stj-indented-crlf-v1`. SourceAttemptReference similarly requires CaseFingerprintAlgorithm; manifest validation requires both the label and digest to match the package. Readers must use the named algorithm, not Environment.NewLine, and must not silently try alternatives until one matches. Unknown/missing labels are rejected. For other historical runs, require provenance or an explicit reviewed binding before assigning a legacy label; do not guess from the evaluator's OS.

This is an explicit amendment to the still-unaccepted schema-1 evaluation draft. Previously committed evaluation drafts without labels require reviewed re-creation; strict readers reject missing/unknown fields. No production evaluation store exists, and only static package/golden references were refreshed. Phase 1A schema-1 records and frozen files are untouched. No public task requirements, hidden observations, verifier check IDs or pass/fail/error semantics changed.

## Caller audit and remaining legacy limits

| Caller / field | Current behavior and reproducibility implications |
|---|---|
| BenchmarkService: AttemptResult.CaseFingerprint | Legacy native indented fingerprint; same case can differ across platforms. Remains unchanged. |
| BenchmarkService: RunManifest.SuiteFingerprint | Same legacy limitation; suite case order is also meaningful. Remains unchanged. |
| BenchmarkService: RunManifest.ProfileFingerprint, copied into attempts | Same legacy limitation; defaults and contract additions affect serialized shape. Harness 1.0/1.1 already differ; no migration is implied. |
| RunManifest.DefinitionFileSha256 | Raw input-file bytes. Formatting differences intentionally change it. |
| CLI cases validation / harness binary inventory | Raw definition/binary bytes, intentionally not canonical logical identities. |
| Reports/inspect | Read recorded manifest/attempt fields; do not recompute or migrate fingerprints. New report bytes can differ by platform/formatting and have no canonical identity guarantee. |
| Phase 1A ValidationTests | Compare legacy values within the current process/platform to check round trips and unchanged cases/suites. These tests do not establish cross-platform identity. |
| Evaluation package binding | Explicit compatibility algorithm and fixed source vector; no host-OS inference. |
| New evaluation object round-trip tests | Tagged canonical fingerprints; raw package/manifest references still checked separately against exact bytes. |

New evaluation code must use EvaluationFingerprints rather than adding more LabJson.Fingerprint callers. A future change to Phase 1A identity generation needs its own versioned harness/artifact policy and approval. Do not equate bare historical digest strings across platforms or reconstruct identities with changed DTO/default shapes. Both corrected hosted CI jobs must pass at one authorized corrected SHA before Phase 1B.1 acceptance; local Windows tests alone are insufficient.
