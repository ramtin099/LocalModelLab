using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalModelLab.Core;

public static class LabJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public static string Fingerprint<T>(T value) => Hash(JsonSerializer.SerializeToUtf8Bytes(value, Options));
}

public enum CapabilityState { Missing, Unknown, Unsupported, Supported }
public enum ModelCapability { Chat, Fim, Embedding, Vision, Decision, Thinking }
public enum SchedulingPolicy { ModelFirst, Rotating }
public enum ResidencyPolicy { ProviderManaged, SingleModel }

public sealed record BenchmarkCase(string Id, string Version, string Prompt, string? System = null);
public sealed record BenchmarkSuite(string Id, string Version, string[] Cases);
public sealed record ExecutionProfile(string Id, string Version, int Warmups = 1, int Repetitions = 3,
    double Temperature = 0, int Seed = 42, int NumCtx = 4096, int NumPredict = 512,
    JsonElement? Thinking = null, bool Stream = true, int TimeoutSeconds = 180, int KeepAliveSeconds = 300,
    SchedulingPolicy Scheduling = SchedulingPolicy.ModelFirst, ResidencyPolicy Residency = ResidencyPolicy.ProviderManaged);
public sealed record ModelDeclaration(string Model, string Digest,
    Dictionary<ModelCapability, CapabilityState> Capabilities, JsonElement[]? ThinkingValues = null);
public sealed record BenchmarkDefinitions(int SchemaVersion, BenchmarkCase[] Cases, BenchmarkSuite[] Suites,
    ExecutionProfile[] Profiles, ModelDeclaration[]? ModelDeclarations = null);

public static class DefinitionValidator
{
    public static void Validate(BenchmarkDefinitions definitions)
    {
        if (definitions.SchemaVersion != 1) throw new ArgumentException("Only definition schemaVersion 1 is supported.");
        if (definitions.Cases is null || definitions.Suites is null || definitions.Profiles is null ||
            definitions.Cases.Any(c => c is null) || definitions.Suites.Any(s => s is null) || definitions.Profiles.Any(p => p is null))
            throw new ArgumentException("cases, suites and profiles are required.");
        ValidateIds(definitions.Cases.Select(x => (x.Id, x.Version)), "case");
        ValidateIds(definitions.Suites.Select(x => (x.Id, x.Version)), "suite");
        ValidateIds(definitions.Profiles.Select(x => (x.Id, x.Version)), "profile");
        foreach (var item in definitions.Cases)
            if (string.IsNullOrWhiteSpace(item.Prompt)) throw new ArgumentException($"Case {item.Id}: prompt is required.");
        foreach (var suite in definitions.Suites)
            if (suite.Cases is null || suite.Cases.Length == 0 || suite.Cases.Distinct().Count() != suite.Cases.Length ||
                suite.Cases.Any(id => !definitions.Cases.Any(c => c.Id == id)))
                throw new ArgumentException($"Suite {suite.Id}: empty, duplicate or unknown case reference.");
        foreach (var profile in definitions.Profiles)
        {
            if (!Enum.IsDefined(profile.Scheduling) || !Enum.IsDefined(profile.Residency))
                throw new ArgumentException($"Profile {profile.Id}: invalid scheduling or residency policy.");
            if (profile.Warmups < 0 || profile.Repetitions < 1 || !double.IsFinite(profile.Temperature) ||
                profile.Temperature < 0 || profile.Temperature > 2 || profile.Seed < 0 || profile.NumCtx < 1 ||
                profile.NumPredict < 1 || profile.TimeoutSeconds < 1 || profile.TimeoutSeconds > 86400 ||
                profile.KeepAliveSeconds < 0 || profile.KeepAliveSeconds > 86400)
                throw new ArgumentException($"Profile {profile.Id}: invalid repetitions, options or timeout.");
            if (profile.Thinking is { } thinking && thinking.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.String))
                throw new ArgumentException($"Profile {profile.Id}: thinking must be omitted, boolean or named string.");
            if (profile.Thinking is { ValueKind: JsonValueKind.String } text && string.IsNullOrWhiteSpace(text.GetString()))
                throw new ArgumentException($"Profile {profile.Id}: thinking cannot be empty.");
        }
        var declarations = definitions.ModelDeclarations ?? [];
        if (declarations.Any(d => d is null) || declarations.Select(d => d.Model).Distinct(StringComparer.Ordinal).Count() != declarations.Length)
            throw new ArgumentException("Duplicate model declarations.");
        foreach (var d in declarations)
            if (string.IsNullOrWhiteSpace(d.Model) || string.IsNullOrWhiteSpace(d.Digest) || d.Capabilities is null ||
                (d.ThinkingValues?.Any(v => v.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.String)) ?? false))
                throw new ArgumentException("Model declarations need model, digest, capabilities and valid thinkingValues.");
    }

    private static void ValidateIds(IEnumerable<(string Id, string Version)> entries, string kind)
    {
        var values = entries.ToArray();
        if (values.Length == 0 || values.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != values.Length ||
            values.Any(x => string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Version)))
            throw new ArgumentException($"Each {kind} needs a unique id and a nonempty version.");
    }
}

public sealed record ModelMetadata(string Name, string Digest, long? SizeBytes, JsonElement Tag,
    JsonElement Show, Dictionary<ModelCapability, CapabilityState> Capabilities,
    JsonElement[]? ThinkingValues, string CapabilitySource)
{
    public bool IsRemote => new[] { Tag, Show }.Any(value => new[] { "remote_host", "remote_model" }.Any(name =>
        value.TryGetProperty(name, out var remote) && remote.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(remote.GetString())));
}

public static class ModelValidation
{
    public static ModelMetadata ApplyDeclaration(ModelMetadata model, ModelDeclaration? declaration)
    {
        if (declaration is null) return model;
        if (model.Digest != declaration.Digest) throw new ArgumentException($"Declaration digest mismatch for {model.Name}.");
        var capabilities = new Dictionary<ModelCapability, CapabilityState>(model.Capabilities);
        foreach (var entry in declaration.Capabilities)
        {
            if (capabilities.GetValueOrDefault(entry.Key) is CapabilityState.Supported or CapabilityState.Unsupported &&
                capabilities[entry.Key] != entry.Value)
                throw new ArgumentException($"Declaration contradicts provider capability {entry.Key} for {model.Name}.");
            capabilities[entry.Key] = entry.Value;
        }
        return model with { Capabilities = capabilities, ThinkingValues = model.ThinkingValues ?? declaration.ThinkingValues,
            CapabilitySource = "provider + digest-bound explicit declaration" };
    }

    public static void Validate(ModelMetadata model, ExecutionProfile profile)
    {
        if (model.IsRemote) throw new ArgumentException($"{model.Name}: remote/cloud model is outside local Phase 1A scope.");
        Require(model, ModelCapability.Chat);
        if (profile.Thinking is { } thinking)
        {
            Require(model, ModelCapability.Thinking);
            if (model.ThinkingValues is null)
                throw new ArgumentException($"{model.Name}: thinking values are missing; supply a digest-bound declaration.");
            if (!model.ThinkingValues.Any(v => v.GetRawText() == thinking.GetRawText()))
                throw new ArgumentException($"{model.Name}: unsupported thinking value {thinking}.");
        }
        if (model.Show.TryGetProperty("model_info", out var info))
        {
            var limits = info.EnumerateObject().Where(p => p.Name.EndsWith(".context_length", StringComparison.Ordinal))
                .Select(p => p.Value.TryGetInt64(out var n) ? (long?)n : null).Where(n => n > 0).ToArray();
            if (limits.Length > 0 && profile.NumCtx > limits.Min())
                throw new ArgumentException($"{model.Name}: numCtx exceeds advertised context length.");
        }
    }

    private static void Require(ModelMetadata model, ModelCapability capability)
    {
        var state = model.Capabilities.GetValueOrDefault(capability, CapabilityState.Missing);
        if (state != CapabilityState.Supported)
            throw new ArgumentException($"{model.Name}: {capability} capability is {state}; cannot run without explicit support.");
    }
}
