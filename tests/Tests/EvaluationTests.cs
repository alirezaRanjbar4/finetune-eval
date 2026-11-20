using System.Text.Json;
using FineTuneEval;
using Tooling;
namespace Tests;

public sealed class EvaluationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "finetune-eval-tests-" + Guid.NewGuid().ToString("N"));
    private string Data(params object[] rows)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, Guid.NewGuid().ToString("N"));
        File.WriteAllLines(path, rows.Select(row => JsonSerializer.Serialize(row)));
        return path;
    }
    private string Cases() => Data(new { id = "a", references = new[] { "red blue" } }, new { id = "b", references = new[] { "green" } });
    private string Outputs(string a, string b) => Data(new { id = "a", output = a }, new { id = "b", output = b });
    [Theory]
    [InlineData("red blue", "red blue", 1)]
    [InlineData("red", "red blue", 2.0 / 3)]
    [InlineData("red red", "red", 2.0 / 3)]
    [InlineData("blue", "red", 0)]
    [InlineData("", "red", 0)]
    [InlineData("!", "?", 0)]
    [InlineData("سلام دنیا", "سلام دنیا", 1)]
    public void TokenF1MatchesHandCalculatedCases(string prediction, string reference, double expected)
        => Assert.Equal(expected, Evaluator.TokenF1(prediction, reference), 10);
    [Fact]
    public void BestOfMultipleReferencesAndUnicodeNormalizationAreSupported()
    {
        var score = Evaluator.Score("  ＨＥＬＬＯ   world ", ["other", "hello world"]);
        Assert.Equal(1, score.ExactMatch);
        Assert.Equal(1, score.TokenF1);
    }
    [Fact]
    public void ComparisonUsesAllCasesAndComputesDeltas()
    {
        var report = Evaluator.Compare(Cases(), Outputs("red", "wrong"), Outputs("red blue", "green"), 1, 1, 0);
        Assert.True(report.Passed);
        Assert.Equal(2, report.Cases);
        Assert.Equal(1, report.Candidate.ExactMatch);
        Assert.Equal(1, report.Delta.ExactMatch);
        Assert.Equal(1.0 / 3, report.Baseline.TokenF1, 10);
    }
    [Fact]
    public void RegressionFailsTheGate()
    {
        var report = Evaluator.Compare(Cases(), Outputs("red blue", "green"), Outputs("red blue", "wrong"), maximumDrop: 0);
        Assert.False(report.Passed);
        Assert.Equal(-0.5, report.Delta.ExactMatch);
    }
    [Fact]
    public void MissingAndExtraIdsAreRejected()
    {
        var valid = Outputs("a", "b");
        Assert.Throws<InputException>(() => Evaluator.Compare(Cases(), valid, Data(new { id = "a", output = "x" })));
        Assert.Throws<InputException>(() => Evaluator.Compare(Cases(), valid, Data(new { id = "a", output = "x" }, new { id = "b", output = "y" }, new { id = "c", output = "z" })));
    }
    [Fact]
    public void DuplicateIdsAndNullOutputsAreRejected()
    {
        Assert.Throws<InputException>(() => Evaluator.Compare(Cases(), Outputs("a", "b"), Data(new { id = "a", output = "x" }, new { id = "a", output = "y" })));
        Assert.Throws<InputException>(() => Evaluator.Compare(Cases(), Outputs("a", "b"), Data(new { id = "a", output = (string?)null }, new { id = "b", output = "y" })));
    }
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1.01")]
    [InlineData("-0.1")]
    public void InvalidThresholdArgumentsAreRejected(string threshold)
        => Assert.Equal(2, Application.Run(["compare", "--cases", "unused", "--baseline", "unused", "--candidate", "unused", "--min-exact", threshold], new StringWriter(), new StringWriter()));
    [Fact]
    public void CliReturnsGateExitCodeAndDoesNotPrintAnswers()
    {
        var output = new StringWriter();
        var result = Application.Run(["compare", "--cases", Cases(), "--baseline", Outputs("red blue", "green"), "--candidate", Outputs("red", "synthetic-private-output"), "--min-exact", "1"], output, new StringWriter());
        Assert.Equal(1, result);
        Assert.DoesNotContain("synthetic-private-output", output.ToString());
        using var json = JsonDocument.Parse(output.ToString());
        Assert.False(json.RootElement.GetProperty("passed").GetBoolean());
    }
    [Fact]
    public void EmptyReferenceSetIsRejected() => Assert.Throws<InputException>(() => Evaluator.Score("answer", []));
    public void Dispose()
    {
        if (Directory.Exists(root) && Path.GetFileName(root).StartsWith("finetune-eval-tests-", StringComparison.Ordinal)) Directory.Delete(root, true);
    }
}
