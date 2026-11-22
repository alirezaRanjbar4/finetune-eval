# FineTune Eval

[![CI](https://github.com/alirezaRanjbar4/finetune-eval/actions/workflows/ci.yml/badge.svg)](https://github.com/alirezaRanjbar4/finetune-eval/actions/workflows/ci.yml)
[![MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**Did fine-tuning improve the answers you care about? Compare saved outputs on the same test set.**

A C# / .NET 10 CLI that compares baseline and candidate model outputs against reference answers. It enforces complete case coverage, computes exact-match and word-token F1, and returns a failing exit code when your CI quality gates are missed.

All evaluation runs locally on saved JSONL. No model calls, API keys, or paid evaluation service are needed.

## Quick start

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```sh
git clone https://github.com/alirezaRanjbar4/finetune-eval.git
cd finetune-eval
dotnet run --project src/Cli -- compare --cases examples/cases.jsonl --baseline examples/baseline.jsonl --candidate examples/candidate.jsonl --min-exact 1 --min-f1 1 --max-drop 0
```

The three synthetic examples pass: candidate answers match their references. They demonstrate the tool, not measured improvements to an actual trained model.

## Inputs

Evaluation cases, one per line in `cases.jsonl`:

```json
{"id":"refund-policy","references":["14 days","fourteen days"]}
```

Both `baseline.jsonl` and `candidate.jsonl` use:

```json
{"id":"refund-policy","output":"14 days"}
```

IDs are case-sensitive and must be unique. Both output files must cover **all and only** the evaluation IDs. Order may differ. Missing/extra IDs, null outputs, blank references, empty datasets, invalid JSON, and duplicate JSON property names are rejected. Empty prediction strings are allowed and scored.

Generate model outputs independently using the same held-out test cases, prompts, and decoding policy. Keep this set separate from training data. This CLI scores your files; it does not prove their provenance or whether the test data leaked into training.

## Metrics

- **Exact match:** Unicode NFKC, invariant lowercase, whitespace trimming/collapse, then string equality. Punctuation is significant.
- **Token F1:** Unicode letter/mark/number/ZWNJ word tokens, with NFKC/lowercase normalization. Compute multiset overlap: `2 × shared-token-count / (prediction-token-count + reference-token-count)`.
- **Multiple references:** take the best exact match and the best F1 independently.
- **Aggregation:** average each metric over all cases with equal weight. Report candidate minus baseline deltas.
- If either side has no word tokens, F1 is `1` only when the normalized full strings match; otherwise `0`.

Token overlap is a lexical signal. It does not measure factual correctness, safety, reasoning, semantic equivalence, or open-ended writing quality. Persian/Arabic letter variants and punctuation have no extra language-specific mapping beyond NFKC. These definitions are this tool's metric policy, not a claim of exact compatibility with another evaluation suite.

## CI gates

```sh
finetune-eval compare --cases cases.jsonl --baseline baseline.jsonl --candidate candidate.jsonl --min-exact 0.8 --min-f1 0.85 --max-drop 0
```

Thresholds are fractions in `[0,1]`, not percentages. Candidate average exact match and F1 must meet their minima; **neither** metric may drop by more than `--max-drop` versus baseline. Defaults are minimums `0`, maximum drop `1` (reporting without a restrictive gate). Comparisons allow a `1e-12` floating-point tolerance.

Exit codes: `0` gates pass, `1` a gate fails, `2` invalid input/arguments or an unreadable file. JSON goes to standard output, argument/data errors to standard error.

Reports contain aggregate scores, thresholds, and per-ID scores, without reference or prediction text. IDs remain visible, so use opaque identifiers for sensitive test cases.

## Install

Download `FineTuneEval.Tool.0.3.0.nupkg` from the [release](https://github.com/alirezaRanjbar4/finetune-eval/releases/tag/v0.3.0) into an empty folder:

```sh
dotnet tool install --global FineTuneEval.Tool --version 0.3.0 --add-source ./downloaded-package
finetune-eval --help
```

Or build from source:

```sh
dotnet pack src/Cli -c Release -o artifacts
dotnet tool install --global FineTuneEval.Tool --version 0.3.0 --add-source ./artifacts
```

The tool has no runtime package dependencies. Packages are distributed through GitHub Releases, not NuGet.org.

## Tagged slices and per-slice gates

Cases can include optional, unique, nonblank, case-sensitive tags:

```json
{"id":"support-fa","references":["از بخش پشتیبانی پیام بفرستید"],"tags":["fa","support"]}
```

Every observed tag gets a `slices` report with its support, baseline/candidate scores, and deltas. Tags can overlap; an example counts once per tag, so slice counts need not sum to the overall count. Untagged examples still contribute to aggregate scores. Tags without an explicit gate have `gate: null` and `passed: null` and do not affect pass/fail.

```sh
finetune-eval compare --cases examples/cases.jsonl --baseline examples/baseline.jsonl --candidate examples/candidate.jsonl --slice-gates examples/slice-gates.json
```

Gate configuration is a JSON object keyed by tag, with `minExact`, `minF1`, and/or `maxDrop`:

```json
{"fa":{"minExact":1,"minF1":1,"maxDrop":0}}
```

Omitted gate fields inherit the corresponding aggregate thresholds, and resolved fields are included in the report. Aggregate gates remain in force. Exit `1` means the aggregate or any explicitly gated slice failed. Unknown/zero-support gate tags, duplicate/blank case tags, unknown threshold names, duplicate JSON properties, and thresholds outside [0,1] are invalid input (exit `2`). There are no zero-support score averages.

This catches a candidate that improves overall while regressing on a named subgroup; it does not change the lexical meaning or limitations of the underlying metrics.

## Small-slice support warnings

Use an explicit minimum case count to flag small slices before interpreting their scores:

```sh
finetune-eval compare --cases examples/cases.jsonl --baseline examples/baseline.jsonl --candidate examples/candidate.jsonl --slice-gates examples/slice-gates.json --min-slice-support 2
```

`--min-slice-support N` accepts a nonnegative 32-bit integer case count; the default `0` disables these warnings. Every observed tag with **fewer than N cases** gets one warning, whether gated or report-only. A slice exactly at the threshold does not warn. The example reports `fa` and `support` (one case each); `en` has two cases and does not warn. It still exits `0` because its score gates pass. The value `2` demonstrates the boundary; it is not a recommended evaluation sample size.

The JSON report always includes `minimumSliceSupport` and a `supportWarnings` array (empty when no tags are affected). Each warning contains only `tag`, `cases`, `minimumSupport`, and `gated`, for example:

```json
{"tag":"fa","cases":1,"minimumSupport":2,"gated":true}
```

Warnings go only in JSON stdout, without reference/prediction text or extra stderr output. They do **not** change aggregate or slice scores, gate thresholds, pass/fail decisions, or exit codes. Invalid counts are input errors (exit `2`); unknown/zero-support gated tags remain input errors even when warnings are enabled.

Overlapping tags are checked independently: one case contributes once to each of its tags, and may cause more than one tag warning. Untagged cases remain in aggregate scores and create no slice warning. Tags remain case-sensitive; warnings follow the same ordinal tag order as `slices`.

These are sample-count warnings, **not statistical confidence intervals**, significance tests, or assurances that larger slices are representative or independent. Choose the threshold for your task and inspect data coverage and provenance separately.

## Development and feedback

```sh
dotnet test -c Release
dotnet format --verify-no-changes
```

The [design guide](docs/DESIGN.md) explains ID coverage, lexical metrics, overlapping slices, score gates and separate support warnings. Use the [troubleshooting guide](docs/TROUBLESHOOTING.md) for installation, invalid inputs/options and unexpected metric or gate results.

Tests cover hand-calculated F1 cases, repeated tokens, Persian text, Unicode normalization, coverage errors, and failing gates. Share synthetic examples in [issues](https://github.com/alirezaRanjbar4/finetune-eval/issues/new/choose). See [CONTRIBUTING.md](CONTRIBUTING.md) and [ROADMAP.md](ROADMAP.md).

## فارسی

خروجی مدل پایه و مدل پس از Fine-tuning را روی آزمون مرجع یکسان مقایسه می‌کند. شناسه‌های ناقص یا تکراری را رد می‌کند، امتیاز تطابق و F1 واژه‌ای را می‌دهد و با افت امتیاز می‌تواند CI را متوقف کند. این ابزار آموزش یا فراخوانی مدل انجام نمی‌دهد و امتیازهای آن ارزیابی معنایی یا صحت factual پاسخ نیستند.

گزینه `--min-slice-support` گروه‌های دارای نمونه کم را در گزارش مشخص می‌کند؛ این هشدار امتیازها و نتیجه CI را تغییر نمی‌دهد و به معنی فاصله اطمینان آماری نیست. مقدار پیش‌فرض صفر، هشدار را غیرفعال می‌کند.

[MIT License](LICENSE)
