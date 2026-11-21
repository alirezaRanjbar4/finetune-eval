# Changelog

## 0.3.0 — 2026-11-21

Added opt-in `--min-slice-support N` warnings for gated and report-only tags with fewer than N cases. Reports expose the configured count and affected tag/support/gate status without answer text. Warnings preserve all metric values and gate outcomes; the default `0` disables them. Added threshold-boundary, overlapping-tag, invalid-count and unchanged-score/exit-code tests, plus documentation of statistical limits.

## 0.2.0 — 2026-11-21

Added optional case tags, per-slice score reports, and explicit per-tag quality gates with complete support/threshold validation.

## 0.1.0 — 2026-11-20

Initial usable release with a C# CLI, synthetic examples, automated tests, and .NET tool packaging. See the README for the supported scope and limitations.
