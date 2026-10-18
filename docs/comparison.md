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
ollama pull qwen3.5:4b
ollama pull gemma4:e2b
ollama pull qwen2.5-coder:7b
ollama pull granite4:3b
```

Tags verified against the official [Qwen3.5](https://ollama.com/library/qwen3.5), [Gemma 4](https://ollama.com/library/gemma4), [Qwen2.5-Coder](https://ollama.com/library/qwen2.5-coder) and [Granite 4](https://ollama.com/library/granite4:3b) catalogs on 2026-10-18. Tags are mutable: the run records full installed digests, quantization metadata and sizes. Different quantization is part of the tested artifact, not normalized by parameter count.

Close any existing Ollama tray/server process before starting a dedicated server. In a separate PowerShell terminal:

```powershell
$env:OLLAMA_NUM_PARALLEL = '1'
$env:OLLAMA_MAX_LOADED_MODELS = '1'
ollama serve
```

These variables apply only to the newly launched server. The harness does not verify server environment values; record the launch environment/version externally. The singleModel API policy independently verifies observed residency. It may unload any other resident model on this server.

## Execution

```powershell
dotnet run --project LocalModelLab.Cli -c Release --no-build -- models list
dotnet run --project LocalModelLab.Cli -c Release --no-build -- cases validate --definitions benchmarks/comparison.v1.json
dotnet run --project LocalModelLab.Cli -c Release --no-build -- benchmark run --definitions benchmarks/comparison.v1.json --suite initial --profile comparison --models 'qwen3.5:4b,gemma4:e2b,qwen2.5-coder:7b,granite4:3b' --output results/comparison
```

The comparison profile uses one warmup per model/case, four measured repetitions, temperature 0, seed 42, context 4096, output cap 512, streaming, 300-second attempt timeout, 300-second keep-alive, rotating scheduling and singleModel residency. There are 80 scheduled attempts: 16 warmups and 64 measured trials. Every model occupies each ordering slot once per case in measured rounds. Inspect `schedule.json` before interpreting the results.

Thinking is deliberately omitted: each model uses its provider/model default. This is a default-behavior comparison, not equal reasoning budgets. For a different thinking policy, use a separately versioned definition and digest-bound declarations only when evidence supports the requested values. Do not invent shared support for `think=false` across all four models. A length-limited answer remains a completed but truncated inference; inspect truncation rates and reasoning separately.

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
