# Contributing

Start with a small, reproducible issue and synthetic data. Bug reports, tests, documentation fixes, and focused improvements are welcome.

Read the [design guide](docs/DESIGN.md) before changing ID coverage, metric definitions or gate/warning policy. Keep the [troubleshooting guide](docs/TROUBLESHOOTING.md) and its current-release examples consistent with behavior.

- Keep changes tied to a concrete user problem.
- Add regression tests for changed behavior and CLI exit codes.
- Run `dotnet test -c Release` and `dotnet format --verify-no-changes`.
- After committing intended changes, run `dotnet pack src/Cli -c Release -o artifacts` and `pwsh -File examples/Smoke.ps1` to check the final package and installed examples. The Git commit assertion intentionally fails for a stale archive. PowerShell 7 is required on Linux/macOS and is available on GitHub-hosted runners.
- Keep reports free of raw training text and reference/prediction contents.
- Include expected results and validation in pull requests.
- Discuss large changes before implementing them.

Use respectful, technical feedback. Never commit private datasets, credentials, generated model artifacts, or misleading benchmark claims.

The v0.3.1 first-scope handoff keeps the repository public/unarchived for reproducible bugs and feedback. Optional new metrics or statistical methods do not reopen that completed scope; discuss a concrete use case first. See the [synthetic examples](examples/README.md) and [roadmap](ROADMAP.md).
