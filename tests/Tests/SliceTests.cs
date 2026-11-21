using System.Text.Json;
using FineTuneEval;
using Tooling;

namespace Tests;

public sealed class SliceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "finetune-slices-" + Guid.NewGuid().ToString("N"));
    private string Data(params object[] rows)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, Guid.NewGuid().ToString("N"));
        File.WriteAllLines(path, rows.Select(row => JsonSerializer.Serialize(row)));
        return path;
    }
    private string Cases() => Data(new { id = "fa", references = new[] { "correct" }, tags = new[] { "fa", "priority" } },
        new { id = "en1", references = new[] { "correct" }, tags = new[] { "en" } },
        new { id = "en2", references = new[] { "correct" }, tags = new[] { "en" } },
        new { id = "en3", references = new[] { "correct" } });
    private string Outputs(bool candidate) => Data(new { id = "fa", output = candidate ? "wrong" : "correct" },
        new { id = "en1", output = candidate ? "correct" : "wrong" }, new { id = "en2", output = candidate ? "correct" : "wrong" },
        new { id = "en3", output = candidate ? "correct" : "wrong" });

    [Fact]
    public void OverallImprovementCanFailASliceGate()
    {
        var report = Evaluator.Compare(Cases(), Outputs(false), Outputs(true), sliceGates: new Dictionary<string, SliceGate> { ["fa"] = new(MaxDrop: 0) });
        Assert.Equal(0.5, report.Delta.ExactMatch);
        Assert.False(report.Passed);
        var fa = report.Slices.Single(row => row.Tag == "fa");
        Assert.Equal(1, fa.Cases);
        Assert.False(fa.Passed);
        Assert.Equal(-1, fa.Delta.ExactMatch);
        Assert.Null(report.Slices.Single(row => row.Tag == "priority").Passed);
    }

    [Fact]
    public void UngatedTagsAreReportOnlyAndCanOverlap()
    {
        var report = Evaluator.Compare(Cases(), Outputs(false), Outputs(true));
        Assert.True(report.Passed);
        Assert.Equal(3, report.Slices.Count);
        Assert.Equal(2, report.Slices.Single(row => row.Tag == "en").Cases);
        Assert.All(report.Slices, row => Assert.Null(row.Gate));
    }

    [Fact]
    public void ZeroSupportUnknownTagsAndInvalidThresholdsAreRejected()
    {
        Assert.Throws<InputException>(() => Evaluator.Compare(Cases(), Outputs(false), Outputs(true), sliceGates: new Dictionary<string, SliceGate> { ["missing"] = new() }));
        Assert.Throws<InputException>(() => Evaluator.Compare(Cases(), Outputs(false), Outputs(true), sliceGates: new Dictionary<string, SliceGate> { ["fa"] = new(MinF1: double.NaN) }));
    }

    [Theory]
    [InlineData("{\"fa\":{\"minF11\":0.9}}")]
    [InlineData("{\"fa\":{},\"fa\":{}}")]
    [InlineData("[]")]
    public void MalformedGateConfigurationIsRejected(string json)
    {
        var path = Data(); File.WriteAllText(path, json);
        Assert.Throws<InputException>(() => Evaluator.LoadSliceGates(path));
    }

    [Fact]
    public void DuplicateAndBlankCaseTagsAreRejected()
    {
        foreach (var tags in new[] { new[] { "fa", "fa" }, new[] { " " } })
        {
            var cases = Data(new { id = "a", references = new[] { "correct" }, tags });
            var outputs = Data(new { id = "a", output = "correct" });
            Assert.Throws<InputException>(() => Evaluator.Compare(cases, outputs, outputs));
        }
    }

    [Fact]
    public void CliLoadsSliceGatesAndReturnsFailure()
    {
        var path = Data(); File.WriteAllText(path, "{\"fa\":{\"maxDrop\":0}}");
        var output = new StringWriter();
        Assert.Equal(1, Application.Run(["compare", "--cases", Cases(), "--baseline", Outputs(false), "--candidate", Outputs(true), "--slice-gates", path], output, new StringWriter()));
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal(3, json.RootElement.GetProperty("slices").GetArrayLength());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    public void SupportWarningBoundaryUsesEachOverlappingTagIndependently(int minimum, int expectedWarnings)
    {
        var report = Evaluator.Compare(Cases(), Outputs(false), Outputs(true), minimumSliceSupport: minimum);
        Assert.Equal(minimum, report.MinimumSliceSupport);
        Assert.Equal(expectedWarnings, report.SupportWarnings.Count);
        Assert.Equal(4, report.Cases); // Includes the untagged case exactly once.
        Assert.Equal(0.25, report.Baseline.ExactMatch);
        Assert.Equal(0.75, report.Candidate.ExactMatch);
        Assert.All(report.SupportWarnings, warning =>
        {
            Assert.Equal(warning.Tag == "en" ? 2 : 1, warning.Cases);
            Assert.Equal(minimum, warning.MinimumSupport);
            Assert.False(warning.Gated);
        });
        if (minimum == 2) Assert.Equal(new[] { "fa", "priority" }, report.SupportWarnings.Select(warning => warning.Tag));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void WarningsPreserveAggregateAndSliceScoresAndGateDecisions(bool gateSlice, bool failAggregate)
    {
        var gates = gateSlice ? new Dictionary<string, SliceGate> { ["fa"] = new(MaxDrop: 0) } : null;
        var cases = Cases(); var baseline = Outputs(false); var candidate = Outputs(true);
        var original = Evaluator.Compare(cases, baseline, candidate, minimumExactMatch: failAggregate ? 1 : 0, sliceGates: gates);
        var warned = Evaluator.Compare(cases, baseline, candidate, minimumExactMatch: failAggregate ? 1 : 0, sliceGates: gates, minimumSliceSupport: 3);
        Assert.Equal(!gateSlice && !failAggregate, warned.Passed);
        Assert.Equal(original.Passed, warned.Passed);
        Assert.Equal(original.Baseline, warned.Baseline);
        Assert.Equal(original.Candidate, warned.Candidate);
        Assert.Equal(original.Delta, warned.Delta);
        Assert.Equal(original.Results, warned.Results);
        Assert.Equal(original.Slices, warned.Slices);
        Assert.Empty(original.SupportWarnings);
        Assert.Equal(3, warned.SupportWarnings.Count);
        Assert.Equal(gateSlice, warned.SupportWarnings.Single(warning => warning.Tag == "fa").Gated);
    }

    [Fact]
    public void UntaggedCasesHaveNoSliceWarningsAndUnknownGatesRemainErrors()
    {
        var cases = Data(new { id = "a", references = new[] { "correct" } });
        var outputs = Data(new { id = "a", output = "correct" });
        var report = Evaluator.Compare(cases, outputs, outputs, minimumSliceSupport: 100);
        Assert.Equal(1, report.Cases);
        Assert.Equal(1, report.Candidate.ExactMatch);
        Assert.Empty(report.Slices);
        Assert.Empty(report.SupportWarnings);
        Assert.Throws<InputException>(() => Evaluator.Compare(cases, outputs, outputs,
            sliceGates: new Dictionary<string, SliceGate> { ["missing"] = new() }, minimumSliceSupport: 100));
        Assert.Throws<InputException>(() => Evaluator.Compare(cases, outputs, outputs, minimumSliceSupport: -1));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("NaN")]
    [InlineData("2147483648")]
    public void CliRejectsInvalidSupportCountsWithoutPartialReports(string minimum)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        Assert.Equal(2, Application.Run(["compare", "--cases", Cases(), "--baseline", Outputs(false), "--candidate", Outputs(true),
            "--min-slice-support", minimum], output, error));
        Assert.Empty(output.ToString());
        Assert.NotEmpty(error.ToString());
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void CliWarningsAreJsonOnlyAndPreserveExitCodes(bool gated, int expectedExit)
    {
        var args = new List<string> { "compare", "--cases", Cases(), "--baseline", Outputs(false), "--candidate", Outputs(true), "--min-slice-support", "2" };
        if (gated)
        {
            var gates = Data(); File.WriteAllText(gates, "{\"fa\":{\"maxDrop\":0}}");
            args.AddRange(["--slice-gates", gates]);
        }
        var output = new StringWriter();
        var error = new StringWriter();
        Assert.Equal(expectedExit, Application.Run(args.ToArray(), output, error));
        Assert.Empty(error.ToString());
        Assert.DoesNotContain("correct", output.ToString());
        Assert.DoesNotContain("wrong", output.ToString());
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal(2, json.RootElement.GetProperty("minimumSliceSupport").GetInt32());
        var warnings = json.RootElement.GetProperty("supportWarnings").EnumerateArray().ToArray();
        Assert.Equal(2, warnings.Length);
        var fa = warnings.Single(warning => warning.GetProperty("tag").GetString() == "fa");
        Assert.Equal(new[] { "tag", "cases", "minimumSupport", "gated" }, fa.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, fa.GetProperty("cases").GetInt32());
        Assert.Equal(2, fa.GetProperty("minimumSupport").GetInt32());
        Assert.Equal(gated, fa.GetProperty("gated").GetBoolean());
    }

    public void Dispose()
    {
        if (Directory.Exists(root) && Path.GetFileName(root).StartsWith("finetune-slices-", StringComparison.Ordinal)) Directory.Delete(root, true);
    }
}
