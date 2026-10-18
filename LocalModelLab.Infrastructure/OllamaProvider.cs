using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LocalModelLab.Core;

namespace LocalModelLab.Infrastructure;

public sealed class OllamaProvider(HttpClient http) : IInferenceProvider
{
    public async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        var json = await GetAsync("api/version", cancellationToken);
        var version = json.GetProperty("version").GetString();
        return !string.IsNullOrWhiteSpace(version) ? version : throw new InvalidOperationException("Ollama version is missing.");
    }

    public async Task<IReadOnlyList<ModelMetadata>> ListModelsAsync(CancellationToken cancellationToken)
    {
        var tags = await GetAsync("api/tags", cancellationToken);
        var result = new List<ModelMetadata>();
        foreach (var tag in tags.GetProperty("models").EnumerateArray())
        {
            var name = tag.GetProperty("name").GetString() ?? throw new InvalidOperationException("Model name missing.");
            var digest = tag.GetProperty("digest").GetString();
            if (string.IsNullOrWhiteSpace(digest)) throw new InvalidOperationException($"{name}: model digest missing.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            using var response = await http.PostAsJsonAsync("api/show", new { model = name }, timeout.Token);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var show = document.RootElement.Clone();
            result.Add(new ModelMetadata(name, digest, Number(tag, "size"), tag.Clone(), show,
                ResolveCapabilities(show), ThinkingValues(show), "provider /api/show"));
        }
        return result;
    }

    public async Task VerifyDigestAsync(ModelMetadata model, CancellationToken cancellationToken)
    {
        var tags = await GetAsync("api/tags", cancellationToken);
        var found = tags.GetProperty("models").EnumerateArray()
            .FirstOrDefault(t => t.GetProperty("name").GetString() == model.Name);
        if (found.ValueKind == JsonValueKind.Undefined || found.GetProperty("digest").GetString() != model.Digest)
            throw new ModelDigestChangedException($"Model digest changed or model removed: {model.Name}. Attribution is invalid; stop changing models during runs.");
    }

    public async Task CaptureRuntimeMetadataAsync(ModelMetadata model, ExecutionProfile profile, AttemptResult result, CancellationToken cancellationToken)
    {
        try { result.ProviderRuntime = await GetAsync("api/ps", cancellationToken); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.RuntimeMetadataError = ex.Message;
            return;
        }
        if (result.ProviderRuntime is { } runtime && runtime.TryGetProperty("models", out var loaded))
        {
            var current = loaded.EnumerateArray().FirstOrDefault(m =>
                m.TryGetProperty("digest", out var digest) && digest.GetString() == model.Digest);
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty("context_length", out var context) &&
                context.TryGetInt64(out var length))
            {
                result.VerifiedContextLength = length;
                if (length != profile.NumCtx) throw new InvalidOperationException($"Provider context_length {length} differs from requested numCtx {profile.NumCtx}.");
            }
        }
    }

    public static Dictionary<ModelCapability, CapabilityState> ResolveCapabilities(JsonElement show)
    {
        var result = Enum.GetValues<ModelCapability>().ToDictionary(c => c, _ => CapabilityState.Missing);
        if (!show.TryGetProperty("capabilities", out var values)) return result;
        if (values.ValueKind == JsonValueKind.Null)
            return Enum.GetValues<ModelCapability>().ToDictionary(c => c, _ => CapabilityState.Unknown);
        var names = values.EnumerateArray().Select(v => v.GetString()).ToHashSet(StringComparer.Ordinal);
        result[ModelCapability.Chat] = names.Contains("completion") ? CapabilityState.Supported : CapabilityState.Unsupported;
        result[ModelCapability.Embedding] = names.Contains("embedding") ? CapabilityState.Supported : CapabilityState.Unsupported;
        result[ModelCapability.Vision] = names.Contains("vision") ? CapabilityState.Supported : CapabilityState.Unsupported;
        result[ModelCapability.Thinking] = names.Contains("thinking") ? CapabilityState.Supported : CapabilityState.Unsupported;
        result[ModelCapability.Fim] = names.Contains("insert") ? CapabilityState.Supported : CapabilityState.Unknown;
        // Decision behavior has no standardized Ollama capability declaration.
        result[ModelCapability.Decision] = CapabilityState.Unknown;
        return result;
    }

    private static JsonElement[]? ThinkingValues(JsonElement show)
    {
        if (show.TryGetProperty("thinking", out var thinking) && thinking.ValueKind == JsonValueKind.Object &&
            thinking.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Array)
            return values.EnumerateArray().Select(v => v.Clone()).ToArray();
        return null;
    }

    public string BuildRequest(ModelMetadata model, BenchmarkCase item, ExecutionProfile profile)
    {
        var messages = new List<object>();
        if (item.System is not null) messages.Add(new { role = "system", content = item.System });
        messages.Add(new { role = "user", content = item.Prompt });
        var body = new Dictionary<string, object>
        {
            ["model"] = model.Name, ["messages"] = messages, ["stream"] = profile.Stream,
            ["keep_alive"] = $"{profile.KeepAliveSeconds}s",
            ["options"] = new { temperature = profile.Temperature, seed = profile.Seed, num_ctx = profile.NumCtx, num_predict = profile.NumPredict }
        };
        if (profile.Thinking is { } thinking) body["think"] = thinking;
        return JsonSerializer.Serialize(body, LabJson.Options);
    }

    public async Task InferAsync(string request, bool streaming, Stream rawResponse, AttemptResult result, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/chat")
        {
            Content = new StringContent(request, new UTF8Encoding(false), "application/json")
        };
        var clock = Stopwatch.StartNew();
        try
        {
            using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            result.HttpStatus = (int)response.StatusCode;
            result.ResponseHeaders = response.Headers.Concat(response.Content.Headers)
                .ToDictionary(p => p.Key, p => p.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var recorded = new RecordingStream(source, rawResponse);
            using var reader = new StreamReader(recorded, new UTF8Encoding(false, true));
            if (!response.IsSuccessStatusCode)
            {
                await reader.ReadToEndAsync(cancellationToken);
                throw new HttpRequestException($"Ollama returned HTTP {(int)response.StatusCode}; see response.raw.");
            }
            if (streaming)
            {
                while (await reader.ReadLineAsync(cancellationToken) is { } line)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    ParseChunk(line, result, clock.Elapsed.TotalMilliseconds, true);
                }
            }
            else
            {
                ParseChunk(await reader.ReadToEndAsync(cancellationToken), result, clock.Elapsed.TotalMilliseconds, false);
            }
            if (!result.ProviderCompleted) throw new IOException("Provider stream ended without done=true.");
        }
        finally
        {
            result.Metrics.ClientElapsedMs = clock.Elapsed.TotalMilliseconds;
        }
    }

    private static void ParseChunk(string raw, AttemptResult result, double elapsedMs, bool streaming)
    {
        using var document = JsonDocument.Parse(raw);
        var chunk = document.RootElement;
        if (result.ProviderCompleted) throw new IOException("Provider sent data after terminal response.");
        if (chunk.TryGetProperty("error", out var error)) throw new IOException($"Ollama stream error: {error}");
        if (chunk.TryGetProperty("message", out var message))
        {
            if (message.TryGetProperty("tool_calls", out var tools) && tools.ValueKind == JsonValueKind.Array && tools.GetArrayLength() > 0)
                throw new IOException("Unexpected tool calls in pure inference response; no tools were executed.");
            if (message.TryGetProperty("content", out var content) && content.GetString() is { Length: > 0 } answer)
            {
                result.Answer += answer;
                if (streaming)
                {
                    result.Metrics.TimeToFirstAnswerMs ??= elapsedMs;
                    result.Metrics.TimeToFirstOutputMs ??= elapsedMs;
                }
            }
            if (message.TryGetProperty("thinking", out var thinking))
            {
                var reasoning = thinking.GetString();
                if (reasoning is not null) result.Reasoning = (result.Reasoning ?? "") + reasoning;
                if (streaming && reasoning is { Length: > 0 })
                {
                    result.Metrics.TimeToFirstReasoningMs ??= elapsedMs;
                    result.Metrics.TimeToFirstOutputMs ??= elapsedMs;
                }
            }
        }
        if (chunk.TryGetProperty("done", out var done) && done.GetBoolean())
        {
            result.ProviderCompleted = true;
            result.CompletionReason = chunk.TryGetProperty("done_reason", out var reason) ? reason.GetString() : null;
            result.OutputTruncated = result.CompletionReason switch { "length" => true, "stop" => false, _ => null };
            result.Metrics.ProviderTotalNs = Number(chunk, "total_duration");
            result.Metrics.LoadNs = Number(chunk, "load_duration");
            result.Metrics.PromptEvalNs = Number(chunk, "prompt_eval_duration");
            result.Metrics.EvalNs = Number(chunk, "eval_duration");
            result.Metrics.PromptTokens = Number(chunk, "prompt_eval_count");
            result.Metrics.CachedPromptTokens = Number(chunk, "prompt_eval_cached_count");
            result.Metrics.GeneratedTokens = Number(chunk, "eval_count");
        }
    }

    private static long? Number(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null) return null;
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt64(out var number) || number < 0)
            throw new JsonException($"Invalid provider metric: {name}");
        return number;
    }

    private async Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await http.GetAsync(path, timeout.Token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        return document.RootElement.Clone();
    }

    // Copy transport bytes before decoding so malformed and interrupted responses remain inspectable.
    private sealed class RecordingStream(Stream source, Stream destination) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = source.Read(buffer, offset, count);
            destination.Write(buffer, offset, read);
            destination.Flush();
            return read;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            // Once received, preserve bytes even when the inference cancellation token has just fired.
            await destination.WriteAsync(buffer[..read], CancellationToken.None);
            await destination.FlushAsync(CancellationToken.None);
            return read;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override void Flush() => destination.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
