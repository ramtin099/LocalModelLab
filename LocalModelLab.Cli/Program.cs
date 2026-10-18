// See https://aka.ms/new-console-template for more information
using System.Reflection;
using System.Text.Json;
using LocalModelLab.Core;
using LocalModelLab.Infrastructure;

return await ExecuteAsync(args);

static async Task<int> ExecuteAsync(string[] args)
{
    if (args.Length == 0 || args is ["--help"] or ["help"])
    {
        Console.WriteLine("""
            LocalModelLab 1.0.0 — Phase 1A pure inference
            models list [--endpoint http://localhost:11434/]
            cases validate [--definitions benchmarks/initial.v1.json]
            benchmark run --models NAME[,NAME] [--suite initial --profile default --definitions benchmarks/initial.v1.json --output results --endpoint URL]
            benchmark report --run DIRECTORY
            benchmark inspect --run DIRECTORY [--attempt 000001]
            Run from repository root. Use exact model names from models list. No retries, scoring or code execution.
            """);
        return 0;
    }
    using var cancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    Console.CancelKeyPress += handler;
    try
    {
        if (args.Length < 2) throw new ArgumentException("Expected a command and subcommand; use --help.");
        var command = $"{args[0]} {args[1]}";
        var allowed = command switch
        {
            "models list" => new[] { "endpoint" },
            "cases validate" => ["definitions"],
            "benchmark run" => ["models", "suite", "profile", "definitions", "output", "endpoint"],
            "benchmark report" => ["run"],
            "benchmark inspect" => ["run", "attempt"],
            _ => throw new ArgumentException($"Unknown command: {command}")
        };
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 2; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Options require --name value pairs.");
            var key = args[i][2..];
            if (!allowed.Contains(key) || !options.TryAdd(key, args[i + 1])) throw new ArgumentException($"Unknown or duplicate option: {args[i]}");
        }
        string Get(string name, string? fallback = null) => options.GetValueOrDefault(name) ?? fallback
            ?? throw new ArgumentException($"--{name} is required.");

        if (command == "benchmark report")
        {
            Console.WriteLine(Reports.Generate(Get("run")));
            return 0;
        }
        if (command == "benchmark inspect")
        {
            var run = Get("run");
            if (options.TryGetValue("attempt", out var attempt))
            {
                if (attempt.Length != 6 || attempt.Any(c => c < '0' || c > '9')) throw new ArgumentException("Attempt id must contain six digits.");
                Console.WriteLine(JsonSerializer.Serialize(Reports.Read<AttemptResult>(Path.Combine(run, "attempts", attempt, "result.json")), LabJson.Options));
            }
            else
            {
                Console.WriteLine(JsonSerializer.Serialize(new { manifest = Reports.Read<RunManifest>(Path.Combine(run, "manifest.json")),
                    journal = Reports.Read<RunState>(Path.Combine(run, "state.json")), attempts = Reports.ReadAttempts(run) }, LabJson.Options));
            }
            return 0;
        }
        BenchmarkDefinitions? definitions = null;
        byte[]? definitionBytes = null;
        if (command is "cases validate" or "benchmark run")
        {
            definitionBytes = await File.ReadAllBytesAsync(Get("definitions", "benchmarks/initial.v1.json"), cancellation.Token);
            definitions = JsonSerializer.Deserialize<BenchmarkDefinitions>(definitionBytes, LabJson.Options) ?? throw new JsonException("Empty definitions.");
            DefinitionValidator.Validate(definitions);
            if (command == "cases validate")
            {
                Console.WriteLine($"Valid schema v1: {definitions.Cases.Length} cases, {definitions.Suites.Length} suites, {definitions.Profiles.Length} profiles. SHA256 {LabJson.Hash(definitionBytes)}");
                return 0;
            }
        }
        var endpoint = Get("endpoint", "http://localhost:11434/");
        if (!Uri.TryCreate(endpoint.TrimEnd('/') + "/", UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("endpoint must be an absolute HTTP(S) URL without credentials, query or fragment.");
        using var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { BaseAddress = uri, Timeout = Timeout.InfiniteTimeSpan };
        var provider = new OllamaProvider(http);
        if (command == "models list")
        {
            Console.WriteLine(JsonSerializer.Serialize(new { providerVersion = await provider.GetVersionAsync(cancellation.Token),
                models = await provider.ListModelsAsync(cancellation.Token) }, LabJson.Options));
            return 0;
        }
        var names = Get("models").Split(',', StringSplitOptions.TrimEntries);
        var machine = await MachineMetadata.CaptureAsync(cancellation.Token);
        var binaries = new[] { Assembly.GetExecutingAssembly(), typeof(BenchmarkService).Assembly, typeof(OllamaProvider).Assembly }
            .ToDictionary(a => a.GetName().Name!, a => LabJson.Hash(File.ReadAllBytes(a.Location)));
        var store = new FileRunStore(Get("output", "results"));
        Console.WriteLine($"Run directory: {store.Location}");
        var state = await new BenchmarkService(provider).RunAsync(definitions!, definitionBytes!, Get("suite", "initial"), Get("profile", "default"),
            names, uri.ToString(), machine, binaries, store, Console.WriteLine, cancellation.Token);
        Console.WriteLine($"{state.Status}; reports: {Reports.Generate(store.Location)}");
        return state.Status switch { "completed" => 0, "interrupted" => 130, _ => 1 };
    }
    catch (OperationCanceledException) { Console.Error.WriteLine("Interrupted."); return 130; }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or JsonException or HttpRequestException or OverflowException or KeyNotFoundException)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
    finally { Console.CancelKeyPress -= handler; }
}
