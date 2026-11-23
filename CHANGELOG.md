# Changelog

## 0.3.1 — 2026-11-23

Package the linked design/troubleshooting and contributor/roadmap/changelog/license files, plus runnable synthetic input/gate examples. Align installation and CLI version with this handoff release. Add an isolated local-feed installed-package smoke for final-commit metadata, bundled example bytes, support-warning boundaries, unchanged scores/inputs and 0/1/2 exits on all supported CI platforms. Scoring and gate behavior are unchanged from 0.3.0. This is the closing release for the bounded first scope in issue #4, subject to final package/tag/asset verification before maintenance.

## 0.3.0 — 2026-11-21

Added opt-in `--min-slice-support N` warnings for gated and report-only tags with fewer than N cases. Reports expose the configured count and affected tag/support/gate status without answer text. Warnings preserve all metric values and gate outcomes; the default `0` disables them. Added threshold-boundary, overlapping-tag, invalid-count and unchanged-score/exit-code tests, plus documentation of statistical limits.

## 0.2.0 — 2026-11-21

Added optional case tags, per-slice score reports, and explicit per-tag quality gates with complete support/threshold validation.

## 0.1.0 — 2026-11-20

Initial usable release with a C# CLI, synthetic examples, automated tests, and .NET tool packaging. See the README for the supported scope and limitations.
