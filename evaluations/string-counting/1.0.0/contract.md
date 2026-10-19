# Legacy string counting 1.0.0

Evaluation case: `string-counting-legacy/1.0.0`. Source case: `csharp-generation/1.0.0` from `benchmarks/comparison.non-thinking.v1.json`, bound by its existing LabJson case fingerprint. The source prompt and Phase 1A artifacts are unchanged.

## Explicit original requirements

> Write a .NET 8 C# method that counts occurrences of nonempty strings from IEnumerable<string?> using ordinal case-insensitive comparison. Reject a null enumerable, ignore null/empty elements, and return Dictionary<string,int>. Include a short usage example. Return code as text only; do not execute it.

Required observations: reject null input rather than return a dictionary; ignore null and empty elements; count all other strings by ordinal case-insensitive equivalence; return Dictionary<string,int>; include a usage example. Whitespace-only strings are nonempty. Whitespace in strings must not be trimmed: `"a"` and `" a "` are different groups. Empty input returns an empty dictionary. Input order and dictionary enumeration order are not scoring criteria.

To compare dictionaries, inspect all entries and compare key groups using OrdinalIgnoreCase; preserve whitespace and require exactly one entry per group and the expected count. Do not normalize the candidate's output or use its comparer as the oracle. Representative spelling within a case-equivalent group is unspecified. The lowercase labels in observations.json identify equivalence classes, not required key spelling.

## Ambiguities and limits: do not strengthen retroactively

- Rejecting null requires an exception at invocation of this synchronous dictionary-returning method, but the original prompt does not prescribe ArgumentNullException, parameter name or message. Any exception type satisfies this narrow check. Distinguish a candidate exception from worker/transport failure using trusted supervision later.
- Ordinal case-insensitive grouping is required. A returned dictionary that has correct groups but a case-sensitive lookup comparer does not fail for that comparer. Case-insensitive lookup of an unseen casing is diagnostic only.
- No required method/class name, visibility, static modifier, argument name, nullable-annotation spelling or full project layout was given. Identify a unique compatible method through an audited binding; do not rename it or force a new signature.
- IEnumerable permits deferred/single-use inputs. The single-pass observation checks correct counts for such an input; it does not require a particular loop, exact MoveNext count, or forbid materializing the sequence once. Implementations that require a second traversal cannot handle this valid input. Enumerator instrumentation is verifier-owned, not candidate source.
- No complexity, overflow, mutation, exception-wrapping, key ordering or serialization policy was specified. Do not add those checks in this legacy version.
- Fences/prose and a usage example can make source extraction ambiguous. Apply extraction.md; never infer behavioral failure from unresolved extraction.

## Strict future case (draft only; not released or used for legacy scoring)

A separate case `string-counting-strict/2.0.0` would explicitly require a named public static method, one source artifact, ArgumentNullException for null input, and a returned dictionary using StringComparer.OrdinalIgnoreCase for lookup as well as grouping. Approve the exact signature, example format and comparer requirement before creating a new inference case. It cannot rescore old answers against stronger rules.

## Verification status

observations.json contains independently inspectable expected behavior and rationale; incorrect-fixtures.json describes trusted negative controls and non-failing ambiguity controls. They are specifications, not executed tests or model output. `reviewStatus: pendingIndependentReview` is intentional. No independent human sign-off, executable oracle, hidden unit-test runner or generated-code evaluation is claimed.

definition.json hash-binds this contract and all verifier-only files. Public requirements may be model-facing in a future case; observations, probes, negative controls and extraction decisions must remain outside inference prompts. Once released, change a verifier version for changed checks/policy and a case version for changed requirements. While this draft is under review, update hashes for every edit and record the decision before release.
