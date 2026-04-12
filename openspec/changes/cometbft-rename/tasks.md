# Tasks : Renommage Tendermint → CometBFT

## Phase 0 — GitHub (manuel)
- [ ] 0.1 Renommer repo GitHub `Rinzler78/CometBFT.Client` → `Rinzler78/CometBFT.Client`
- [ ] 0.2 `git remote set-url origin https://github.com/Rinzler78/CometBFT.Client.git`
- [ ] 0.3 Vérifier redirect GitHub actif

## Phase 1 — OpenSpec
- [x] 1.1 Créer `openspec/changes/cometbft-rename/spec.md` et `tasks.md`
- [ ] 1.2 Renommer le dossier racine `CometBFT.Client/` → `CometBFT.Client/` (après tout le reste)

## Phase 2 — Dossiers de projets (16)
- [ ] 2.1 src/CometBFT.Client.Core → src/CometBFT.Client.Core
- [ ] 2.2 src/CometBFT.Client.Extensions → src/CometBFT.Client.Extensions
- [ ] 2.3 src/CometBFT.Client.Grpc → src/CometBFT.Client.Grpc
- [ ] 2.4 src/CometBFT.Client.Rest → src/CometBFT.Client.Rest
- [ ] 2.5 src/CometBFT.Client.WebSocket → src/CometBFT.Client.WebSocket
- [ ] 2.6 tests/CometBFT.Client.Core.Tests → tests/CometBFT.Client.Core.Tests
- [ ] 2.7 tests/CometBFT.Client.E2E.Tests → tests/CometBFT.Client.E2E.Tests
- [ ] 2.8 tests/CometBFT.Client.Grpc.Tests → tests/CometBFT.Client.Grpc.Tests
- [ ] 2.9 tests/CometBFT.Client.Integration.Tests → tests/CometBFT.Client.Integration.Tests
- [ ] 2.10 tests/CometBFT.Client.Rest.Tests → tests/CometBFT.Client.Rest.Tests
- [ ] 2.11 tests/CometBFT.Client.WebSocket.Tests → tests/CometBFT.Client.WebSocket.Tests
- [ ] 2.12 samples/CometBFT.Client.Demo.Grpc → samples/CometBFT.Client.Demo.Grpc
- [ ] 2.13 samples/CometBFT.Client.Demo.Rest → samples/CometBFT.Client.Demo.Rest
- [ ] 2.14 samples/CometBFT.Client.Demo.WebSocket → samples/CometBFT.Client.Demo.WebSocket
- [ ] 2.15 samples/CometBFT.Client.Sample → samples/CometBFT.Client.Sample
- [ ] 2.16 tools/CometBFT.Client.CoverageGate → tools/CometBFT.Client.CoverageGate
- [ ] 2.17 Renommer les fichiers .csproj dans chaque dossier renommé

## Phase 3 — Solution files
- [ ] 3.1 CometBFT.Client.sln → CometBFT.Client.sln (contenu mis à jour)
- [ ] 3.2 CometBFT.Client.src.slnf → CometBFT.Client.src.slnf
- [ ] 3.3 CometBFT.Client.tests.slnf → CometBFT.Client.tests.slnf

## Phase 4 — Contenu .csproj
- [ ] 4.1 ProjectReferences mis à jour (tous les projets)
- [ ] 4.2 PackageId / Description / PackageTags mis à jour
- [ ] 4.3 InternalsVisibleTo dans CometBFT.Client.Grpc.csproj
- [ ] 4.4 Directory.Build.props : ExcludeByFile chemins

## Phase 5 — C# (namespaces + identifiants)
- [ ] 5.1 namespace/using CometBFT.Client.* → CometBFT.Client.*
- [ ] 5.2 ITendermintRestClient → ICometBftRestClient (+ impl + usages)
- [ ] 5.3 ITendermintWebSocketClient → ICometBftWebSocketClient
- [ ] 5.4 ITendermintGrpcClient → ICometBftGrpcClient
- [ ] 5.5 TendermintRestClient → CometBftRestClient
- [ ] 5.6 TendermintWebSocketClient → CometBftWebSocketClient
- [ ] 5.7 TendermintGrpcClient → CometBftGrpcClient
- [ ] 5.8 TendermintRest/WebSocket/GrpcOptions → CometBftRest/WebSocket/GrpcOptions
- [ ] 5.9 TendermintClientException → CometBftClientException (+ sous-types)
- [ ] 5.10 TendermintJsonContext → CometBftJsonContext
- [ ] 5.11 AddTendermintRest/WebSocket/Grpc → AddCometBftRest/WebSocket/Grpc
- [ ] 5.12 Gate build : dotnet build CometBFT.Client.sln --warnaserror

## Phase 6 — Scripts
- [ ] 6.1 scripts/*.sh (7 fichiers) : sln + chemins projets + CoverageGate
- [ ] 6.2 scripts/docker/*.sh (8 fichiers) : images Docker + chemins

## Phase 7 — CI/CD
- [ ] 7.1 .github/workflows/ci.yml : sln + chemins projets
- [ ] 7.2 .github/workflows/publish.yml : sln

## Phase 8 — Documentation
- [ ] 8.1 README.md : titre, packages, exemples
- [ ] 8.2 CHANGELOG.md : en-tête et noms de packages
- [ ] 8.3 src/*/README.md × 5 : packages + exemples
- [ ] 8.4 samples/*/README.md × 4 : chemins scripts
- [ ] 8.5 openspec/changes/initial-library-creation/ : références aux noms

## Phase 9 — CoverageGate
- [ ] 9.1 tools/CometBFT.Client.CoverageGate/Program.cs : excludedPrefixes/Suffixes

## Validation finale
- [ ] V.1 `dotnet build CometBFT.Client.sln --warnaserror` → 0 erreur
- [ ] V.2 `dotnet format CometBFT.Client.sln --verify-no-changes` → OK
- [ ] V.3 `./scripts/test.sh` → verts + coverage ≥ 90%
- [ ] V.4 `grep -r "Tendermint\.Client" src/ tests/ --include="*.cs" | grep -v LegacyProto` → 0
- [ ] V.5 `./scripts/publish.sh --dry-run` → 0 warning
- [ ] V.6 Démos REST + WS + gRPC → démarrent sans erreur
- [ ] V.7 `git remote -v` → URL CometBFT.Client
