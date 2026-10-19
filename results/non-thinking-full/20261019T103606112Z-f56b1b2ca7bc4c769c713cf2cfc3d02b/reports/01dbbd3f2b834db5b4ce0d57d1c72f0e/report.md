# LocalModelLab inference report

Run: 20261019T103606112Z-f56b1b2ca7bc4c769c713cf2cfc3d02b

Journal status: completed; finalized attempts: 80/80.

Phase: execution; failure kind: none; error: none.

Scheduling: Rotating; residency: SingleModel. See schedule.json for the exact order.

Measured attempts only; warmups excluded. Means use available values from completed attempts, including length-limited completions. Completed means successful execution, not a usable or correct answer. Empty final answers (null/empty/whitespace), reasoning-only (empty final with nonblank exposed reasoning), and missing first-answer timing count completed measured attempts only; these counts may overlap. Missing timing does not imply an empty answer (non-streaming timing is unavailable). Truncated counts all measured attempts with outputTruncated=true; unknown is not false. null means unavailable; failures and unfinished attempts are counted separately. First output/answer latency includes model loading and network time. Provider minus load is not proof of a warm run. No quality scores or universal ranking. Token rates use each model's tokenizer and are not directly comparable across tokenizers.

| Scope | Model | Case | Completed/attempts | Failed | In progress | Truncated | Empty final | Reasoning only | Missing first answer | Mean client ms | Median client ms | First output ms | First answer ms | Load ms | Provider minus load ms | Gen tokens/s |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| model | qwen3.5:4b-q4_K_M | all | 16/16 | 0 | 0 | 0 | 0 | 0 | 0 | 14040.165 | 10835.160 | 4850.827 | 4850.827 | 4635.603 | 9403.197 | 71.978 |
| model | gemma4:e2b-it-q4_K_M | all | 16/16 | 0 | 0 | 0 | 0 | 0 | 0 | 14204.509 | 13170.335 | 9232.189 | 9232.189 | 8811.847 | 5391.722 | 156.363 |
| model | qwen2.5-coder:7b | all | 16/16 | 0 | 0 | 0 | 0 | 0 | 0 | 16617.827 | 15518.052 | 5556.856 | 5556.856 | 5366.229 | 11250.739 | 29.038 |
| model | granite4:3b | all | 16/16 | 0 | 0 | 0 | 0 | 0 | 0 | 6781.523 | 6882.475 | 2919.762 | 2919.762 | 2810.137 | 3970.142 | 100.313 |
| case | qwen3.5:4b-q4_K_M | csharp-explanation | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 9419.590 | 9125.453 | 5036.803 | 5036.803 | 4814.707 | 4604.332 | 71.425 |
| case | qwen3.5:4b-q4_K_M | csharp-bug-detection | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 9238.436 | 9172.559 | 4872.981 | 4872.981 | 4652.397 | 4583.933 | 72.817 |
| case | qwen3.5:4b-q4_K_M | csharp-generation | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 11125.901 | 11128.612 | 4777.883 | 4777.883 | 4558.181 | 6567.389 | 72.762 |
| case | qwen3.5:4b-q4_K_M | persian-technical-explanation | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 26376.733 | 26397.143 | 4715.640 | 4715.640 | 4517.125 | 21857.136 | 70.908 |
| case | gemma4:e2b-it-q4_K_M | csharp-explanation | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 13689.862 | 13199.754 | 9595.717 | 9595.717 | 9120.263 | 4568.702 | 152.975 |
| case | gemma4:e2b-it-q4_K_M | csharp-bug-detection | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 11958.028 | 11236.171 | 9410.902 | 9410.902 | 9002.900 | 2953.624 | 166.362 |
| case | gemma4:e2b-it-q4_K_M | csharp-generation | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 12502.864 | 12373.355 | 8798.206 | 8798.206 | 8429.479 | 4072.816 | 183.960 |
| case | gemma4:e2b-it-q4_K_M | persian-technical-explanation | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 18667.283 | 18658.207 | 9123.931 | 9123.931 | 8694.747 | 9971.744 | 122.155 |
| case | qwen2.5-coder:7b | csharp-explanation | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 15987.324 | 15565.964 | 5725.265 | 5725.265 | 5536.103 | 10450.459 | 28.683 |
| case | qwen2.5-coder:7b | csharp-bug-detection | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 14333.197 | 13896.808 | 5741.155 | 5741.155 | 5551.398 | 8780.587 | 28.797 |
| case | qwen2.5-coder:7b | csharp-generation | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 14421.676 | 14392.224 | 5364.198 | 5364.198 | 5175.712 | 9245.327 | 29.470 |
| case | qwen2.5-coder:7b | persian-technical-explanation | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 21729.110 | 21741.912 | 5396.805 | 5396.805 | 5201.701 | 16526.585 | 29.201 |
| case | granite4:3b | csharp-explanation | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 8278.050 | 8180.998 | 2941.505 | 2941.505 | 2835.262 | 5442.202 | 98.774 |
| case | granite4:3b | csharp-bug-detection | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 5676.036 | 5632.494 | 2943.103 | 2943.103 | 2831.954 | 2842.614 | 100.170 |
| case | granite4:3b | csharp-generation | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 3965.202 | 3970.350 | 2893.528 | 2893.528 | 2787.766 | 1175.119 | 102.430 |
| case | granite4:3b | persian-technical-explanation | 4/4 | 0 | 0 | 0 | 0 | 0 | 0 | 9206.807 | 9217.121 | 2900.911 | 2900.911 | 2785.567 | 6420.633 | 99.879 |
