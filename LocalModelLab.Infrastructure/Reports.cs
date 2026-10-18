using System.Globalization;
using System.Text;
using System.Text.Json;
using LocalModelLab.Core;

namespace LocalModelLab.Infrastructure;

public sealed record ReportGroup(string Scope, string Model, string? CaseId, int Attempts, int Completed,
    int Failed, int InProgress, int Truncated, double? MeanClientMs, double? MedianClientMs,
    double? MeanFirstOutputMs, double? MeanFirstAnswerMs, double? MeanLoadMs,
    double? MeanProviderExcludingLoadMs, double? MeanGenerationTokensPerSecond)
{
    public int EmptyFinalAnswers { get; init; }
    public int ReasoningOnly { get; init; }
    public int MissingFirstAnswer { get; init; }
}

public static class Reports
{
    public static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), LabJson.Options)
        ?? throw new JsonException($"Empty artifact: {path}");

    public static AttemptResult[] ReadAttempts(string runDirectory)
    {
        var root = Path.Combine(runDirectory, "attempts");
        return Directory.Exists(root) ? Directory.GetFiles(root, "result.json", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(Read<AttemptResult>).ToArray() : [];
    }

    public static ReportGroup[] Aggregate(IEnumerable<AttemptResult> results)
    {
        var measured = results.Where(r => !r.Warmup).ToArray();
        return measured.GroupBy(r => r.Model).Select(g => Summarize("model", g.Key, null, g))
            .Concat(measured.GroupBy(r => (r.Model, r.CaseId)).Select(g => Summarize("case", g.Key.Model, g.Key.CaseId, g))).ToArray();
    }

    private static ReportGroup Summarize(string scope, string model, string? caseId, IEnumerable<AttemptResult> values)
    {
        var all = values.ToArray();
        var completed = all.Where(a => a.Status == "completed").ToArray();
        var elapsed = completed.Select(a => a.Metrics.ClientElapsedMs).OfType<double>().Order().ToArray();
        double? median = elapsed.Length == 0 ? null : elapsed.Length % 2 == 1 ? elapsed[elapsed.Length / 2]
            : (elapsed[elapsed.Length / 2 - 1] + elapsed[elapsed.Length / 2]) / 2;
        return new(scope, model, caseId, all.Length, completed.Length,
            all.Count(a => a.Status is not ("completed" or "inProgress")), all.Count(a => a.Status == "inProgress"),
            all.Count(a => a.OutputTruncated == true), Mean(completed.Select(a => a.Metrics.ClientElapsedMs)), median,
            Mean(completed.Select(a => a.Metrics.TimeToFirstOutputMs)), Mean(completed.Select(a => a.Metrics.TimeToFirstAnswerMs)),
            Mean(completed.Select(a => a.Metrics.LoadNs / 1_000_000.0)), Mean(completed.Select(a => a.Metrics.ProviderExcludingLoadMs)),
            Mean(completed.Select(a => a.Metrics.GenerationTokensPerSecond)))
        {
            EmptyFinalAnswers = completed.Count(a => string.IsNullOrWhiteSpace(a.Answer)),
            ReasoningOnly = completed.Count(a => string.IsNullOrWhiteSpace(a.Answer) && !string.IsNullOrWhiteSpace(a.Reasoning)),
            MissingFirstAnswer = completed.Count(a => a.Metrics.TimeToFirstAnswerMs is null)
        };
    }

    private static double? Mean(IEnumerable<double?> values)
    {
        var available = values.OfType<double>().ToArray();
        return available.Length == 0 ? null : available.Average();
    }

    public static string Generate(string runDirectory)
    {
        var manifestPath = Path.Combine(runDirectory, "manifest.json");
        var manifest = File.Exists(manifestPath) ? Read<RunManifest>(manifestPath) : null;
        var state = Read<RunState>(Path.Combine(runDirectory, "state.json"));
        var attempts = ReadAttempts(runDirectory);
        var measured = attempts.Where(a => !a.Warmup).ToArray();
        var groups = manifest is null ? Aggregate(attempts) : manifest.Models.Select(m => Summarize("model", m.Name, null, measured.Where(a => a.Model == m.Name)))
            .Concat(manifest.Models.SelectMany(m => manifest.Cases.Select(c =>
                Summarize("case", m.Name, c.Id, measured.Where(a => a.Model == m.Name && a.CaseId == c.Id))))).ToArray();
        var output = Path.Combine(runDirectory, "reports", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        var markdown = new StringBuilder($"# LocalModelLab inference report\n\nRun: {Safe(manifest?.Id ?? Path.GetFileName(runDirectory))}\n\n" +
            $"Journal status: {Safe(state.Status)}; finalized attempts: {attempts.Count(a => a.Status != "inProgress")}/{state.PlannedAttempts}.\n\n" +
            $"Phase: {Safe(state.Phase)}; failure kind: {Safe(state.FailureKind ?? "none")}; error: {Safe(state.Error ?? "none")}.\n\n" +
            $"Scheduling: {manifest?.Profile.Scheduling.ToString() ?? "unavailable"}; residency: {manifest?.Profile.Residency.ToString() ?? "unavailable"}. See schedule.json for the exact order.\n\n" +
            "Measured attempts only; warmups excluded. Means use available values from completed attempts, including length-limited completions. " +
            "Completed means successful execution, not a usable or correct answer. " +
            "Empty final answers (null/empty/whitespace), reasoning-only (empty final with nonblank exposed reasoning), " +
            "and missing first-answer timing count completed measured attempts only; these counts may overlap. " +
            "Missing timing does not imply an empty answer (non-streaming timing is unavailable). " +
            "Truncated counts all measured attempts with outputTruncated=true; unknown is not false. " +
            "null means unavailable; failures and unfinished attempts are counted separately. " +
            "First output/answer latency includes model loading and network time. Provider minus load is not proof of a warm run. " +
            "No quality scores or universal ranking. Token rates use each model's tokenizer and are not directly comparable across tokenizers.\n\n" +
            "| Scope | Model | Case | Completed/attempts | Failed | In progress | Truncated | Empty final | Reasoning only | Missing first answer | Mean client ms | Median client ms | First output ms | First answer ms | Load ms | Provider minus load ms | Gen tokens/s |\n" +
            "|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|\n");
        foreach (var g in groups)
            markdown.AppendLine($"| {g.Scope} | {Safe(g.Model)} | {Safe(g.CaseId ?? "all")} | {g.Completed}/{g.Attempts} | {g.Failed} | {g.InProgress} | {g.Truncated} | {g.EmptyFinalAnswers} | {g.ReasoningOnly} | {g.MissingFirstAnswer} | {N(g.MeanClientMs)} | {N(g.MedianClientMs)} | {N(g.MeanFirstOutputMs)} | {N(g.MeanFirstAnswerMs)} | {N(g.MeanLoadMs)} | {N(g.MeanProviderExcludingLoadMs)} | {N(g.MeanGenerationTokensPerSecond)} |");
        var csv = new StringBuilder("attempt,model,digest,case,caseVersion,warmup,repetition,status,error,completionReason,truncated,clientMs,firstOutputMs,firstAnswerMs,firstReasoningMs,providerTotalNs,loadNs,promptEvalNs,evalNs,promptTokens,cachedPromptTokens,generatedTokens,providerExcludingLoadMs,generationTokensPerSecond\r\n");
        foreach (var a in attempts)
            csv.AppendLine(string.Join(",", new object?[] { a.Id, a.Model, a.Digest, a.CaseId, a.CaseVersion, a.Warmup, a.Repetition,
                a.Status, a.Error, a.CompletionReason, a.OutputTruncated, a.Metrics.ClientElapsedMs, a.Metrics.TimeToFirstOutputMs,
                a.Metrics.TimeToFirstAnswerMs, a.Metrics.TimeToFirstReasoningMs, a.Metrics.ProviderTotalNs, a.Metrics.LoadNs,
                a.Metrics.PromptEvalNs, a.Metrics.EvalNs, a.Metrics.PromptTokens, a.Metrics.CachedPromptTokens,
                a.Metrics.GeneratedTokens, a.Metrics.ProviderExcludingLoadMs, a.Metrics.GenerationTokensPerSecond }.Select(Csv)));
        var summary = new StringBuilder("scope,model,case,attempts,completed,failed,inProgress,truncated,meanClientMs,medianClientMs,meanFirstOutputMs,meanFirstAnswerMs,meanLoadMs,meanProviderExcludingLoadMs,meanGenerationTokensPerSecond,emptyFinalAnswers,reasoningOnly,missingFirstAnswer\r\n");
        foreach (var g in groups)
            summary.AppendLine(string.Join(",", new object?[] { g.Scope, g.Model, g.CaseId, g.Attempts, g.Completed, g.Failed, g.InProgress,
                g.Truncated, g.MeanClientMs, g.MedianClientMs, g.MeanFirstOutputMs, g.MeanFirstAnswerMs, g.MeanLoadMs,
                g.MeanProviderExcludingLoadMs, g.MeanGenerationTokensPerSecond, g.EmptyFinalAnswers, g.ReasoningOnly, g.MissingFirstAnswer }.Select(Csv)));
        FileRunStore.AtomicWrite(Path.Combine(output, "report.md"), Encoding.UTF8.GetBytes(markdown.ToString()));
        FileRunStore.AtomicWrite(Path.Combine(output, "attempts.csv"), Encoding.UTF8.GetBytes(csv.ToString()));
        FileRunStore.AtomicWrite(Path.Combine(output, "summary.csv"), Encoding.UTF8.GetBytes(summary.ToString()));
        return output;
    }

    private static string N(double? value) => value?.ToString("F3", CultureInfo.InvariantCulture) ?? "null";
    private static string Safe(string value) => value.Replace("|", "&#124;").Replace("\r", " ").Replace("\n", " ").Replace("<", "&lt;");
    private static string Csv(object? value)
    {
        var text = value is IFormattable number ? number.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? "null";
        // CSV is often opened in Excel; provider-controlled text must not become a formula.
        if (value is string && text.TrimStart().Length > 0 && "=+-@".Contains(text.TrimStart()[0])) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
