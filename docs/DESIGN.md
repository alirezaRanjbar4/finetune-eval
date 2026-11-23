# Design and first-scope handoff

FineTune Eval compares saved baseline/candidate text outputs against labeled reference answers. Its bounded first scope is complete ID coverage, deterministic exact-match/token-F1 scoring, tagged slices, explicit quality gates and opt-in small-support warnings. It does not train, call or semantically judge models.

## Boundaries and input flow

`Application.Run` parses the `compare` options, loads optional slice gates, calls `Evaluator.Compare`, serializes JSON to stdout, and returns `0`/`1` from the report's pass/fail result. Supported argument/data/file errors produce stderr and exit `2`, without a JSON report. `JsonSupport` rejects duplicate JSON property names recursively before deserialization.

`Evaluator.Compare` validates thresholds, loads all cases, validates IDs/references/tags and resolves slice gates. It then loads each output file into an ID-keyed dictionary and requires exactly the case ID set: no missing, extra or duplicate predictions. Output row order can differ; scoring joins by ID and `results` follows case input order.

Cases require a unique nonblank ID and at least one nonblank reference. Output strings may be empty but not null. IDs and tags are case-sensitive and are not trimmed or text-normalized. Optional case tags must be unique/nonblank within each case. Dataset lists, output dictionaries and result/report structures are held in memory.

## Metric policy

For exact match, apply NFKC, invariant lowercase, trimming and collapsed whitespace to both strings, then compare them. Punctuation remains significant.

For token F1, apply NFKC/invariant lowercase and extract runs of Unicode letters, marks, numbers or ZWNJ (`U+200C`). Count token multiplicity. With shared count `sum(min(predictedCount, referenceCount))`, F1 is `2 * shared / (predictedTokens + referenceTokens)`. If either side has no word tokens, return `1` only for equal normalized full strings, otherwise `0`.

With multiple references, choose the best exact match and best token F1 independently. Average each score across cases with equal case weight; deltas are candidate minus baseline. This is this tool's lexical policy, not a compatibility claim for another suite or a measure of factual/semantic correctness. There is no extra Persian/Arabic letter mapping beyond NFKC; ZWNJ remains part of word tokens.

## Slices and score gates

Every observed tag produces a slice in ordinal tag order, using the same per-case scores as the aggregate. A case contributes once to each of its tags, so slices may overlap and their counts need not sum to the overall count. Untagged cases remain in the aggregate.

The aggregate gate requires candidate exact match/F1 minima and a maximum permitted drop for each metric. Score thresholds must be finite fractions in `[0,1]`; defaults are minima `0` and maximum drop `1`. Comparisons allow `1e-12` floating-point tolerance.

Only explicitly configured slice gates affect pass/fail. Missing or null threshold fields inherit corresponding aggregate thresholds; resolved values are reported. A gate must name an observed tag with positive support. Unknown tags, null gate objects, unknown threshold names and invalid values are errors. A report-only slice has `gate: null` and `passed: null`.

Overall `passed` requires both the aggregate and every explicitly gated slice to pass. A slice regression can therefore fail a run whose overall scores improve.

## Support warnings are a separate policy

`minimumSliceSupport` is a nonnegative 32-bit integer count; `0` disables warnings. Each observed tag with strictly fewer cases gets one `supportWarnings` entry containing tag, support, configured minimum and whether it has an explicit gate. Exactly-at-threshold support does not warn. Gated/report-only tags use the same policy, independently for overlapping tags.

Warnings do not change any score, threshold, gate decision or exit code. Unknown/zero-support gate tags remain errors, not warnings. These counts do not provide confidence intervals, significance tests or a guarantee of adequate representation.

## Read-only reports and provenance limits

The tool reads local files and creates no output files itself; callers may redirect stdout to a separate fresh report path. Reports expose aggregate/slice/per-ID scores, tags, counts, resolved gates and warnings, without reference/prediction text. IDs and tags can still disclose information; use suitable identifiers before sharing reports.

Inputs are loaded separately and no file hashes or generation provenance are recorded in this report. Keep files stable during comparison and retain your dataset/model/prompt/decoding metadata separately. Complete ID coverage does not prove an independent test set, absence of leakage or comparable model-generation conditions. The synthetic examples demonstrate the workflow, not measured improvements to a trained model.

CI tests, verifies formatting, packages the CLI and runs isolated installed-package examples on Windows, Linux and macOS. The v0.3.1 archive bundles these guides, contributor/roadmap/changelog/license files and the synthetic JSONL/gate fixtures. The smoke verifies expected Git commit metadata, original example bytes, the installed assembly's hash, support-warning boundaries, unchanged scores/inputs and 0/1/2 exits without using an online package feed. See [CONTRIBUTING](../CONTRIBUTING.md), [examples](../examples/README.md), [troubleshooting](TROUBLESHOOTING.md) and the [README](../README.md). Issue #4 records final-publication acceptance before maintenance. Extra metrics, structured-output validators and statistical intervals are optional future candidates in the [roadmap](../ROADMAP.md).
