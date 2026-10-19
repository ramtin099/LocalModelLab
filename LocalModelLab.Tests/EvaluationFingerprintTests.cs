using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using LocalModelLab.Core;

namespace LocalModelLab.Tests;

public class EvaluationFingerprintTests
{
    private const string CrlfCase = "90ce6abc62003f6fce835c6eced0fcd8573e20b68b803fe8d7996cd01c365eef";
    private const string LfCase = "ad82ca5fb410ce52d6bd7b5aebe03acdd4e291b6cabb20c82b166c36b0b7273d";
    private const string CanonicalCase = "8cd8825520f72d0eae873d82c68597c556580008ec863e6dc7464251ffea20c5";

    [Theory]
    [InlineData(EvaluationFingerprints.Phase1ACrlfV1, CrlfCase)]
    [InlineData(EvaluationFingerprints.Phase1ALfV1, LfCase)]
    [InlineData(EvaluationFingerprints.CanonicalV1, CanonicalCase)]
    public void SourceCaseHasFixedCrossPlatformVectors(string algorithm, string expected)
    {
        var result = EvaluationFingerprints.ForSourceCase(Source(), algorithm);
        Assert.Equal(algorithm, result.Algorithm);
        Assert.Equal(expected, result.Sha256);
    }

    [Fact]
    public void ActualPhase1ASerializationReproducesBothHistoricalByteLayouts()
    {
        var source = Source();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(source, LabJson.Options);
        Assert.Equal(LabJson.Hash(bytes), LabJson.Fingerprint(source));
        var lf = Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n", StringComparison.Ordinal);
        var crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);
        Assert.Equal(409, Encoding.UTF8.GetByteCount(lf));
        Assert.Equal(414, Encoding.UTF8.GetByteCount(crlf));
        Assert.Equal(LfCase, LabJson.Hash(Encoding.UTF8.GetBytes(lf)));
        Assert.Equal(CrlfCase, LabJson.Hash(Encoding.UTF8.GetBytes(crlf)));
        Assert.Equal(source, JsonSerializer.Deserialize<BenchmarkCase>(lf, LabJson.Options));
        Assert.Equal(source, JsonSerializer.Deserialize<BenchmarkCase>(crlf, LabJson.Options));
    }

    [Theory]
    [InlineData("{\"b\":2,\"a\":[true,null,\"x\"]}")]
    [InlineData("{\"a\":[true,null,\"x\"],\"b\":2}")]
    [InlineData("{\n  \"b\": 2,\n  \"a\": [true, null, \"x\"]\n}")]
    [InlineData("{\r\n    \"b\": 2,\r\n    \"a\": [true, null, \"x\"]\r\n}")]
    [InlineData("{\"b\":2.00,\"a\":[true,null,\"\\u0078\"]}")]
    public void FormattingAndOrderingHaveOneFixedCanonicalVector(string json)
    {
        var fingerprint = Canonical(json);
        Assert.Equal(EvaluationFingerprints.CanonicalV1, fingerprint.Algorithm);
        Assert.Equal("93a8948fe39e65ab30bbadd37837323fa0c2380e3ef8b7a939372bb21a804ae3", fingerprint.Sha256);
    }

    [Theory]
    [InlineData("{\"text\":\"caf\u00e9\"}")]
    [InlineData("{\"text\":\"caf\\u00e9\"}")]
    public void UnicodeEscapeSpellingsHaveOneFixedVector(string json) =>
        Assert.Equal("a4cd11d25ea7d4bd958f8879fdf1c284607add4eb0e613a576cc91c3f71dcb62", Canonical(json).Sha256);

    [Theory]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("1.000E+00")]
    [InlineData("0.1e1")]
    [InlineData("10e-1")]
    [InlineData("100e-2")]
    public void EquivalentNumbersHaveOneFixedVector(string number) =>
        Assert.Equal("66347a9f2f053ddfd1c86c1d7ad140d5d7ab48c562486a9d3cc045abcc55768c", Canonical("{\"value\":" + number + "}").Sha256);

    [Theory]
    [InlineData("-0.000e100", "0", true)]
    [InlineData("1e-999", "0", false)]
    [InlineData("9007199254740993", "9007199254740992", false)]
    [InlineData("1.0000000000000000000000000001", "1", false)]
    [InlineData("1.2340e+3", "1234", true)]
    public void NumericNormalizationDoesNotRound(string left, string right, bool equal) =>
        Assert.Equal(equal, Canonical(left) == Canonical(right));

    [Fact]
    public void TypedFingerprintsIgnoreSerializerIndentationAndEscapeOptions()
    {
        foreach (var indented in new[] { false, true })
        foreach (var encoder in new[] { JavaScriptEncoder.Default, JavaScriptEncoder.UnsafeRelaxedJsonEscaping })
        {
            var options = new JsonSerializerOptions(LabJson.Options) { WriteIndented = indented, Encoder = encoder };
            var json = JsonSerializer.Serialize(Source(), options).Replace("\r\n", "\n", StringComparison.Ordinal);
            foreach (var representation in new[] { json, json.Replace("\n", "\r\n", StringComparison.Ordinal) })
            {
                var source = JsonSerializer.Deserialize<BenchmarkCase>(representation, LabJson.Options)!;
                Assert.Equal(CanonicalCase, EvaluationFingerprints.Create(source).Sha256);
            }
        }
    }

    [Fact]
    public void StringContentsAndArrayOrderRemainSignificant()
    {
        Assert.NotEqual(Canonical("\"a\\r\\nb\""), Canonical("\"a\\nb\""));
        Assert.NotEqual(Canonical("[1,2]"), Canonical("[2,1]"));
        Assert.NotEqual(Canonical("{\"A\":1}"), Canonical("{\"a\":1}"));
        var withCrlf = Source() with { Prompt = "a\r\nb" };
        var withLf = Source() with { Prompt = "a\nb" };
        foreach (var algorithm in new[] { EvaluationFingerprints.CanonicalV1, EvaluationFingerprints.Phase1ACrlfV1, EvaluationFingerprints.Phase1ALfV1 })
            Assert.NotEqual(EvaluationFingerprints.ForSourceCase(withCrlf, algorithm), EvaluationFingerprints.ForSourceCase(withLf, algorithm));
    }

    [Fact]
    public void DuplicateJsonPropertiesAreRejected() =>
        Assert.Throws<ArgumentException>(() => Canonical("{\"nested\":{\"a\":1,\"a\":2}}"));

    [Fact]
    public void AlgorithmLabelsAreRequiredAndMustMatch()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "evaluations", "string-counting", "1.0.0");
        var definition = JsonSerializer.Deserialize<EvaluationDefinition>(File.ReadAllBytes(Path.Combine(root, "definition.json")), LabJson.Options)!;
        var manifest = JsonSerializer.Deserialize<EvaluationManifest>(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Evaluation", "v1", "manifest.json")), LabJson.Options)!;
        foreach (var algorithm in new[] { "", "unknown", null! })
        {
            Assert.Throws<ArgumentException>(() => EvaluationFingerprints.ForSourceCase(Source(), algorithm));
            Assert.Throws<ArgumentException>(() => EvaluationValidator.Validate(definition with
            {
                Cases = [definition.Cases[0] with { SourceCaseFingerprintAlgorithm = algorithm }]
            }));
        }
        Assert.Throws<ArgumentException>(() => EvaluationValidator.Validate(manifest with
        {
            Source = manifest.Source with { CaseFingerprintAlgorithm = EvaluationFingerprints.Phase1ALfV1 }
        }, definition));
        var packageNode = JsonSerializer.SerializeToNode(definition, LabJson.Options)!;
        packageNode["cases"]![0]!.AsObject().Remove("sourceCaseFingerprintAlgorithm");
        Assert.Throws<JsonException>(() => packageNode.Deserialize<EvaluationDefinition>(LabJson.Options));
        var manifestNode = JsonSerializer.SerializeToNode(manifest, LabJson.Options)!;
        manifestNode["source"]!.AsObject().Remove("caseFingerprintAlgorithm");
        Assert.Throws<JsonException>(() => manifestNode.Deserialize<EvaluationManifest>(LabJson.Options));
    }

    [Fact]
    public void RawHashesRemainExactEvenWhenLogicalFingerprintsAgree()
    {
        const string lf = "{\n \"b\":2, \"a\":1\n}";
        var crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);
        Assert.NotEqual(LabJson.Hash(Encoding.UTF8.GetBytes(lf)), LabJson.Hash(Encoding.UTF8.GetBytes(crlf)));
        Assert.Equal(Canonical(lf), Canonical(crlf));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", LabJson.Hash(Encoding.UTF8.GetBytes("abc")));
        var result = EvaluationFingerprints.Create(Source());
        var roundtrip = JsonSerializer.Deserialize<LogicalFingerprint>(JsonSerializer.Serialize(result, LabJson.Options), LabJson.Options);
        Assert.Equal(result, roundtrip);
    }

    private static LogicalFingerprint Canonical(string json)
    {
        using var document = JsonDocument.Parse(json);
        return EvaluationFingerprints.Create(document.RootElement);
    }

    private static BenchmarkCase Source()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "benchmarks", "comparison.non-thinking.v1.json");
        return JsonSerializer.Deserialize<BenchmarkDefinitions>(File.ReadAllBytes(path), LabJson.Options)!.Cases.Single(c => c.Id == "csharp-generation");
    }
}
