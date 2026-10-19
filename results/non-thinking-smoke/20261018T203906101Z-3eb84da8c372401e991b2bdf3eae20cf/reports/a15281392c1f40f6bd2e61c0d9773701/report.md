# LocalModelLab inference report

Run: 20261018T203906101Z-3eb84da8c372401e991b2bdf3eae20cf

Journal status: completed; finalized attempts: 10/10.

Phase: execution; failure kind: none; error: none.

Scheduling: Rotating; residency: SingleModel. See schedule.json for the exact order.

Measured attempts only; warmups excluded. Means use available values from completed attempts, including length-limited completions. Completed means successful execution, not a usable or correct answer. Empty final answers (null/empty/whitespace), reasoning-only (empty final with nonblank exposed reasoning), and missing first-answer timing count completed measured attempts only; these counts may overlap. Missing timing does not imply an empty answer (non-streaming timing is unavailable). Truncated counts all measured attempts with outputTruncated=true; unknown is not false. null means unavailable; failures and unfinished attempts are counted separately. First output/answer latency includes model loading and network time. Provider minus load is not proof of a warm run. No quality scores or universal ranking. Token rates use each model's tokenizer and are not directly comparable across tokenizers.

| Scope | Model | Case | Completed/attempts | Failed | In progress | Truncated | Empty final | Reasoning only | Missing first answer | Mean client ms | Median client ms | First output ms | First answer ms | Load ms | Provider minus load ms | Gen tokens/s |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| model | qwen3.5:4b-q4_K_M | all | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 7874.281 | 9038.619 | 3677.381 | 3677.381 | 3480.436 | 4392.109 | 74.523 |
| model | gemma4:e2b-it-q4_K_M | all | 4/4 | 0 | 0 | 4 | 0 | 0 | 0 | 8002.920 | 7901.948 | 4828.006 | 4828.006 | 4491.388 | 3511.111 | 161.623 |
| case | qwen3.5:4b-q4_K_M | csharp-explanation | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 7874.281 | 9038.619 | 3677.381 | 3677.381 | 3480.436 | 4392.109 | 74.523 |
| case | gemma4:e2b-it-q4_K_M | csharp-explanation | 4/4 | 0 | 0 | 4 | 0 | 0 | 0 | 8002.920 | 7901.948 | 4828.006 | 4828.006 | 4491.388 | 3511.111 | 161.623 |
