using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LocalModelLab.Core;
using LocalModelLab.Infrastructure;

namespace LocalModelLab.Tests;

public class EvaluationContractTests
{
    private static readonly string PackageRoot = Path.Combine(AppContext.BaseDirectory, "evaluations", "string-counting", "1.0.0");
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Evaluation", "v1");

    [Fact]
    public void VersionOnePackageAndSourceBindingAreIntact()
    {
        var definition = Definition();
        EvaluationValidator.Validate(definition);
        var item = Assert.Single(definition.Cases);
        var verifier = Assert.Single(definition.Verifiers);
        foreach (var artifact in verifier.Artifacts.Prepend(item.Contract))
        {
            var actual = LabJson.Hash(File.ReadAllBytes(Path.Combine(PackageRoot, artifact.Path)));
            Assert.True(artifact.Sha256 == actual, $"{artifact.Path}: recorded SHA-256 {artifact.Sha256}; actual {actual}.");
        }

        var benchmark = Read<BenchmarkDefinitions>(Path.Combine(AppContext.BaseDirectory, "benchmarks", "comparison.non-thinking.v1.json"));
        var source = Assert.Single(benchmark.Cases, c => c.Id == item.SourceCaseId);
        Assert.Equal(source.Version, item.SourceCaseVersion);
        Assert.Equal(LabJson.Fingerprint(source), item.SourceCaseFingerprint);
        var manifest = Manifest();
        Assert.Equal(LabJson.Hash(File.ReadAllBytes(Path.Combine(PackageRoot, "definition.json"))), manifest.Definition.Sha256);
        EvaluationValidator.Validate(manifest, definition);
    }

    [Fact]
    public void VersionOneDefinitionHasStableWireShape()
    {
        var json = File.ReadAllText(Path.Combine(PackageRoot, "definition.json"));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonSerializer.SerializeToNode(Definition(), LabJson.Options)));
        Assert.Equal(LabJson.Fingerprint(Definition()), LabJson.Fingerprint(Roundtrip(Definition())));
    }

    [Theory]
    [InlineData("candidate-failure.json", CheckOutcome.Fail, "compilationRejected")]
    [InlineData("worker-error.json", CheckOutcome.Error, "environmentUnavailable")]
    public void StaticWorkerResultsKeepCandidateFailureSeparateFromEvaluatorError(string name, CheckOutcome outcome, string reason)
    {
        var json = File.ReadAllText(Path.Combine(FixtureRoot, name));
        var result = Read<EvaluationResult>(Path.Combine(FixtureRoot, name));
        EvaluationValidator.Validate(result, Manifest(), Definition());
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(reason, Assert.Single(result.Checks, c => c.Id == "compilation").ReasonCode);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonSerializer.SerializeToNode(result, LabJson.Options)));
        Assert.Equal(LabJson.Fingerprint(result), LabJson.Fingerprint(Roundtrip(result)));
        Assert.Equal("manifest.json", result.Manifest.Path);
        Assert.Equal(LabJson.Hash(File.ReadAllBytes(Path.Combine(FixtureRoot, result.Manifest.Path))), result.Manifest.Sha256);
    }

    [Fact]
    public void PackageAndGoldenFilesUseExactUtf8LfBytes()
    {
        foreach (var path in Directory.GetFiles(PackageRoot).Concat(Directory.GetFiles(FixtureRoot, "*.json")))
        {
            var bytes = File.ReadAllBytes(path);
            Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }), $"{path}: UTF-8 BOM changes artifact hashes.");
            Assert.DoesNotContain((byte)'\r', bytes);
            Assert.NotEmpty(bytes);
            Assert.Equal((byte)'\n', bytes[^1]);
            Assert.Equal(bytes, new UTF8Encoding(false, true).GetBytes(new UTF8Encoding(false, true).GetString(bytes)));
        }
    }

    [Fact]
    public void HashesDoNotHideByteChangesOrNormalizeLineEndings()
    {
        var bytes = File.ReadAllBytes(Path.Combine(PackageRoot, "observations.json"));
        var crlf = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\n", "\r\n", StringComparison.Ordinal));
        Assert.NotEqual(LabJson.Hash(bytes), LabJson.Hash(crlf));
        Assert.NotEqual(LabJson.Hash(bytes), LabJson.Hash(new byte[] { 0xef, 0xbb, 0xbf }.Concat(bytes).ToArray()));
        var changed = bytes.ToArray();
        changed[0] ^= 1;
        Assert.NotEqual(LabJson.Hash(bytes), LabJson.Hash(changed));
    }

    [Fact]
    public void ManifestHasStableWireShape()
    {
        var json = File.ReadAllText(Path.Combine(FixtureRoot, "manifest.json"));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonSerializer.SerializeToNode(Manifest(), LabJson.Options)));
    }

    [Fact]
    public async Task VerifierOnlyArtifactsNeverEnterTheModelRequest()
    {
        var definition = Definition();
        var benchmark = Read<BenchmarkDefinitions>(Path.Combine(AppContext.BaseDirectory, "benchmarks", "comparison.non-thinking.v1.json"));
        var source = benchmark.Cases.Single(c => c.Id == definition.Cases[0].SourceCaseId);
        using var mock = new MockOllama();
        using var client = mock.Client();
        var provider = new OllamaProvider(client);
        var model = (await provider.ListModelsAsync(default))[0];
        using var request = JsonDocument.Parse(provider.BuildRequest(model, source, benchmark.Profiles[0]));
        var message = Assert.Single(request.RootElement.GetProperty("messages").EnumerateArray());
        Assert.Equal("user", message.GetProperty("role").GetString());
        Assert.Equal(source.Prompt, message.GetProperty("content").GetString());
        Assert.Empty(mock.Requests);
    }

    [Fact]
    public void AllOutcomeAndLifecycleNamesAreStable()
    {
        Assert.Equal(new[] { "\"notRun\"", "\"pass\"", "\"fail\"", "\"error\"" },
            Enum.GetValues<CheckOutcome>().Select(v => JsonSerializer.Serialize(v, LabJson.Options)));
        Assert.Equal(new[] { "\"planned\"", "\"running\"", "\"finalized\"", "\"cancelled\"", "\"interrupted\"" },
            Enum.GetValues<EvaluationLifecycle>().Select(v => JsonSerializer.Serialize(v, LabJson.Options)));
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("lifecycle")]
    [InlineData("outcome")]
    [InlineData("checks")]
    [InlineData("manifest")]
    public void MissingResultPropertiesCannotBecomeImplicitSuccess(string property)
    {
        var node = JsonSerializer.SerializeToNode(Result(), LabJson.Options)!.AsObject();
        node.Remove(property);
        Assert.Throws<JsonException>(() => node.Deserialize<EvaluationResult>(LabJson.Options));
    }

    [Theory]
    [InlineData("outcome", "\"unknown\"")]
    [InlineData("outcome", "1")]
    [InlineData("lifecycle", "\"completed\"")]
    [InlineData("lifecycle", "0")]
    [InlineData("extraField", "true")]
    public void UnknownWireValuesAreRejected(string field, string value)
    {
        var node = JsonSerializer.SerializeToNode(Result(), LabJson.Options)!.AsObject();
        node[field] = JsonNode.Parse(value);
        Assert.Throws<JsonException>(() => node.Deserialize<EvaluationResult>(LabJson.Options));
    }

    [Theory]
    [InlineData(CheckOutcome.Pass, CheckOutcome.Pass)]
    [InlineData(CheckOutcome.Fail, CheckOutcome.Fail)]
    [InlineData(CheckOutcome.Error, CheckOutcome.Error)]
    [InlineData(CheckOutcome.NotRun, CheckOutcome.NotRun)]
    public void FinalizedOutcomeRequiresConsistentChecks(CheckOutcome checkOutcome, CheckOutcome overall)
    {
        var result = Result();
        result = result with
        {
            Outcome = overall,
            Checks = result.Checks.Select(c => c with
            {
                Outcome = checkOutcome,
                Evidence = checkOutcome == CheckOutcome.NotRun ? [] : [Evidence()]
            }).ToImmutableArray()
        };
        EvaluationValidator.Validate(result, Manifest(), Definition());
        Assert.Throws<ArgumentException>(() => EvaluationValidator.Validate(result with
        {
            Outcome = overall == CheckOutcome.Pass ? CheckOutcome.Fail : CheckOutcome.Pass
        }, Manifest(), Definition()));
    }

    [Fact]
    public void ProvenFailureSurvivesAnIndependentInfrastructureError()
    {
        var result = Result();
        var checks = result.Checks.SetItem(0, result.Checks[0] with { Outcome = CheckOutcome.Error, Evidence = [Evidence()] });
        EvaluationValidator.Validate(result with { Checks = checks }, Manifest(), Definition());
        Assert.Equal(CheckOutcome.Error, checks[0].Outcome);
        Assert.Equal(CheckOutcome.Fail, checks[1].Outcome);
    }

    [Theory]
    [InlineData(EvaluationLifecycle.Planned)]
    [InlineData(EvaluationLifecycle.Running)]
    [InlineData(EvaluationLifecycle.Cancelled)]
    [InlineData(EvaluationLifecycle.Interrupted)]
    public void UnfinishedLifecycleCannotClaimCorrectness(EvaluationLifecycle lifecycle)
    {
        var result = Result() with { Lifecycle = lifecycle, Outcome = CheckOutcome.NotRun };
        EvaluationValidator.Validate(result, Manifest(), Definition());
        Assert.Throws<ArgumentException>(() => EvaluationValidator.Validate(result with { Outcome = CheckOutcome.Pass }, Manifest(), Definition()));
    }

    [Fact]
    public void MissingDuplicateUnknownOrUnevidencedChecksAreRejected()
    {
        var result = Result();
        var invalid = new[]
        {
            result with { Checks = [] },
            result with { Checks = default },
            result with { Checks = result.Checks.RemoveAt(0) },
            result with { Checks = result.Checks.Add(result.Checks[0]) },
            result with { Checks = result.Checks.SetItem(0, result.Checks[0] with { Id = "unknown" }) },
            result with { Checks = result.Checks.SetItem(1, result.Checks[1] with { Evidence = [] }) },
            result with { Checks = result.Checks.SetItem(1, result.Checks[1] with { ReasonCode = "" }) },
            result with { Checks = result.Checks.SetItem(1, result.Checks[1] with { Outcome = (CheckOutcome)99 }) },
            result with { EvaluationId = "other" },
            result with { SchemaVersion = 2 },
            result with { Lifecycle = (EvaluationLifecycle)99 }
        };
        foreach (var item in invalid)
            Assert.ThrowsAny<ArgumentException>(() => EvaluationValidator.Validate(item, Manifest(), Definition()));
    }

    [Fact]
    public void InvalidDefinitionsAndBindingsAreRejected()
    {
        var definition = Definition();
        var item = definition.Cases[0];
        var verifier = definition.Verifiers[0];
        foreach (var invalid in new[]
        {
            definition with { SchemaVersion = 2 },
            definition with { Version = "1" },
            definition with { Version = "01.0.0" },
            definition with { Cases = [] },
            definition with { Cases = [null!] },
            definition with { Cases = [item, item] },
            definition with { Verifiers = [] },
            definition with { Verifiers = [verifier with { RequiredChecks = ["x", "x"] }] },
            definition with { Verifiers = [verifier with { Artifacts = [] }] },
            definition with { Verifiers = [verifier with { Artifacts = [verifier.Artifacts[0], verifier.Artifacts[0] with { Path = verifier.Artifacts[0].Path.ToUpperInvariant() }] }] },
            definition with { Cases = [item with { Verifier = item.Verifier with { Version = "2.0.0" } }] },
            definition with { Cases = [item with { Contract = verifier.Artifacts[0] with { Sha256 = new string('1', 64) } }] }
        })
            Assert.ThrowsAny<ArgumentException>(() => EvaluationValidator.Validate(invalid));

        var manifest = Manifest();
        foreach (var invalid in new[]
        {
            manifest with { SchemaVersion = 2 },
            manifest with { Source = manifest.Source with { CaseFingerprint = new string('1', 64) } },
            manifest with { Source = manifest.Source with { Repetition = 0 } },
            manifest with { Verifier = manifest.Verifier with { Version = "2.0.0" } },
            manifest with { Case = manifest.Case with { Id = "unknown" } },
            manifest with { CreatedUtc = default },
            manifest with { Provenance = default }
        })
            Assert.ThrowsAny<ArgumentException>(() => EvaluationValidator.Validate(invalid, definition));
    }

    [Fact]
    public void ReferencesWithinOneRootCannotContradictHashesOrPathCasing()
    {
        var definition = Definition();
        var item = definition.Cases[0];
        var verifier = definition.Verifiers[0];
        Assert.Throws<ArgumentException>(() => EvaluationValidator.Validate(definition with
        {
            Verifiers = [verifier with { Artifacts = verifier.Artifacts.Add(item.Contract with { Path = item.Contract.Path.ToUpperInvariant() }) }]
        }));
        var manifest = Manifest();
        Assert.Throws<ArgumentException>(() => EvaluationValidator.Validate(manifest with
        {
            Provenance = [manifest.Definition with { Sha256 = new string('1', 64) }]
        }, definition));
        var result = Result();
        foreach (var conflicting in new[] { result.Manifest with { Sha256 = new string('1', 64) }, result.Manifest with { Path = "MANIFEST.JSON" } })
            Assert.Throws<ArgumentException>(() => EvaluationValidator.Validate(result with
            {
                Checks = result.Checks.SetItem(1, result.Checks[1] with { Evidence = [conflicting] })
            }, manifest, definition));
        var check = result.Checks[1];
        Assert.Throws<ArgumentException>(() => EvaluationValidator.Validate(result with
        {
            Checks = result.Checks.SetItem(0, check with
            {
                Id = "submission", Evidence = [check.Evidence[0] with { Sha256 = new string('1', 64) }]
            })
        }, manifest, definition));
        EvaluationValidator.Validate(result with
        {
            Checks = result.Checks.SetItem(0, check with { Id = "submission" })
        }, manifest, definition);
    }

    [Theory]
    [InlineData("../result.json")]
    [InlineData("/result.json")]
    [InlineData("C:/result.json")]
    [InlineData("attempts\\result.json")]
    [InlineData("attempts/./result.json")]
    [InlineData("attempts//result.json")]
    [InlineData("attempts/result.json.")]
    [InlineData("attempts/*.json")]
    [InlineData("attempts/result.json:stream")]
    [InlineData("NUL")]
    [InlineData("evidence/con.json")]
    [InlineData("evidence/COM1.txt")]
    [InlineData("evidence/lpt9/log.json")]
    [InlineData("evidence/COM\u00b9.txt")]
    [InlineData("CONOUT$")]
    public void UnsafeArtifactReferencesAreRejected(string path)
    {
        var result = Result() with { Manifest = Evidence() with { Path = path } };
        Assert.Throws<ArgumentException>(() => EvaluationValidator.Validate(result, Manifest(), Definition()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void InvalidArtifactHashesAreRejected(string hash)
    {
        var result = Result() with { Manifest = Evidence() with { Sha256 = hash } };
        Assert.Throws<ArgumentException>(() => EvaluationValidator.Validate(result, Manifest(), Definition()));
    }

    [Fact]
    public void EvaluationHasOnlyValueLinksToInferenceAndCannotMutateItsSnapshot()
    {
        var source = TestData.Attempt();
        source.Status = "completed";
        source.Answer = "trusted static answer, not C#";
        var before = JsonSerializer.SerializeToUtf8Bytes(source, LabJson.Options);
        var manifest = Manifest() with { Source = Manifest().Source with
        {
            Result = new() { Path = "attempts/000001/result.json", Sha256 = LabJson.Hash(before) }
        } };
        EvaluationValidator.Validate(Result() with { Outcome = CheckOutcome.Fail }, manifest, Definition());
        _ = JsonSerializer.SerializeToUtf8Bytes(manifest, LabJson.Options);
        Assert.Equal(before, JsonSerializer.SerializeToUtf8Bytes(source, LabJson.Options));
        Assert.Equal(LabJson.Hash(before), manifest.Source.Result.Sha256);
        Assert.DoesNotContain(typeof(SourceAttemptReference).GetProperties(), p => p.PropertyType == typeof(AttemptResult) || p.PropertyType == typeof(IRunStore));
    }

    [Fact]
    public void StaticGroundTruthCoversRequiredBehaviorsWithoutStrengtheningAmbiguities()
    {
        using var observations = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(PackageRoot, "observations.json")));
        using var fixtures = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(PackageRoot, "incorrect-fixtures.json")));
        Assert.Equal("pendingIndependentReview", observations.RootElement.GetProperty("reviewStatus").GetString());
        Assert.Equal("specificationOnly", fixtures.RootElement.GetProperty("execution").GetString());
        var probes = observations.RootElement.GetProperty("observations").EnumerateArray().ToDictionary(o => o.GetProperty("id").GetString()!);
        Assert.Equal("unspecified", probes["null-input"].GetProperty("expected").GetProperty("type").GetString());
        Assert.False(probes["lookup-comparer-diagnostic"].GetProperty("scored").GetBoolean());
        foreach (var required in Definition().Verifiers[0].RequiredChecks.Except(["submission", "compilation"]))
        {
            var probe = probes[required];
            Assert.True(probe.TryGetProperty("expected", out _));
            Assert.False(string.IsNullOrWhiteSpace(probe.GetProperty("rationale").GetString()));
        }
        var controls = fixtures.RootElement.GetProperty("fixtures").EnumerateArray().ToDictionary(f => f.GetProperty("id").GetString()!);
        Assert.Equal("pass", controls["prescribed-exception-not-required"].GetProperty("expectedOutcome").GetString());
        Assert.Equal("pass", controls["case-sensitive-lookup-allowed"].GetProperty("expectedOutcome").GetString());
        Assert.Equal("notRun", controls["ambiguous-two-blocks"].GetProperty("expectedOutcome").GetString());
        Assert.Equal("compilationRejected", controls["duplicate-declarations"].GetProperty("reasonCode").GetString());
        foreach (var control in controls.Values)
            Assert.Contains(control.GetProperty("observation").GetString(), probes.Keys.Concat(["submission", "compilation"]));
    }

    [Fact]
    public void DeclaredExpectedGroupsAgreeWithTheirStaticInputs()
    {
        using var observations = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(PackageRoot, "observations.json")));
        foreach (var observation in observations.RootElement.GetProperty("observations").EnumerateArray())
        {
            if (!observation.GetProperty("expected").TryGetProperty("groups", out var groups)) continue;
            var input = observation.GetProperty("input").EnumerateArray().Select(v => v.GetString()).Where(v => !string.IsNullOrEmpty(v)).ToArray();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var total = 0;
            foreach (var group in groups.EnumerateArray())
            {
                var key = group.GetProperty("key").GetString()!;
                Assert.True(seen.Add(key), $"{observation.GetProperty("id")}: duplicate expected equivalence class.");
                var expected = group.GetProperty("count").GetInt32();
                Assert.True(expected > 0);
                Assert.Equal(expected, input.Count(value => StringComparer.OrdinalIgnoreCase.Equals(value, key)));
                total += expected;
            }
            Assert.Equal(input.Length, total);
        }
    }

    private static EvaluationDefinition Definition() => Read<EvaluationDefinition>(Path.Combine(PackageRoot, "definition.json"));
    private static EvaluationManifest Manifest() => Read<EvaluationManifest>(Path.Combine(FixtureRoot, "manifest.json"));
    private static EvaluationResult Result() => Read<EvaluationResult>(Path.Combine(FixtureRoot, "candidate-failure.json"));
    private static ArtifactReference Evidence() => new() { Path = "evidence/observation.json", Sha256 = new string('0', 64) };
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), LabJson.Options)!;
    private static T Roundtrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value, LabJson.Options), LabJson.Options)!;
}
