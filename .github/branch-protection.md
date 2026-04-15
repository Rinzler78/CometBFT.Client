# Branch Protection Rules

Configure the following rules in GitHub repository settings under
**Settings > Branches > Branch protection rules** for `master`, `develop`, and `release/*`.

## master

| Rule | Setting |
|------|---------|
| Require a pull request before merging | Enabled |
| Require approvals | 1 minimum |
| Dismiss stale pull request approvals when new commits are pushed | Enabled |
| Require status checks to pass before merging | Enabled |
| Required status checks | `CI / English-only language check (cspell)`, `CI / Build & Test (.NET 10)`, `CI / Integration Tests`, `CI / E2E Tests` |
| Require branches to be up to date before merging | Enabled |
| Do not allow bypassing the above settings | Enabled |
| Allow force pushes | **Disabled** |
| Allow deletions | **Disabled** |

## develop

| Rule | Setting |
|------|---------|
| Require a pull request before merging | Enabled |
| Require approvals | 1 minimum |
| Require status checks to pass before merging | Enabled |
| Required status checks | `CI / English-only language check (cspell)`, `CI / Build & Test (.NET 10)`, `CI / Integration Tests`, `CI / E2E Tests` |
| Allow force pushes | **Disabled** |
| Allow deletions | **Disabled** |

## release/*

| Rule | Setting |
|------|---------|
| Require a pull request before merging | Enabled (targeting `master`) |
| Require status checks to pass before merging | Enabled |
| Required status checks | `CI / English-only language check (cspell)`, `CI / Build & Test (.NET 10)`, `CI / Integration Tests`, `CI / E2E Tests` |
| Allow force pushes | **Disabled** |
| Allow deletions | Enabled (delete branch after merge) |

## Tag Protection Rules

Configure in **Settings > Tags > Protected tags**:

| Tag pattern | Restriction |
|-------------|-------------|
| `v*` | Only users with `maintain` or `admin` role may create tags matching this pattern |

This ensures only maintainers can trigger the publish workflow via a versioned tag.

## Rationale

- No direct push to `master`, `develop`, or `release/*` enforces code review on all changes.
- All CI check-runs must pass before merge (exact GitHub names: `CI / English-only language check (cspell)`, `CI / Build & Test (.NET 10)`, `CI / Integration Tests`, `CI / E2E Tests`).
- Disabling force push protects commit history integrity.
- Tag protection on `v*` ensures only authorized maintainers can initiate a NuGet release.
- `release/*` branches require a PR targeting `master` and are deleted after merge to keep the branch list clean.
