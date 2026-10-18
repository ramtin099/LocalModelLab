# LocalModelLab

Phase 1A: reproducible local inference benchmarks using .NET 8 and Ollama. Four projects, sequential execution, independent attempts, immutable run snapshots, streamed answer/reasoning capture, raw responses and Markdown/CSV reports. No generated-code execution, quality scoring or model ranking.

- [Persian operating guide](docs/operations.fa.md)
- [Approved architecture and boundaries](docs/architecture.md)
- [Implementation and verification record](docs/implementation-status.md)
- [Versioned initial cases, suites and profiles](benchmarks/initial.v1.json)

From the repository root, with a .NET SDK supporting net8.0:

```powershell
dotnet build LocalModelLab.sln -c Release
dotnet test LocalModelLab.sln -c Release
dotnet run --project LocalModelLab.Cli -c Release -- models list
dotnet run --project LocalModelLab.Cli -c Release -- cases validate
dotnet run --project LocalModelLab.Cli -c Release -- benchmark run --models granite4:3b --suite smoke --profile smoke
dotnet run --project LocalModelLab.Cli -c Release -- benchmark report --run "results/RUN_ID"
dotnet run --project LocalModelLab.Cli -c Release -- benchmark inspect --run "results/RUN_ID" --attempt 000002
```

Use exact installed model names from `models list`; the harness never pulls models. For multiple models, pass `--models "model:tag,other:tag"`. Use `--help` for all options. Test execution requires no running Ollama server. Production uses only .NET libraries; xUnit and the test SDK are test-only dependencies.

Results are local and gitignored. A run directory contains `manifest.json`, `definitions.json`, `state.json`, and `attempts/000001/{request.json,response.raw,result.json}`. Reports get their own new directory on each generation. Completed inference artifacts are never regenerated or resumed. `Ctrl+C` preserves the active attempt; forced termination leaves its raw bytes and an `inProgress` marker.

The Ollama adapter follows the upstream [chat contract](https://docs.ollama.com/api/chat), [model discovery contract](https://docs.ollama.com/api/tags), and [capability/thinking types](https://github.com/ollama/ollama/blob/main/api/types.go). Requested thinking values require explicit model support; unsupported or unknown settings are never automatically substituted. Server-internal effective values that Ollama does not expose remain unknown.
