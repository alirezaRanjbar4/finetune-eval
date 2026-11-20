using System.Text;
using System.Text.RegularExpressions;
using Tooling;

namespace FineTuneEval;

public sealed record EvalCase(string? Id, List<string?>? References);
public sealed record ModelOutput(string? Id, string? Output);
public sealed record Scores(double ExactMatch, double TokenF1);
public sealed record CaseResult(string Id, Scores Baseline, Scores Candidate);
public sealed record EvaluationReport(int Cases, Scores Baseline, Scores Candidate, Scores Delta, bool Passed,
    double MinimumExactMatch, double MinimumTokenF1, double MaximumDrop, IReadOnlyList<CaseResult> Results);

public static partial class Evaluator
{
    public static EvaluationReport Compare(string casesPath, string baselinePath, string candidatePath,
        double minimumExactMatch = 0, double minimumTokenF1 = 0, double maximumDrop = 1)
    {
        foreach (var threshold in new[] { minimumExactMatch, minimumTokenF1, maximumDrop })
            if (!double.IsFinite(threshold) || threshold is < 0 or > 1)
                throw new InputException("Thresholds must be finite numbers between 0 and 1.");
        var cases = JsonSupport.Read<EvalCase>(casesPath);
        if (cases.Any(row => string.IsNullOrWhiteSpace(row.Id) || row.References is null || row.References.Count == 0 || row.References.Any(string.IsNullOrWhiteSpace)))
            throw new InputException("Each case requires a nonblank id and one or more nonblank reference strings.");
        var ids = cases.Select(row => row.Id!).ToHashSet(StringComparer.Ordinal);
        if (ids.Count != cases.Count) throw new InputException("Case ids must be unique.");
        var baseline = ReadOutputs(baselinePath, ids);
        var candidate = ReadOutputs(candidatePath, ids);
        var results = cases.Select(row => new CaseResult(row.Id!, Score(baseline[row.Id!], row.References!), Score(candidate[row.Id!], row.References!))).ToArray();
        var baselineScore = new Scores(results.Average(r => r.Baseline.ExactMatch), results.Average(r => r.Baseline.TokenF1));
        var candidateScore = new Scores(results.Average(r => r.Candidate.ExactMatch), results.Average(r => r.Candidate.TokenF1));
        var delta = new Scores(candidateScore.ExactMatch - baselineScore.ExactMatch, candidateScore.TokenF1 - baselineScore.TokenF1);
        const double tolerance = 1e-12;
        var passed = candidateScore.ExactMatch + tolerance >= minimumExactMatch && candidateScore.TokenF1 + tolerance >= minimumTokenF1 &&
            delta.ExactMatch + tolerance >= -maximumDrop && delta.TokenF1 + tolerance >= -maximumDrop;
        return new(cases.Count, baselineScore, candidateScore, delta, passed, minimumExactMatch, minimumTokenF1, maximumDrop, results);
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
