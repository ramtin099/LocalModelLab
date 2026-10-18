using System.Text.Json;
using LocalModelLab.Core;
using LocalModelLab.Infrastructure;

namespace LocalModelLab.Tests;

public class ValidationTests
{
    [Fact]
    public void ValidDefinitionsHaveStableFingerprints()
    {
        var definitions = TestData.Definitions();
        DefinitionValidator.Validate(definitions);
        var roundtrip = JsonSerializer.Deserialize<BenchmarkDefinitions>(JsonSerializer.Serialize(definitions, LabJson.Options), LabJson.Options)!;
        Assert.Equal(LabJson.Fingerprint(definitions), LabJson.Fingerprint(roundtrip));
        Assert.NotEqual(LabJson.Fingerprint(definitions), LabJson.Fingerprint(definitions with { SchemaVersion = 2 }));
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"unknown\":1}")]
    [InlineData("{\"schemaVersion\":1,\"profiles\":[{\"id\":\"x\",\"version\":\"1\",\"num_ctx\":512}]}")]
    public void RejectsUnknownConfigurationFields(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BenchmarkDefinitions>(json, LabJson.Options));

    [Theory]
    [InlineData(-1, 1, 1, 1)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(0, 1, 0, 1)]
    [InlineData(0, 1, 1, 0)]
    public void RejectsInvalidProfile(int warmups, int reps, int context, int timeout) =>
        Assert.Throws<ArgumentException>(() => DefinitionValidator.Validate(TestData.Definitions(new("default", "1", warmups, reps, NumCtx: context, TimeoutSeconds: timeout))));

    [Fact]
    public void RejectsBadReferencesDuplicatesAndNullEntries()
    {
        var d = TestData.Definitions();
        Assert.Throws<ArgumentException>(() => DefinitionValidator.Validate(d with { Suites = [new("s", "1", ["missing"])] }));
        Assert.Throws<ArgumentException>(() => DefinitionValidator.Validate(d with { Cases = [d.Cases[0], d.Cases[0]] }));
        Assert.Throws<ArgumentException>(() => DefinitionValidator.Validate(d with { Cases = [null!] }));
        Assert.Throws<ArgumentException>(() => DefinitionValidator.Validate(d with { SchemaVersion = 2 }));
    }

    [Theory]
    [InlineData("{}", CapabilityState.Missing)]
    [InlineData("{\"capabilities\":null}", CapabilityState.Unknown)]
    [InlineData("{\"capabilities\":[]}", CapabilityState.Unsupported)]
    [InlineData("{\"capabilities\":[\"completion\"]}", CapabilityState.Supported)]
    public void CapabilityStatesAreDistinct(string json, CapabilityState expected) =>
        Assert.Equal(expected, OllamaProvider.ResolveCapabilities(TestData.Json(json))[ModelCapability.Chat]);

    [Fact]
    public async Task ExplicitThinkingRequiresAnAdvertisedValue()
    {
        using var mock = new MockOllama();
        using var client = mock.Client();
        var model = (await new OllamaProvider(client).ListModelsAsync(default))[0];
        ModelValidation.Validate(model, new("p", "1", Thinking: TestData.Json("false")));
        Assert.Throws<ArgumentException>(() => ModelValidation.Validate(model, new("p", "1", Thinking: TestData.Json("\"high\""))));
        Assert.Throws<ArgumentException>(() => ModelValidation.Validate(model with { ThinkingValues = null }, new("p", "1", Thinking: TestData.Json("true"))));
        Assert.Throws<ArgumentException>(() => ModelValidation.Validate(model, new("p", "1", NumCtx: 9000)));
    }

    [Fact]
    public async Task RemoteModelsAndUnsupportedThinkingAreRejected()
    {
        using var mock = new MockOllama { ShowBody = "{\"capabilities\":[\"completion\"],\"remote_host\":\"https://example.test\"}" };
        using var client = mock.Client();
        var model = (await new OllamaProvider(client).ListModelsAsync(default))[0];
        Assert.True(model.IsRemote);
        Assert.Throws<ArgumentException>(() => ModelValidation.Validate(model, new("p", "1")));
        mock.ShowBody = "{\"capabilities\":[\"completion\"]}";
        var local = (await new OllamaProvider(client).ListModelsAsync(default))[0];
        Assert.Throws<ArgumentException>(() => ModelValidation.Validate(local, new("p", "1", Thinking: TestData.Json("false"))));
    }

    [Fact]
    public async Task DeclarationsAreDigestBoundAndCannotContradictProvider()
    {
        using var mock = new MockOllama { ShowBody = "{}" };
        using var client = mock.Client();
        var model = (await new OllamaProvider(client).ListModelsAsync(default))[0];
        Assert.Throws<ArgumentException>(() => ModelValidation.Validate(model, new("p", "1")));
        Assert.Throws<ArgumentException>(() => ModelValidation.ApplyDeclaration(model, new(model.Name, "wrong", new())));
        var declared = ModelValidation.ApplyDeclaration(model, new(model.Name, model.Digest, new() { [ModelCapability.Chat] = CapabilityState.Supported }));
        ModelValidation.Validate(declared, new("p", "1"));
        Assert.Throws<ArgumentException>(() => ModelValidation.ApplyDeclaration(declared, new(model.Name, model.Digest, new() { [ModelCapability.Chat] = CapabilityState.Unsupported })));
    }
}
