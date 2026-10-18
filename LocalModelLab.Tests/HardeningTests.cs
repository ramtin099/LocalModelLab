using System.Net;
using System.Text;
using LocalModelLab.Core;
using LocalModelLab.Infrastructure;

namespace LocalModelLab.Tests;

public class HardeningTests
{
    [Fact]
    public void LegacyProfilesKeepTheirPoliciesAndUnknownPoliciesAreRejected()
    {
        var profile = System.Text.Json.JsonSerializer.Deserialize<ExecutionProfile>("{\"id\":\"default\",\"version\":\"1\"}", LabJson.Options)!;
        Assert.Equal(SchedulingPolicy.ModelFirst, profile.Scheduling);
        Assert.Equal(ResidencyPolicy.ProviderManaged, profile.Residency);
        Assert.Throws<ArgumentException>(() => DefinitionValidator.Validate(TestData.Definitions(profile with { Scheduling = (SchedulingPolicy)99 })));
        Assert.Throws<ArgumentException>(() => DefinitionValidator.Validate(TestData.Definitions(profile with { Residency = (ResidencyPolicy)99 })));
    }

    [Fact]
    public async Task SingleModelDoesNotSendInferenceWhenUnloadFails()
    {
        using var directory = new TemporaryDirectory();
        using var mock = new MockOllama { RuntimeBody = "{\"models\":[{\"name\":\"other\",\"digest\":\"other\"}]}" };
        mock.Override = (request, _) => Task.FromResult<HttpResponseMessage?>(request.RequestUri!.AbsolutePath == "/api/generate"
            ? MockOllama.Response("failed", HttpStatusCode.InternalServerError) : null);
        var store = new FileRunStore(directory.Path);
        await TestData.Run(mock, store, TestData.Definitions(new("default", "2", Warmups: 0, Repetitions: 1, Residency: ResidencyPolicy.SingleModel)));
        var result = Assert.Single(Reports.ReadAttempts(store.Location));
        Assert.Equal("failed", result.Status);
        Assert.False(result.ResidencyVerified);
        Assert.Equal(["other"], result.UnloadRequests);
        Assert.Equal(0, mock.ChatCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingRuntimeObservationIsExplicitAndStrictPolicyFails(bool strict)
    {
        using var directory = new TemporaryDirectory();
        using var mock = new MockOllama();
        mock.Override = (request, _) => Task.FromResult<HttpResponseMessage?>(request.RequestUri!.AbsolutePath == "/api/ps"
            ? MockOllama.Response("unavailable", HttpStatusCode.NotFound) : null);
        var store = new FileRunStore(directory.Path);
        await TestData.Run(mock, store, TestData.Definitions(new("default", "2", Warmups: 0, Repetitions: 1,
            Residency: strict ? ResidencyPolicy.SingleModel : ResidencyPolicy.ProviderManaged)));
        var result = Assert.Single(Reports.ReadAttempts(store.Location));
        Assert.Equal(strict ? "failed" : "completed", result.Status);
        Assert.Null(result.ProviderRuntime);
        Assert.Null(result.VerifiedContextLength);
        Assert.Null(result.RuntimeSampleUtc);
        if (!strict) Assert.NotNull(result.RuntimeMetadataError);
    }

    [Fact]
    public async Task MalformedResidencyObservationFinalizesAttemptAsFailed()
    {
        using var directory = new TemporaryDirectory();
        using var mock = new MockOllama { RuntimeBody = "{}" };
        var store = new FileRunStore(directory.Path);
        await TestData.Run(mock, store, TestData.Definitions(new("default", "2", Warmups: 0, Repetitions: 1, Residency: ResidencyPolicy.SingleModel)));
        var result = Assert.Single(Reports.ReadAttempts(store.Location));
        Assert.Equal("failed", result.Status);
        Assert.NotNull(result.FinishedUtc);
        Assert.Equal(0, mock.ChatCalls);
    }

    [Fact]
    public async Task RotatingScheduleIsDeterministicRecordedAndBalanced()
    {
        using var directory = new TemporaryDirectory();
        using var mock = new MockOllama();
        var profile = new ExecutionProfile("default", "2", Warmups: 1, Repetitions: 4, Scheduling: SchedulingPolicy.Rotating);
        var store = new FileRunStore(directory.Path);
        await TestData.Run(mock, store, TestData.Definitions(profile), ["small:latest", "other:latest"]);
        var plan = Reports.Read<BenchmarkService.ScheduledAttempt[]>(Path.Combine(store.Location, "schedule.json"));
        var attempts = Reports.ReadAttempts(store.Location);
        Assert.Equal(plan.Select(p => (p.Id, p.Model, p.CaseId, p.Warmup, p.Repetition)),
            attempts.Select(p => (p.Id, p.Model, p.CaseId, p.Warmup, p.Repetition)));
        Assert.Equal(["small:latest", "other:latest", "other:latest", "small:latest", "small:latest", "other:latest", "other:latest", "small:latest"],
            plan.Where(p => !p.Warmup).Select(p => p.Model));
        var manifest = Reports.Read<RunManifest>(Path.Combine(store.Location, "manifest.json"));
        Assert.Equal(plan, BenchmarkService.CreateSchedule(manifest.Models, manifest.Cases, profile));
        var four = new[] { manifest.Models[0], manifest.Models[1], manifest.Models[0] with { Name = "third" }, manifest.Models[0] with { Name = "fourth" } };
        var expanded = BenchmarkService.CreateSchedule(four, [manifest.Cases[0], manifest.Cases[0] with { Id = "second" }], profile);
        foreach (var item in expanded.Where(p => !p.Warmup).GroupBy(p => p.CaseId))
        for (var slot = 0; slot < 4; slot++)
            Assert.Equal(4, item.Where((_, i) => i % 4 == slot).Select(p => p.Model).Distinct().Count());
        Assert.All(expanded.Take(8), p => Assert.True(p.Warmup));
    }

    [Theory]
    [InlineData("missing", "unavailableModel")]
    [InlineData("invalid", "invalidConfiguration")]
    [InlineData("provider", "providerFailure")]
    [InlineData("providerTimeout", "providerFailure")]
    [InlineData("discoveryMalformed", "providerFailure")]
    [InlineData("unsupported", "invalidConfiguration")]
    [InlineData("cancelDuringDiscovery", "cancelled")]
    [InlineData("cancel", "cancelled")]
    public async Task PreflightAlwaysLeavesStructuredEvidence(string failure, string expected)
    {
        using var directory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        using var mock = new MockOllama();
        if (failure == "cancel") cancellation.Cancel();
        if (failure == "provider") mock.Override = (_, _) => Task.FromResult<HttpResponseMessage?>(MockOllama.Response("down", HttpStatusCode.ServiceUnavailable));
        if (failure == "providerTimeout") mock.Override = (_, _) => throw new OperationCanceledException("provider timeout");
        if (failure == "discoveryMalformed") mock.TagsBody = "{}";
        if (failure == "unsupported") mock.ShowBody = "{\"capabilities\":[\"embedding\"]}";
        if (failure == "cancelDuringDiscovery") mock.Override = (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/show") cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult<HttpResponseMessage?>(null);
        };
        var store = new FileRunStore(directory.Path);
        var defs = TestData.Definitions(failure == "invalid" ? new("default", "1", Repetitions: 0) : null);
        try { await TestData.Run(mock, store, defs, failure == "missing" ? ["missing"] : null, cancellation.Token); }
        catch (Exception) when (failure != "cancel") { }
        var state = Reports.Read<RunState>(Path.Combine(store.Location, "state.json"));
        Assert.Equal(expected, state.FailureKind);
        Assert.Equal(expected == "cancelled" ? "interrupted" : "failed", state.Status);
        Assert.Equal("preflight", state.Phase);
        Assert.NotNull(state.FinishedUtc);
        Assert.True(File.Exists(Path.Combine(store.Location, "definitions.json")));
        Assert.True(File.Exists(Path.Combine(store.Location, "selection.json")));
        Assert.Empty(Reports.ReadAttempts(store.Location));
        Assert.Equal(0, mock.ChatCalls);
        Assert.Contains(expected, File.ReadAllText(Path.Combine(Reports.Generate(store.Location), "report.md")));
    }

    [Fact]
    public async Task SingleModelUnloadsOthersAndRetainsLoadAndSnapshots()
    {
        using var directory = new TemporaryDirectory();
        using var mock = new MockOllama();
        var unloads = new List<string>();
        mock.Override = async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/generate") return null;
            var body = TestData.Json(await request.Content!.ReadAsStringAsync(token));
            unloads.Add(body.GetProperty("model").GetString()!);
            Assert.Equal(0, body.GetProperty("keep_alive").GetInt32());
            mock.RuntimeBody = """{"models":[{"name":"small:latest","digest":"sha256:abc","context_length":4096,"size_vram":123}]}""";
            return MockOllama.Response("{\"done\":true}");
        };
        mock.RuntimeBody = """{"models":[{"name":"unrelated:latest","digest":"other"}]}""";
        var store = new FileRunStore(directory.Path);
        await TestData.Run(mock, store, TestData.Definitions(new("default", "2", Warmups: 0, Repetitions: 1, Residency: ResidencyPolicy.SingleModel)));
        var result = Assert.Single(Reports.ReadAttempts(store.Location));
        Assert.Equal("completed", result.Status);
        Assert.Equal(["unrelated:latest"], unloads);
        Assert.Equal(unloads, result.UnloadRequests);
        Assert.NotNull(result.ResidencyBefore);
        Assert.NotNull(result.ResidencyAfter);
        Assert.NotNull(result.RuntimeSampleUtc);
        Assert.True(result.ResidencyVerified);
        Assert.Equal(200000000, result.Metrics.LoadNs);
        Assert.Equal(123, result.ProviderRuntime!.Value.GetProperty("models")[0].GetProperty("size_vram").GetInt32());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SingleModelFailsClosedWhenPsUnavailableOrContaminated(bool afterInference)
    {
        using var directory = new TemporaryDirectory();
        using var mock = new MockOllama { RuntimeBody = "{\"models\":[]}" };
        mock.Override = (request, _) => Task.FromResult<HttpResponseMessage?>(request.RequestUri!.AbsolutePath == "/api/ps" && !afterInference
            ? MockOllama.Response("unavailable", HttpStatusCode.NotFound) : null);
        mock.Chat = (_, _) =>
        {
            mock.RuntimeBody = MockOllama.Tags;
            return Task.FromResult(MockOllama.Response(MockOllama.Final));
        };
        var store = new FileRunStore(directory.Path);
        await TestData.Run(mock, store, TestData.Definitions(new("default", "2", Warmups: 0, Repetitions: 1, Residency: ResidencyPolicy.SingleModel)));
        var result = Assert.Single(Reports.ReadAttempts(store.Location));
        Assert.Equal("failed", result.Status);
        Assert.False(result.ResidencyVerified);
        Assert.Equal(afterInference ? 1 : 0, mock.ChatCalls);
        if (afterInference) Assert.Equal("answer", result.Answer);
        else Assert.Null(result.Metrics.ClientElapsedMs);
    }

    [Fact]
    public async Task ManyChunksPreserveBothBuffersAndIncompleteRawTailOnCancellation()
    {
        var chunk = "{\"message\":{\"content\":\"a\",\"thinking\":\"b\"},\"done\":false}\n";
        var body = string.Concat(Enumerable.Repeat(chunk, 20)) + "{\"message\":";
        using var cancellation = new CancellationTokenSource();
        using var mock = new MockOllama { Chat = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StreamContent(new CancelAfterChunkStream(body, cancellation)) }) };
        using var client = mock.Client();
        using var raw = new MemoryStream();
        var result = TestData.Attempt();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new OllamaProvider(client).InferAsync("{}", true, raw, result, cancellation.Token));
        Assert.Equal(new string('a', 20), result.Answer);
        Assert.Equal(new string('b', 20), result.Reasoning);
        Assert.Equal(Encoding.UTF8.GetBytes(body), raw.ToArray());
    }
}
