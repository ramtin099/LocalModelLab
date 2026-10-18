# Controlled Phase 1A comparison

Run commands from the repository root in PowerShell. No generated code is executed. This procedure measures local inference, not correctness or model quality. Use a dedicated Ollama instance, AC power, a fixed power mode and stable cooling. Stop other inference clients and GPU-heavy applications. Do not pull/update models or change Ollama during a run.

## Preparation

Build and test first:

```powershell
dotnet build LocalModelLab.sln -c Release
dotnet test LocalModelLab.sln -c Release --no-build
```

Download before benchmarking, if these exact local models are not already installed:

```powershell
ollama pull qwen3.5:4b-q4_K_M
ollama pull gemma4
ollama pull qwen2.5-coder:7b
ollama pull granite4:3b
```

Model families are described in the official [Qwen3.5](https://ollama.com/library/qwen3.5), [Gemma 4](https://ollama.com/library/gemma4), [Qwen2.5-Coder](https://ollama.com/library/qwen2.5-coder) and [Granite 4](https://ollama.com/library/granite4:3b) catalogs. The requested operator tags are qwen3.5:4b-q4_K_M and gemma4; verify digests locally before comparing. Tags are mutable: the run records full installed digests, quantization metadata and sizes. Different quantization is part of the tested artifact, not normalized by parameter count.

Close any existing Ollama tray/server process before starting a dedicated server. In a separate PowerShell terminal:

```powershell
$env:OLLAMA_NUM_PARALLEL = '1'
$env:OLLAMA_MAX_LOADED_MODELS = '1'
ollama serve
```

These variables apply only to the newly launched server. The harness does not verify server environment values; record the launch environment/version externally. The singleModel API policy independently verifies observed residency. It may unload any other resident model on this server.

## Non-Thinking comparison v1

The original initial.v1.json and comparison.v1.json are unchanged. The latter remains the historical 512-token, provider-default-thinking comparison. New comparison.non-thinking.v1.json reuses the exact same case/suite definitions and adds separately versioned non-thinking-smoke and non-thinking-full profiles (both 1.0.0). Both explicitly set thinking=false (wire field think=false), context 4096, one warmup, four measured repetitions, temperature 0, seed 42, streaming, 300-second timeout/keep-alive, rotating scheduling and singleModel residency. Smoke retains 512 tokens to isolate thinking mode; full uses 2048. The larger budget is a separate experiment and may still truncate.

Saved smoke manifest evidence (Ollama 0.40.5, harness 1.1.0) advertises thinking.values=[false,true], default=true for both tested artifacts:

- qwen3.5:4b-q4_K_M: d8b0f5e9760cd1682034f292d7ef72ec46f432149be0df7574bf2d6e92e38c04
- Gemma operator tag gemma4; saved installed name gemma4:e2b-it-q4_K_M: b37049369adfe3d2b653af0ab301a062ca5cbe96ace6aa0d3f2559ee9b563fc2

Do not assume a mutable gemma4 tag still names that artifact. The commands below resolve the exact saved digest to an installed name, avoiding an alias change or a silent model substitution. Missing artifacts fail before running. No declaration is needed: support is provider-advertised. Thinking-capable models must advertise false; missing/unknown support fails preflight. For explicitly non-thinking models (Qwen2.5-Coder and Granite in saved discovery), false is accepted without claiming they support a thinking mode. This matches [Ollama 0.40.5 ChatHandler](https://github.com/ollama/ollama/blob/v0.40.5/server/routes.go): non-thinking models reject a truthy think setting, not false.

Run from the repository root after building:

```powershell
dotnet run --project LocalModelLab.Cli -c Release --no-build -- cases validate --definitions benchmarks/comparison.non-thinking.v1.json
$inventory = dotnet run --project LocalModelLab.Cli -c Release --no-build -- models list | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Model discovery failed' }
$qwen = @($inventory.models | Where-Object { $_.name -eq 'qwen3.5:4b-q4_K_M' -and $_.digest -eq 'd8b0f5e9760cd1682034f292d7ef72ec46f432149be0df7574bf2d6e92e38c04' })[0].name
$gemma = @($inventory.models | Where-Object { $_.digest -eq 'b37049369adfe3d2b653af0ab301a062ca5cbe96ace6aa0d3f2559ee9b563fc2' })[0].name
if (!$qwen -or !$gemma) { throw 'Install the tested Qwen/Gemma digests before continuing' }
```

Two-model smoke (10 attempts: 2 warmups, 8 measured):

```powershell
dotnet run --project LocalModelLab.Cli -c Release --no-build -- benchmark run --definitions benchmarks/comparison.non-thinking.v1.json --suite smoke --profile non-thinking-smoke --models "$qwen,$gemma" --output results/non-thinking-smoke
```

Full four-model benchmark (80 attempts: 16 warmups, 64 measured):

```powershell
dotnet run --project LocalModelLab.Cli -c Release --no-build -- benchmark run --definitions benchmarks/comparison.non-thinking.v1.json --suite initial --profile non-thinking-full --models "$qwen,$gemma,qwen2.5-coder:7b,granite4:3b" --output results/non-thinking-full
```

Every model occupies each ordering slot once per case in measured rounds. Inspect schedule.json. Do not compare the full profile against the old smoke as if thinking were the only changed variable.

After the run, replace RUN_DIRECTORY with the printed path:

```powershell
$run = 'RUN_DIRECTORY'
dotnet run --project LocalModelLab.Cli -c Release --no-build -- benchmark inspect --run $run
dotnet run --project LocalModelLab.Cli -c Release --no-build -- benchmark report --run $run
Get-Content (Join-Path $run 'state.json')
Get-Content (Join-Path $run 'schedule.json')
```

Check state, per-attempt failures, digest verification, observed context, residencyVerified, loadNs, cachedPromptTokens and outputTruncated. Null is unknown, not zero. Allow the machine to return to a stable idle temperature, then repeat the same command for a new run directory. For an ordering sensitivity check, run a separate block with the model list reversed, preserving all other settings. Do not pool incompatible digests, profiles, server versions or harness binaries. The harness reports each run separately; it does not perform statistical significance testing.

## Measurement evidence

- Static inventory: OS/runtime/CPU metadata; Windows CIM hardware and optional NVIDIA name, UUID, driver and total VRAM. `machine.capturedUtc` timestamps inventory. NVIDIA total memory is MiB; Win32 AdapterRAM can overflow and is not authoritative for large VRAM.
- Sampled residency: exact post-inference `/api/ps` plus timestamp, including provider-reported `size`, `size_vram`, digest, expiry and context when available. `residencyBefore` is the initial pre-attempt snapshot; `residencyAfter` is the snapshot after unload requests, before inference. `unloadRequests` records attempted evictions; `providerRuntime` is the post-inference snapshot. These do not establish a utilization percentage, peak allocation or precise CPU/GPU split. `/api/ps` is documented by [Ollama](https://docs.ollama.com/api/ps); unload semantics use [keep_alive](https://docs.ollama.com/api/generate).
- Not collected: CPU/GPU utilization, periodic temperature/power samples, process memory attribution, peak VRAM and energy. These are explicitly marked notCollected/null in machine telemetry. Optional external `nvidia-smi --query-gpu=timestamp,uuid,temperature.gpu,utilization.gpu,memory.used,power.draw --format=csv -l 1` can supply a separate device-wide time series; it is not captured or attributed by this harness. Unsupported fields must remain N/A, not zero. NVIDIA documents [query semantics](https://docs.nvidia.com/deploy/nvidia-smi/index.html) and [WDDM process-memory limitations](https://docs.nvidia.com/deploy/nvml-api/latest/api/structnvmlProcessDetail__v1__t.html).

Rotating order balances positions; it does not eliminate thermal drift or OS/provider caches. Warmups are excluded from aggregates but do not promise resident/warm measured trials. With four rotating models and singleModel eviction, model reloads are expected. Load-inclusive client latency and provider load duration are retained separately; provider-minus-load is not a warm-cache claim. No automatic cache flush, cooling delay, GPU/CPU attribution or benchmark ranking is added.

## Smoke findings and report diagnostics

The saved default-thinking smoke completed all 10 provider executions, including two warmups, with length termination, empty final content, exposed reasoning and no first-answer timing. Context and residency checks passed. This shows successful inference execution but no usable final answer; it does not measure answer correctness. Generated-token counts are provider totals; no separate answer/reasoning token counts are inferred.

Regenerated reports exclude warmups: each model has 4 completed measured attempts, 4 empty finals, 4 reasoning-only, 4 truncated and 4 missing first-answer times. Empty finals include null/empty/whitespace content; reasoning-only additionally requires nonblank exposed reasoning. Empty/reasoning-only/missing-timing counts cover completed measured attempts; truncation covers all measured attempts explicitly marked true. Counts overlap. A nonempty buffered answer can have unavailable first-answer timing. Means use only available completed measurements; null is never zero. No attempt status, exit code or correctness score changes.

Markdown displays all diagnostics. summary.csv appends emptyFinalAnswers, reasoningOnly and missingFirstAnswer after existing columns; truncated already exists. attempts.csv and schema-1 JSON are unchanged. Reports read old artifacts without migration and always write a new report folder. Consumers requiring an exact summary CSV header must accept the three appended columns.

## Interpreting Ollama memory fields

For the observed Ollama 0.40.5 llamacpp runner, /api/ps size and size_vram are runtime byte accounting, distinct from the installed artifact size in /api/tags. [Scheduler snapshots](https://github.com/ollama/ollama/blob/v0.40.5/server/sched.go) obtain them from runner MemorySize. The [runner implementation](https://github.com/ollama/ollama/blob/v0.40.5/llm/llama_server.go) uses parsed post-load buffer logs, adjusts mmap overlap and may set total equal to GPU accounting when all layers are offloaded; absent parsed accounting, it falls back to model-file size.

Gemma reported size=size_vram=330783784 bytes while Qwen reported 3140495604. Preserve these provider values as observations. Gemma's surprisingly small value remains an accounting anomaly requiring server/runner logs to explain; these artifacts cannot establish its cause. File size is not runtime allocation. Equality does not prove all process memory is on GPU. Do not derive GPU utilization, CPU spill, peak VRAM, energy, a precise offload percentage, or hardware efficiency from these fields. Compare against independent device/process observations and the matching server logs if needed; none are collected by this harness. Semantics can vary by Ollama version/backend.
