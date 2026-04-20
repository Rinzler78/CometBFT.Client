# CometBFT.Client — Development Governance

## Stack
- .NET 10, C#, Avalonia (GUI samples)
- xUnit, Coverlet (coverage gate ≥ 90 %)
- pre-commit framework, dotnet format, cspell, detect-secrets

## Development Workflow (mandatory)

### 1. Never work on master or develop directly
Both branches are protected: a `pre-commit` branch guard blocks direct commits,
and `pre-push` blocks direct pushes. All work goes through a feature/bugfix/hotfix
branch in a dedicated worktree.

### 2. Create a worktree for every task

```bash
# Feature
git worktree add .worktrees/feature/<name> -b feature/<name>
cd .worktrees/feature/<name>

# Bugfix
git worktree add .worktrees/bugfix/<name> -b bugfix/<name>

# Hotfix (from master)
git worktree add .worktrees/hotfix/<name> -b hotfix/<name> master
```

Worktrees live in `.worktrees/` (git-ignored). Each worktree is a full working
directory with its own index — parallel branches without stashing.

### 3. Develop, commit, push, open PR

```bash
# Inside the worktree
git add <files>
git commit -m "feat(scope): description"   # Conventional Commits enforced
git push -u origin <branch>
# Open PR → develop (features/bugfixes) or master (hotfixes)
```

Pre-commit hooks run automatically on every `git commit`:
| Hook | What it checks |
|------|---------------|
| `branch-guard` | Blocks commits on master/develop |
| `dotnet-restore-lock` | NuGet lock files in sync |
| `dotnet-outdated` | No outdated direct dependencies |
| `dotnet-format` | Code style (dotnet format) |
| `dotnet-build` | Solution builds with 0 warnings |
| `dotnet-test` | All unit tests pass |
| `coverage-gate` | Line/branch coverage ≥ 90 % |
| `detect-secrets` | No secrets in staged files |
| `cspell` | English-only spelling |

### 4. Clean up after merge

```bash
git worktree remove .worktrees/feature/<name>
git branch -d feature/<name>
```

---

## Branch Model (git flow)

| Branch | Purpose | Merges into |
|--------|---------|-------------|
| `master` | Production releases | — |
| `develop` | Integration branch | master (via release) |
| `feature/<name>` | New features | develop |
| `bugfix/<name>` | Bug fixes | develop |
| `hotfix/<name>` | Production fixes | master + develop |
| `release/<version>` | Release preparation | master + develop |

---

## Quality Gates

- **Build**: `TreatWarningsAsErrors=true` — zero warnings allowed
- **Tests**: unit tests must pass before any merge
- **Coverage**: line + branch + method ≥ 90 % (enforced by CoverageGate tool)
- **Format**: `dotnet format --verify-no-changes` must pass
- **Secrets**: `detect-secrets` baseline must be clean

---

## Hook Installation

Run once after cloning (or after updating `.hooks/`):

```bash
./scripts/install-hooks.sh
```

Canonical hook sources are versioned in `.hooks/`. The `.git/hooks/` directory
is derived from them — never edit `.git/hooks/` directly.

---

## Agent Routing

| Task | Agent |
|------|-------|
| Multi-step feature, architecture decision | `project-orchestrator` |
| Code review, PR review | `project-code-reviewer` |
| Test strategy, coverage | `project-test-guardian` |
| Security concern | `security-auditor` |
| Token / context optimization | `project-cost-optimizer` |
| Release, changelog, SemVer | `release-manager` |
| MCP config issues | `mcp-governor` |
| Documentation | `docs-spec-writer` |
