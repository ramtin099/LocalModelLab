using System.Text.Json;
using LocalModelLab.Core;
using LocalModelLab.Infrastructure;

namespace LocalModelLab.Tests;

public class ReportingDiagnosticsTests
{
    [Theory]
    [InlineData("", "thinking", null, true, 1, 1, 1, 1)]
    [InlineData("  ", "thinking", null, true, 1, 1, 1, 1)]
    [InlineData(null, "thinking", null, null, 1, 1, 1, 0)]
    [InlineData("", null, null, false, 1, 0, 1, 0)]
    [InlineData("", "  ", 1.0, null, 1, 0, 0, 0)]
    [InlineData("answer", "thinking", 0.0, false, 0, 0, 0, 0)]
    [InlineData("answer", null, null, true, 0, 0, 1, 1)]
    public void DiagnosticsDescribeOutputWithoutChangingExecution(string? answer, string? reasoning,
        double? firstAnswer, bool? truncated, int empty, int reasoningOnly, int missing, int cut)
    {
        var attempt = TestData.Attempt();
        attempt.Status = "completed";
        attempt.ProviderCompleted = true;
        attempt.Answer = answer!;
        attempt.Reasoning = reasoning;
        attempt.Metrics.TimeToFirstAnswerMs = firstAnswer;
        attempt.OutputTruncated = truncated;
        var before = JsonSerializer.Serialize(attempt, LabJson.Options);
        var roundtrip = JsonSerializer.Deserialize<AttemptResult>(before, LabJson.Options)!;
        Assert.All(Reports.Aggregate([roundtrip]), group =>
        {
            Assert.Equal(1, group.Completed);
            Assert.Equal(empty, group.EmptyFinalAnswers);
            Assert.Equal(reasoningOnly, group.ReasoningOnly);
            Assert.Equal(missing, group.MissingFirstAnswer);
            Assert.Equal(cut, group.Truncated);
        });
        Assert.Equal(before, JsonSerializer.Serialize(roundtrip, LabJson.Options));
    }

    [Fact]
    public void WarmupsAndUnsuccessfulExecutionsDoNotInflateAnswerDiagnostics()
    {
        var attempts = new[] { TestData.Attempt(true), TestData.Attempt(), TestData.Attempt(), TestData.Attempt() };
        attempts[0].Status = "completed";
        attempts[1].Status = "failed";
        attempts[2].Status = "interrupted";
        foreach (var attempt in attempts)
        {
            attempt.Reasoning = "partial reasoning";
            attempt.OutputTruncated = true;
        }
        Assert.All(Reports.Aggregate(attempts), group =>
        {
            Assert.Equal(3, group.Attempts);
            Assert.Equal(0, group.EmptyFinalAnswers);
            Assert.Equal(0, group.ReasoningOnly);
            Assert.Equal(0, group.MissingFirstAnswer);
            Assert.Equal(3, group.Truncated);
        });
        Assert.Empty(Reports.Aggregate([]));
    }

    [Fact]
    public void GeneratedReportsExposeDiagnosticsAndKeepAttemptCsvCompatible()
    {
        using var directory = new TemporaryDirectory();
        var store = new FileRunStore(directory.Path);
        store.WriteJson("state.json", new RunState { Status = "completed", PlannedAttempts = 1 });
        var attempt = TestData.Attempt();
        attempt.Status = "completed";
        attempt.Reasoning = "thinking";
        attempt.OutputTruncated = true;
        store.WriteJson("attempts/000001/result.json", attempt);
        var output = Reports.Generate(store.Location);
        var markdown = File.ReadAllText(Path.Combine(output, "report.md"));
        Assert.Contains("| Truncated | Empty final | Reasoning only | Missing first answer |", markdown);
        Assert.Contains("| 1/1 | 0 | 0 | 1 | 1 | 1 | 1 |", markdown);
        Assert.Contains("not a usable or correct answer", markdown);
        var summary = File.ReadAllLines(Path.Combine(output, "summary.csv"));
        Assert.EndsWith(",emptyFinalAnswers,reasoningOnly,missingFirstAnswer", summary[0]);
        Assert.All(summary.Skip(1), row => Assert.EndsWith(",\"1\",\"1\",\"1\"", row));
        Assert.DoesNotContain("emptyFinalAnswers", File.ReadAllLines(Path.Combine(output, "attempts.csv"))[0]);
    }
}
