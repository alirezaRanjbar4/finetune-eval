# Roadmap

## Available in v0.1.0

- Compare saved baseline/candidate outputs on a fixed JSONL evaluation set.
- Best-reference exact match and Unicode word-token F1.
- Complete ID coverage checks, per-case results, and CI gates.

## Delivered in v0.2.0

Added optional case tags, per-slice score reports, and explicit per-tag quality gates with complete support/threshold validation.

## Delivered in v0.3.0

Explicit minimum-support warnings for small gated and report-only slices, without changing scores, gates or exit codes.

## First-scope completion

The [design guide](docs/DESIGN.md) and [troubleshooting guide](docs/TROUBLESHOOTING.md) ship with v0.3.1, along with the license, contributor/roadmap/changelog files and synthetic examples. The bounded scope is complete coverage, best-reference lexical metrics, tagged slices, explicit score gates and support warnings, plus user/developer guides.

The v0.3.1 handoff retains that fixed scope. Before closing issue #4, verify final-main/tag/package commit consistency, supported-platform CI, isolated installed-package examples, bundled links and downloaded public package/checksum digests. Record the publication evidence in the issue. Then keep the repository public/unarchived in maintenance for reproducible bugs and feedback. Future candidates below remain optional rather than extending the first scope.

## Future candidates

- Add structured JSON-output validators and task-specific deterministic metrics.
- Evaluate paired bootstrap confidence intervals for supported metrics.

Live model calls, semantic judging, BLEU/ROUGE compatibility, and general model-quality claims are outside the current release. Features follow real use cases.
