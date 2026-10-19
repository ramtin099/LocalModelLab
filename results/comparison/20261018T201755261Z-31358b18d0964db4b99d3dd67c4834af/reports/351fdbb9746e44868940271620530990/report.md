# LocalModelLab inference report

Run: 20261018T201755261Z-31358b18d0964db4b99d3dd67c4834af

Journal status: completed; finalized attempts: 10/10.

Phase: execution; failure kind: none; error: none.

Scheduling: Rotating; residency: SingleModel. See schedule.json for the exact order.

Measured attempts only; warmups excluded. Means use available values from completed attempts, including length-limited completions. Completed means successful execution, not a usable or correct answer. Empty final answers (null/empty/whitespace), reasoning-only (empty final with nonblank exposed reasoning), and missing first-answer timing count completed measured attempts only; these counts may overlap. Missing timing does not imply an empty answer (non-streaming timing is unavailable). Truncated counts all measured attempts with outputTruncated=true; unknown is not false. null means unavailable; failures and unfinished attempts are counted separately. First output/answer latency includes model loading and network time. Provider minus load is not proof of a warm run. No quality scores or universal ranking. Token rates use each model's tokenizer and are not directly comparable across tokenizers.

| Scope | Model | Case | Completed/attempts | Failed | In progress | Truncated | Empty final | Reasoning only | Missing first answer | Mean client ms | Median client ms | First output ms | First answer ms | Load ms | Provider minus load ms | Gen tokens/s |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| model | qwen3.5:4b-q4_K_M | all | 4/4 | 0 | 0 | 4 | 4 | 4 | 4 | 25505.790 | 26376.035 | 4814.497 | null | 4444.524 | 21060.615 | 26.409 |
| model | gemma4:e2b-it-q4_K_M | all | 4/4 | 0 | 0 | 4 | 4 | 4 | 4 | 14892.819 | 15720.366 | 7767.485 | null | 4890.981 | 10000.851 | 81.936 |
| case | qwen3.5:4b-q4_K_M | csharp-explanation | 4/4 | 0 | 0 | 4 | 4 | 4 | 4 | 25505.790 | 26376.035 | 4814.497 | null | 4444.524 | 21060.615 | 26.409 |
| case | gemma4:e2b-it-q4_K_M | csharp-explanation | 4/4 | 0 | 0 | 4 | 4 | 4 | 4 | 14892.819 | 15720.366 | 7767.485 | null | 4890.981 | 10000.851 | 81.936 |
