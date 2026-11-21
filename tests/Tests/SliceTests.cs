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

    public void Dispose()
    {
        if (Directory.Exists(root) && Path.GetFileName(root).StartsWith("finetune-slices-", StringComparison.Ordinal)) Directory.Delete(root, true);
    }
}
