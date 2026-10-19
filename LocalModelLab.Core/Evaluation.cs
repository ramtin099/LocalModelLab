using System.Collections.Immutable;

namespace LocalModelLab.Core;

public enum CheckOutcome { NotRun, Pass, Fail, Error }
public enum EvaluationLifecycle { Planned, Running, Finalized, Cancelled, Interrupted }

public sealed record ArtifactReference
{
    public required string Path { get; init; }
    public required string Sha256 { get; init; }
}

public sealed record VersionedEvaluationReference
{
    public required string Id { get; init; }
    public required string Version { get; init; }
}

public sealed record EvaluationDefinition
{
    public required int SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required ImmutableArray<EvaluationCase> Cases { get; init; }
    public required ImmutableArray<VerifierDefinition> Verifiers { get; init; }
}

public sealed record EvaluationCase
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string SourceCaseId { get; init; }
    public required string SourceCaseVersion { get; init; }
    public required string SourceCaseFingerprint { get; init; }
    public required string SourceCaseFingerprintAlgorithm { get; init; }
    public required ArtifactReference Contract { get; init; }
    public required VersionedEvaluationReference Verifier { get; init; }
}

public sealed record VerifierDefinition
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string Kind { get; init; }
    public required ImmutableArray<string> RequiredChecks { get; init; }
    public required ImmutableArray<ArtifactReference> Artifacts { get; init; }
}

// This is a value-only link to frozen evidence, never a live AttemptResult or writable run store.
public sealed record SourceAttemptReference
{
    public required string RunId { get; init; }
    public required string AttemptId { get; init; }
    public required ArtifactReference Result { get; init; }
    public required string CaseFingerprint { get; init; }
    public required string CaseFingerprintAlgorithm { get; init; }
    public required bool Warmup { get; init; }
    public required int Repetition { get; init; }
}

public sealed record EvaluationManifest
{
    public required int SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required string EngineVersion { get; init; }
    public required ArtifactReference Definition { get; init; }
    public required VersionedEvaluationReference Case { get; init; }
    public required VersionedEvaluationReference Verifier { get; init; }
    public required SourceAttemptReference Source { get; init; }
    public required ImmutableArray<ArtifactReference> Provenance { get; init; }
}

public sealed record CheckResult
{
    public required string Id { get; init; }
    public required CheckOutcome Outcome { get; init; }
    public required string ReasonCode { get; init; }
    public required ImmutableArray<ArtifactReference> Evidence { get; init; }
}

public sealed record EvaluationResult
{
    public required int SchemaVersion { get; init; }
    public required string EvaluationId { get; init; }
    public required ArtifactReference Manifest { get; init; }
    public required EvaluationLifecycle Lifecycle { get; init; }
    public required CheckOutcome Outcome { get; init; }
    public required ImmutableArray<CheckResult> Checks { get; init; }
}

public static class EvaluationValidator
{
    public static void Validate(EvaluationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Schema(definition.SchemaVersion);
        Identity(definition.Id, definition.Version);
        Items(definition.Cases);
        Items(definition.Verifiers);
        Unique(definition.Cases.Select(c => c.Id));
        Unique(definition.Verifiers.Select(v => v.Id));
        foreach (var verifier in definition.Verifiers)
        {
            Identity(verifier.Id, verifier.Version);
            Text(verifier.Kind);
            Items(verifier.RequiredChecks);
            Unique(verifier.RequiredChecks);
            Artifacts(verifier.Artifacts, required: true);
        }
        foreach (var item in definition.Cases)
        {
            Identity(item.Id, item.Version);
            Identity(item.SourceCaseId, item.SourceCaseVersion);
            Hash(item.SourceCaseFingerprint);
            if (!EvaluationFingerprints.IsSourceAlgorithm(item.SourceCaseFingerprintAlgorithm))
                throw new ArgumentException("An explicit supported source-case fingerprint algorithm is required.");
            Artifact(item.Contract);
            Reference(item.Verifier);
            if (!definition.Verifiers.Any(v => v.Id == item.Verifier.Id && v.Version == item.Verifier.Version))
                throw new ArgumentException("Evaluation case references an unknown verifier version.");
        }
        var artifacts = definition.Cases.Select(c => c.Contract).Concat(definition.Verifiers.SelectMany(v => v.Artifacts));
        ConsistentReferences(artifacts);
    }

    public static void Validate(EvaluationManifest manifest, EvaluationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        Validate(definition);
        Schema(manifest.SchemaVersion);
        Text(manifest.Id);
        Identity("engine", manifest.EngineVersion);
        if (manifest.CreatedUtc == default || manifest.CreatedUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Manifest needs a UTC creation timestamp.");
        Artifact(manifest.Definition);
        Reference(manifest.Case);
        Reference(manifest.Verifier);
        ArgumentNullException.ThrowIfNull(manifest.Source);
        Text(manifest.Source.RunId);
        Text(manifest.Source.AttemptId);
        Artifact(manifest.Source.Result);
        Hash(manifest.Source.CaseFingerprint);
        if (!EvaluationFingerprints.IsSourceAlgorithm(manifest.Source.CaseFingerprintAlgorithm))
            throw new ArgumentException("An explicit supported source-attempt fingerprint algorithm is required.");
        if (manifest.Source.Repetition < 1) throw new ArgumentException("Source repetition must be positive.");
        Artifacts(manifest.Provenance, required: true);
        ConsistentReferences(manifest.Provenance.Prepend(manifest.Definition));
        var item = definition.Cases.SingleOrDefault(c => c.Id == manifest.Case.Id && c.Version == manifest.Case.Version);
        if (item is null || item.Verifier != manifest.Verifier || item.SourceCaseFingerprint != manifest.Source.CaseFingerprint ||
            item.SourceCaseFingerprintAlgorithm != manifest.Source.CaseFingerprintAlgorithm)
            throw new ArgumentException("Manifest case, verifier and source fingerprint must agree with the definition.");
    }

    public static void Validate(EvaluationResult result, EvaluationManifest manifest, EvaluationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(result);
        Validate(manifest, definition);
        Schema(result.SchemaVersion);
        Artifact(result.Manifest);
        if (result.EvaluationId != manifest.Id || !Enum.IsDefined(result.Lifecycle) || !Enum.IsDefined(result.Outcome))
            throw new ArgumentException("Invalid evaluation identity, lifecycle or outcome.");
        Items(result.Checks);
        Unique(result.Checks.Select(c => c.Id));
        var verifier = definition.Verifiers.Single(v => v.Id == manifest.Verifier.Id && v.Version == manifest.Verifier.Version);
        if (!verifier.RequiredChecks.ToHashSet(StringComparer.Ordinal).SetEquals(result.Checks.Select(c => c.Id)))
            throw new ArgumentException("Results must contain every required check exactly once, including notRun checks.");
        foreach (var check in result.Checks)
        {
            if (!Enum.IsDefined(check.Outcome)) throw new ArgumentException("Unknown check outcome.");
            Text(check.ReasonCode);
            Artifacts(check.Evidence, required: check.Outcome != CheckOutcome.NotRun);
        }
        ConsistentReferences(result.Checks.SelectMany(c => c.Evidence).Prepend(result.Manifest));
        // Lifecycle is operational state; incomplete work cannot claim an overall correctness verdict.
        CheckOutcome expected;
        if (result.Lifecycle != EvaluationLifecycle.Finalized) expected = CheckOutcome.NotRun;
        else if (result.Checks.Any(c => c.Outcome == CheckOutcome.Fail)) expected = CheckOutcome.Fail;
        else if (result.Checks.Any(c => c.Outcome == CheckOutcome.Error)) expected = CheckOutcome.Error;
        else if (result.Checks.Any(c => c.Outcome == CheckOutcome.NotRun)) expected = CheckOutcome.NotRun;
        else expected = CheckOutcome.Pass;
        if (result.Outcome != expected) throw new ArgumentException("Overall outcome contradicts lifecycle or check outcomes.");
    }

    private static void Schema(int version)
    {
        if (version != 1) throw new ArgumentException("Only evaluation schemaVersion 1 is supported.");
    }

    private static void Text(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A nonempty value is required.");
    }

    private static void Identity(string id, string version)
    {
        Text(id);
        if (version is null || version.Split('.').Length != 3 ||
            version.Split('.').Any(p => p.Length == 0 || p.Any(c => c is < '0' or > '9') || (p.Length > 1 && p[0] == '0')))
            throw new ArgumentException("Versions must use major.minor.patch without leading zeros.");
    }

    private static void Reference(VersionedEvaluationReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        Identity(reference.Id, reference.Version);
    }

    private static void Hash(string hash)
    {
        if (hash is null || hash.Length != 64 || hash.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ArgumentException("Artifact hashes must be lowercase SHA-256 hex.");
    }

    private static void Artifact(ArtifactReference artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        Text(artifact.Path);
        if (artifact.Path.Any(c => c is '\\' or ':' or '<' or '>' or '"' or '|' or '?' or '*' || char.IsControl(c)) ||
            artifact.Path.Split('/').Any(p => p is "" or "." or ".." || p.EndsWith(' ') || p.EndsWith('.')))
            throw new ArgumentException("Artifact paths must be portable relative paths without traversal.");
        foreach (var segment in artifact.Path.Split('/'))
        {
            var stem = segment.Split('.')[0].ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                 "123456789\u00b9\u00b2\u00b3".Contains(stem[3])))
                throw new ArgumentException("Artifact paths cannot contain Windows device names.");
        }
        Hash(artifact.Sha256);
    }

    private static void Items<T>(ImmutableArray<T> items, bool required = true)
    {
        if (items.IsDefault || (required && items.IsEmpty) || items.Any(i => i is null))
            throw new ArgumentException("An initialized collection without null entries is required.");
    }

    private static void Unique(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            Text(value);
            if (!seen.Add(value)) throw new ArgumentException("Duplicate identifier or artifact path.");
        }
    }

    private static void Artifacts(ImmutableArray<ArtifactReference> artifacts, bool required)
    {
        Items(artifacts, required);
        foreach (var artifact in artifacts) Artifact(artifact);
        if (artifacts.Select(a => a.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != artifacts.Length)
            throw new ArgumentException("Artifact paths must be unique even on case-insensitive filesystems.");
    }

    private static void ConsistentReferences(IEnumerable<ArtifactReference> artifacts)
    {
        foreach (var group in artifacts.GroupBy(a => a.Path, StringComparer.OrdinalIgnoreCase))
            if (group.Select(a => a.Path).Distinct(StringComparer.Ordinal).Count() != 1 ||
                group.Select(a => a.Sha256).Distinct(StringComparer.Ordinal).Count() != 1)
                throw new ArgumentException("References within one artifact root must use identical path spelling and SHA-256.");
    }
}
