# Tasks : Renommage Tendermint → CometBFT

## Statut — 2026-04-13

Implémentation complète. Tous les phases 1–9 ont été réalisées dans les commits
`1cf06f1` (rename identifiants C#), `02f1df5` (cleanup fichiers obsolètes),
`5dd4b4e` (fix proto namespace), `26f71e2` (initial CometBFT.Client project).
Les critères d'acceptance de validation finale sont satisfaits.

---

## Phase 0 — GitHub (manuel)
- [x] 0.1 Repo GitHub déjà nommé `Rinzler78/CometBFT.Client` — aucun renommage nécessaire
- [x] 0.2 `git remote set-url origin https://github.com/Rinzler78/CometBFT.Client.git` — vérifié : remote = `https://github.com/Rinzler78/CometBFT.Client.git`
- [x] 0.3 Vérifier redirect GitHub actif

## Phase 1 — OpenSpec
- [x] 1.1 Créer `openspec/changes/cometbft-rename/spec.md` et `tasks.md`
- [x] 1.2 Renommer le dossier racine `CometBFT.Client/` — déjà nommé correctement

## Phase 2 — Dossiers de projets (16)
- [x] 2.1 src/CometBFT.Client.Core → src/CometBFT.Client.Core
- [x] 2.2 src/CometBFT.Client.Extensions → src/CometBFT.Client.Extensions
- [x] 2.3 src/CometBFT.Client.Grpc → src/CometBFT.Client.Grpc
- [x] 2.4 src/CometBFT.Client.Rest → src/CometBFT.Client.Rest
- [x] 2.5 src/CometBFT.Client.WebSocket → src/CometBFT.Client.WebSocket
- [x] 2.6 tests/CometBFT.Client.Core.Tests → tests/CometBFT.Client.Core.Tests
- [x] 2.7 tests/CometBFT.Client.E2E.Tests → tests/CometBFT.Client.E2E.Tests
- [x] 2.8 tests/CometBFT.Client.Grpc.Tests → tests/CometBFT.Client.Grpc.Tests
- [x] 2.9 tests/CometBFT.Client.Integration.Tests → tests/CometBFT.Client.Integration.Tests
- [x] 2.10 tests/CometBFT.Client.Rest.Tests → tests/CometBFT.Client.Rest.Tests
- [x] 2.11 tests/CometBFT.Client.WebSocket.Tests → tests/CometBFT.Client.WebSocket.Tests
- [x] 2.12 samples/CometBFT.Client.Demo.Grpc → samples/CometBFT.Client.Demo.Grpc
- [x] 2.13 samples/CometBFT.Client.Demo.Rest → samples/CometBFT.Client.Demo.Rest
- [x] 2.14 samples/CometBFT.Client.Demo.WebSocket → samples/CometBFT.Client.Demo.WebSocket
- [x] 2.15 samples/CometBFT.Client.Sample → samples/CometBFT.Client.Sample
- [x] 2.16 tools/CometBFT.Client.CoverageGate → tools/CometBFT.Client.CoverageGate
- [x] 2.17 Renommer les fichiers .csproj dans chaque dossier renommé

## Phase 3 — Solution files
- [x] 3.1 CometBFT.Client.sln → CometBFT.Client.sln (contenu mis à jour)
- [x] 3.2 CometBFT.Client.src.slnf → CometBFT.Client.src.slnf
- [x] 3.3 CometBFT.Client.tests.slnf → CometBFT.Client.tests.slnf

## Phase 4 — Contenu .csproj
- [x] 4.1 ProjectReferences mis à jour (tous les projets)
- [x] 4.2 PackageId / Description / PackageTags mis à jour
- [x] 4.3 InternalsVisibleTo dans CometBFT.Client.Grpc.csproj
- [x] 4.4 Directory.Build.props : ExcludeByFile chemins

## Phase 5 — C# (namespaces + identifiants)
- [x] 5.1 namespace/using CometBFT.Client.* → CometBFT.Client.*
- [x] 5.2 ITendermintRestClient → ICometBftRestClient (+ impl + usages)
- [x] 5.3 ITendermintWebSocketClient → ICometBftWebSocketClient
- [x] 5.4 ITendermintGrpcClient → ICometBftGrpcClient
- [x] 5.5 TendermintRestClient → CometBftRestClient
- [x] 5.6 TendermintWebSocketClient → CometBftWebSocketClient
- [x] 5.7 TendermintGrpcClient → CometBftGrpcClient
- [x] 5.8 TendermintRest/WebSocket/GrpcOptions → CometBftRest/WebSocket/GrpcOptions
- [x] 5.9 TendermintClientException → CometBftClientException (+ sous-types)
- [x] 5.10 TendermintJsonContext → CometBftJsonContext
- [x] 5.11 AddTendermintRest/WebSocket/Grpc → AddCometBftRest/WebSocket/Grpc (+ AddCometBftSdkGrpc)
- [x] 5.12 Gate build : dotnet build CometBFT.Client.sln --warnaserror

## Phase 6 — Scripts
- [x] 6.1 scripts/*.sh (7 fichiers) : sln + chemins projets + CoverageGate
- [x] 6.2 scripts/docker/*.sh (8 fichiers) : images Docker + chemins

## Phase 7 — CI/CD
- [x] 7.1 .github/workflows/ci.yml : sln + chemins projets
- [x] 7.2 .github/workflows/publish.yml : sln

## Phase 8 — Documentation
- [x] 8.1 README.md : titre, packages, exemples
- [x] 8.2 CHANGELOG.md : en-tête et noms de packages (corrigé `AddTendermintRest/WS/Grpc` → `AddCometBftRest/WS/Grpc`)
- [x] 8.3 src/*/README.md × 5 : packages + exemples
- [x] 8.4 samples/*/README.md × 4 : chemins scripts
- [x] 8.5 openspec/changes/initial-library-creation/ : références aux noms

## Phase 9 — CoverageGate
- [x] 9.1 tools/CometBFT.Client.CoverageGate/Program.cs : excludedPrefixes/Suffixes

## Validation finale
- [x] V.1 `dotnet build CometBFT.Client.sln --warnaserror` → 0 erreur
- [x] V.2 `dotnet format CometBFT.Client.sln --verify-no-changes` → OK
- [x] V.3 `./scripts/test.sh` → verts + coverage ≥ 90% (couverture réelle : 97%)
- [x] V.4 `grep -r "Tendermint\.Client" src/ tests/ --include="*.cs" | grep -v LegacyProto` → 0
- [x] V.5 `./scripts/publish.sh --dry-run` → 0 warning
- [x] V.6 Démos REST + WS + gRPC → démarrent sans erreur
- [x] V.7 `git remote -v` → URL `https://github.com/Rinzler78/CometBFT.Client.git`
