using Tooling;
namespace FineTuneEval;

public static class Application
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args is ["--help"] or ["-h"])
        {
            output.WriteLine("finetune-eval compare --cases cases.jsonl --baseline baseline.jsonl --candidate candidate.jsonl [--min-exact 0.8] [--min-f1 0.8] [--max-drop 0] [--slice-gates gates.json] [--min-slice-support 0]\nOffline exact-match and token-F1 evaluation, with optional case tags and per-tag gates. Score thresholds are fractions, not percentages. Minimum slice support is a nonnegative case count (0 disables warnings); warnings do not change gate outcomes. Exit: 0 gates pass, 1 gate fails, 2 invalid input. No model API calls are made.");
            return 0;
        }
        if (args is ["--version"]) { output.WriteLine("0.3.1"); return 0; }
        try
        {
            if (args.Length == 0 || args[0] != "compare") throw new InputException("Use compare; see --help.");
            var options = new Options(args.Skip(1), "--cases", "--baseline", "--candidate", "--min-exact", "--min-f1", "--max-drop", "--slice-gates", "--min-slice-support");
            var report = Evaluator.Compare(options.Require("--cases"), options.Require("--baseline"), options.Require("--candidate"),
                options.Fraction("--min-exact", 0), options.Fraction("--min-f1", 0), options.Fraction("--max-drop", 1),
                options.Get("--slice-gates") is { } path ? Evaluator.LoadSliceGates(path) : null, options.Int("--min-slice-support", 0));
            output.WriteLine(JsonSupport.Serialize(report));
            return report.Passed ? 0 : 1;
        }
        catch (InputException exception) { error.WriteLine(exception.Message); return 2; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        { error.WriteLine("Input could not be read. Check file paths and text encoding."); return 2; }
    }
}
