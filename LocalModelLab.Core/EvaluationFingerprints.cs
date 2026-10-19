using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalModelLab.Core;

public sealed record LogicalFingerprint
{
    public required string Algorithm { get; init; }
    public required string Sha256 { get; init; }
}

public static class EvaluationFingerprints
{
    public const string CanonicalV1 = "lab-json-canonical-v1";
    public const string Phase1ACrlfV1 = "phase1a-stj-indented-crlf-v1";
    public const string Phase1ALfV1 = "phase1a-stj-indented-lf-v1";

    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    private static readonly JsonSerializerOptions LegacyOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public static LogicalFingerprint Create<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value, CanonicalOptions);
        using var stream = new MemoryStream();
        // Domain separation keeps a logical identity distinct from a raw artifact digest.
        stream.Write(Encoding.UTF8.GetBytes(CanonicalV1 + "\n"));
        using (var writer = new Utf8JsonWriter(stream, new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            WriteCanonical(writer, element);
        return new() { Algorithm = CanonicalV1, Sha256 = LabJson.Hash(stream.ToArray()) };
    }

    public static LogicalFingerprint ForSourceCase(BenchmarkCase value, string algorithm)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (algorithm == CanonicalV1) return Create(value);
        if (algorithm is not (Phase1ACrlfV1 or Phase1ALfV1))
            throw new ArgumentException("Unknown source-case fingerprint algorithm.", nameof(algorithm));

        var json = JsonSerializer.Serialize(value, LegacyOptions).Replace("\r\n", "\n", StringComparison.Ordinal);
        if (algorithm == Phase1ACrlfV1) json = json.Replace("\n", "\r\n", StringComparison.Ordinal);
        // Serialized string contents contain escaped newlines; only indentation bytes change here.
        return new() { Algorithm = algorithm, Sha256 = LabJson.Hash(Encoding.UTF8.GetBytes(json)) };
    }

    public static bool IsSourceAlgorithm(string algorithm) =>
        algorithm is CanonicalV1 or Phase1ACrlfV1 or Phase1ALfV1;

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var properties = value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                for (var i = 0; i < properties.Length; i++)
                {
                    var property = properties[i];
                    if (i > 0 && properties[i - 1].Name == property.Name)
                        throw new ArgumentException("Canonical fingerprints reject duplicate JSON property names.");
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break;
            case JsonValueKind.Number: writer.WriteRawValue(NormalizeNumber(value.GetRawText())); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new ArgumentException("A defined JSON value is required for a canonical fingerprint.");
        }
    }

    private static string NormalizeNumber(string json)
    {
        var parts = json.Split(['e', 'E']);
        var digits = parts[0];
        var negative = digits[0] == '-';
        if (negative) digits = digits[1..];
        var exponent = parts.Length == 2
            ? BigInteger.Parse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)
            : BigInteger.Zero;
        var point = digits.IndexOf('.');
        if (point >= 0)
        {
            exponent -= digits.Length - point - 1;
            digits = digits.Remove(point, 1);
        }
        digits = digits.TrimStart('0');
        if (digits.Length == 0) return "0";
        var significant = digits.TrimEnd('0');
        exponent += digits.Length - significant.Length;
        return (negative ? "-" : "") + significant + (exponent.IsZero ? "" : "e" + exponent.ToString(CultureInfo.InvariantCulture));
    }
}
