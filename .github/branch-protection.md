# Branch Protection Rules

Configure the following rules in GitHub repository settings under
**Settings > Branches > Branch protection rules** for both `master` and `develop`.

## master

| Rule | Setting |
|------|---------|
| Require a pull request before merging | Enabled |
| Require approvals | 1 minimum |
| Dismiss stale pull request approvals when new commits are pushed | Enabled |
| Require status checks to pass before merging | Enabled |
| Required status checks | `build-and-test` (CI workflow) |
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
| Required status checks | `build-and-test` (CI workflow) |
| Allow force pushes | **Disabled** |
| Allow deletions | **Disabled** |

## Rationale

- No direct push to `master` or `develop` enforces code review on all changes.
- CI must pass before merge prevents broken builds entering the mainline.
- Disabling force push protects commit history integrity.
