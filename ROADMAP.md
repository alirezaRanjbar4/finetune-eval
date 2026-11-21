# Roadmap

## Available in v0.1.0

- Compare saved baseline/candidate outputs on a fixed JSONL evaluation set.
- Best-reference exact match and Unicode word-token F1.
- Complete ID coverage checks, per-case results, and CI gates.

## Delivered in v0.2.0

Added optional case tags, per-slice score reports, and explicit per-tag quality gates with complete support/threshold validation.

## Next

- Add optional minimum-support warnings for small slices.
- Add structured JSON-output validators and task-specific deterministic metrics.
- Evaluate paired bootstrap confidence intervals for supported metrics.

Live model calls, semantic judging, BLEU/ROUGE compatibility, and general model-quality claims are outside the current release. Features follow real use cases.
