using System.Text;
using LocalModelLab.Core;
using LocalModelLab.Infrastructure;

namespace LocalModelLab.Tests;

public class BenchmarkServiceTests
{
    [Fact]
    public async Task MultipleModelsRunSequentiallyAndAllArtifactsAreAttributable()
    {
        using var directory = new TemporaryDirectory();
        using var mock = new MockOllama();
        var active = 0;
        mock.Chat = async (_, token) =>
        {
            Assert.Equal(1, Interlocked.Increment(ref active));
            await Task.Delay(5, token);
            Interlocked.Decrement(ref active);
            return MockOllama.Response(MockOllama.Final);
        };
        var store = new FileRunStore(directory.Path);
        var state = await TestData.Run(mock, store, models: ["small:latest", "other:latest"]);
        Assert.Equal("completed", state.Status);
        Assert.Equal(6, mock.ChatCalls);
        var attempts = Reports.ReadAttempts(store.Location);
        Assert.Equal(6, attempts.Length);
        Assert.Equal(2, attempts.Count(a => a.Warmup));
        Assert.All(attempts, a => Assert.True(a.DigestVerifiedAfter));
        Assert.Equal(["small:latest", "small:latest", "small:latest", "other:latest", "other:latest", "other:latest"], attempts.Select(a => a.Model));
        for (var i = 0; i < attempts.Length; i++)
        {
            var path = Path.Combine(store.Location, "attempts", attempts[i].Id);
            Assert.Equal(mock.Requests[i], File.ReadAllText(Path.Combine(path, "request.json")));
            Assert.Equal(MockOllama.Final, File.ReadAllText(Path.Combine(path, "response.raw")));
        }
        var manifest = Reports.Read<RunManifest>(Path.Combine(store.Location, "manifest.json"));
        Assert.Equal("test-1", manifest.ProviderVersion);
        Assert.Equal(LabJson.Hash(File.ReadAllBytes(Path.Combine(store.Location, "definitions.json"))), manifest.DefinitionFileSha256);
        Assert.All(attempts, a => Assert.Equal(manifest.ProfileFingerprint, a.ProfileFingerprint));
        Assert.Equal(4, Reports.Aggregate(attempts).Length);
        Assert.All(Reports.Aggregate(attempts), g => Assert.Equal(2, g.Attempts));
        var reports = Reports.Generate(store.Location);
        Assert.Contains("No quality scores", File.ReadAllText(Path.Combine(reports, "report.md")));
        Assert.Equal(7, File.ReadAllLines(Path.Combine(reports, "attempts.csv")).Length);
        Assert.NotEqual(reports, Reports.Generate(store.Location));
    }

    [Fact]
    public async Task AllModelsValidatedBeforeAnyInference()
    {
        using var directory = new TemporaryDirectory();
        using var mock = new MockOllama();
        await Assert.ThrowsAsync<ModelUnavailableException>(() => TestData.Run(mock, new FileRunStore(directory.Path), models: ["small:latest", "not-installed"]));
        Assert.Equal(0, mock.ChatCalls);
        mock.ShowBody = "{\"capabilities\":[\"embedding\"]}";
        await Assert.ThrowsAsync<ArgumentException>(() => TestData.Run(mock, new FileRunStore(directory.Path)));
        Assert.Equal(0, mock.ChatCalls);
    }

    [Fact]
    public async Task FailurePreservesArtifactsAndDoesNotRetry()
    {
        using var directory = new TemporaryDirectory();
        using var mock = new MockOllama { Chat = (_, _) => Task.FromResult(MockOllama.Response("{\"message\":{\"content\":\"partial\"},\"done\":false}\n")) };
        var store = new FileRunStore(directory.Path);
        var state = await TestData.Run(mock, store, TestData.Definitions(new("default", "1", Warmups: 0, Repetitions: 1)));
        Assert.Equal("completedWithErrors", state.Status);
        Assert.Equal(1, mock.ChatCalls);
        var attempt = Assert.Single(Reports.ReadAttempts(store.Location));
        Assert.Equal("failed", attempt.Status);
        Assert.Equal("partial", attempt.Answer);
        Assert.True(attempt.OutputTruncated);
        Assert.Null(attempt.Metrics.GeneratedTokens);
        Assert.Equal(0, Assert.Single(Reports.Aggregate([attempt]).Where(g => g.Scope == "model")).Completed);
    }

    [Fact]
    public async Task CancellationPreservesActiveAttemptAndStopsRemainingAttempts()
    {
        using var directory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        using var mock = new MockOllama { Chat = async (_, token) =>
        {
            cancellation.Cancel();
            await Task.Delay(10000, token);
            return MockOllama.Response(MockOllama.Final);
        }};
        var store = new FileRunStore(directory.Path);
        var state = await TestData.Run(mock, store, cancellationToken: cancellation.Token);
        Assert.Equal("interrupted", state.Status);
        Assert.Equal(1, mock.ChatCalls);
        var attempt = Assert.Single(Reports.ReadAttempts(store.Location));
        Assert.Equal("interrupted", attempt.Status);
        Assert.NotNull(attempt.FinishedUtc);
        Assert.True(File.Exists(Path.Combine(store.Location, "attempts", "000001", "request.json")));
        Assert.True(File.Exists(Path.Combine(store.Location, "attempts", "000001", "response.raw")));
        Reports.Generate(store.Location);
    }

    [Fact]
    public async Task CancellationDuringStreamingRetainsReceivedBytesAndParsedAnswer()
    {
        const string partial = "{\"message\":{\"content\":\"partial پاسخ\"},\"done\":false}\n";
        using var directory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        using var mock = new MockOllama { Chat = (_, _) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StreamContent(new CancelAfterChunkStream(partial, cancellation))
        }) };
        var store = new FileRunStore(directory.Path);
        var state = await TestData.Run(mock, store, cancellationToken: cancellation.Token);
        Assert.Equal("interrupted", state.Status);
        var result = Assert.Single(Reports.ReadAttempts(store.Location));
        Assert.Equal("partial پاسخ", result.Answer);
        Assert.Equal(partial, File.ReadAllText(Path.Combine(store.Location, "attempts", result.Id, "response.raw")));
        Assert.Equal(1, mock.ChatCalls);
        Assert.True(result.OutputTruncated);
    }

    [Fact]
    public async Task TimeoutIsRecordedWithoutRetry()
    {
        using var directory = new TemporaryDirectory();
        using var mock = new MockOllama { Chat = async (_, token) =>
        {
            await Task.Delay(10000, token);
            return MockOllama.Response(MockOllama.Final);
        }};
        var store = new FileRunStore(directory.Path);
        var state = await TestData.Run(mock, store, TestData.Definitions(new("default", "1", Warmups: 0, Repetitions: 1, TimeoutSeconds: 1)));
        Assert.Equal("completedWithErrors", state.Status);
        Assert.Equal("timedOut", Assert.Single(Reports.ReadAttempts(store.Location)).Status);
        Assert.Equal(1, mock.ChatCalls);
    }

    [Fact]
    public async Task DigestDriftInvalidatesAttributionWithoutRegeneration()
    {
        using var directory = new TemporaryDirectory();
        using var mock = new MockOllama();
        mock.Chat = (_, _) =>
        {
            mock.TagsBody = MockOllama.Tags.Replace("sha256:abc", "changed");
            return Task.FromResult(MockOllama.Response(MockOllama.Final));
        };
        var store = new FileRunStore(directory.Path);
        var state = await TestData.Run(mock, store, TestData.Definitions(new("default", "1", Warmups: 0, Repetitions: 1)));
        var result = Assert.Single(Reports.ReadAttempts(store.Location));
        Assert.Equal("completedWithErrors", state.Status);
        Assert.False(result.DigestVerifiedAfter);
        Assert.Equal("failed", result.Status);
        Assert.Equal(1, mock.ChatCalls);
        Assert.True(result.ProviderCompleted);
    }

    [Fact]
    public async Task ProviderContextClampIsAnExplicitFailure()
    {
        using var directory = new TemporaryDirectory();
        using var mock = new MockOllama { RuntimeBody = "{\"models\":[{\"digest\":\"sha256:abc\",\"context_length\":2048}]}" };
        var store = new FileRunStore(directory.Path);
        await TestData.Run(mock, store, TestData.Definitions(new("default", "1", Warmups: 0, Repetitions: 1)));
        var result = Assert.Single(Reports.ReadAttempts(store.Location));
        Assert.Equal("failed", result.Status);
        Assert.Equal(2048, result.VerifiedContextLength);
        Assert.Contains("differs from requested", result.Error);
    }

    [Fact]
    public void AtomicStoreRejectsTraversalAndNeverReusesRunDirectories()
    {
        using var directory = new TemporaryDirectory();
        var store = new FileRunStore(directory.Path);
        Assert.NotEqual(store.Location, new FileRunStore(directory.Path).Location);
        Assert.Throws<ArgumentException>(() => store.WriteBytes("../escape", []));
        store.WriteBytes("test.json", Encoding.UTF8.GetBytes("old"));
        store.WriteBytes("test.json", Encoding.UTF8.GetBytes("new"));
        Assert.Equal("new", File.ReadAllText(Path.Combine(store.Location, "test.json")));
        Assert.Empty(Directory.GetFiles(store.Location, "*.tmp"));
        using var raw = store.OpenRaw("response.raw");
        Assert.Throws<IOException>(() => store.OpenRaw("response.raw"));
    }

    [Fact]
    public void ReportExcludesWarmupsAndFailureMetricsAndPreservesUnknownValues()
    {
        var warmup = TestData.Attempt(true);
        warmup.Status = "completed";
        warmup.Metrics.ClientElapsedMs = 100000;
        var measured = TestData.Attempt();
        measured.Status = "completed";
        measured.Metrics.ClientElapsedMs = 10;
        var failed = TestData.Attempt();
        failed.Status = "failed";
        failed.Metrics.ClientElapsedMs = 1000;
        var inProgress = TestData.Attempt();
        var group = Reports.Aggregate([warmup, measured, failed, inProgress]).First();
        Assert.Equal(3, group.Attempts);
        Assert.Equal(1, group.Completed);
        Assert.Equal(1, group.Failed);
        Assert.Equal(1, group.InProgress);
        Assert.Equal(10, group.MeanClientMs);
        Assert.Null(group.MeanLoadMs);
    }
}
