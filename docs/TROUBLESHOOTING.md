# Troubleshooting

Commands target the published **0.3.0** package and synthetic repository examples. Run from the repository root. See the [README](../README.md) for schemas/options and the [design guide](DESIGN.md) for scoring, slices and warning policy.

## Install and find the intended executable

Download `FineTuneEval.Tool.0.3.0.nupkg` and `SHA256SUMS.txt` from the [release](https://github.com/alirezaRanjbar4/finetune-eval/releases/tag/v0.3.0) into an empty `downloaded-package` folder. Check the package SHA-256, then choose a fresh isolated tool path:

```sh
dotnet tool install FineTuneEval.Tool --version 0.3.0 --tool-path ./artifacts/handoff-tool --add-source ./downloaded-package
```

Run `./artifacts/handoff-tool/finetune-eval --version` on Linux/macOS or `.\artifacts\handoff-tool\finetune-eval.exe --version` in PowerShell. For an existing isolated installation, use `dotnet tool update` with the same package/version/source/tool-path options. Global installs use `--global` instead of `--tool-path ...`; inspect `dotnet tool list --global` and PATH when the command is missing.

Check `dotnet --info` if the .NET 10 host/runtime cannot start. A package-source/version error can mean the download folder is wrong: packages are on GitHub Releases, not NuGet.org. The examples below use `finetune-eval` as shorthand for the intended executable.

## Distinguish gate failures from invalid input

```sh
finetune-eval compare --cases examples/cases.jsonl --baseline examples/baseline.jsonl --candidate examples/candidate.jsonl --min-exact 1 --min-f1 1 --max-drop 0
```

This sample exits `0` because its candidate matches the references. Exit `1` means an aggregate or explicitly gated slice failed; a JSON report is still printed. Exit `2` means invalid input/arguments or a file error; read stderr and correct the input before interpreting scores. The application produces no partial JSON report for those errors. Host/runtime startup failures can occur before the application runs.

The tool creates no report file itself. If redirecting stdout, choose a fresh report path separate from every input; shell redirection is performed by your shell. Reports omit answer text but retain IDs and tags.

## Missing/extra IDs, duplicate records or invalid JSONL

Every physical line must be a complete JSON object. Blank/multiline records, duplicate properties, null outputs, blank references and empty datasets are rejected. Cases need unique nonblank IDs and a nonempty reference list. Empty output strings are allowed and scored.

Both model-output files must cover exactly the case IDs. Compare IDs exactly: `CaseA`, `casea` and ` CaseA ` differ. Reordering rows is allowed; omitting difficult cases or adding extra outputs is not. Correct the coverage rather than comparing unequal subsets.

## Invalid tags or unknown gate names

Case tags are optional but must be nonblank and unique per case. Tag names are case-sensitive and not trimmed, so `fa`, `FA` and ` fa ` are different tags. Each configured gate must refer to a tag actually present in the cases. Unknown/zero-support tags remain errors even with support warnings enabled.

The gate file is a JSON object keyed by tag. Its threshold names are exactly `minExact`, `minF1` and `maxDrop`; misspellings and duplicate JSON properties are rejected. A null gate object is invalid. Empty gate objects and omitted/null threshold fields inherit aggregate thresholds, and the resolved values are included in the report.

## Fractions versus sample counts

`--min-exact`, `--min-f1` and `--max-drop` accept finite fractions from `0` through `1`, such as `0.8`, not percentages such as `80`. `--max-drop 0` permits no decrease in either metric; a positive candidate average can still fail if it drops too far relative to baseline. Default minima `0` and maximum drop `1` are for reporting without restrictive gates.

`--min-slice-support` instead takes a nonnegative 32-bit integer count. Negative, fractional, NaN or overflow values are invalid. Zero disables warnings; it does not disable score gates.

```sh
finetune-eval compare --cases examples/cases.jsonl --baseline examples/baseline.jsonl --candidate examples/candidate.jsonl --slice-gates examples/slice-gates.json --min-slice-support 2
```

This sample still exits `0`. `fa` and `support` each have one case and warn; `en` has exactly two and does not. `fa` is explicitly gated, while `support` is report-only. Warnings appear only in JSON stdout and do not change scores or exit codes. The value `2` demonstrates the boundary, not a recommended evaluation size or confidence interval.

## Overall improvement but a failed slice

Inspect `slices[].passed` and its resolved gate values in addition to overall scores. Overlapping tags are evaluated independently; one case may appear in several slices, and untagged cases contribute only to the aggregate. `passed: null` means the slice has no explicit gate; it is not a failure. A small-support warning is separate from all these decisions and does not relax a failing gate.

## Exact match and F1 disagree

| Prediction / reference | Exact match | Token F1 |
| --- | --- | --- |
| `red blue!` / `red blue` | `0` | `1` |
| `red red` / `red` | `0` | `2/3` |
| `!` / `?` | `0` | `0` |

Exact match preserves punctuation after NFKC/lowercase/whitespace normalization. F1 counts word-token multiplicity, so repetition does not create extra overlap. With no word tokens on either side, equal normalized strings score `1`; different strings score `0`. Multiple references use the best score for each metric, not an average over references.

Persian/Arabic letter variants have no extra mapping beyond NFKC, and ZWNJ is part of a word token. Synonyms and paraphrases can score poorly despite expressing the same meaning. Lexical scores cannot establish factual correctness or general model quality.

## Traceability, memory and reproducible reports

Generate baseline/candidate files using comparable cases, prompts and decoding policies, and keep the holdout separate from training/model selection. This report does not record input hashes or prove generation provenance. Retain those details alongside stable input files if comparisons need to be reproduced.

Cases, outputs and report structures are held in memory; there is no streaming-comparison flag. Share a small synthetic fixture, version, command, expected result and platform in [issues](https://github.com/alirezaRanjbar4/finetune-eval/issues/new/choose), following [CONTRIBUTING](../CONTRIBUTING.md). Inspect IDs/tags as well as answer data before publishing a report.
