# Synthetic packaged examples

The archive includes [cases](cases.jsonl), [baseline outputs](baseline.jsonl), [candidate outputs](candidate.jsonl) and [slice gates](slice-gates.json). They contain three invented cases and demonstrate lexical metrics and gates, without a trained model or a measured quality improvement.

Run the [README quick start](../README.md) from a repository checkout. With `--min-slice-support 2`, the `fa` and `support` tags (one case each) warn; `en` (two cases) does not. The candidate still passes because warnings do not change scores or gates. The count 2 demonstrates a boundary, not a recommended evaluation size or statistical confidence.

After committing and packing source, `pwsh -File examples/Smoke.ps1` installs that package with a fresh cache and a local-only feed. It reads the four known packaged fixtures, checks their original bytes and the installed assembly hash, then verifies passing/failing gates, invalid-option exits and input preservation. Evidence stays in a fresh `artifacts/smoke-*` directory. The script is also run by [three-platform CI](../.github/workflows/ci.yml); see [contributing](../CONTRIBUTING.md) for prerequisites and [troubleshooting](../docs/TROUBLESHOOTING.md) for lexical examples and error contracts.
