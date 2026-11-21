using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tooling;

namespace FineTuneEval;

public sealed record EvalCase(string? Id, List<string?>? References, List<string?>? Tags = null);
public sealed record ModelOutput(string? Id, string? Output);
public sealed record Scores(double ExactMatch, double TokenF1);
public sealed record CaseResult(string Id, Scores Baseline, Scores Candidate);
public sealed record EvaluationReport(int Cases, Scores Baseline, Scores Candidate, Scores Delta, bool Passed,
    double MinimumExactMatch, double MinimumTokenF1, double MaximumDrop, IReadOnlyList<CaseResult> Results, IReadOnlyList<SliceReport> Slices);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SliceGate(double? MinExact = null, double? MinF1 = null, double? MaxDrop = null);
public sealed record SliceReport(string Tag, int Cases, Scores Baseline, Scores Candidate, Scores Delta, SliceGate? Gate, bool? Passed);

public static partial class Evaluator
{
    public static EvaluationReport Compare(string casesPath, string baselinePath, string candidatePath,
        double minimumExactMatch = 0, double minimumTokenF1 = 0, double maximumDrop = 1, IReadOnlyDictionary<string, SliceGate>? sliceGates = null)
    {
        foreach (var threshold in new[] { minimumExactMatch, minimumTokenF1, maximumDrop })
            if (!double.IsFinite(threshold) || threshold is < 0 or > 1)
                throw new InputException("Thresholds must be finite numbers between 0 and 1.");
        var cases = JsonSupport.Read<EvalCase>(casesPath);
        if (cases.Any(row => string.IsNullOrWhiteSpace(row.Id) || row.References is null || row.References.Count == 0 || row.References.Any(string.IsNullOrWhiteSpace)))
            throw new InputException("Each case requires a nonblank id and one or more nonblank reference strings.");
        if (cases.Any(row => row.Tags is not null && (row.Tags.Any(string.IsNullOrWhiteSpace) || row.Tags.Distinct(StringComparer.Ordinal).Count() != row.Tags.Count)))
            throw new InputException("Case tags must be nonblank, unique strings.");
        var tags = cases.SelectMany(row => row.Tags ?? []).Select(tag => tag!).Distinct(StringComparer.Ordinal).OrderBy(tag => tag, StringComparer.Ordinal).ToArray();
        var effectiveGates = new Dictionary<string, SliceGate>(StringComparer.Ordinal);
        foreach (var (tag, gate) in sliceGates ?? new Dictionary<string, SliceGate>())
        {
            if (string.IsNullOrWhiteSpace(tag) || gate is null || !tags.Contains(tag, StringComparer.Ordinal))
                throw new InputException("Every slice gate requires a nonblank tag with at least one evaluation case.");
            var resolved = new SliceGate(gate.MinExact ?? minimumExactMatch, gate.MinF1 ?? minimumTokenF1, gate.MaxDrop ?? maximumDrop);
            foreach (var threshold in new[] { resolved.MinExact!.Value, resolved.MinF1!.Value, resolved.MaxDrop!.Value })
                if (!double.IsFinite(threshold) || threshold is < 0 or > 1) throw new InputException("Slice thresholds must be finite numbers between 0 and 1.");
            effectiveGates.Add(tag, resolved);
        }
        var ids = cases.Select(row => row.Id!).ToHashSet(StringComparer.Ordinal);
        if (ids.Count != cases.Count) throw new InputException("Case ids must be unique.");
        var baseline = ReadOutputs(baselinePath, ids);
        var candidate = ReadOutputs(candidatePath, ids);
        var results = cases.Select(row => new CaseResult(row.Id!, Score(baseline[row.Id!], row.References!), Score(candidate[row.Id!], row.References!))).ToArray();
        var baselineScore = new Scores(results.Average(r => r.Baseline.ExactMatch), results.Average(r => r.Baseline.TokenF1));
        var candidateScore = new Scores(results.Average(r => r.Candidate.ExactMatch), results.Average(r => r.Candidate.TokenF1));
        var delta = new Scores(candidateScore.ExactMatch - baselineScore.ExactMatch, candidateScore.TokenF1 - baselineScore.TokenF1);
        var slices = tags.Select(tag =>
        {
            var selected = results.Where((_, index) => cases[index].Tags?.Contains(tag, StringComparer.Ordinal) == true).ToArray();
            var b = new Scores(selected.Average(row => row.Baseline.ExactMatch), selected.Average(row => row.Baseline.TokenF1));
            var c = new Scores(selected.Average(row => row.Candidate.ExactMatch), selected.Average(row => row.Candidate.TokenF1));
            var d = new Scores(c.ExactMatch - b.ExactMatch, c.TokenF1 - b.TokenF1);
            effectiveGates.TryGetValue(tag, out var gate);
            return new SliceReport(tag, selected.Length, b, c, d, gate,
                gate is null ? null : Passes(c, d, gate.MinExact!.Value, gate.MinF1!.Value, gate.MaxDrop!.Value));
        }).ToArray();
        var passed = Passes(candidateScore, delta, minimumExactMatch, minimumTokenF1, maximumDrop) && slices.All(slice => slice.Passed != false);
        return new(cases.Count, baselineScore, candidateScore, delta, passed, minimumExactMatch, minimumTokenF1, maximumDrop, results, slices);
    }

    public static IReadOnlyDictionary<string, SliceGate> LoadSliceGates(string path)
    {
        try { return JsonSupport.Parse<Dictionary<string, SliceGate>>(File.ReadAllText(path)); }
        catch (JsonException) { throw new InputException("Slice gates must be a JSON object of tag names to threshold objects; unknown threshold names are rejected."); }
    }

    private static bool Passes(Scores candidate, Scores delta, double minimumExact, double minimumF1, double maximumDrop)
    {
        const double tolerance = 1e-12;
        return candidate.ExactMatch + tolerance >= minimumExact && candidate.TokenF1 + tolerance >= minimumF1 &&
            delta.ExactMatch + tolerance >= -maximumDrop && delta.TokenF1 + tolerance >= -maximumDrop;
    }

    public static Scores Score(string prediction, IReadOnlyList<string?> references)
    {
        if (references.Count == 0 || references.Any(string.IsNullOrWhiteSpace)) throw new InputException("At least one nonblank reference is required.");
        return new(references.Any(reference => Normalize(prediction) == Normalize(reference!)) ? 1 : 0,
            references.Max(reference => TokenF1(prediction, reference!)));
    }

    public static double TokenF1(string prediction, string reference)
    {
        var predicted = Tokens(prediction);
        var expected = Tokens(reference);
        if (predicted.Length == 0 || expected.Length == 0)
            return Normalize(prediction) == Normalize(reference) ? 1 : 0;
        var predictedCounts = predicted.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var expectedCounts = expected.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var common = predictedCounts.Sum(pair => Math.Min(pair.Value, expectedCounts.GetValueOrDefault(pair.Key)));
        return 2.0 * common / (predicted.Length + expected.Length);
    }

    private static Dictionary<string, string> ReadOutputs(string path, HashSet<string> expectedIds)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in JsonSupport.Read<ModelOutput>(path))
        {
            if (string.IsNullOrWhiteSpace(row.Id) || row.Output is null) throw new InputException("Each prediction requires a nonblank id and a string output (which may be empty).");
            if (!result.TryAdd(row.Id, row.Output)) throw new InputException("Prediction ids must be unique.");
        }
        if (!expectedIds.SetEquals(result.Keys)) throw new InputException("Prediction ids must match all evaluation ids exactly; missing and extra ids are rejected.");
        return result;
    }

    private static string Normalize(string value) => Whitespace().Replace(value.Normalize(NormalizationForm.FormKC).ToLowerInvariant().Trim(), " ");
    private static string[] Tokens(string value) => Word().Matches(value.Normalize(NormalizationForm.FormKC).ToLowerInvariant()).Select(match => match.Value).ToArray();
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
    [GeneratedRegex(@"[\p{L}\p{M}\p{N}\u200C]+")]
    private static partial Regex Word();
}
