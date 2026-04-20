# Contributing to CometBFT.Client

## Development Workflow

### Branching Strategy

This project follows [Git Flow](https://nvie.com/posts/a-successful-git-branching-model/).

| Branch | Purpose |
|--------|---------|
| `master` | Production-ready releases only |
| `develop` | Integration branch — all features merge here |
| `feature/*` | New features and non-critical improvements |
| `release/*` | Release preparation |
| `hotfix/*` | Critical production fixes |

All worktrees must be created inside `.worktrees/` at the repository root.

### PR Merge Strategy (§0.7.1)

- **feature/* → develop**: **squash-merge** — one commit per feature on `develop`
- **release/* → master**: **merge commit (no squash)** — preserves the full release history
- **hotfix/* → master + develop**: merge commit on both targets

### Release Branch Lifecycle (§0.7.2)

1. Create `release/vX.Y.Z` from `develop`
2. Run the full test suite: `./scripts/test.sh` — must pass (coverage gate ≥ 90 % global + ≥ 90 % per file)
3. Run `./scripts/publish.sh --dry-run` — must produce exactly one `.nupkg`
4. Update `CHANGELOG.md`: move `[Unreleased]` → `[vX.Y.Z] - YYYY-MM-DD`
5. Bump `<Version>` in `Directory.Build.props`
6. Open PR `release/vX.Y.Z → master`; require CI green + 1 approving review
7. Merge to `master`; tag `vX.Y.Z`; the `publish.yml` workflow triggers automatically
8. Back-merge `master → develop`

### SemVer Bump Rules (§0.7.3)

| Bump | When |
|------|------|
| **Major** | Breaking public API change **or** protocol major version upgrade (e.g., CometBFT v0.38 → v1.x) |
| **Minor** | New endpoint, transport, or DI extension added (backward-compatible) |
| **Patch** | Bug fix, dependency update, or documentation-only change |

### PR Title Format (§0.7.4)

PR titles must follow the [Conventional Commits](https://www.conventionalcommits.org/) format:

```
<type>(<scope>): <short description>
```

Examples:
- `feat(rest): add GetBlockResultsAsync`
- `fix(ws): handle reconnection on timeout`
- `docs: update README quickstart`
- `chore(ci): pin actions/checkout to v4`

Valid types: `feat`, `fix`, `docs`, `chore`, `test`, `refactor`, `perf`, `ci`.

---

## Coding Conventions (§1.1.5c)

These conventions are enforced at code-review time. Violations block merge.

### Logging

- `Console.WriteLine` is **forbidden** in library code (`src/**`).
- Sample and demo projects (`samples/**`) may use `Console.WriteLine` or `Spectre.Console`.

### Async

- **`ConfigureAwait(false)`** is required on every `await` expression inside library code (`src/**`).
- It is **not** required in test projects or sample programs.
- `CancellationToken` must be propagated to **all** internal async helpers, not only the public surface.

### Disposable Clients

- All public client types must implement `IAsyncDisposable`.
- A synchronous `Dispose()` shim is required as a fallback:
  ```csharp
  public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
  ```

### Polly Default Policy Values

| Parameter | Default |
|-----------|---------|
| Retry count | 3 |
| Backoff | Exponential: `RetryDelay (1 s) * Math.Pow(2, attempt - 1)` + random jitter (0–100 ms) |
| Circuit breaker — failure threshold | 5 consecutive failures |
| Circuit breaker — open duration | 30 seconds |

### Numeric Types for Financial / Amount Fields

- Use **`decimal`** for all monetary, price, amount, and fee fields.
- `double` and `float` are **forbidden** for domain value types.

---

## Testing Conventions

### Test Method Naming (§7.0.1)

All test methods must follow this pattern:

```
MethodName_WhenScenario_ShouldExpectedResult
```

Examples:
- `GetBlockAsync_WhenNodeReturns200_ShouldReturnTypedBlock`
- `BroadcastTxAsync_WhenGrpcThrows_ShouldWrapInCometBftGrpcException`
- `DisposeAsync_WhenCalledTwice_ShouldBeIdempotent`

### Mock Strategy (§7.0.2)

| Scope | Tool | When to use |
|-------|------|-------------|
| External I/O — HTTP channel | **WireMock.Net** | REST client tests (HTTP layer) |
| External I/O — gRPC channel | **NSubstitute** | gRPC client tests |
| External I/O — WebSocket transport | **NSubstitute** | WebSocket client tests |
| Domain records | **Real types** | Always — never mock `record` types |
| JSON serialization | **Real paths** | Always — never mock `JsonSerializerContext` |
| Pagination / option validation | **Real paths** | Always |

**Never mock** domain logic or serialization. These must be tested with real types.

### One Test Project Per Source Assembly (§7.0.3)

| Source project | Test project |
|---------------|-------------|
| `CometBFT.Client.Core` | `CometBFT.Client.Core.Tests` |
| `CometBFT.Client.Rest` | `CometBFT.Client.Rest.Tests` |
| `CometBFT.Client.Grpc` | `CometBFT.Client.Grpc.Tests` |
| `CometBFT.Client.WebSocket` | `CometBFT.Client.WebSocket.Tests` |
| (cross-project) | `CometBFT.Client.Integration.Tests` |
| (end-to-end) | `CometBFT.Client.E2E.Tests` |

### Test Project MSBuild Settings (§7.0.4)

All test projects inherit from `Directory.Build.props` the following settings (no need to repeat them in individual `.csproj` files):

```xml
<IsPackable>false</IsPackable>
<GenerateDocumentationFile>false</GenerateDocumentationFile>
```

These are enforced via the `$(MSBuildProjectName.EndsWith('.Tests'))` condition in `Directory.Build.props`.

---

## Coverage Gate

- **Global line coverage ≥ 90 %** — enforced by `./scripts/test.sh`, `.git/hooks/pre-push`, and CI.
- **Per-file line coverage ≥ 90 %** — same enforcement.
- The pre-push hook blocks any push that fails the gate.

Run locally:
```bash
./scripts/test.sh
```

---

## Scripts

| Script | Purpose |
|--------|---------|
| `./scripts/build.sh` | Build the solution |
| `./scripts/test.sh` | Run tests + coverage gate |
| `./scripts/publish.sh` | Pack and push to NuGet (reads `NUGET_API_KEY` from env) |
| `./scripts/demo.sh` | Run the unified Avalonia dashboard demo (REST + WebSocket + gRPC) |

Docker equivalents live in `scripts/docker/`.
