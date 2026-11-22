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

The [design guide](docs/DESIGN.md) and [troubleshooting guide](docs/TROUBLESHOOTING.md) are delivered on main, with commands for the current published v0.3.0 package. The bounded scope is complete coverage, best-reference lexical metrics, tagged slices, explicit score gates and support warnings, plus user/developer guides.

Issue #4 still requires a validated closing release: consistent versions/docs, packaged install/examples, supported-platform CI and published checksums. Complete that acceptance before entering maintenance. Future candidates below remain optional rather than extending the first scope.

## Future candidates

- Add structured JSON-output validators and task-specific deterministic metrics.
- Evaluate paired bootstrap confidence intervals for supported metrics.

Live model calls, semantic judging, BLEU/ROUGE compatibility, and general model-quality claims are outside the current release. Features follow real use cases.
