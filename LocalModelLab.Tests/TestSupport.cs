using System.Net;
using System.Text;
using System.Text.Json;
using LocalModelLab.Core;
using LocalModelLab.Infrastructure;

namespace LocalModelLab.Tests;

internal sealed class MockOllama : HttpMessageHandler
{
    public const string Tags = """{"models":[{"name":"small:latest","digest":"sha256:abc","size":123},{"name":"other:latest","digest":"sha256:def","size":456}]}""";
    public const string Show = """{"capabilities":["completion","thinking"],"model_info":{"test.context_length":8192},"thinking":{"values":[true,false,"low"],"default":true}}""";
    public const string Final = """{"model":"small:latest","message":{"content":"answer"},"done":true,"done_reason":"stop","total_duration":1000000000,"load_duration":200000000,"prompt_eval_duration":300000000,"eval_duration":500000000,"prompt_eval_count":10,"eval_count":20}""";
    public List<string> Requests { get; } = [];
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Chat { get; set; }
    public string ShowBody { get; set; } = Show;
    public string TagsBody { get; set; } = Tags;
    public string RuntimeBody { get; set; } = """{"models":[{"digest":"sha256:abc","context_length":4096},{"digest":"sha256:def","context_length":4096}]}""";
    public int ChatCalls { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        switch (request.RequestUri!.AbsolutePath)
        {
            case "/api/version": return Response("""{"version":"test-1"}""");
            case "/api/tags": return Response(TagsBody);
            case "/api/show": return Response(ShowBody);
            case "/api/ps": return Response(RuntimeBody);
            case "/api/chat":
                ChatCalls++;
                Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                return Chat is null ? Response(Final) : await Chat(request, cancellationToken);
            default: throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        }
    }

    public static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/x-ndjson") };

    public HttpClient Client() => new(this, disposeHandler: false) { BaseAddress = new Uri("http://ollama.test/"), Timeout = Timeout.InfiniteTimeSpan };
}

internal static class TestData
{
    public static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();
    public static BenchmarkDefinitions Definitions(ExecutionProfile? profile = null) => new(1,
        [new("case", "1.0.0", "Explain C#", "Be brief")], [new("initial", "1.0.0", ["case"])],
        [profile ?? new("default", "1.0.0", Warmups: 1, Repetitions: 2)]);
    public static AttemptResult Attempt(bool warmup = false) => new()
    {
        Id = "000001", Model = "small:latest", Digest = "sha256:abc", CaseId = "case", CaseVersion = "1.0.0",
        CaseFingerprint = "casehash", ProfileFingerprint = "profilehash", Warmup = warmup, Repetition = 1
    };
    public static async Task<RunState> Run(MockOllama mock, FileRunStore store, BenchmarkDefinitions? definitions = null,
        string[]? models = null, CancellationToken cancellationToken = default)
    {
        using var client = mock.Client();
        var defs = definitions ?? Definitions();
        return await new BenchmarkService(new OllamaProvider(client)).RunAsync(defs,
            JsonSerializer.SerializeToUtf8Bytes(defs, LabJson.Options), "initial", "default", models ?? ["small:latest"],
            "http://ollama.test/", Json("{}"), new() { ["test"] = "binaryhash" }, store, null, cancellationToken);
    }
}

internal sealed class CancelAfterChunkStream(string chunk, CancellationTokenSource cancellation) : Stream
{
    private readonly byte[] bytes = Encoding.UTF8.GetBytes(chunk);
    private bool delivered;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!delivered)
        {
            delivered = true;
            bytes.CopyTo(buffer);
            return ValueTask.FromResult(bytes.Length);
        }
        cancellation.Cancel();
        return ValueTask.FromCanceled<int>(cancellation.Token);
    }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

public sealed class TemporaryDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LocalModelLabTests", Guid.NewGuid().ToString("N"));
    public TemporaryDirectory() => Directory.CreateDirectory(Path);
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
