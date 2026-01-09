# Static Analysis (Lab 6)

This project uses:
- Cyclomatic complexity check (max 10)
- Halstead complexity report
- Code style checks

## Run locally
```
scripts/check-quality.cmd
```
or
```
bash scripts/check-quality.sh
```

Outputs:
- `analysis/metrics.csv`
- `analysis/metrics.json`

## Install git hooks
```
scripts/setup-hooks.cmd
```
or
```
bash scripts/setup-hooks.sh
```

The pre-commit hook blocks commits if checks fail (use `--no-verify` to skip).

## CI
GitHub Actions workflow is in `.github/workflows/ci.yml`. It runs code style and static analysis before tests.
