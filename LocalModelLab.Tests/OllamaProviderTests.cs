using System.Net;
using System.Text;
using System.Text.Json;
using LocalModelLab.Core;
using LocalModelLab.Infrastructure;

namespace LocalModelLab.Tests;

public class OllamaProviderTests
{
    [Fact]
    public async Task DiscoversDigestsMetadataCapabilitiesAndVersion()
    {
        using var mock = new MockOllama();
        using var client = mock.Client();
        var provider = new OllamaProvider(client);
        Assert.Equal("test-1", await provider.GetVersionAsync(default));
        var models = await provider.ListModelsAsync(default);
        Assert.Equal(2, models.Count);
        Assert.Equal("sha256:abc", models[0].Digest);
        Assert.Equal(123, models[0].SizeBytes);
        Assert.Equal(CapabilityState.Supported, models[0].Capabilities[ModelCapability.Chat]);
        Assert.Equal(CapabilityState.Unsupported, models[0].Capabilities[ModelCapability.Embedding]);
        Assert.Equal(3, models[0].ThinkingValues!.Length);
        await provider.VerifyDigestAsync(models[0], default);
        mock.TagsBody = MockOllama.Tags.Replace("sha256:abc", "changed");
        await Assert.ThrowsAsync<ModelDigestChangedException>(() => provider.VerifyDigestAsync(models[0], default));
    }

    [Fact]
    public async Task SendsExactOptionsAndFreshMessages()
    {
        using var mock = new MockOllama();
        using var client = mock.Client();
        var provider = new OllamaProvider(client);
        var model = (await provider.ListModelsAsync(default))[0];
        var profile = new ExecutionProfile("p", "1", Temperature: 0.2, Seed: 99, NumCtx: 1024, NumPredict: 77, Thinking: TestData.Json("false"));
        var request = provider.BuildRequest(model, new("c", "1", "question", "instruction"), profile);
        var json = TestData.Json(request);
        Assert.Equal(2, json.GetProperty("messages").GetArrayLength());
        Assert.Equal("question", json.GetProperty("messages")[1].GetProperty("content").GetString());
        Assert.False(json.GetProperty("think").GetBoolean());
        Assert.Equal(99, json.GetProperty("options").GetProperty("seed").GetInt32());
        Assert.Equal(77, json.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.Equal(1024, json.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.Equal(0.2, json.GetProperty("options").GetProperty("temperature").GetDouble());
        using var raw = new MemoryStream();
        await provider.InferAsync(request, true, raw, TestData.Attempt(), default);
        Assert.Equal(request, Assert.Single(mock.Requests));
        Assert.Equal(request, provider.BuildRequest(model, new("c", "1", "question", "instruction"), profile));
    }

    [Fact]
    public async Task SeparatesReasoningAndAnswerAndCapturesExactUtf8Body()
    {
        var body = "{\"message\":{\"thinking\":\"فکر\"},\"done\":false}\r\n" +
            "{\"message\":{\"content\":\"پاسخ\"},\"done\":false}\n" + MockOllama.Final.Replace("answer", "!") + "\n";
        using var mock = new MockOllama { Chat = (_, _) => Task.FromResult(MockOllama.Response(body)) };
        using var client = mock.Client();
        using var raw = new MemoryStream();
        var result = TestData.Attempt();
        await new OllamaProvider(client).InferAsync("{}", true, raw, result, default);
        Assert.Equal(Encoding.UTF8.GetBytes(body), raw.ToArray());
        Assert.Equal("فکر", result.Reasoning);
        Assert.Equal("پاسخ!", result.Answer);
        Assert.True(result.ProviderCompleted);
        Assert.Equal("stop", result.CompletionReason);
        Assert.False(result.OutputTruncated);
        Assert.Equal(800, result.Metrics.ProviderExcludingLoadMs);
        Assert.Equal(40, result.Metrics.GenerationTokensPerSecond);
        Assert.NotNull(result.Metrics.TimeToFirstReasoningMs);
        Assert.NotNull(result.Metrics.TimeToFirstAnswerMs);
        Assert.True(result.Metrics.TimeToFirstReasoningMs <= result.Metrics.TimeToFirstAnswerMs);
        Assert.Null(result.Metrics.CachedPromptTokens);
    }

    [Theory]
    [InlineData("length", true)]
    [InlineData("stop", false)]
    [InlineData("other", null)]
    public async Task NonStreamingDoesNotInventFirstTokenTime(string reason, bool? truncated)
    {
        using var mock = new MockOllama { Chat = (_, _) => Task.FromResult(MockOllama.Response(MockOllama.Final.Replace("\"stop\"", $"\"{reason}\""))) };
        using var client = mock.Client();
        using var raw = new MemoryStream();
        var result = TestData.Attempt();
        await new OllamaProvider(client).InferAsync("{}", false, raw, result, default);
        Assert.Null(result.Metrics.TimeToFirstOutputMs);
        Assert.Null(result.Metrics.TimeToFirstAnswerMs);
        Assert.Null(result.Reasoning);
        Assert.Equal(truncated, result.OutputTruncated);
    }

    [Theory]
    [InlineData("{\"message\":{\"content\":\"partial\"},\"done\":false}\n", "partial")]
    [InlineData("{\"error\":\"out of memory\"}\n", "")]
    [InlineData("{\"message\":{\"tool_calls\":[{}]},\"done\":true}\n", "")]
    public async Task IncompleteStreamsAndErrorsAreNotRetried(string body, string answer)
    {
        using var mock = new MockOllama { Chat = (_, _) => Task.FromResult(MockOllama.Response(body)) };
        using var client = mock.Client();
        using var raw = new MemoryStream();
        var result = TestData.Attempt();
        await Assert.ThrowsAsync<IOException>(() => new OllamaProvider(client).InferAsync("{}", true, raw, result, default));
        Assert.Equal(answer, result.Answer);
        Assert.Equal(body, Encoding.UTF8.GetString(raw.ToArray()));
        Assert.Equal(1, mock.ChatCalls);
        Assert.NotNull(result.Metrics.ClientElapsedMs);
    }

    [Fact]
    public async Task MissingProviderMetricsRemainNullAndZeroDurationHasNoRate()
    {
        using var mock = new MockOllama { Chat = (_, _) => Task.FromResult(MockOllama.Response("{\"done\":true,\"eval_count\":0,\"eval_duration\":0}")) };
        using var client = mock.Client();
        using var raw = new MemoryStream();
        var result = TestData.Attempt();
        await new OllamaProvider(client).InferAsync("{}", true, raw, result, default);
        Assert.Null(result.CompletionReason);
        Assert.Null(result.OutputTruncated);
        Assert.Null(result.Metrics.LoadNs);
        Assert.Null(result.Metrics.ProviderExcludingLoadMs);
        Assert.Null(result.Metrics.GenerationTokensPerSecond);
    }

    [Fact]
    public async Task HttpErrorBodyIsPreserved()
    {
        const string body = "{\"error\":\"unsupported option\"}";
        using var mock = new MockOllama { Chat = (_, _) => Task.FromResult(MockOllama.Response(body, HttpStatusCode.BadRequest)) };
        using var client = mock.Client();
        using var raw = new MemoryStream();
        var result = TestData.Attempt();
        await Assert.ThrowsAsync<HttpRequestException>(() => new OllamaProvider(client).InferAsync("{}", true, raw, result, default));
        Assert.Equal(400, result.HttpStatus);
        Assert.Equal(body, Encoding.UTF8.GetString(raw.ToArray()));
    }

    [Fact]
    public async Task MalformedBodyIsRetained()
    {
        using var mock = new MockOllama { Chat = (_, _) => Task.FromResult(MockOllama.Response("not-json\n")) };
        using var client = mock.Client();
        using var raw = new MemoryStream();
        await Assert.ThrowsAnyAsync<JsonException>(() => new OllamaProvider(client).InferAsync("{}", true, raw, TestData.Attempt(), default));
        Assert.Equal("not-json\n", Encoding.UTF8.GetString(raw.ToArray()));
    }
}
