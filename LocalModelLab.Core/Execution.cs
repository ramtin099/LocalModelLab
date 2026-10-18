using System.Text.Json;

namespace LocalModelLab.Core;

public interface IInferenceProvider
{
    Task<string> GetVersionAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelMetadata>> ListModelsAsync(CancellationToken cancellationToken);
    Task VerifyDigestAsync(ModelMetadata model, CancellationToken cancellationToken);
    Task PrepareResidencyAsync(ModelMetadata model, ExecutionProfile profile, AttemptResult result, CancellationToken cancellationToken);
    Task CaptureRuntimeMetadataAsync(ModelMetadata model, ExecutionProfile profile, AttemptResult result, CancellationToken cancellationToken);
    string BuildRequest(ModelMetadata model, BenchmarkCase item, ExecutionProfile profile);
    Task InferAsync(string request, bool streaming, Stream rawResponse, AttemptResult result, CancellationToken cancellationToken);
}

public interface IRunStore
{
    string Location { get; }
    void WriteJson<T>(string relativePath, T value);
    void WriteBytes(string relativePath, byte[] bytes);
    Stream OpenRaw(string relativePath);
}

public sealed class ModelUnavailableException(string message) : ArgumentException(message);

public sealed class ModelDigestChangedException(string message) : InvalidOperationException(message);

public sealed class InferenceMetrics
{
    public double? ClientElapsedMs { get; set; }
    public double? TimeToFirstOutputMs { get; set; }
    public double? TimeToFirstAnswerMs { get; set; }
    public double? TimeToFirstReasoningMs { get; set; }
    public long? ProviderTotalNs { get; set; }
    public long? LoadNs { get; set; }
    public long? PromptEvalNs { get; set; }
    public long? EvalNs { get; set; }
    public long? PromptTokens { get; set; }
    public long? CachedPromptTokens { get; set; }
    public long? GeneratedTokens { get; set; }
    public double? ProviderExcludingLoadMs => ProviderTotalNs is { } total && LoadNs is { } load && total >= load
        ? (total - load) / 1_000_000.0 : null;
    public double? GenerationTokensPerSecond => GeneratedTokens is { } count && EvalNs is > 0
        ? count * 1_000_000_000.0 / EvalNs.Value : null;
}

public sealed class AttemptResult
{
    public int SchemaVersion { get; init; } = 1;
    public required string Id { get; init; }
    public required string Model { get; init; }
    public required string Digest { get; init; }
    public required string CaseId { get; init; }
    public required string CaseVersion { get; init; }
    public required string CaseFingerprint { get; init; }
    public required string ProfileFingerprint { get; init; }
    public required bool Warmup { get; init; }
    public required int Repetition { get; init; }
    public DateTimeOffset StartedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedUtc { get; set; }
    public string Status { get; set; } = "inProgress";
    public string? Error { get; set; }
    public string Answer { get; set; } = "";
    public string? Reasoning { get; set; }
    public string? CompletionReason { get; set; }
    public bool? OutputTruncated { get; set; }
    public bool ProviderCompleted { get; set; }
    public bool? DigestVerifiedAfter { get; set; }
    public int? HttpStatus { get; set; }
    public Dictionary<string, string[]>? ResponseHeaders { get; set; }
    public JsonElement? ResidencyBefore { get; set; }
    public JsonElement? ResidencyAfter { get; set; }
    public List<string> UnloadRequests { get; set; } = [];
    public bool? ResidencyVerified { get; set; }
    public DateTimeOffset? RuntimeSampleUtc { get; set; }
    public JsonElement? ProviderRuntime { get; set; }
    public string? RuntimeMetadataError { get; set; }
    public long? VerifiedContextLength { get; set; }
    public InferenceMetrics Metrics { get; set; } = new();
}

public sealed record RunManifest(int SchemaVersion, string HarnessVersion, Dictionary<string, string> HarnessBinaries,
    string Id, DateTimeOffset StartedUtc, string DefinitionFileSha256, string SuiteFingerprint,
    string ProfileFingerprint, BenchmarkSuite Suite, ExecutionProfile Profile, BenchmarkCase[] Cases,
    ModelMetadata[] Models, string ProviderVersion, string ProviderEndpoint, JsonElement Machine,
    string SettingsEvidence = "Explicit options are in request.json; provider defaults are in model metadata when exposed. Server-internal effective values are otherwise unknown.");

public sealed class RunState
{
    public string Status { get; set; } = "running";
    public int PlannedAttempts { get; set; }
    public string Phase { get; set; } = "unknown";
    public string? FailureKind { get; set; }
    public int FinishedAttempts { get; set; }
    public DateTimeOffset? FinishedUtc { get; set; }
    public string? Error { get; set; }
}

public sealed class BenchmarkService(IInferenceProvider provider)
{
    public async Task<RunState> RunAsync(BenchmarkDefinitions definitions, byte[] definitionBytes, string suiteId,
        string profileId, string[] modelNames, string endpoint, JsonElement machine,
        Dictionary<string, string> binaries, IRunStore store, Action<string>? progress, CancellationToken cancellationToken)
    {
        var state = new RunState { Phase = "preflight" };
        store.WriteJson("state.json", state);
        store.WriteBytes("definitions.json", definitionBytes);
        store.WriteJson("selection.json", new { suiteId, profileId, modelNames, endpoint });
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            DefinitionValidator.Validate(definitions);
            var suite = definitions.Suites.SingleOrDefault(s => s.Id == suiteId)
                ?? throw new ArgumentException($"Unknown suite: {suiteId}");
            var profile = definitions.Profiles.SingleOrDefault(p => p.Id == profileId)
                ?? throw new ArgumentException($"Unknown profile: {profileId}");
            if (modelNames.Length == 0 || modelNames.Any(string.IsNullOrWhiteSpace) || modelNames.Distinct().Count() != modelNames.Length)
                throw new ArgumentException("Select one or more unique model names.");
            var version = await provider.GetVersionAsync(cancellationToken);
            var available = await provider.ListModelsAsync(cancellationToken);
            var models = modelNames.Select(name =>
            {
                var model = available.SingleOrDefault(m => m.Name == name)
                    ?? throw new ModelUnavailableException($"Model not installed (use exact name from models list): {name}");
                model = ModelValidation.ApplyDeclaration(model, definitions.ModelDeclarations?.SingleOrDefault(d => d.Model == name));
                ModelValidation.Validate(model, profile);
                return model;
            }).ToArray();
            var cases = suite.Cases.Select(id => definitions.Cases.Single(c => c.Id == id)).ToArray();
            var planned = (long)models.Length * cases.Length * ((long)profile.Warmups + profile.Repetitions);
            if (planned > 999999) throw new ArgumentException("A run supports at most 999999 attempts; split the benchmark into smaller runs.");
            var manifest = new RunManifest(1, "1.1.0", binaries, Path.GetFileName(store.Location), DateTimeOffset.UtcNow,
                LabJson.Hash(definitionBytes), LabJson.Fingerprint(suite), LabJson.Fingerprint(profile), suite, profile,
                cases, models, version, endpoint, machine);
            store.WriteJson("manifest.json", manifest);
            state.PlannedAttempts = (int)planned;
            state.Phase = "execution";
            var schedule = CreateSchedule(models, cases, profile);
            store.WriteJson("schedule.json", schedule);
            store.WriteJson("state.json", state);
            foreach (var entry in schedule)
            {
                var model = models.Single(m => m.Name == entry.Model);
                var item = cases.Single(c => c.Id == entry.CaseId);
                cancellationToken.ThrowIfCancellationRequested();
                await provider.VerifyDigestAsync(model, cancellationToken);
                var result = new AttemptResult
                {
                    Id = entry.Id, Model = model.Name, Digest = model.Digest,
                    CaseId = item.Id, CaseVersion = item.Version, CaseFingerprint = LabJson.Fingerprint(item),
                    ProfileFingerprint = manifest.ProfileFingerprint, Warmup = entry.Warmup,
                    Repetition = entry.Repetition
                };
                var directory = $"attempts/{result.Id}";
                var request = provider.BuildRequest(model, item, profile);
                store.WriteBytes($"{directory}/request.json", System.Text.Encoding.UTF8.GetBytes(request));
                store.WriteJson($"{directory}/result.json", result);
                progress?.Invoke($"{result.Id} {model.Name} {item.Id} {(result.Warmup ? "warmup" : "measured")} {result.Repetition}");
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(profile.TimeoutSeconds));
                try
                {
                    await provider.PrepareResidencyAsync(model, profile, result, timeout.Token);
                    using (var raw = store.OpenRaw($"{directory}/response.raw"))
                        await provider.InferAsync(request, profile.Stream, raw, result, timeout.Token);
                    await provider.CaptureRuntimeMetadataAsync(model, profile, result, timeout.Token);
                    await provider.VerifyDigestAsync(model, timeout.Token);
                    result.DigestVerifiedAfter = true;
                    result.Status = "completed";
                }
                catch (OperationCanceledException)
                {
                    result.Status = cancellationToken.IsCancellationRequested ? "interrupted" : "timedOut";
                    result.Error = result.Status;
                    result.OutputTruncated = result.ProviderCompleted ? result.OutputTruncated : true;
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException)
                {
                    result.Status = "failed";
                    result.Error = ex.Message;
                    if (ex is ModelDigestChangedException) result.DigestVerifiedAfter = false;
                    result.OutputTruncated = result.ProviderCompleted ? result.OutputTruncated : true;
                }
                finally
                {
                    result.FinishedUtc = DateTimeOffset.UtcNow;
                    store.WriteJson($"{directory}/result.json", result);
                    state.FinishedAttempts++;
                    if (result.Status != "completed") state.Status = "completedWithErrors";
                    store.WriteJson("state.json", state);
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (state.Status == "running") state.Status = "completed";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            state.Status = "interrupted";
            state.FailureKind = "cancelled";
        }
        catch (Exception ex)
        {
            state.Status = "failed";
            state.Error = ex.Message;
            state.FailureKind = ex switch
            {
                ModelUnavailableException => "unavailableModel",
                ArgumentException => "invalidConfiguration",
                _ => "providerFailure"
            };
            throw;
        }
        finally
        {
            state.FinishedUtc = DateTimeOffset.UtcNow;
            store.WriteJson("state.json", state);
        }
        return state;
    }

    public sealed record ScheduledAttempt(string Id, string Model, string CaseId, bool Warmup, int Repetition);

    public static ScheduledAttempt[] CreateSchedule(ModelMetadata[] models, BenchmarkCase[] cases, ExecutionProfile profile)
    {
        var result = new List<ScheduledAttempt>();
        void Add(ModelMetadata model, BenchmarkCase item, int repetition) => result.Add(new(
            (result.Count + 1).ToString("D6"), model.Name, item.Id, repetition < profile.Warmups,
            repetition < profile.Warmups ? repetition + 1 : repetition - profile.Warmups + 1));
        if (profile.Scheduling == SchedulingPolicy.ModelFirst)
        {
            foreach (var model in models)
            foreach (var item in cases)
            for (var repetition = 0; repetition < (long)profile.Warmups + profile.Repetitions; repetition++)
                Add(model, item, repetition);
        }
        else
        {
            // Rotate independently within warmup and measured phases; warmups never shift measured starting order.
            foreach (var warmup in new[] { true, false })
            {
                var rounds = warmup ? profile.Warmups : profile.Repetitions;
                for (var round = 0; round < rounds; round++)
                for (var caseIndex = 0; caseIndex < cases.Length; caseIndex++)
                for (var slot = 0; slot < models.Length; slot++)
                    Add(models[(slot + round + caseIndex) % models.Length], cases[caseIndex],
                        warmup ? round : profile.Warmups + round);
            }
        }
        return result.ToArray();
    }
}
